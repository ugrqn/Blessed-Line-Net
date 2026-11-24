using System.Diagnostics;
using System.Media;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed class QuotaNotifications(
    WindowStateData settings, Func<bool> canCutWifi,
    Func<Counter, QuotaAction, long, string?> apply) : IDisposable
{
    private static readonly string[] SoundNames = ["normal", "soft", "strong"];
    private SoundPlayer? _player;
    private readonly Dictionary<Guid, (Counter Counter, CounterEvent Notification)> _pending = new();
    private QuotaAlertWindow? _window;
    private (Counter Counter, CounterEvent Notification)? _active;
    private bool _disposing;

    internal string Notify(Counter counter, CounterEvent notification, int sound)
    {
        var message = TextCatalog.Format(notification == CounterEvent.Limit ? "CounterAtLimit" : "CounterNearLimit", counter.Name);
        Play(sound);
        if (_window is not null && _active?.Counter.Id == counter.Id)
        {
            _active = (counter, notification);
            _window.Update(notification, canCutWifi());
            if (notification == CounterEvent.Limit) _window.Activate();
        }
        else
        {
            _pending[counter.Id] = (counter, notification);
            if (notification == CounterEvent.Limit && _active is { Notification: CounterEvent.Warning } previous)
            {
                _pending[previous.Counter.Id] = previous;
                _window?.Close();
            }
            ShowNext();
        }
        return message;
    }

    private void ShowNext()
    {
        if (_disposing || _window is not null || _pending.Count == 0) return;
        var next = _pending.MaxBy(item => item.Value.Notification == CounterEvent.Limit);
        _pending.Remove(next.Key);
        _active = next.Value;
        var (counter, notification) = next.Value;
        var window = new QuotaAlertWindow(counter, apply, settings.Theme);
        _window = window;
        window.Update(notification, canCutWifi());
        window.Closed += (_, _) =>
        {
            _window = null;
            _active = null;
            if (!_disposing && !window.Dispatcher.HasShutdownStarted)
                window.Dispatcher.BeginInvoke(ShowNext);
        };
        window.Show();
        window.Activate();
    }

    internal void RefreshWifi() => _window?.SetWifiAvailable(canCutWifi());

    internal void Play(int sound)
    {
        try
        {
            var name = SoundNames[Math.Clamp(sound, 0, SoundNames.Length - 1)];
            var wav = System.Windows.Application.GetResourceStream(new Uri($"Assets/{name}.wav", UriKind.Relative));
            if (wav is null) return;
            _player?.Dispose();
            _player = new SoundPlayer(wav.Stream);
            _player.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or TimeoutException)
        {
            Debug.WriteLine(ex);
            SystemSounds.Exclamation.Play();
        }
    }

    public void Dispose()
    {
        _disposing = true;
        _window?.Close();
        _player?.Dispose();
    }
}
