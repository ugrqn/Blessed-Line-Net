using System.Windows;

namespace BlessedLine.Desktop;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args is ["--traffic", var pipe, var owner] && Guid.TryParseExact(pipe, "N", out var id) && int.TryParse(owner, out var parent))
        {
            StartupUri = null;
            TrafficHelper.Run(id, parent);
            Shutdown();
            return;
        }
        _instance = new Mutex(false, @"Local\BlessedLine.Desktop");
        try
        {
            var wait = e.Args.Contains("--handoff") ? TimeSpan.FromSeconds(8) : TimeSpan.Zero;
            _ownsInstance = _instance.WaitOne(wait);
        }
        catch (AbandonedMutexException) { _ownsInstance = true; }

        if (!_ownsInstance)
        {
            MessageBox.Show("BlessedLine zaten açık.", "BlessedLine");
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
