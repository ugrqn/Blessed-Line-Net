using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
    private readonly NetworkBlock _block = new();
    private readonly BitmapImage _fallbackIcon = new(new Uri("pack://application:,,,/Assets/Uygulamalar.png"));
    private readonly bool _isAdmin = IsAdministrator();
    private WindowStateData _settings = new();
    private ApplicationTraffic? _applicationTraffic;
    private SoundPlayer? _player;
    private AdapterChoice? _adapter;
    private TrafficReading? _previous;
    private Guid? _armedCounter;
    private Guid? _blockedCounter;
    private string? _armedAdapter;
    private bool _ready, _refreshing, _mini, _details, _pendingSave;
    private int _ticks;
    private double _mainLeft, _mainTop;
    private double _quotaFraction;
    private TimeSpan _lastAppSample;
    private TimeSpan _lastSave;
    private Counter Current => (Counter?)Counters.SelectedItem ?? _counters[0];

    public MainWindow()
    {
        InitializeComponent();
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

        _details = _settings.MainDetails;
        _ready = true;
        ApplyLayout();
        SetCounterFields();
        RefreshAdapters();
        if (_isAdmin)
            _applicationTraffic = new ApplicationTraffic();
        UpdateDetailsStatus();
        _timer.Start();
    }

    private void RefreshAdapters()
    {
        try
        {
            var list = AdapterChoice.Connected();
            var selected = list.FirstOrDefault(item => item.Id == _adapter?.Id)
                ?? list.FirstOrDefault(item => item.Id == _settings.AdapterId)
                ?? list.FirstOrDefault();
            if (_adapter?.Id != selected?.Id && !ReleaseBlock())
                return;

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
                NetworkStatus.Text = "Bağlı ağ bulunamadı";
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
        if (ReleaseBlock())
        {
            ChangeAdapter(Adapters.SelectedItem as AdapterChoice);
            Save();
        }
        else
        {
            _refreshing = true;
            Adapters.SelectedItem = (Adapters.ItemsSource as IEnumerable<AdapterChoice>)?.FirstOrDefault(item => item.Id == _adapter?.Id);
            _refreshing = false;
        }
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
        if (_adapter is null || ++_ticks % 5 == 0)
            RefreshAdapters();
        if (_adapter is { } adapter)
            SampleAdapter(adapter);

        if (_applicationTraffic is { } traffic)
        {
            var (status, batch) = traffic.Drain();
            var elapsed = (_clock.Elapsed - _lastAppSample).TotalSeconds;
            _lastAppSample = _clock.Elapsed;
            UpdateAppRows(batch, elapsed > 0 && elapsed < 10 ? elapsed : 0);
            if (_details)
                DetailStatus.Text = status;
        }
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

            NetworkStatus.Text = _block.IsActive ? $"{adapter.Label} · kota engeli açık" : $"{adapter.Label} · ölçülüyor";
            DownloadRate.Text = Units.Format((long)(sample.Received / sample.Elapsed.TotalSeconds)) + "/sn";
            UploadRate.Text = Units.Format((long)(sample.Sent / sample.Elapsed.TotalSeconds)) + "/sn";
            if (_block.IsActive)
                return;

            foreach (var counter in _counters)
            {
                var notification = counter.Add(sample.Received, sample.Sent);
                if (notification is null)
                    continue;

                Feedback.Text = notification == CounterEvent.Limit
                    ? $"{counter.Name} · limit doldu"
                    : $"{counter.Name} · limite yaklaşıldı";
                PlaySound();
                if (Save() && notification == CounterEvent.Limit && counter.Id == _armedCounter && adapter.Id == _armedAdapter)
                    ApplyBlock(adapter.Id, counter.Id);
            }

            _pendingSave |= sample.Received > 0 || sample.Sent > 0;
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
        QuotaHint.Text = _block.IsActive ? "Seçili bağlantının ağ filtresi açık"
            : counter.State == CounterState.Paused ? "Sayaç duraklatıldı"
            : counter.LimitBytes is null ? "Limit belirlenmedi"
            : counter.UsedBytes >= counter.LimitBytes ? "Limit doldu"
            : $"{Units.Format(counter.LimitBytes.Value - counter.UsedBytes)} kaldı";
        if (counter.Direction != TrafficDirection.Both)
            QuotaHint.Text += counter.Direction == TrafficDirection.Download ? " · yalnız indirme" : " · yalnız yükleme";
        PauseButton.Content = counter.State == CounterState.Paused ? "Sürdür" : "Duraklat";
        ExtendButton.IsEnabled = counter.LimitBytes.HasValue;
        QuotaWall.Opacity = counter.LimitBytes.HasValue ? 1 : 0.45;
        RestoreButton.Visibility = _block.IsActive ? Visibility.Visible : Visibility.Collapsed;
        UpdateMarker();
    }

    private void TrackResized(object sender, SizeChangedEventArgs e) => UpdateMarker();

    private void UpdateMarker()
    {
        var position = Math.Max(0, WallTrack.ActualWidth - WifiMarker.Width) * _quotaFraction;
        Canvas.SetLeft(WifiMarker, position);
        TrackFill.Width = position + WifiMarker.Width / 2;
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
            SettingsPopup.IsOpen = false;
            Feedback.Text = "Son sayacı silmek için önce yeni bir sayaç oluşturun.";
            return;
        }
        SettingsPopup.IsOpen = false;
        if (MessageBox.Show(this, $"{counter.Name} silinsin mi?", "BlessedLine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes ||
            (_blockedCounter == counter.Id && !ReleaseBlock())) return;
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
            (_blockedCounter == Current.Id && !ReleaseBlock())) return;
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
            if (_blockedCounter == Current.Id && !ReleaseBlock()) return;
            ClearArming();
            Current.LimitBytes = limit;
            Current.WarningBeforeBytes = limit is long bytes ? bytes / 10 : 0;
            Current.LimitDelivered = false;
            Current.WarningDelivered = false;
            UpdateQuota();
            Save();
            Feedback.Text = limit.HasValue ? "Limit güncellendi · %90’da uyarı" : "Limit kaldırıldı";
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
            if (Current.UsedBytes < extended && _blockedCounter == Current.Id && !ReleaseBlock()) return;
            Current.Extend(100_000_000);
            SetCounterFields();
            Save();
        }
        catch (OverflowException) { Feedback.Text = "Limit daha fazla artırılamıyor."; }
    }

    private void ArmBlock(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (_block.IsActive)
        {
            ClearArming();
            Feedback.Text = "Yeni bir kota engeli için önce bağlantıyı geri açın.";
            return;
        }
        if (Current.LimitBytes is not long limit || Current.UsedBytes >= limit || _adapter is null)
        {
            ClearArming();
            Feedback.Text = "Bağlantıyı kesmek için kalan payı olan bir limit ve bağlı bir ağ seçin.";
            return;
        }
        if (!_isAdmin)
        {
            ClearArming();
            Feedback.Text = "Bağlantıyı kesmek için uygulamayı yönetici olarak açın.";
            OfferElevation();
            return;
        }

        var adapter = _adapter;
        var counter = Current;
        var answer = MessageBox.Show(this,
            $"Kota dolunca {adapter.Label} üzerindeki internet ve yerel ağ trafiği kesilir. Bu Windows önizleme özelliğini açmak istiyor musun?",
            "BlessedLine · kota engeli", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) { ClearArming(); return; }
        if (_adapter?.Id != adapter.Id || Current.Id != counter.Id || counter.UsedBytes >= limit)
        {
            ClearArming();
            Feedback.Text = "Onay sırasında bağlantı değişti veya limit doldu. Limiti kontrol edin.";
            return;
        }
        _armedCounter = counter.Id;
        _armedAdapter = adapter.Id;
    }

    private void DisarmBlock(object sender, RoutedEventArgs e)
    {
        _armedCounter = null;
        _armedAdapter = null;
    }

    private void ClearArming()
    {
        _armedCounter = null;
        _armedAdapter = null;
        BlockCheck.IsChecked = false;
    }

    private void ApplyBlock(string adapterId, Guid counter)
    {
        try
        {
            _block.Apply(adapterId);
            _blockedCounter = counter;
            Feedback.Text = "Kota doldu · seçili bağlantının ağ filtresi etkin.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Feedback.Text = $"Ağ kesilemedi. {ex.Message}";
            ClearArming();
        }
    }

    private bool ReleaseBlock()
    {
        if (!_block.IsActive) return true;
        try
        {
            _block.Release();
            _blockedCounter = null;
            ClearArming();
            _previous = null;
            return true;
        }
        catch (Win32Exception ex)
        {
            Feedback.Text = $"Filtre kaldırılamadı. Uygulamayı kapatıp yeniden açın. {ex.Message}";
            return false;
        }
    }

    private void RestoreConnection(object sender, RoutedEventArgs e)
    {
        if (!ReleaseBlock()) return;
        Feedback.Text = "Kota engeli kaldırıldı.";
        UpdateQuota();
    }

    private void Authorize(object sender, RoutedEventArgs e) => OfferElevation();

    private void OfferElevation()
    {
        if (MessageBox.Show(this, "Uygulama ölçümünü ve kota engelini etkinleştirmek için BlessedLine’ı yönetici olarak yeniden aç.",
            "BlessedLine", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        Save();
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException();
            using var process = Process.Start(new ProcessStartInfo(executable, "--handoff") { UseShellExecute = true, Verb = "runas" });
            if (process is not null) Close();
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
        if (!_details)
        {
            DetailStatus.Text = "";
            AuthorizeButton.Visibility = Visibility.Collapsed;
            return;
        }
        AuthorizeButton.Visibility = _isAdmin ? Visibility.Collapsed : Visibility.Visible;
        DetailStatus.Text = _isAdmin ? "Tüm bağlantılardaki uygulama trafiği" : "Yönetici erişimi gerekli";
    }

    private void ToggleMini(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
        if (!_mini) { _mainLeft = Left; _mainTop = Top; }
        _mini = !_mini;
        _details = _mini ? _settings.MiniDetails : _settings.MainDetails;
        ApplyLayout();
        UpdateDetailsStatus();
        if (!_mini)
        {
            var desktop = SystemParameters.WorkArea;
            Left = Math.Clamp(_mainLeft, desktop.Left, Math.Max(desktop.Left, desktop.Right - Width));
            Top = Math.Clamp(_mainTop, desktop.Top, Math.Max(desktop.Top, desktop.Bottom - Height));
        }
    }

    private void ApplyLayout()
    {
        var work = SystemParameters.WorkArea;
        Topmost = _mini;
        var mainSize = Math.Min(568, Math.Min(work.Width, work.Height) - 12);
        Width = _mini ? 365 : mainSize;
        Height = _mini ? (_details ? 337 : 234) : mainSize;
        NetworkPanel.Visibility = CounterPanel.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        Applications.Visibility = _details ? Visibility.Visible : Visibility.Collapsed;
        DetailsLabel.Text = _details ? "Uygulamalar ▴" : "Uygulamalar ▾";
        MiniButton.Content = _mini ? "□" : "◫";
        MiniButton.ToolTip = _mini ? "Ana panel" : "Mini panel";
        if (_mini)
        {
            Left = work.Right - Width - 8;
            Top = work.Top + 8;
        }
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

    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        ReleaseBlock();
        _applicationTraffic?.Dispose();
        _player?.Dispose();
        if (_ready) Save();
    }
}
