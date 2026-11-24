using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace BlessedLine.Desktop;

// One-way local channel. The elevated process only reports measurements and accepts no commands.
internal sealed class TrafficHelper : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, AppTraffic> _pending = new(StringComparer.OrdinalIgnoreCase);
    private string _status = "Windows izni bekleniyor…";
    private volatile bool _finished;
    public bool Finished => _finished;
    public sealed record Packet(string Status, AppTraffic[] Traffic);

    public async Task StartAsync()
    {
        var id = Guid.NewGuid();
        var token = _stop.Token;
        try
        {
            using var pipe = new NamedPipeServerStream($"BlessedLine-{id:N}", PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            using var process = Process.Start(new ProcessStartInfo(executable, $"--traffic {id:N} {Environment.ProcessId}")
                { UseShellExecute = true, Verb = "runas" }) ?? throw new InvalidOperationException();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            // Reject any peer other than the elevated instance that this UI just launched.
            while (true)
            {
                await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
                if (GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var peer) && peer == process.Id) break;
                pipe.Disconnect();
            }
            using var reader = new StreamReader(pipe);
            while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
            {
                var packet = JsonSerializer.Deserialize<Packet>(line);
                if (packet is null) continue;
                lock (_pending)
                {
                    _status = packet.Status;
                    foreach (var sample in packet.Traffic)
                    {
                        var key = sample.Path ?? "";
                        _pending[key] = _pending.TryGetValue(key, out var old)
                            ? sample with { Received = old.Received + sample.Received, Sent = old.Sent + sample.Sent }
                            : sample;
                    }
                }
            }
            lock (_pending) _status = "Hız ölçümü sona erdi";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or Win32Exception or InvalidOperationException or JsonException)
        {
            lock (_pending)
                _status = ex is Win32Exception { NativeErrorCode: 1223 } ? "Ölçüm izni iptal edildi" : "Ölçüm yardımcısına erişilemedi";
        }
        finally { _finished = true; }
    }

    public (string Status, AppTraffic[] Traffic) Drain()
    {
        lock (_pending)
        {
            var samples = _pending.Values.ToArray();
            _pending.Clear();
            return (_status, samples);
        }
    }

    internal static void Run(Guid id, int parent)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", $"BlessedLine-{id:N}", PipeDirection.Out,
                PipeOptions.None, System.Security.Principal.TokenImpersonationLevel.Identification);
            pipe.Connect(10000);
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var server) || server != parent) return;
            using var capture = new ApplicationTraffic();
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            while (true)
            {
                Thread.Sleep(700);
                var (status, batch) = capture.Drain();
                writer.WriteLine(JsonSerializer.Serialize(new Packet(status, batch)));
                if (capture.Failed) return;
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { }
    }

    public void Dispose() => _stop.Cancel();

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
}
