using System.IO;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RetroNet.Core;

namespace RetroNet.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string _statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroNet", "counters.json");
    private List<Counter> _counters = [];
    private NetworkInterface? _adapter;
    private long? _previousReceived;
    private long? _previousSent;
    private long _receivedTotal;
    private long _sentTotal;
    private long _lastSampleTick;
    private bool _aero;

    public MainWindow()
    {
        InitializeComponent();
        LoadState();
        RefreshAdapters();
        RenderCounters();
        _timer.Tick += (_, _) => Sample();
        _timer.Start();
    }

    private void RefreshAdapters()
    {
        var selectedId = _adapter?.Id;
        AdapterBox.Items.Clear();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback && a.OperationalStatus == OperationalStatus.Up))
            AdapterBox.Items.Add(new ComboBoxItem { Content = adapter.Name, Tag = adapter.Id });
        var selection = AdapterBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == selectedId);
        AdapterBox.SelectedItem = selection ?? AdapterBox.Items.Cast<ComboBoxItem>().FirstOrDefault();
    }

    private void AdapterChanged(object sender, SelectionChangedEventArgs e)
    {
        var id = (AdapterBox.SelectedItem as ComboBoxItem)?.Tag as string;
        _adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(a => a.Id == id);
        _previousReceived = null;
        _previousSent = null;
        _lastSampleTick = 0;
        StatusText.Text = _adapter is null ? "Ölçüm kesildi · Etkin bağdaştırıcı bulunamadı" : "Ölçüm hazırlanıyor · " + _adapter.Name;
    }

    private void Sample()
    {
        try
        {
            if (_adapter is null || _adapter.OperationalStatus != OperationalStatus.Up)
            {
                RefreshAdapters();
                return;
            }
            var stats = _adapter.GetIPStatistics();
            var received = stats.BytesReceived;
            var sent = stats.BytesSent;
            var now = Stopwatch.GetTimestamp();
            if (_previousReceived is long previousReceived && _previousSent is long previousSent && received >= previousReceived && sent >= previousSent && _lastSampleTick != 0)
            {
                var seconds = Stopwatch.GetElapsedTime(_lastSampleTick, now).TotalSeconds;
                if (seconds > 0 && seconds < 10)
                {
                    var receiveDelta = received - previousReceived;
                    var sendDelta = sent - previousSent;
                    _receivedTotal = checked(_receivedTotal + receiveDelta);
                    _sentTotal = checked(_sentTotal + sendDelta);
                    DownloadSpeed.Text = Units.Format((long)(receiveDelta / seconds)) + "/sn";
                    UploadSpeed.Text = Units.Format((long)(sendDelta / seconds)) + "/sn";
                    DownloadTotal.Text = "Oturum " + Units.Format(_receivedTotal);
                    UploadTotal.Text = "Oturum " + Units.Format(_sentTotal);
                    foreach (var counter in _counters)
                    {
                        var raised = counter.Add(receiveDelta, sendDelta);
                        if (raised is not null)
                            MessageText.Text = $"{counter.Name}: {(raised == CounterEvent.Limit ? "Limit doldu" : "Kota uyarısı")}";
                    }
                    RenderCounters();
                    SaveState();
                }
            }
            _previousReceived = received;
            _previousSent = sent;
            _lastSampleTick = now;
            StatusText.Text = "Ölçülüyor · " + _adapter.Name + " · " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            _previousReceived = null;
            _previousSent = null;
            StatusText.Text = "Ölçüm kesildi · " + ex.Message;
        }
    }

    private void AddCounter(object sender, RoutedEventArgs e)
    {
        try
        {
            long? limit = string.IsNullOrWhiteSpace(LimitBox.Text) ? null : Units.ParseMegabytes(LimitBox.Text);
            long warning = limit is null ? 0 : Units.ParseMegabytes(WarningBox.Text);
            if (limit is not null && warning >= limit) throw new FormatException("Uyarı miktarı kotadan küçük olmalı.");
            _counters.Add(new Counter { Name = string.IsNullOrWhiteSpace(CounterName.Text) ? "Yeni Sayaç" : CounterName.Text.Trim(), Direction = (TrafficDirection)DirectionBox.SelectedIndex, LimitBytes = limit, WarningBeforeBytes = warning });
            RenderCounters();
            SaveState();
            MessageText.Text = "Sayaç başladı.";
        }
        catch (Exception ex) { MessageText.Text = ex.Message; }
    }

    private void RenderCounters()
    {
        CounterList.Items.Clear();
        foreach (var counter in _counters)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8), Background = (System.Windows.Media.Brush)FindResource("Card") };
            row.Children.Add(new TextBlock { Text = counter.Name, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 6, 8, 0) });
            row.Children.Add(new TextBlock { Text = Units.Format(counter.UsedBytes) + (counter.LimitBytes is long limit ? " / " + Units.Format(limit) : "") + " · " + (counter.State == CounterState.Running ? "Çalışıyor" : "Duraklatıldı"), Margin = new Thickness(8, 2, 8, 2) });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 6) };
            var toggle = new Button { Content = counter.State == CounterState.Running ? "Duraklat" : "Sürdür", Padding = new Thickness(6, 2, 6, 2) };
            toggle.Click += (_, _) => { counter.State = counter.State == CounterState.Running ? CounterState.Paused : CounterState.Running; SaveState(); RenderCounters(); };
            actions.Children.Add(toggle);
            var reset = new Button { Content = "Sıfırla", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(6, 2, 6, 2) };
            reset.Click += (_, _) => { counter.Reset(); SaveState(); RenderCounters(); };
            actions.Children.Add(reset);
            if (counter.LimitBytes is not null)
            {
                var extend = new Button { Content = "+100 MB", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(6, 2, 6, 2) };
                extend.Click += (_, _) => { counter.Extend(100_000_000); SaveState(); RenderCounters(); };
                actions.Children.Add(extend);
            }
            var delete = new Button { Content = "Sil", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(6, 2, 6, 2) };
            delete.Click += (_, _) => { _counters.Remove(counter); SaveState(); RenderCounters(); };
            actions.Children.Add(delete);
            row.Children.Add(actions);
            CounterList.Items.Add(row);
        }
    }

    private void LoadState()
    {
        try { if (File.Exists(_statePath)) _counters = JsonSerializer.Deserialize<List<Counter>>(File.ReadAllText(_statePath)) ?? []; }
        catch (Exception ex) { MessageBox.Show("Sayaçlar yüklenemedi: " + ex.Message, "RetroNet Control"); }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var temp = _statePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_counters));
            File.Move(temp, _statePath, true);
        }
        catch (Exception ex) { MessageText.Text = "Sayaç kaydedilemedi: " + ex.Message; }
    }

    private void ChangeTheme(object sender, RoutedEventArgs e)
    {
        _aero = !_aero;
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri("Themes/" + (_aero ? "Aero" : "Luna") + ".xaml", UriKind.Relative) };
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) => SaveState();
}
