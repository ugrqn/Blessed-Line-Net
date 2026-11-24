using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BlessedLine.Desktop;

internal enum PolicyOperation { Block, Upload, ClearAll }

internal sealed record PolicyRequest(PolicyOperation Operation, string? Path = null, long Value = 0);
internal sealed record PolicyResult(bool Success, string? Error = null, bool FirewallCleared = false);

internal sealed class AppPolicyRule
{
    public bool Blocked { get; set; }
    public long UploadKbps { get; set; }
    public long QuotaBytes { get; set; }
    public long UsedBytes { get; set; }
    public bool BudgetBlocked { get; set; }
    internal bool IsBlocked => Blocked || BudgetBlocked;
    internal bool IsEmpty => !IsBlocked && UploadKbps == 0 && QuotaBytes == 0;
}

internal static class ApplicationPolicy
{
    private const string Group = "BlessedLine.AppControls";
    internal const long MaximumUploadKbps = 10_000_000;
    private static readonly SearchValues<char> InvalidPathSyntax = SearchValues.Create("<>:\"/|?*");
    private static readonly HashSet<string> SharedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost.exe", "System", "lsass.exe", "services.exe", "winlogon.exe", "csrss.exe", "smss.exe",
        "wininit.exe", "fontdrvhost.exe", "dwm.exe", "RuntimeBroker.exe", "ApplicationFrameHost.exe",
        "BlessedLine.exe"
    };

    internal static bool IsControllable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length is < 8 or > 1024 ||
            !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.AsSpan(3).ContainsAny(InvalidPathSyntax) || !Path.IsPathFullyQualified(path)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(path), path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) &&
                !SharedHosts.Contains(Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static string NameFor(string path) =>
        Group + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..24];

    internal static async Task<PolicyResult> ExecuteAsync(PolicyRequest request)
    {
        if (!Enum.IsDefined(request.Operation) ||
            (request.Operation != PolicyOperation.ClearAll && !IsControllable(request.Path)))
            return new(false, "InvalidAppPolicy");
        if ((request.Operation == PolicyOperation.Block && request.Value is not (0 or 1)) ||
            (request.Operation == PolicyOperation.Upload && (request.Value < 0 || request.Value > MaximumUploadKbps)))
            return new(false, "InvalidAppPolicy");

        try
        {
            switch (request.Operation)
            {
                case PolicyOperation.Block:
                    SetBlock(request.Path!, request.Value != 0);
                    break;
                case PolicyOperation.Upload:
                    await SetUploadAsync(NameFor(request.Path!), request.Path!, request.Value).ConfigureAwait(false);
                    break;
                case PolicyOperation.ClearAll:
                    var qosError = await TryClearAllUploadAsync().ConfigureAwait(false);
                    ClearAllBlocks();
                    if (qosError is not null) return new(false, qosError, FirewallCleared: true);
                    break;
            }
            return new(true);
        }
        catch (Exception ex) when (ex is COMException or Win32Exception or UnauthorizedAccessException or IOException or
                                   InvalidOperationException or NotSupportedException or TargetInvocationException or
                                   Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            Debug.WriteLine(cause);
            return new(false, cause.Message);
        }
    }

    private static object Activate(string progId) => Activator.CreateInstance(Type.GetTypeFromProgID(progId, throwOnError: true)!)
        ?? throw new InvalidOperationException(progId);

    private static void SetBlock(string path, bool block)
    {
        var id = NameFor(path);
        object policy = Activate("HNetCfg.FwPolicy2");
        object? rules = null;
        try
        {
            rules = ((dynamic)policy).Rules;
            var outName = id + ".Out";
            var inName = id + ".In";
            if (!block)
            {
                RemoveRule(rules, inName);
                RemoveRule(rules, outName);
                return;
            }
            var newOutbound = false;
            try
            {
                newOutbound = AddRule(rules, outName, path, 2);
                AddRule(rules, inName, path, 1);
            }
            catch
            {
                if (newOutbound) RemoveRule(rules, outName);
                throw;
            }
        }
        finally
        {
            if (rules is not null) Marshal.ReleaseComObject(rules);
            Marshal.ReleaseComObject(policy);
        }
    }

    private static bool AddRule(object rules, string name, string path, int direction)
    {
        if (RuleExists(rules, name)) return false;
        object rule = Activate("HNetCfg.FWRule");
        try
        {
            dynamic settings = rule;
            settings.Name = name;
            settings.Grouping = Group;
            settings.ApplicationName = path;
            settings.Direction = direction;
            settings.Protocol = 256; // NET_FW_IP_PROTOCOL_ANY
            settings.RemoteAddresses = "Internet";
            settings.Action = 0; // NET_FW_ACTION_BLOCK
            settings.Profiles = 7; // domain, private, public
            settings.Enabled = true;
            ((dynamic)rules).Add(rule);
            return true;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException)
        {
            var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            throw new InvalidOperationException($"Windows Firewall: {cause.Message}", cause);
        }
        finally { Marshal.ReleaseComObject(rule); }
    }

    private static bool RuleExists(object rules, string name)
    {
        try
        {
            object rule = ((dynamic)rules).Item(name);
            Marshal.ReleaseComObject(rule);
            return true;
        }
        catch (Exception ex) when (IsMissingRule(ex)) { return false; }
    }

    private static bool IsMissingRule(Exception error) =>
        (error is TargetInvocationException { InnerException: { } inner } ? inner : error).HResult == unchecked((int)0x80070002);

    private static void RemoveRule(object rules, string name)
    {
        if (RuleExists(rules, name)) ((dynamic)rules).Remove(name);
    }

    private static void ClearAllBlocks()
    {
        object policy = Activate("HNetCfg.FwPolicy2");
        object? rules = null;
        try
        {
            rules = ((dynamic)policy).Rules;
            var names = new List<string>();
            foreach (object rule in (System.Collections.IEnumerable)rules)
            {
                try
                {
                    dynamic item = rule;
                    string name = item.Name;
                    if ((string)item.Grouping == Group && name.StartsWith(Group + ".", StringComparison.Ordinal))
                        names.Add(name);
                }
                finally { Marshal.ReleaseComObject(rule); }
            }
            foreach (var name in names) RemoveRule(rules, name);
        }
        finally
        {
            if (rules is not null) Marshal.ReleaseComObject(rules);
            Marshal.ReleaseComObject(policy);
        }
    }

    private static async Task SetUploadAsync(string name, string path, long kbps)
    {
        var ruleName = Quote(name);
        var pathBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(path));
        var rate = (kbps * 1000).ToString(CultureInfo.InvariantCulture);
        var query = $"Import-Module -Name $qosModule; $app = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{pathBase64}')); " +
            $"$q = Get-NetQosPolicy -Name {ruleName} -ErrorAction SilentlyContinue;";
        var command = kbps == 0
            ? $"if ($q) {{ Remove-NetQosPolicy -Name {ruleName} -Confirm:$false }}"
            : $"if ($q) {{ Set-NetQosPolicy -Name {ruleName} -AppPathNameMatchCondition $app -ThrottleRateActionBitsPerSecond {rate} -Confirm:$false }} " +
              $"else {{ New-NetQosPolicy -Name {ruleName} -AppPathNameMatchCondition $app -ThrottleRateActionBitsPerSecond {rate} | Out-Null }}";
        await PowerShellAsync(query + command).ConfigureAwait(false);
    }

    private static async Task<string?> TryClearAllUploadAsync()
    {
        try
        {
            await PowerShellAsync("if ([IO.File]::Exists($qosModule)) { Import-Module -Name $qosModule; " +
                $"Get-NetQosPolicy | Where-Object {{ $_.Name -match '^BlessedLine\\.AppControls\\.[0-9A-F]{{24}}$' }} " +
                "| ForEach-Object { Remove-NetQosPolicy -Name $_.Name -Confirm:$false }}").ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Debug.WriteLine(ex);
            return ex.Message;
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    private static async Task PowerShellAsync(string script)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var native = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var executable = Path.Combine(windows, native, "WindowsPowerShell", "v1.0", "powershell.exe");
        var modules = Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "Modules");
        var modules64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(modules));
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; $PSModuleAutoLoadingPreference = 'None'; " +
            $"$env:PSModulePath = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{modules64}')); " +
            "$qosModule = [IO.Path]::Combine($env:PSModulePath, 'NetQos', 'NetQos.psd1'); " + script));
        var start = new ProcessStartInfo(executable, $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.Combine(windows, "System32"),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["PSModulePath"] = modules;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows PowerShell");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(TextCatalog.Format("PolicyTimeout"));
        }

        await output.ConfigureAwait(false);
        var standardError = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            // Encoded-command diagnostics may use CLIXML. Keep status feedback bounded.
            var message = standardError.StartsWith("#< CLIXML", StringComparison.Ordinal)
                ? "Windows NetQoS" : standardError.Trim();
            throw new InvalidOperationException(message.Length > 200 ? message[..200] : message);
        }
    }
}
