using System.Security.Principal;

namespace BlessedLine.Desktop;

internal sealed class TrafficMeasurement : IDisposable
{
    private ApplicationTraffic? _direct;
    private TrafficHelper? _helper;
    internal bool Elevated { get; } = IsAdministrator();
    internal bool CanStart => Elevated ? _direct is null || _direct.Failed : _helper is null || _helper.Finished;

    internal void Start()
    {
        if (!CanStart) return;
        if (Elevated)
        {
            _direct?.Dispose();
            _direct = new ApplicationTraffic();
        }
        else
        {
            _helper?.Dispose();
            _helper = new TrafficHelper();
            _ = _helper.StartAsync();
        }
    }

    internal (string Status, AppTraffic[] Traffic)? Drain() => _direct?.Drain() ?? _helper?.Drain();

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal void Stop()
    {
        _direct?.Dispose();
        _direct = null;
        _helper?.Dispose();
        _helper = null;
    }

    public void Dispose() => Stop();
}
