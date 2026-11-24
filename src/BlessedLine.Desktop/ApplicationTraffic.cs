using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace BlessedLine.Desktop;

internal readonly record struct AppTraffic(string? Path, long Received, long Sent);

internal sealed class ApplicationTraffic : IDisposable
{
    internal const int MaxApplications = 2048;
    private const string Unattributed = "<unknown>";
    private readonly object _gate = new();
    private readonly Dictionary<int, ProcessIdentity> _processes = new();
    private readonly Dictionary<string, AppTraffic> _traffic = new(StringComparer.OrdinalIgnoreCase);
    private readonly Thread _worker;
    private TraceEventSession? _session;
    private bool _disposed;
    private volatile bool _failed;
    public bool Failed => _failed;
    private string _status = "MeasurementStarting";

    private sealed record ProcessIdentity(string? Path, DateTime StartedUtc, DateTime RetryAfterUtc = default);

    public ApplicationTraffic()
    {
        _worker = new Thread(ReadEvents) { IsBackground = true, Name = "BlessedLine ETW" };
        _worker.Start();
    }

    public (string Status, AppTraffic[] Traffic) Drain()
    {
        lock (_gate)
        {
            var lost = 0;
            try { lost = _session?.EventsLost ?? 0; }
            catch (COMException) { }
            var status = lost > 0 ? $"EventsLost\t{lost}" : _status;
            var batch = _traffic.Values.ToArray();
            _traffic.Clear();
            return (status, batch);
        }
    }

    private void ReadEvents()
    {
        // Reusing the name for this desktop session also recovers our logger after a crash.
        try
        {
            using var process = Process.GetCurrentProcess();
            using var session = new TraceEventSession($"BlessedLine.ApplicationTraffic.{process.SessionId}") { BufferSizeMB = 8 };
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP | KernelTraceEventParser.Keywords.Process);
            var parser = session.Source.Kernel;
            parser.ProcessStart += e => _processes.Remove(e.ProcessID);
            parser.ProcessStop += e => _processes.Remove(e.ProcessID);
            // TraceEvent repairs ProcessID from the TCP/UDP event payload in Fixup.
            parser.TcpIpSend += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: true);
            parser.TcpIpRecv += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: false);
            parser.TcpIpSendIPV6 += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: true);
            parser.TcpIpRecvIPV6 += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: false);
            parser.UdpIpSend += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: true);
            parser.UdpIpRecv += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: false);
            parser.UdpIpSendIPV6 += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: true);
            parser.UdpIpRecvIPV6 += e => Observe(e.ProcessID, e.TimeStamp, e.saddr, e.daddr, e.size, upload: false);

            lock (_gate)
            {
                if (_disposed)
                    return;

                _session = session;
                _status = "MeasurementActive";
            }

            try
            {
                session.Source.Process();
                lock (_gate)
                    _status = "MeasurementStopped";
            }
            finally
            {
                lock (_gate)
                    _session = null;
            }
        }
        catch (Exception ex)
        {
            _failed = true;
            Debug.WriteLine(ex);
            lock (_gate)
                _status = "MeasurementFailed";
        }
    }

    private void Observe(int pid, DateTime time, IPAddress local, IPAddress remote, int bytes, bool upload)
    {
        if (bytes <= 0 || IsLoopback(local) || IsLoopback(remote))
            return;

        var utc = time.ToUniversalTime();
        var identity = Resolve(pid, utc);
        var path = identity is not null && identity.StartedUtc <= utc ? identity.Path : null;
        var key = path ?? Unattributed;

        lock (_gate)
        {
            if (_traffic.Count >= MaxApplications && !_traffic.ContainsKey(key))
            {
                key = Unattributed;
                path = null;
            }
            if (!_traffic.TryGetValue(key, out var sample))
                sample = new AppTraffic(path, 0, 0);

            _traffic[key] = upload
                ? sample with { Sent = sample.Sent + bytes }
                : sample with { Received = sample.Received + bytes };
        }
    }

    private ProcessIdentity? Resolve(int pid, DateTime now)
    {
        if (pid <= 4)
            return null;
        if (_processes.TryGetValue(pid, out var found) && (found.Path is not null || found.RetryAfterUtc > now))
            return found;

        if (_processes.Count >= 4096)
            _processes.Clear();

        try
        {
            using var process = Process.GetProcessById(pid);
            found = new ProcessIdentity(ConnectedApps.ImagePath(pid), process.StartTime.ToUniversalTime(), now.AddSeconds(2));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            found = new ProcessIdentity(null, DateTime.MaxValue, now.AddSeconds(2));
        }
        _processes[pid] = found;
        return found;
    }

    private static bool IsLoopback(IPAddress address) =>
        IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    public void Dispose()
    {
        TraceEventSession? session;
        lock (_gate)
        {
            _disposed = true;
            session = _session;
        }
        try { session?.Stop(); }
        catch (Exception ex) when (ex is COMException or ObjectDisposedException) { Debug.WriteLine(ex); }
    }
}

internal static class ApplicationName
{
    public static string FromPath(string? path)
    {
        if (path is null) return "Atfedilemeyen";
        var executable = Path.GetFileNameWithoutExtension(path);
        return executable.ToLowerInvariant() switch
        {
            "chrome" => "Chrome",
            "msedge" => "Edge",
            "firefox" => "Firefox",
            "whatsapp" or "whatsapp.root" or "whatsapp.desktop" => "WhatsApp",
            "steam" => "Steam",
            "steamwebhelper" => "Steam Web",
            "discord" => "Discord",
            "ms-teams" or "teams" => "Teams",
            "chatgpt" => "ChatGPT",
            "codex" => "Codex",
            "svchost" => "Windows Hizmeti",
            "pwsh" or "powershell" => "PowerShell",
            "blessedline" => "BlessedLine",
            _ => executable.Length <= 25 ? executable : executable[..24] + "…"
        };
    }
}
