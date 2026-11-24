using System.Diagnostics;
using System.Media;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed class QuotaNotifications(TrayMenu tray) : IDisposable
{
    private static readonly string[] SoundNames = ["normal", "soft", "strong"];
    private SoundPlayer? _player;

    internal string Notify(Counter counter, CounterEvent notification, int sound)
    {
        var message = TextCatalog.Format(notification == CounterEvent.Limit ? "CounterAtLimit" : "CounterNearLimit", counter.Name);
        Play(sound);
        tray.Notify(message);
        return message;
    }

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

    public void Dispose() => _player?.Dispose();
}
