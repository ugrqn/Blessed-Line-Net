using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace BlessedLine.Desktop;

// The elevated peer exposes only ETW batches and a typed, local application-policy protocol.
internal sealed class TrafficHelper(bool captureTraffic) : IDisposable
{
    internal bool Capturing { get; } = captureTraffic;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<StreamWriter?> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly Dictionary<string, AppTraffic> _pending = new(StringComparer.OrdinalIgnoreCase);
    private string _status = "AwaitingPermission";
    private volatile bool _finished;
    private volatile TaskCompletionSource<PolicyResponse>? _reply;
    private long _nextRequest;
    internal bool Finished => _finished;

    public sealed record Packet(string Status, AppTraffic[] Traffic, PolicyResponse? Policy = null);
    public sealed record PolicyCommand(long Id, PolicyRequest Request);
    public sealed record PolicyResponse(long Id, PolicyResult Result);

    internal async Task StartAsync()
    {
        var id = Guid.NewGuid();
        var token = _stop.Token;
        try
        {
            using var pipe = new NamedPipeServerStream($"BlessedLine-{id:N}", PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            var mode = Capturing ? "--traffic" : "--policy-only";
            using var process = await Task.Run(() => Process.Start(new ProcessStartInfo(executable, $"{mode} {id:N} {Environment.ProcessId}")
                { UseShellExecute = true, Verb = "runas" }), token).ConfigureAwait(false) ?? throw new InvalidOperationException();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            while (true)
            {
                await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
                if (GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var peer) && peer == process.Id) break;
                pipe.Disconnect();
            }

            using var reader = new StreamReader(pipe, leaveOpen: true);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            _connected.TrySetResult(writer);
            while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                var packet = JsonSerializer.Deserialize<Packet>(line);
                if (packet is null) continue;
                if (packet.Policy is { } reply)
                {
                    if (reply.Id == Interlocked.Read(ref _nextRequest)) _reply?.TrySetResult(reply);
                    continue;
                }
                lock (_pending)
                {
                    _status = packet.Status;
                    foreach (var sample in packet.Traffic)
                    {
                        var key = sample.Path ?? "";
                        if (_pending.Count >= ApplicationTraffic.MaxApplications && !_pending.ContainsKey(key))
                            key = "";
                        _pending.TryGetValue(key, out var old);
                        _pending[key] = new AppTraffic(key.Length == 0 ? null : key,
                            old.Received + sample.Received, old.Sent + sample.Sent);
                    }
                }
            }
            lock (_pending) _status = "RateMeasurementEnded";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or Win32Exception or InvalidOperationException or JsonException)
        {
            lock (_pending)
                _status = ex is Win32Exception { NativeErrorCode: 1223 } ? "PermissionCancelled" : "HelperUnavailable";
        }
        finally
        {
            _finished = true;
            _connected.TrySetResult(null);
            _reply?.TrySetCanceled();
        }
    }

    internal async Task<PolicyResult> ApplyAsync(PolicyRequest request)
    {
        if (_finished) return new(false, "HelperUnavailable");
        try
        {
            await _requests.WaitAsync(_stop.Token).ConfigureAwait(false);
            try
            {
                var writer = await _connected.Task.WaitAsync(_stop.Token).ConfigureAwait(false);
                if (writer is null || _finished) return new(false, _status);
                var number = Interlocked.Increment(ref _nextRequest);
                var reply = new TaskCompletionSource<PolicyResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                _reply = reply;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new PolicyCommand(number, request))).ConfigureAwait(false);
                var response = await reply.Task.WaitAsync(TimeSpan.FromSeconds(28), _stop.Token).ConfigureAwait(false);
                return response.Id == number ? response.Result : new(false, "HelperUnavailable");
            }
            finally { _reply = null; _requests.Release(); }
        }
        catch (TimeoutException) { return new(false, "PolicyTimeout"); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
        {
            return new(false, "HelperUnavailable");
        }
    }

    internal (string Status, AppTraffic[] Traffic) Drain()
    {
        lock (_pending)
        {
            var samples = _pending.Values.ToArray();
            _pending.Clear();
            return (_status, samples);
        }
    }

    internal static void Run(Guid id, int parent, bool captureTraffic)
    {
        try { ServeAsync(id, parent, captureTraffic).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or JsonException) { }
    }

    private static async Task ServeAsync(Guid id, int parent, bool captureTraffic)
    {
        using var pipe = new NamedPipeClientStream(".", $"BlessedLine-{id:N}", PipeDirection.InOut,
            PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(10000).ConfigureAwait(false);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var server) || server != parent) return;
        using var capture = captureTraffic ? new ApplicationTraffic() : null;
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1500));
        var read = reader.ReadLineAsync();
        var tick = timer.WaitForNextTickAsync().AsTask();

        while (true)
        {
            if (await Task.WhenAny(read, tick).ConfigureAwait(false) == read)
            {
                var line = await read.ConfigureAwait(false);
                if (line is null) return;
                if (line.Length <= 16_384 && JsonSerializer.Deserialize<PolicyCommand>(line) is { Request: { } request } command)
                {
                    var result = await ApplicationPolicy.ExecuteAsync(request).ConfigureAwait(false);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new Packet("", [], new PolicyResponse(command.Id, result))))
                        .ConfigureAwait(false);
                }
                read = reader.ReadLineAsync();
            }
            else
            {
                if (!await tick.ConfigureAwait(false)) return;
                var (status, batch) = capture?.Drain() ?? ("ConnectionsOnly", Array.Empty<AppTraffic>());
                await writer.WriteLineAsync(JsonSerializer.Serialize(new Packet(status, batch))).ConfigureAwait(false);
                tick = timer.WaitForNextTickAsync().AsTask();
            }
        }
    }

    public void Dispose() => _stop.Cancel();

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
}
