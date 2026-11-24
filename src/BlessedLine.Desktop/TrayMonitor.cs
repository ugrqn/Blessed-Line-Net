using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace BlessedLine.Desktop;

// Keeps aggregate counters running without creating the main window.
internal sealed class TrayMonitor : IDisposable
{
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private readonly DispatcherTimer _timer = new() { Interval = Interval };
    private readonly WindowStateData _settings;
    private readonly SettingsStore _store;
    private readonly AdapterMeter _meter;
    private readonly TrayMenu _tray;
    private readonly QuotaNotifications _notifications;
    private readonly WifiControl _wifi;
    private bool _dirty;
    private int _ticks;

    internal TrayMonitor(WindowStateData settings, SettingsStore store, AdapterMeter meter, TrayMenu tray, QuotaNotifications notifications, WifiControl wifi)
    {
        _settings = settings;
        _store = store;
        _meter = meter;
        _tray = tray;
        _notifications = notifications;
        _wifi = wifi;
        _timer.Tick += Sample;
        Sample(this, EventArgs.Empty);
        _timer.Start();
    }

    private void Sample(object? sender, EventArgs args)
    {
        try
        {
            if (_settings.WifiRecovery is not null)
            {
                _tray.Update("—", "—", TextCatalog.Format("WifiOff"));
                return;
            }

            if (++_ticks % 3 == 0 || _meter.Selected is null)
                _meter.Select(AdapterMeter.Preferred(AdapterChoice.Connected(), _settings.AdapterId, pin: _settings.ArmedWifi is not null));

            if (_meter.Read() is not { } sample) return;
            var changed = sample.Received > 0 || sample.Sent > 0;
            var notify = false;
            foreach (var counter in _settings.SavedCounters!)
            {
                if (counter.Add(sample.Received, sample.Sent) is not { } notification) continue;
                _notifications.Notify(counter, notification, _settings.Sound);
                notify = true;
            }

            var selected = _settings.Counter!;
            var remaining = selected.LimitBytes is { } limit
                ? TextCatalog.Format("QuotaLeft", TextCatalog.Bytes(Math.Max(0, limit - selected.UsedBytes)))
                : TextCatalog.Format("NoLimit");
            _tray.Update(
                TextCatalog.Bytes((long)(sample.Received / sample.Elapsed.TotalSeconds)) + TextCatalog.Format("PerSecond"),
                TextCatalog.Bytes((long)(sample.Sent / sample.Elapsed.TotalSeconds)) + TextCatalog.Format("PerSecond"),
                remaining);

            _dirty |= changed || notify;
            if (notify || _ticks % 2 == 0) Flush();
            _wifi.CheckLimit(_settings.SavedCounters!);
        }
        catch (Exception ex) when (ex is NetworkInformationException or OverflowException)
        {
            _meter.Reset();
            Debug.WriteLine(ex);
        }
    }

    private void Flush()
    {
        if (!_dirty) return;
        try
        {
            _store.Save(_settings);
            _dirty = false;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Flush();
    }
}
