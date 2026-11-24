using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

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
                    await SetBlockAsync(request.Path!, request.Value != 0).ConfigureAwait(false);
                    break;
                case PolicyOperation.Upload:
                    await SetUploadAsync(NameFor(request.Path!), request.Path!, request.Value).ConfigureAwait(false);
                    break;
                case PolicyOperation.ClearAll:
                    await ClearAllBlocksAsync().ConfigureAwait(false);
                    var qosError = await TryClearAllUploadAsync().ConfigureAwait(false);
                    if (qosError is not null) return new(false, qosError, FirewallCleared: true);
                    break;
            }
            return new(true);
        }
        catch (Exception ex) when (ex is Win32Exception or UnauthorizedAccessException or IOException or
                                   InvalidOperationException or NotSupportedException)
        {
            Debug.WriteLine(ex);
            return new(false, ex.Message);
        }
    }

    private static Task SetBlockAsync(string path, bool block)
    {
        var id = NameFor(path);
        var query = $"$names = '{id}.Out', '{id}.In'; " +
            "$rules = @(Get-NetFirewallRule -PolicyStore PersistentStore -Name $names -ErrorAction SilentlyContinue " +
            $"| Where-Object Group -eq '{Group}'); ";
        if (!block)
            return PowerShellAsync("NetSecurity", query + "$rules | Remove-NetFirewallRule -Confirm:$false");

        var encodedPath = Convert.ToBase64String(Encoding.Unicode.GetBytes(path));
        var change = $"$app = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{encodedPath}')); " +
            "$created = @(); try { foreach ($i in 0, 1) { $name = $names[$i]; " +
            "if ($rules.Name -contains $name) { continue }; $direction = @('Outbound', 'Inbound')[$i]; " +
            $"$created += New-NetFirewallRule -PolicyStore PersistentStore -Name $name -DisplayName $name -Group '{Group}' " +
            "-Program $app -Direction $direction -Action Block -Profile Any -RemoteAddress Internet } } " +
            "catch { $created | Remove-NetFirewallRule -Confirm:$false -ErrorAction SilentlyContinue; throw }";
        return PowerShellAsync("NetSecurity", query + change);
    }

    private static Task ClearAllBlocksAsync() => PowerShellAsync("NetSecurity",
        $"Get-NetFirewallRule -PolicyStore PersistentStore -Group '{Group}' -ErrorAction SilentlyContinue " +
        "| Where-Object { $_.Name -match '^BlessedLine\\.AppControls\\.[0-9A-F]{24}\\.(Out|In)$' } " +
        "| Remove-NetFirewallRule -Confirm:$false");

    private static async Task SetUploadAsync(string name, string path, long kbps)
    {
        var ruleName = Quote(name);
        var pathBase64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(path));
        var rate = (kbps * 1000).ToString(CultureInfo.InvariantCulture);
        var query = $"$app = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{pathBase64}')); " +
            $"$q = Get-NetQosPolicy -Name {ruleName} -ErrorAction SilentlyContinue;";
        var command = kbps == 0
            ? $"if ($q) {{ Remove-NetQosPolicy -Name {ruleName} -Confirm:$false }}"
            : $"if ($q) {{ Set-NetQosPolicy -Name {ruleName} -AppPathNameMatchCondition $app -ThrottleRateActionBitsPerSecond {rate} -Confirm:$false }} " +
              $"else {{ New-NetQosPolicy -Name {ruleName} -AppPathNameMatchCondition $app -ThrottleRateActionBitsPerSecond {rate} | Out-Null }}";
        await PowerShellAsync("NetQos", query + command).ConfigureAwait(false);
    }

    private static async Task<string?> TryClearAllUploadAsync()
    {
        try
        {
            await PowerShellAsync("NetQos", "Get-NetQosPolicy | Where-Object { $_.Name -match '^BlessedLine\\.AppControls\\.[0-9A-F]{24}$' } " +
                "| ForEach-Object { Remove-NetQosPolicy -Name $_.Name -Confirm:$false }", optional: true).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Debug.WriteLine(ex);
            return ex.Message;
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    private static async Task PowerShellAsync(string module, string script, bool optional = false)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var native = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var executable = Path.Combine(windows, native, "WindowsPowerShell", "v1.0", "powershell.exe");
        var modules = Path.Combine(windows, "System32", "WindowsPowerShell", "v1.0", "Modules");
        var modules64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(modules));
        var missing = optional ? "if (![IO.File]::Exists($module)) { exit 0 }; " : "";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; $PSModuleAutoLoadingPreference = 'None'; " +
            $"$env:PSModulePath = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{modules64}')); " +
            $"$module = [IO.Path]::Combine($env:PSModulePath, '{module}', '{module}.psd1'); " +
            missing + "Import-Module -Name $module; " + script));
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
            var message = ReadPowerShellError(standardError, module);
            throw new InvalidOperationException(message.Length > 200 ? message[..200] : message);
        }
    }

    private static string ReadPowerShellError(string diagnostic, string module)
    {
        if (!diagnostic.StartsWith("#< CLIXML", StringComparison.Ordinal))
            return string.IsNullOrWhiteSpace(diagnostic) ? $"Windows {module}" : diagnostic.Trim();
        try
        {
            return XDocument.Parse(diagnostic["#< CLIXML".Length..].Trim())
                .Descendants().FirstOrDefault(item => item.Name.LocalName == "S" && (string?)item.Attribute("S") == "Error")?
                .Value.Replace("_x000D__x000A_", " ").Trim() ?? $"Windows {module}";
        }
        catch (System.Xml.XmlException) { return $"Windows {module}"; }
    }
}
