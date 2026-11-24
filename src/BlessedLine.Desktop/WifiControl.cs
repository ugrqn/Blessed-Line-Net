using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed class WifiControl(WindowStateData settings, SettingsStore store, AdapterMeter meter, Action<string> report)
{
    private int _connectionAttempt;
    internal bool CanCut => settings.WifiRecovery is null &&
        meter.Selected?.Interface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;

    internal void CheckLimit(Counter counter, long previousUsage)
    {
        if (!CanCut || settings.ArmedWifi is not { } armed || !Guid.TryParse(meter.Selected?.Id, out var adapter)) return;
        if (!armed.Crossed(counter, adapter, previousUsage)) return;
        var error = TurnOff(counter);
        if (error is null)
        {
            report(TextCatalog.Format("WifiOff"));
        }
        else
        {
            // Retry only after the user re-enables automatic cutoff, rather than reporting each sample.
            settings.ArmedWifi = null;
            TrySave();
            report(error);
        }
    }

    internal string? TurnOff(Counter counter)
    {
        if (!CanCut || !Guid.TryParse(meter.Selected?.Id, out var adapter))
            return TextCatalog.Format("NeedWifiQuota");

        ++_connectionAttempt;
        try
        {
            using var radio = WifiRadio.Open();
            var enabled = radio.SoftwareEnabled(adapter);
            if (enabled.Length == 0) return TextCatalog.Format("WifiOffError", "");
            var recovery = new WifiRecovery(adapter, counter.Id, enabled, radio.ConnectedProfile(adapter));
            settings.WifiRecovery = recovery;
            if (!TrySave())
            {
                settings.WifiRecovery = null;
                return TextCatalog.Format("SaveError");
            }
            radio.Set(recovery, enabled: false);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Restore(reconnect: false);
            return TextCatalog.Format("WifiOffError", ex.Message);
        }
    }

    internal string? Restore(bool reconnect = true)
    {
        if (settings.WifiRecovery is not { } recovery) return null;

        try
        {
            using var radio = WifiRadio.Open();
            radio.Set(recovery, enabled: true);
            settings.WifiRecovery = null;
            meter.Reset();
            TrySave();
            if (reconnect && !string.IsNullOrEmpty(recovery.Profile))
                _ = ReconnectAsync(recovery);
            return null;
        }
        catch (Win32Exception ex)
        {
            return TextCatalog.Format("WifiOnError", ex.Message);
        }
    }

    private async Task ReconnectAsync(WifiRecovery recovery)
    {
        var attempt = ++_connectionAttempt;
        var profile = recovery.Profile!;
        try
        {
            using var radio = WifiRadio.Open();
            var nextRequest = 0;
            for (var second = 0; second < 12; second++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (attempt != _connectionAttempt) return;
                if (radio.ConnectedProfile(recovery.Adapter) == profile)
                {
                    report(TextCatalog.Format("WifiReconnected", profile));
                    return;
                }
                if (second < nextRequest) continue;
                try { radio.Connect(recovery.Adapter, profile); nextRequest = second + 4; }
                catch (Win32Exception ex) { Debug.WriteLine(ex); }
            }
        }
        catch (Win32Exception ex) { Debug.WriteLine(ex); }
        if (attempt == _connectionAttempt) report(TextCatalog.Format("WifiReconnectFailed", profile));
    }

    private bool TrySave()
    {
        try { store.Save(settings); return true; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
