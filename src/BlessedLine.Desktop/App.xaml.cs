using System.Windows;

namespace BlessedLine.Desktop;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;
    private EventWaitHandle? _restore;
    private RegisteredWaitHandle? _listener;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args is ["--traffic", var pipe, var owner] && Guid.TryParseExact(pipe, "N", out var id) && int.TryParse(owner, out var parent))
        {
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
            if (EventWaitHandle.TryOpenExisting(@"Local\BlessedLine.Restore", out var signal))
            {
                using (signal) signal.Set();
            }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        _restore = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BlessedLine.Restore");
        _listener = ThreadPool.RegisterWaitForSingleObject(_restore, (_, _) =>
            Dispatcher.BeginInvoke(window.RestoreMainPanel), null, Timeout.Infinite, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _listener?.Unregister(null);
        _restore?.Dispose();
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
