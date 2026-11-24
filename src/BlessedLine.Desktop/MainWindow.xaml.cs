using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public partial class MainWindow : Window
{
    private static readonly string[] SoundNames = ["normal", "soft", "strong"];
    private const string UnknownApplication = "<unknown>";
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ObservableCollection<ApplicationRow> _rows = new();
    private readonly Dictionary<string, ApplicationRow> _byApplication = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<Counter> _counters = new();
    private readonly SettingsStore _store = new();
    private readonly ImageSource _fallbackIcon;
    private TrayMenu? _tray;
    private TrafficHelper? _helper;
    private bool _quit, _discoveryPending, _sessionEnding;
    private string _measureStatus = "Bağlantılar listeleniyor · hız ölçümü kapalı";
    private string _discoveryStatus = "";
    private readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush AvailableBrush = new(Color.FromRgb(62, 146, 64));
    private static readonly SolidColorBrush FullBrush = new(Color.FromRgb(193, 55, 45));
    private readonly bool _isAdmin = IsAdministrator();
    private WindowStateData _settings = new();
    private ApplicationTraffic? _applicationTraffic;
    private SoundPlayer? _player;
    private AdapterChoice? _adapter;
    private TrafficReading? _previous;
    private WifiQuota? _wifiQuota;
    private bool _ready, _refreshing, _mini, _details, _pendingSave;
    private int _ticks;
    private double _mainLeft, _mainTop;
    private double _quotaFraction;
    private TimeSpan _lastAppSample;
    private TimeSpan _lastSave;
    private Counter Current => (Counter?)Counters.SelectedItem ?? _counters[0];

    public MainWindow()
    {
        using var fallback = ApplicationIcon.Stock(2);
        _fallbackIcon = ApplicationIcon.Image(fallback);
        InitializeComponent();
        System.Windows.Application.Current.SessionEnding += (_, _) => _sessionEnding = true;
        StateChanged += (_, _) =>
        {
            if (_ready && WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                ShowPanel(true);
            }
        };
        Applications.ItemsSource = _rows;
        Counters.ItemsSource = _counters;
        _timer.Tick += (_, _) => Sample();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        try { _settings = _store.Load(); }
        catch (Exception ex) when (ex is System.IO.IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Feedback.Text = "Önceki sayaç verileri okunamadı.";
            Debug.WriteLine(ex);
        }
        foreach (var counter in _settings.SavedCounters ?? [])
            _counters.Add(counter);
        if (_counters.Count == 0)
            _counters.Add(_settings.Counter ?? new Counter { Name = "Toplam sayaç" });
        Counters.SelectedItem = _counters.FirstOrDefault(item => item.Id == _settings.Counter?.Id) ?? _counters[0];
        PinDetails.IsChecked = _settings.MainDetails;
        PinMiniDetails.IsChecked = _settings.MiniDetails;
        Sounds.SelectedIndex = Math.Clamp(_settings.Sound, 0, SoundNames.Length - 1);

        (_settings.Theme == "Win7" ? AeroThemeOption : XpThemeOption).IsChecked = true;
        SetTheme();
        _details = _settings.MainDetails || Environment.GetCommandLineArgs().Contains("--handoff");
        _tray = new TrayMenu(() => ShowPanel(false), () => ShowPanel(true), () => RestoreConnection(this, new()), Quit);
        _ready = true;
        ApplyLayout();
        SetCounterFields();
        RefreshAdapters();
        if (_isAdmin)
            _applicationTraffic = new ApplicationTraffic();
        UpdateDetailsStatus();
        _timer.Start();
        _ = DiscoverAsync();
        if (_isAdmin && Environment.GetCommandLineArgs().Contains("--arm-wifi"))
            BlockCheck.IsChecked = true;
        if (_isAdmin && Environment.GetCommandLineArgs().Contains("--restore-wifi"))
            RestoreConnection(this, new());
    }

    private void RefreshAdapters()
    {
        try
        {
            var list = AdapterChoice.Connected();
            var selected = list.FirstOrDefault(item => item.Id == _adapter?.Id)
                ?? list.FirstOrDefault(item => item.Id == _settings.AdapterId)
                ?? list.FirstOrDefault();
            if (_settings.WifiRecovery is not null && selected?.Id != _settings.AdapterId)
                selected = null;

            var current = Adapters.ItemsSource as IReadOnlyList<AdapterChoice>;
            if (current is null || !current.Select(item => (item.Id, item.Label)).SequenceEqual(list.Select(item => (item.Id, item.Label))))
            {
                _refreshing = true;
                try
                {
                    Adapters.ItemsSource = list;
                    Adapters.SelectedItem = selected;
                }
                finally { _refreshing = false; }
            }

            if (_adapter?.Id != selected?.Id)
                ChangeAdapter(selected);
            else
                _adapter = selected;

            if (selected is null)
                NetworkStatus.Text = _settings.WifiRecovery is null ? "Bağlı ağ bulunamadı" : "Wi-Fi kota nedeniyle kapalı";
        }
        catch (NetworkInformationException ex)
        {
            _previous = null;
            NetworkStatus.Text = "Bağlantı bilgisi alınamadı";
            Debug.WriteLine(ex);
        }
    }

    private void AdapterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshing)
            return;
        ChangeAdapter(Adapters.SelectedItem as AdapterChoice);
        Save();
    }

    private void ChangeAdapter(AdapterChoice? adapter)
    {
        ClearArming();
        _adapter = adapter;
        _previous = null;
        _settings.AdapterId = adapter?.Id ?? _settings.AdapterId;
        NetworkStatus.Text = adapter is null ? "Bağlı ağ bulunamadı" : $"{adapter.Label} · ölçüm hazırlanıyor";
        DownloadRate.Text = UploadRate.Text = "—";
    }

    private void Sample()
    {
        if (++_ticks % 5 == 0 || _adapter is null)
            RefreshAdapters();
        if (_settings.WifiRecovery is null && _adapter is { } adapter)
            SampleAdapter(adapter);

        if (_ticks % 3 == 0 && (_details && IsVisible))
            _ = DiscoverAsync();

        var reading = _applicationTraffic?.Drain() ?? _helper?.Drain();
        if (reading is { } measurement)
        {
            var elapsed = (_clock.Elapsed - _lastAppSample).TotalSeconds;
            _lastAppSample = _clock.Elapsed;
            UpdateAppRows(measurement.Traffic, elapsed > 0 && elapsed < 10 ? elapsed : 0);
            _measureStatus = measurement.Status;
        }
        UpdateDetailsStatus();
        _tray?.Update(DownloadRate.Text, UploadRate.Text, QuotaHint.Text);
        if (_pendingSave && _clock.Elapsed - _lastSave >= TimeSpan.FromSeconds(5))
            Save();
    }

    private void SampleAdapter(AdapterChoice adapter)
    {
        try
        {
            var stats = adapter.Interface.GetIPStatistics();
            var reading = new TrafficReading(adapter.Id, stats.BytesReceived, stats.BytesSent, _clock.Elapsed);
            var delta = reading.DifferenceFrom(_previous);
            _previous = reading;
            if (delta is not { } sample)
            {
                DownloadRate.Text = UploadRate.Text = "—";
                NetworkStatus.Text = $"{adapter.Label} · ölçüm hazırlanıyor";
                return;
            }

            NetworkStatus.Text = $"{adapter.Label} · ölçülüyor";
            DownloadRate.Text = Units.Format((long)(sample.Received / sample.Elapsed.TotalSeconds)) + "/sn";
            UploadRate.Text = Units.Format((long)(sample.Sent / sample.Elapsed.TotalSeconds)) + "/sn";

            foreach (var counter in _counters)
            {
                var notification = counter.Add(sample.Received, sample.Sent);
                if (notification is null)
                    continue;

                Feedback.Text = notification == CounterEvent.Limit
                    ? $"{counter.Name} · limit doldu"
                    : $"{counter.Name} · limite yaklaşıldı";
                PlaySound();
                _tray?.Notify(Feedback.Text);
                Save();
            }

            _pendingSave |= sample.Received > 0 || sample.Sent > 0;
            CheckWifiQuota();
            UpdateQuota();
        }
        catch (Exception ex) when (ex is NetworkInformationException or OverflowException)
        {
            _previous = null;
            NetworkStatus.Text = "Ölçüm kesildi · bağlantı kontrol ediliyor";
            DownloadRate.Text = UploadRate.Text = "—";
            Debug.WriteLine(ex);
        }
    }

    private void UpdateAppRows(AppTraffic[] batch, double seconds)
    {
        var updated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in batch)
        {
            var key = item.Path ?? UnknownApplication;
            if (!_byApplication.TryGetValue(key, out var row))
            {
                if (_rows.Count >= 2048 && key != UnknownApplication)
                    continue;
                row = new ApplicationRow(item.Path, _fallbackIcon);
                _byApplication.Add(key, row);
                _rows.Add(row);
            }
            row.Update(item, seconds);
            updated.Add(key);
        }

        foreach (var (key, row) in _byApplication)
            if (!updated.Contains(key))
                row.Update(null, seconds);
    }

    private async Task DiscoverAsync()
    {
        if (_discoveryPending || _quit) return;
        _discoveryPending = true;
        try
        {
            var found = await Task.Run(ConnectedApps.Read);
            if (_quit) return;
            _connected.Clear();
            foreach (var app in found)
            {
                _connected.Add(app.Path);
                if (!_byApplication.TryGetValue(app.Path, out var row))
                {
                    row = new ApplicationRow(app.Path, _fallbackIcon);
                    _byApplication.Add(app.Path, row);
                    _rows.Add(row);
                }
                row.SetAppId(app.AppId);
            }
            foreach (var row in _rows.Where(row => row.Total == 0 && row.Path is not null && !_connected.Contains(row.Path)).ToArray())
            {
                _rows.Remove(row);
                _byApplication.Remove(row.Path!);
            }
            _discoveryStatus = "";
        }
        catch (Exception ex) when (ex is Win32Exception or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _discoveryStatus = "Bağlantı listesi okunamadı";
            Debug.WriteLine(ex);
        }
        finally
        {
            _discoveryPending = false;
            if (!_quit) UpdateDetailsStatus();
        }
    }

    private void CounterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        ClearArming();
        SetCounterFields();
        Save();
    }

    private void SetCounterFields()
    {
        LimitInput.Text = Current.LimitBytes is long limit
            ? (limit / 1_000_000m).ToString("0.######", CultureInfo.GetCultureInfo("tr-TR"))
            : "";
        UpdateQuota();
    }

    private void UpdateQuota()
    {
        var counter = Current;
        _quotaFraction = QuotaProgress.Fraction(counter.UsedBytes, counter.LimitBytes);
        QuotaText.Text = counter.LimitBytes is long limit
            ? $"Toplam harcanan · {Units.Format(counter.UsedBytes)} / {Units.Format(limit)}"
            : $"Toplam harcanan · {Units.Format(counter.UsedBytes)}";
        QuotaPercent.Text = counter.LimitBytes is null ? "" : $"%{Math.Floor(_quotaFraction * 100):0}";
        QuotaHint.Text = _settings.WifiRecovery is not null && !_mini ? "Wi-Fi kota nedeniyle kapalı"
            : counter.State == CounterState.Paused ? "Sayaç duraklatıldı"
            : counter.LimitBytes is null ? "Limit belirlenmedi"
            : counter.UsedBytes >= counter.LimitBytes ? "Limit doldu"
            : $"{Units.Format(counter.LimitBytes.Value - counter.UsedBytes)} kaldı";
        if (_mini && counter.LimitBytes is long total)
            QuotaHint.Text = $"{Units.Format(total)} kotadan {Units.Format(Math.Max(0, total - counter.UsedBytes))} kaldı";
        if (!_mini && counter.Direction != TrafficDirection.Both)
            QuotaHint.Text += counter.Direction == TrafficDirection.Download ? " · yalnız indirme" : " · yalnız yükleme";
        PauseButton.Content = counter.State == CounterState.Paused ? "Sürdür" : "Duraklat";
        ExtendButton.IsEnabled = counter.LimitBytes.HasValue;
        TrackFill.Background = _quotaFraction >= 1 ? FullBrush : AvailableBrush;
        QuotaPercent.Foreground = _quotaFraction >= 1 ? FullBrush : (Brush)FindResource("Accent");
        EditQuotaButton.Content = counter.LimitBytes.HasValue ? "Kotayı düzenle…" : "Kota ekle…";
        RestoreButton.Visibility = _settings.WifiRecovery is not null ? Visibility.Visible : Visibility.Collapsed;
        Adapters.IsEnabled = _settings.WifiRecovery is null;
        UpdateMarker();
    }

    private void TrackResized(object sender, SizeChangedEventArgs e) => UpdateMarker();

    private void UpdateMarker()
    {
        TrackFill.Width = Track.ActualWidth * _quotaFraction;
    }

    private void NewCounter(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Text = $"Sayaç {_counters.Count + 1}", MaxLength = 40, Margin = new Thickness(0, 7, 0, 9) };
        var direction = new ComboBox { Margin = new Thickness(0, 4, 0, 9) };
        direction.ItemsSource = new[] { "Toplam trafik", "Yalnız indirme", "Yalnız yükleme" };
        direction.SelectedIndex = 0;
        var add = new Button { Content = "Başlat", IsDefault = true };
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "Sayaç adı" });
        panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = "Ölçülecek trafik" });
        panel.Children.Add(direction);
        panel.Children.Add(add);
        var dialog = new Window
        {
            Owner = this, Title = "Yeni sayaç", Content = panel, Width = 280, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = FontFamily, Background = Background
        };
        add.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => { name.SelectAll(); name.Focus(); };
        if (dialog.ShowDialog() != true) return;

        var counter = new Counter
        {
            Name = string.IsNullOrWhiteSpace(name.Text) ? "Yeni sayaç" : name.Text.Trim(),
            Direction = direction.SelectedIndex switch
            {
                1 => TrafficDirection.Download,
                2 => TrafficDirection.Upload,
                _ => TrafficDirection.Both
            }
        };
        _counters.Add(counter);
        Counters.SelectedItem = counter;
    }

    private void DeleteCounter(object sender, RoutedEventArgs e)
    {
        var counter = Current;
        if (_counters.Count == 1)
        {
            QuotaPopup.IsOpen = false;
            Feedback.Text = "Son sayacı silmek için önce yeni bir sayaç oluşturun.";
            return;
        }
        QuotaPopup.IsOpen = false;
        if (MessageBox.Show(this, $"{counter.Name} silinsin mi?", "BlessedLine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes ||
            (_settings.WifiRecovery?.Counter == counter.Id && !RestoreWifi())) return;
        Counters.SelectedItem = _counters.First(item => item != counter);
        _counters.Remove(counter);
        Save();
    }

    private void PauseCounter(object sender, RoutedEventArgs e)
    {
        Current.State = Current.State == CounterState.Paused ? CounterState.Running : CounterState.Paused;
        UpdateQuota();
        Save();
    }

    private void ResetCounter(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"{Current.Name} sıfırlansın mı?", "BlessedLine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes ||
            (_settings.WifiRecovery?.Counter == Current.Id && !RestoreWifi())) return;
        ClearArming();
        Current.Reset();
        UpdateQuota();
        Save();
    }

    private void SetLimit(object sender, RoutedEventArgs e)
    {
        try
        {
            long? limit = string.IsNullOrWhiteSpace(LimitInput.Text) ? null : Units.ParseMegabytes(LimitInput.Text);
            if (_settings.WifiRecovery?.Counter == Current.Id &&
                (limit is null || Current.UsedBytes < limit) && !RestoreWifi()) return;
            if (limit is null) ClearArming();
            Current.LimitBytes = limit;
            Current.WarningBeforeBytes = limit is long bytes ? bytes / 10 : 0;
            Current.LimitDelivered = false;
            Current.WarningDelivered = false;
            UpdateQuota();
            Save();
            Feedback.Text = limit.HasValue ? "Limit güncellendi · %90’da uyarı" : "Limit kaldırıldı";
            CheckWifiQuota();
            UpdateQuota();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            Feedback.Text = "Geçerli bir MB limiti girin. Ondalık ayırıcı virgüldür.";
        }
    }

    private void ExtendLimit(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentLimit = Current.LimitBytes!.Value;
            var extended = checked(currentLimit + 100_000_000);
            if (Current.UsedBytes < extended && _settings.WifiRecovery?.Counter == Current.Id && !RestoreWifi()) return;
            Current.Extend(100_000_000);
            SetCounterFields();
            Save();
        }
        catch (OverflowException) { Feedback.Text = "Limit daha fazla artırılamıyor."; }
    }

    private void ArmBlock(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var counter = Current;
        if (_settings.WifiRecovery is not null || counter.LimitBytes is not > 0 ||
            _adapter?.Interface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 ||
            !Guid.TryParse(_adapter.Id, out var adapter))
        {
            ClearArming();
            Feedback.Text = "Otomatik kapatma için Wi-Fi bağlantısını seçip kota ekleyin.";
            return;
        }

        if (!_isAdmin)
        {
            ClearArming();
            OfferElevation("--arm-wifi");
            return;
        }

        QuotaPopup.IsOpen = false;
        var answer = MessageBox.Show(this, "Kota dolduğunda seçili Wi-Fi radyosu kapanır. Wi-Fi’yi geri aç butonuyla yeniden açabilirsiniz.",
            "BlessedLine · Wi-Fi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes || Current.Id != counter.Id ||
            !Guid.TryParse(_adapter?.Id, out var selected) || selected != adapter)
        {
            ClearArming();
            return;
        }

        _wifiQuota = new(counter.Id, adapter);
        Feedback.Text = "Kota dolunca Wi-Fi otomatik kapanacak.";
        CheckWifiQuota();
        UpdateQuota();
    }

    private void DisarmBlock(object sender, RoutedEventArgs e) => _wifiQuota = null;

    private void ClearArming()
    {
        _wifiQuota = null;
        BlockCheck.IsChecked = false;
    }

    private void CheckWifiQuota()
    {
        if (_wifiQuota is not { } quota || !Guid.TryParse(_adapter?.Id, out var adapter) ||
            !quota.IsDue(Current, adapter) || _settings.WifiRecovery is not null) return;

        ClearArming();
        try
        {
            using var radio = WifiRadio.Open();
            var enabled = radio.SoftwareEnabled(adapter);
            if (enabled.Length == 0) throw new InvalidOperationException();
            var entry = new WifiRecovery(adapter, quota.Counter, enabled);
            _settings.WifiRecovery = entry;
            if (!Save())
            {
                _settings.WifiRecovery = null;
                return;
            }
            radio.Set(entry, enabled: false);
            DownloadRate.Text = UploadRate.Text = "—";
            NetworkStatus.Text = Feedback.Text = "Wi-Fi kota nedeniyle kapalı";
            _tray?.Notify(Feedback.Text);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            RestoreWifi();
            Feedback.Text = $"Wi-Fi kapatılamadı. {ex.Message}";
            _tray?.Notify(Feedback.Text);
        }
    }

    private bool RestoreWifi()
    {
        if (_settings.WifiRecovery is not { } entry) return true;
        try
        {
            using var radio = WifiRadio.Open();
            radio.Set(entry, enabled: true);
            _settings.WifiRecovery = null;
            _previous = null;
            Save();
            return true;
        }
        catch (Win32Exception ex)
        {
            Feedback.Text = $"Wi-Fi açılamadı. {ex.Message}";
            return false;
        }
    }

    private void RestoreConnection(object sender, RoutedEventArgs e)
    {
        if (_settings.WifiRecovery is null) return;
        if (!_isAdmin)
        {
            OfferElevation("--restore-wifi");
            return;
        }
        if (!RestoreWifi()) return;
        Feedback.Text = "Wi-Fi yeniden açıldı.";
        RefreshAdapters();
        UpdateQuota();
    }

    private async void Authorize(object sender, RoutedEventArgs e)
    {
        if (_isAdmin)
        {
            _applicationTraffic?.Dispose();
            _applicationTraffic = new ApplicationTraffic();
        }
        else
        {
            if (_helper is { Finished: false }) return;
            _helper?.Dispose();
            _helper = new TrafficHelper();
            AuthorizeButton.IsEnabled = false;
            try { await _helper.StartAsync(); }
            finally { AuthorizeButton.IsEnabled = true; }
        }
    }

    private void OfferElevation(string action)
    {
        if (MessageBox.Show(this, "Wi-Fi radyosunu yönetmek için BlessedLine’ı yönetici olarak yeniden açın.",
            "BlessedLine", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        Save();
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            using var process = Process.Start(new ProcessStartInfo(executable, $"--handoff {action}") { UseShellExecute = true, Verb = "runas" });
            if (process is not null) Quit();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Feedback.Text = "Yönetici olarak açma iptal edildi veya Windows tarafından reddedildi.";
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void ToggleDetails(object sender, RoutedEventArgs e)
    {
        _details = !_details;
        ApplyLayout();
        UpdateDetailsStatus();
    }

    private void UpdateDetailsStatus()
    {
        var canStart = _isAdmin ? _applicationTraffic?.Failed == true : _helper is null or { Finished: true };
        AuthorizeButton.Visibility = _details && canStart ? Visibility.Visible : Visibility.Collapsed;
        DetailStatus.Text = _details ? (_discoveryStatus.Length > 0 ? _discoveryStatus : _measureStatus) : "";
        EmptyApps.Visibility = _details && _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyApps.Text = "Etkin ağ bağlantısı bekleniyor…";
    }

    internal void RestoreMainPanel() => ShowPanel(false);

    private void ToggleMini(object sender, RoutedEventArgs e) => ShowPanel(!_mini);

    private void ShowPanel(bool mini)
    {
        if (!_mini && mini) { _mainLeft = Left; _mainTop = Top; }
        var restore = _mini && !mini;
        SettingsPopup.IsOpen = QuotaPopup.IsOpen = false;
        _mini = mini;
        _details = mini ? _settings.MiniDetails : _settings.MainDetails;
        ApplyLayout();
        if (restore)
        {
            var work = SystemParameters.WorkArea;
            Left = Math.Clamp(_mainLeft, work.Left, Math.Max(work.Left, work.Right - Width));
            Top = Math.Clamp(_mainTop, work.Top, Math.Max(work.Top, work.Bottom - Height));
        }
        Show();
        Activate();
        UpdateDetailsStatus();
        if (_details) _ = DiscoverAsync();
    }

    private void ApplyLayout()
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var source = PresentationSource.FromVisual(this);
        var fromPixels = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var area = screen.WorkingArea;
        var work = new Rect(fromPixels.Transform(new Point(area.Left, area.Top)), fromPixels.Transform(new Point(area.Right, area.Bottom)));
        Topmost = _mini;
        ShowInTaskbar = !_mini;
        var mainSize = Math.Min(550, Math.Min(work.Width, work.Height) - 12);
        Width = _mini ? (_details ? 462 : 304) : mainSize;
        Height = _mini ? (_details ? 298 : 138) : mainSize;
        NetworkPanel.Visibility = RatesPanel.Visibility = EditQuotaButton.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        MiniRates.Visibility = _mini ? Visibility.Visible : Visibility.Collapsed;
        AppsPanel.Visibility = _mini && !_details ? Visibility.Collapsed : Visibility.Visible;
        Feedback.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        Applications.Visibility = _details ? Visibility.Visible : Visibility.Collapsed;
        QuotaText.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        QuotaPercent.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        DetailsLabel.Text = _details ? "Detayı gizle" : "Detayı göster";
        MiniButton.Content = _mini ? "□" : "–";
        MiniButton.ToolTip = _mini ? "Ana pencereyi aç" : "Küçük çubuğa indir";
        MiniQuotaButton.Visibility = _mini ? Visibility.Visible : Visibility.Collapsed;
        UpdateQuota();
        if (_mini)
        {
            Left = work.Right - Width - 8;
            Top = work.Top + 8;
        }
    }

    private void EditQuota(object sender, RoutedEventArgs e)
    {
        QuotaPopup.PlacementTarget = (UIElement)sender;
        QuotaPopup.IsOpen = !QuotaPopup.IsOpen;
    }

    private void ThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.Theme = sender == AeroThemeOption ? "Win7" : "XP";
        SetTheme();
        UpdateQuota();
        Save();
    }

    private void SetTheme()
    {
        var name = _settings.Theme == "Win7" ? "Win7" : "XP";
        Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri($"/BlessedLine;component/Themes/{name}.xaml", UriKind.Relative) };
    }

    private void OpenSettings(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    private void RememberDetails(object sender, RoutedEventArgs e)
    {
        _settings.MainDetails = PinDetails.IsChecked == true;
        _settings.MiniDetails = PinMiniDetails.IsChecked == true;
        _details = _settings.MainDetails;
        ApplyLayout();
        UpdateDetailsStatus();
        Save();
    }

    private void SoundChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _settings.Sound = Sounds.SelectedIndex;
        Save();
    }

    private void PreviewSound(object sender, RoutedEventArgs e) => PlaySound();

    private void PlaySound()
    {
        try
        {
            var index = Math.Clamp(_settings.Sound, 0, SoundNames.Length - 1);
            var wav = System.Windows.Application.GetResourceStream(new Uri($"Assets/{SoundNames[index]}.wav", UriKind.Relative));
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

    private bool Save()
    {
        _settings.Counter = Current;
        _settings.SavedCounters = _counters.ToList();
        _lastSave = _clock.Elapsed;
        try
        {
            _store.Save(_settings);
            _pendingSave = false;
            return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _pendingSave = true;
            Feedback.Text = "Sayaçlar Windows’a kaydedilemedi.";
            Debug.WriteLine(ex);
            return false;
        }
    }

    private void Quit()
    {
        _quit = true;
        Close();
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_ready && !_quit && !_sessionEnding && !Dispatcher.HasShutdownStarted)
        {
            e.Cancel = true;
            SettingsPopup.IsOpen = QuotaPopup.IsOpen = false;
            Hide();
            _tray?.Notify("Ölçüm sürüyor. Pencereyi açmak için tepsi simgesine çift tıklayın.");
            return;
        }
        _quit = true;
        _timer.Stop();
        _helper?.Dispose();
        _tray?.Dispose();
        if (_isAdmin && _ready) RestoreWifi();
        _applicationTraffic?.Dispose();
        _player?.Dispose();
        if (_ready) Save();
    }
}
