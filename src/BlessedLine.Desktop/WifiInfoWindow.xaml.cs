using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace BlessedLine.Desktop;

public partial class WifiInfoWindow : Window
{
    private readonly string? _adapter;
    private bool _closed;

    internal WifiInfoWindow(string? adapter, string theme)
    {
        _adapter = adapter;
        InitializeComponent();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/BlessedLine;component/Themes/{(theme == "Win7" ? "Win7" : "XP")}.xaml", UriKind.Relative)
        });
        Resources.MergedDictionaries.Add(TextCatalog.WindowResources());
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(TextCatalog.Culture.IetfLanguageTag);
        FlowDirection = TextCatalog.Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        NetworkColumn.Header = T("WifiNetwork");
        SignalColumn.Header = T("WifiSignal");
        SecurityColumn.Header = T("WifiSecurity");
        Loaded += async (_, _) => await LoadAsync(scan: false);
        Closed += (_, _) => _closed = true;
    }

    private static string T(string key) => TextCatalog.Format(key);

    private async void Refresh(object sender, RoutedEventArgs e) => await LoadAsync(scan: false);
    private async void Scan(object sender, RoutedEventArgs e) => await LoadAsync(scan: true);

    private async Task LoadAsync(bool scan)
    {
        WifiRefresh.IsEnabled = WifiScan.IsEnabled = false;
        WifiStatus.Text = T("FindingNetworks");
        try
        {
            var info = await Task.Run(() => WifiDiagnostics.ReadAsync(_adapter, scan));
            if (_closed) return;
            Overview.Text = string.IsNullOrWhiteSpace(info.Overview) ? info.Status : info.Overview;
            NearbyNetworks.ItemsSource = info.Nearby;
            WifiStatus.Text = info.Status;
            LocationButton.Visibility = info.NeedsLocation ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is NetworkInformationException or Win32Exception or InvalidOperationException or NotSupportedException)
        {
            if (!_closed) WifiStatus.Text = TextCatalog.Format("WifiReadError", ex.Message);
        }
        finally
        {
            if (!_closed) WifiRefresh.IsEnabled = WifiScan.IsEnabled = true;
        }
    }

    private void SelectNetwork(object sender, SelectionChangedEventArgs e)
    {
        if (NearbyNetworks.SelectedItem is not WifiNetwork network)
        {
            SelectedNetwork.Text = "";
            return;
        }

        SelectedNetwork.Text = $"{T("WifiStandard")} {network.Standard} · {network.Security} / {network.Cipher}\n" +
            $"{T("WifiAccessPoints")} {network.AccessPoints} · {T(network.Connectable ? "WifiCanConnect" : "WifiCannotConnect")}" +
            (network.Connected ? $" · {T("WifiConnected")}" : "") +
            (string.IsNullOrWhiteSpace(network.Profile) ? "" : $"\n{T("WifiProfile")} {network.Profile}");
    }

    private void OpenWindowsWifi(object sender, RoutedEventArgs e) => OpenSetting("ms-settings:network-wifi");
    private void OpenLocation(object sender, RoutedEventArgs e) => OpenSetting("ms-settings:privacy-location");

    private void OpenSetting(string uri)
    {
        try { using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Win32Exception ex) { WifiStatus.Text = TextCatalog.Format("WifiReadError", ex.Message); }
    }
}
