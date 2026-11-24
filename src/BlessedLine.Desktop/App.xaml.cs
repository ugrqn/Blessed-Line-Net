using System.Diagnostics;
using System.Windows;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public partial class App : Application
{
    private readonly SettingsStore _store = new();
    private readonly AdapterMeter _meter = new();
    private WindowStateData _settings = new();
    private string? _startupError;
    private Mutex? _instance;
    private bool _ownsInstance;
    private EventWaitHandle? _restore;
    private RegisteredWaitHandle? _listener;
    private TrayMenu? _tray;
    private TrayMonitor? _monitor;
    private QuotaNotifications? _notifications;
    private TrafficMeasurement? _traffic;
    private WifiControl? _wifi;
    private AppPolicyController? _policies;

    protected override void OnStartup(StartupEventArgs e)
    {
        var startInTray = e.Args.Contains(WindowsStartup.TrayArgument);
        _instance = new Mutex(false, @"Local\BlessedLine.Desktop");
        try
        {
            var wait = e.Args.Contains("--handoff") ? TimeSpan.FromSeconds(8) : TimeSpan.Zero;
            _ownsInstance = _instance.WaitOne(wait);
        }
        catch (AbandonedMutexException) { _ownsInstance = true; }

        if (!_ownsInstance)
        {
            if (!startInTray) SignalExistingWindow();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        try { _settings = _store.Load(); }
        catch (Exception ex) when (ex is System.IO.IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            _startupError = "ReadStateError";
            Debug.WriteLine(ex);
        }

        if (_startupError is null && !WindowsStartup.SetEnabled(_settings.StartWithWindows))
            _startupError = "StartupWriteError";

        if (startInTray && !_settings.StartWithWindows)
        {
            Shutdown();
            return;
        }

        TextCatalog.Select(_settings.Language);
        _settings.SavedCounters?.RemoveAll(counter => counter is null);
        var firstCounter = _settings.SavedCounters is not { Count: > 0 } && _settings.Counter is null;
        if (_settings.SavedCounters is not { Count: > 0 })
            _settings.SavedCounters = [_settings.Counter ?? new Counter
            {
                Name = TextCatalog.Format("TotalCounter"),
                LimitBytes = 1_000_000_000
            }];
        _settings.Counter = _settings.SavedCounters.FirstOrDefault(counter => counter.Id == _settings.Counter?.Id)
            ?? _settings.SavedCounters[0];
        if (firstCounter && _startupError is null)
        {
            try { _store.Save(_settings); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { Debug.WriteLine(ex); }
        }

        _restore = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\BlessedLine.Restore");
        _listener = ThreadPool.RegisterWaitForSingleObject(_restore, (_, _) =>
            Dispatcher.BeginInvoke(() => ShowWindow(mini: false)), null, Timeout.Infinite, false);
        _tray = new TrayMenu(() => ShowWindow(mini: false), () => ShowWindow(mini: true), RestoreWifi, ExitApplication);
        _traffic = new TrafficMeasurement();
        _settings.FavoriteApps ??= [];
        _settings.AppRules = new(_settings.AppRules ?? [], StringComparer.OrdinalIgnoreCase);
        _policies = new AppPolicyController(_settings, _store, _traffic);
        _policies.Failed += message => _tray.Notify(message);
        _wifi = new WifiControl(_settings, _store, _meter, ReportWifi);
        _notifications = new QuotaNotifications(_settings, () => _wifi.CanCut, ApplyQuotaAction);
        if (_settings.AutoMeasureApplications || _policies.HasByteBudgets)
            Dispatcher.BeginInvoke(_traffic.Start);
        if (startInTray)
            _monitor = new TrayMonitor(_settings, _store, _meter, _tray, _notifications, _wifi, _policies);
        else
            ShowWindow(mini: false);
    }

    private static void SignalExistingWindow()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(@"Local\BlessedLine.Restore", out var signal))
            {
                using (signal) signal.Set();
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(TextCatalog.Format("AlreadyOpen"), "BlessedLine");
        }
    }

    private void ShowWindow(bool mini)
    {
        if (MainWindow is MainWindow existing)
        {
            existing.ShowPanel(mini);
            return;
        }

        _monitor?.Dispose();
        _monitor = null;
        var window = new MainWindow(_settings, _store, _meter, _tray!, _notifications!, _traffic!, _wifi!, _policies!, mini, _startupError);
        MainWindow = window;
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    private string? ApplyQuotaAction(Counter counter, QuotaAction action, long bytes)
    {
        if (_settings.SavedCounters?.Contains(counter) != true) return null;
        if (action == QuotaAction.WifiOff)
        {
            var error = _wifi!.TurnOff(counter);
            if (error is null) ReportWifi(TextCatalog.Format("WifiOff"));
            return error;
        }

        if (action == QuotaAction.Extend)
        {
            if (counter.LimitBytes is not long limit) return TextCatalog.Format("InvalidLimit");
            if (limit > long.MaxValue - bytes) return TextCatalog.Format("LimitOverflow");
            if (limit + bytes <= counter.UsedBytes) return TextCatalog.Format("QuotaNeedsLargerExtension");
        }

        var restoring = _settings.WifiRecovery?.Counter == counter.Id;
        if (restoring && _wifi!.Restore() is { } restoreError) return restoreError;

        var original = (counter.LimitBytes, counter.LimitDelivered, counter.WarningDelivered, _settings.ArmedWifi);
        if (action == QuotaAction.Extend)
            counter.Extend(bytes);
        else if (_settings.ArmedWifi?.Counter == counter.Id && counter.LimitBytes is long limit && counter.UsedBytes >= limit)
            _settings.ArmedWifi = null;

        try { _store.Save(_settings); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            (counter.LimitBytes, counter.LimitDelivered, counter.WarningDelivered, _settings.ArmedWifi) = original;
            Debug.WriteLine(ex);
            return TextCatalog.Format("SaveError");
        }

        if (restoring) ReportWifi(TextCatalog.Format("WifiRestored"));
        if (MainWindow is MainWindow main) main.RefreshQuotaAfterAction(counter);
        return null;
    }

    private void ReportWifi(string status)
    {
        _notifications?.RefreshWifi();
        _tray?.Notify(status);
        if (MainWindow is MainWindow main) main.ShowWifiStatus(status);
    }

    private void RestoreWifi()
    {
        if (_settings.WifiRecovery is null) return;
        ShowWindow(mini: false);
        ((MainWindow)MainWindow).RestoreConnection();
    }

    private void ExitApplication()
    {
        if (MainWindow is MainWindow window) window.Quit();
        else Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _listener?.Unregister(null);
        _restore?.Dispose();
        _monitor?.Dispose();
        _traffic?.Dispose();
        _notifications?.Dispose();
        _tray?.Dispose();
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
