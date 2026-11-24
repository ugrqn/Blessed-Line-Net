using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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
            Overview.ItemsSource = info.Overview;
            NearbyNetworks.ItemsSource = info.Nearby;
            NearbyNetworks.SelectedIndex = info.Nearby.Length > 0 ? 0 : -1;
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
            SelectedNetwork.ItemsSource = null;
            return;
        }

        var facts = new List<WifiFact>();
        void Add(string label, string value) => facts.Add(new(T(label), string.IsNullOrEmpty(value) ? "—" : value));
        Add("WifiNetwork", network.Name);
        Add("WifiSignal", $"{network.Quality}%");
        Add("WifiSecurity", $"{network.Security} / {network.Cipher}");
        Add("WifiStandard", network.Standard);
        Add("WifiAccessPoints", network.AccessPoints.ToString(TextCatalog.Culture));
        Add("WifiState", T(network.Connected ? "WifiConnected" : network.Connectable ? "WifiCanConnect" : "WifiCannotConnect"));
        if (!string.IsNullOrWhiteSpace(network.Profile)) Add("WifiProfile", network.Profile);
        if (network.Radio is { } radio)
        {
            Add("WifiRadioAddress", radio.Bssid);
            if (WifiDiagnostics.Channel(radio.FrequencyKhz) is int channel)
                Add("WifiChannel", channel.ToString(TextCatalog.Culture));
        }
        WifiDiagnostics.AddAccessPointFacts(facts, network.Radio);
        SelectedNetwork.ItemsSource = facts;
    }

    private void CopyFact(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: WifiFact fact }) return;
        try
        {
            System.Windows.Clipboard.SetText(fact.Value);
            WifiStatus.Text = TextCatalog.Format("WifiCopied", fact.Label);
        }
        catch (ExternalException) { WifiStatus.Text = T("WifiCopyFailed"); }
    }

    private void OpenWindowsWifi(object sender, RoutedEventArgs e) => OpenSetting("ms-settings:network-wifi");
    private void OpenLocation(object sender, RoutedEventArgs e) => OpenSetting("ms-settings:privacy-location");

    private void OpenSetting(string uri)
    {
        try { using var process = Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Win32Exception ex) { WifiStatus.Text = TextCatalog.Format("WifiReadError", ex.Message); }
    }
}
