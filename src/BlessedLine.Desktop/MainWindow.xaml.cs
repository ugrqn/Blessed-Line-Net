using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public partial class MainWindow : Window
{
    private const long QuotaStep = 100_000_000;
    private const string UnknownApplication = "<unknown>";
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ObservableCollection<ApplicationRow> _rows = new();
    private readonly Dictionary<string, ApplicationRow> _byApplication = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<Counter> _counters = new();
    private readonly SettingsStore _store;
    private readonly AdapterMeter _meter;
    private readonly QuotaNotifications _notifications;
    private readonly string? _startupError;
    private readonly ImageSource _fallbackIcon;
    private readonly TrayMenu _tray;
    private readonly TrafficMeasurement _measurement;
    private readonly WifiControl _wifi;
    private readonly AppPolicyController _policies;
    private bool _syncingWifi;
    private bool _quit, _discoveryPending, _sessionEnding;
    private string _measureStatus = "ConnectionsOnly";
    private string _discoveryStatus = "";
    private readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _updated = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush AvailableBrush = new(Color.FromRgb(62, 146, 64));
    private static readonly SolidColorBrush FullBrush = new(Color.FromRgb(193, 55, 45));
    private bool _isAdmin => _measurement.Elevated;
    private readonly WindowStateData _settings;
    private AdapterChoice? _adapter => _meter.Selected;
    private WifiQuota? _wifiQuota { get => _settings.ArmedWifi; set => _settings.ArmedWifi = value; }
    private bool _ready, _refreshing, _mini, _details, _pendingSave;
    private int _ticks;
    private double _mainLeft = double.NaN, _mainTop = double.NaN;
    private double _quotaFraction;
    private TimeSpan? _lastAppSample;
    private TimeSpan _lastSave;
    private Counter Current => (Counter?)Counters.SelectedItem ?? _counters[0];

    internal MainWindow(WindowStateData settings, SettingsStore store, AdapterMeter meter, TrayMenu tray,
        QuotaNotifications notifications, TrafficMeasurement measurement, WifiControl wifi, AppPolicyController policies, bool mini, string? startupError)
    {
        _settings = settings;
        _store = store;
        _meter = meter;
        _tray = tray;
        _notifications = notifications;
        _measurement = measurement;
        _wifi = wifi;
        _policies = policies;
        _mini = mini;
        _startupError = startupError;
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
        if (_startupError is not null) Feedback.Text = T(_startupError);
        LanguageList.ItemsSource = TextCatalog.Languages;
        LanguageList.SelectedValue = TextCatalog.GetLanguage(_settings.Language).Code;
        ApplyLanguage();
        foreach (var counter in _settings.SavedCounters!)
            _counters.Add(counter);
        Counters.SelectedItem = _counters.FirstOrDefault(item => item.Id == _settings.Counter?.Id) ?? _counters[0];
        PinDetails.IsChecked = _settings.MainDetails;
        PinMiniDetails.IsChecked = _settings.MiniDetails;
        Sounds.SelectedIndex = Math.Clamp(_settings.Sound, 0, Sounds.Items.Count - 1);
        StartupCheck.IsChecked = _settings.StartWithWindows;
        AutoMeasureCheck.IsChecked = _settings.AutoMeasureApplications;

        (_settings.Theme == "Win7" ? AeroThemeOption : XpThemeOption).IsChecked = true;
        SetTheme();
        _details = _mini ? _settings.MiniDetails : _settings.MainDetails || Environment.GetCommandLineArgs().Contains("--handoff");
        SetupAppControls();
        _ready = true;
        ApplyLayout();
        SetCounterFields();
        RefreshAdapters();
        UpdatePing();
        UpdateDetailsStatus();
        _timer.Start();
        if (_details) _ = DiscoverAsync();
        if (_isAdmin && Environment.GetCommandLineArgs().Contains("--arm-wifi"))
            Dispatcher.BeginInvoke(() => BlockCheck.IsChecked = true);
        if (_isAdmin && Environment.GetCommandLineArgs().Contains("--restore-wifi"))
            RestoreConnection();
    }

    private void RefreshAdapters()
    {
        try
        {
            var list = AdapterChoice.Connected();
            var pin = _settings.WifiRecovery is not null || _wifiQuota is not null;
            var selected = AdapterMeter.Preferred(list, pin ? _settings.AdapterId : _adapter?.Id ?? _settings.AdapterId, pin);

            var current = Adapters.ItemsSource as IReadOnlyList<AdapterChoice>;
            if (current is null || !current.Select(item => (item.Id, item.Label)).SequenceEqual(list.Select(item => (item.Id, item.Label))) ||
                ((AdapterChoice?)Adapters.SelectedItem)?.Id != selected?.Id)
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
                _meter.Select(selected);

            if (selected is not null)
                _settings.AdapterId = selected.Id;
            else
                NetworkStatus.Text = _settings.WifiRecovery is null ? T("NoNetwork") : T("WifiOff");
        }
        catch (NetworkInformationException ex)
        {
            _meter.Reset();
            NetworkStatus.Text = T("ReadNetworkError");
            Debug.WriteLine(ex);
        }
    }

    private void AdapterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshing)
            return;
        ClearArming();
        ChangeAdapter(Adapters.SelectedItem as AdapterChoice);
        Save();
    }

    private void ChangeAdapter(AdapterChoice? adapter)
    {
        _meter.Select(adapter);
        _settings.AdapterId = adapter?.Id ?? _settings.AdapterId;
        NetworkStatus.Text = adapter is null ? T("NoNetwork") : T("AdapterPreparing", adapter.Label);
        DownloadRate.Text = UploadRate.Text = PingValue.Text = "—";
    }

    private void Sample()
    {
        if (++_ticks % 5 == 0 || _adapter is null)
            RefreshAdapters();
        if (_settings.WifiRecovery is null && _adapter is { } adapter)
            SampleAdapter(adapter);

        if (_ticks % 3 == 0 && IsVisible)
        {
            UpdatePing();
            if (_details) _ = DiscoverAsync();
        }

        var reading = _measurement.Drain();
        if (reading is { } measurement)
        {
            var elapsed = _lastAppSample is { } last ? (_clock.Elapsed - last).TotalSeconds : 0;
            _lastAppSample = _clock.Elapsed;
            _policies.Observe(measurement.Traffic);
            UpdateAppRows(measurement.Traffic, elapsed > 0 && elapsed < 10 ? elapsed : 0);
            _appDetails?.Sample();
            _measureStatus = measurement.Status;
        }
        UpdateDetailsStatus();
        _tray.Update(DownloadRate.Text, UploadRate.Text, QuotaHint.Text);
        if (_pendingSave && _clock.Elapsed - _lastSave >= TimeSpan.FromSeconds(5))
            Save();
    }

    private void SampleAdapter(AdapterChoice adapter)
    {
        try
        {
            if (_meter.Read() is not { } sample)
            {
                DownloadRate.Text = UploadRate.Text = "—";
                NetworkStatus.Text = T("AdapterPreparing", adapter.Label);
                return;
            }

            NetworkStatus.Text = T("AdapterMeasuring", adapter.Label);
            DownloadRate.Text = TextCatalog.Bytes((long)(sample.Received / sample.Elapsed.TotalSeconds)) + T("PerSecond");
            UploadRate.Text = TextCatalog.Bytes((long)(sample.Sent / sample.Elapsed.TotalSeconds)) + T("PerSecond");

            foreach (var counter in _counters)
            {
                var notification = counter.Add(sample.Received, sample.Sent);
                if (notification is null)
                    continue;

                Feedback.Text = _notifications.Notify(counter, notification.Value, _settings.Sound);
                Save();
            }

            _pendingSave |= sample.Received > 0 || sample.Sent > 0;
            _wifi.CheckLimit(_counters);
            UpdateQuota();
        }
        catch (Exception ex) when (ex is NetworkInformationException or OverflowException)
        {
            _meter.Reset();
            NetworkStatus.Text = T("MeasurementInterrupted");
            DownloadRate.Text = UploadRate.Text = "—";
            Debug.WriteLine(ex);
        }
    }

    private void UpdateAppRows(AppTraffic[] batch, double seconds)
    {
        _updated.Clear();
        foreach (var item in batch)
        {
            var key = item.Path ?? UnknownApplication;
            if (!_byApplication.TryGetValue(key, out var row))
            {
                if (_rows.Count >= 2048 && key != UnknownApplication)
                    continue;
                row = AddAppRow(item.Path);
            }
            row.Update(item, seconds);
            _updated.Add(key);
        }

        foreach (var (key, row) in _byApplication)
            if (!_updated.Contains(key))
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
                    if (_rows.Count >= 2048) continue;
                    row = AddAppRow(app.Path);
                }
                row.SetAppId(app.AppId);
            }
            foreach (var row in _rows.Where(row => row.Total == 0 && !row.Favorite && !row.HasControls && row.Path is not null && !_connected.Contains(row.Path)).ToArray())
            {
                _rows.Remove(row);
                _byApplication.Remove(row.Path!);
            }
            _discoveryStatus = "";
        }
        catch (Exception ex) when (ex is Win32Exception or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _discoveryStatus = "ReadConnectionsError";
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
        SetCounterFields();
        Save();
    }

    private void SetCounterFields()
    {
        WarningInput.Text = (Current.WarningBeforeBytes / 1_000_000m).ToString("0.######", TextCatalog.Culture);
        LimitInput.Text = Current.LimitBytes is long limit
            ? (limit / 1_000_000m).ToString("0.######", TextCatalog.Culture)
            : "";
        UpdateQuota();
    }

    private void UpdateQuota()
    {
        var counter = Current;
        _quotaFraction = QuotaProgress.Fraction(counter.UsedBytes, counter.LimitBytes);
        QuotaText.Text = counter.LimitBytes is long limit
            ? T("TotalUsage") + $" · {TextCatalog.Bytes(counter.UsedBytes)} / {TextCatalog.Bytes(limit)}"
            : T("TotalUsage") + $" · {TextCatalog.Bytes(counter.UsedBytes)}";
        QuotaPercent.Text = counter.LimitBytes is null ? "" : _quotaFraction.ToString("P0", TextCatalog.Culture);
        QuotaHint.Text = _settings.WifiRecovery is not null && !_mini ? T("WifiOff")
            : counter.State == CounterState.Paused ? T("CounterPaused")
            : counter.LimitBytes is null ? T("NoLimit")
            : counter.UsedBytes >= counter.LimitBytes ? T("LimitReached")
            : T("QuotaLeft", TextCatalog.Bytes(counter.LimitBytes.Value - counter.UsedBytes));
        if (_mini && counter.LimitBytes is long total)
            QuotaHint.Text = T("QuotaLeftOf", TextCatalog.Bytes(total), TextCatalog.Bytes(Math.Max(0, total - counter.UsedBytes)));
        if (!_mini && counter.Direction != TrafficDirection.Both)
            QuotaHint.Text += counter.Direction == TrafficDirection.Download ? T("DownloadSuffix") : T("UploadSuffix");
        PauseButton.Content = counter.State == CounterState.Paused ? T("Resume") : T("Pause");
        ExtendButton.IsEnabled = counter.LimitBytes.HasValue;
        LowerButton.IsEnabled = counter.LimitBytes > QuotaStep;
        TrackFill.Background = _quotaFraction >= 1 ? FullBrush : AvailableBrush;
        QuotaPercent.Foreground = _quotaFraction >= 1 ? FullBrush : (Brush)FindResource("Accent");
        EditQuotaButton.Content = counter.LimitBytes.HasValue ? T("EditQuotaEllipsis") : "Kota ekle…";
        RestoreButton.Visibility = _settings.WifiRecovery is not null ? Visibility.Visible : Visibility.Collapsed;
        _syncingWifi = true;
        BlockCheck.IsChecked = _wifiQuota?.Counter == counter.Id;
        _syncingWifi = false;
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
        var name = new TextBox { Text = T("CounterNumber", _counters.Count + 1), MaxLength = 40, Margin = new Thickness(0, 7, 0, 9) };
        var direction = new ComboBox { Margin = new Thickness(0, 4, 0, 9) };
        direction.ItemsSource = new[] { T("TotalTraffic"), T("DownloadOnly"), T("UploadOnly") };
        direction.SelectedIndex = 0;
        var add = new Button { Content = T("Start"), IsDefault = true };
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = T("CounterName") });
        panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = T("TrafficToMeasure") });
        panel.Children.Add(direction);
        panel.Children.Add(add);
        var dialog = new Window
        {
            Owner = this, Title = T("NewCounter"), Content = panel, Width = 280, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = FontFamily, Background = Background, FlowDirection = FlowDirection, Language = Language
        };
        add.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => { name.SelectAll(); name.Focus(); };
        if (dialog.ShowDialog() != true) return;

        var counter = new Counter
        {
            Name = string.IsNullOrWhiteSpace(name.Text) ? T("NewCounter") : name.Text.Trim(),
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
            Feedback.Text = T("LastCounter");
            return;
        }
        QuotaPopup.IsOpen = false;
        if (MessageBox.Show(this, T("DeleteCounter", counter.Name), "BlessedLine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes ||
            (_settings.WifiRecovery?.Counter == counter.Id && !RestoreWifi())) return;
        Counters.SelectedItem = _counters.First(item => item != counter);
        _counters.Remove(counter);
        if (_wifiQuota?.Counter == counter.Id) ClearArming();
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
        if (MessageBox.Show(this, T("ResetCounter", Current.Name), "BlessedLine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes ||
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
            long? limit = string.IsNullOrWhiteSpace(LimitInput.Text) ? null : Units.ParseMegabytes(LimitInput.Text, TextCatalog.Culture);
            var warning = string.IsNullOrWhiteSpace(WarningInput.Text)
                ? 0 : Units.ParseMegabytes(WarningInput.Text, TextCatalog.Culture, allowZero: true);
            if (_settings.WifiRecovery?.Counter == Current.Id &&
                (limit is null || Current.UsedBytes < limit) && !RestoreWifi()) return;
            if (limit is null && _wifiQuota?.Counter == Current.Id) ClearArming();
            if (Current.LimitBytes != limit)
            {
                Current.LimitDelivered = false;
                Current.WarningDelivered = false;
            }
            else if (Current.WarningBeforeBytes != warning)
                Current.WarningDelivered = false;
            Current.LimitBytes = limit;
            Current.WarningBeforeBytes = warning;
            UpdateQuota();
            Save();
            Feedback.Text = limit.HasValue ? T("LimitUpdated") : T("LimitRemoved");
            NotifyCurrentThreshold();
            _wifi.CheckLimit(_counters);
            UpdateQuota();
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            Feedback.Text = T("InvalidLimit");
        }
    }

    private void ExtendLimit(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentLimit = Current.LimitBytes!.Value;
            var extended = checked(currentLimit + QuotaStep);
            if (Current.UsedBytes < extended && _settings.WifiRecovery?.Counter == Current.Id && !RestoreWifi()) return;
            Current.Extend(QuotaStep);
            SetCounterFields();
            Save();
        }
        catch (OverflowException) { Feedback.Text = T("LimitOverflow"); }
    }

    private void LowerLimit(object sender, RoutedEventArgs e)
    {
        if (Current.LimitBytes is not long limit || limit <= QuotaStep) return;
        Current.LimitBytes = limit - QuotaStep;
        Current.LimitDelivered = false;
        Current.WarningDelivered = false;
        SetCounterFields();
        Save();
        NotifyCurrentThreshold();
        _wifi.CheckLimit(_counters);
        UpdateQuota();
    }

    private void NotifyCurrentThreshold()
    {
        if (Current.EvaluateThreshold() is not { } notification) return;
        Feedback.Text = _notifications.Notify(Current, notification, _settings.Sound);
        Save();
    }

    private void ArmBlock(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncingWifi) return;
        var counter = Current;
        if (_settings.WifiRecovery is not null || counter.LimitBytes is not > 0 ||
            _adapter?.Interface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 ||
            !Guid.TryParse(_adapter.Id, out var adapter))
        {
            ClearArming();
            Feedback.Text = T("NeedWifiQuota");
            return;
        }

        if (!_isAdmin)
        {
            ClearArming();
            OfferElevation("--arm-wifi");
            return;
        }

        QuotaPopup.IsOpen = false;
        var answer = MessageBox.Show(this, T("ConfirmWifiOff"),
            "BlessedLine · Wi-Fi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes || Current.Id != counter.Id ||
            !Guid.TryParse(_adapter?.Id, out var selected) || selected != adapter)
        {
            ClearArming();
            return;
        }

        _wifiQuota = new(counter.Id, adapter);
        Save();
        Feedback.Text = T("WifiArmed");
        NotifyCurrentThreshold();
        _wifi.CheckLimit(_counters);
        UpdateQuota();
    }

    private void DisarmBlock(object sender, RoutedEventArgs e)
    {
        if (!_ready || _syncingWifi) return;
        _wifiQuota = null;
        Save();
    }

    private void ClearArming()
    {
        _wifiQuota = null;
        BlockCheck.IsChecked = false;
    }

    private bool RestoreWifi()
    {
        var error = _wifi.Restore();
        if (error is null) return true;
        Feedback.Text = error;
        return false;
    }

    private void RestoreConnection(object sender, RoutedEventArgs e) => RestoreConnection();

    internal void RestoreConnection()
    {
        if (_settings.WifiRecovery is null) return;
        var blocked = _counters.FirstOrDefault(counter => counter.Id == _settings.WifiRecovery.Counter);
        if (blocked is { LimitBytes: long limit } && blocked.UsedBytes >= limit)
            ClearArming();
        if (!RestoreWifi())
        {
            if (!_isAdmin) OfferElevation("--restore-wifi");
            return;
        }
        ShowWifiStatus(T("WifiRestored"));
    }

    internal void ShowWifiStatus(string status)
    {
        Feedback.Text = status;
        if (_settings.WifiRecovery is not null)
        {
            DownloadRate.Text = UploadRate.Text = "—";
            NetworkStatus.Text = T("WifiOff");
        }
        RefreshAdapters();
        UpdateQuota();
    }

    internal void RefreshQuotaAfterAction(Counter counter)
    {
        if (ReferenceEquals(Current, counter)) SetCounterFields();
        else UpdateQuota();
    }

    private void Authorize(object sender, RoutedEventArgs e)
    {
        _measurement.Start();
        UpdateDetailsStatus();
    }

    private void OfferElevation(string action)
    {
        if (MessageBox.Show(this, T("ElevateWifi"),
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
            Feedback.Text = T("ElevationDenied");
        }
    }

    private void ToggleDetails(object sender, RoutedEventArgs e)
    {
        _details = !_details;
        ApplyLayout();
        UpdateDetailsStatus();
    }

    private void UpdateDetailsStatus()
    {
        var canStart = _measurement.CanStart;
        AuthorizeButton.Visibility = _details && canStart ? Visibility.Visible : Visibility.Collapsed;
        DetailStatus.Text = _details ? TextCatalog.Status(_discoveryStatus.Length > 0 ? _discoveryStatus : _measureStatus) : "";
        EmptyApps.Visibility = _details && _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyApps.Text = T("AwaitingConnections");
    }

    private void ToggleMini(object sender, RoutedEventArgs e) => ShowPanel(!_mini);

    internal void ShowPanel(bool mini)
    {
        if (!_mini && mini) { _mainLeft = Left; _mainTop = Top; }
        var restore = _mini && !mini;
        SettingsPopup.IsOpen = QuotaPopup.IsOpen = false;
        _mini = mini;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _details = mini ? _settings.MiniDetails : _settings.MainDetails;
        ApplyLayout();
        if (restore)
        {
            var work = SystemParameters.WorkArea;
            Left = double.IsNaN(_mainLeft) ? work.Left + (work.Width - Width) / 2
                : Math.Clamp(_mainLeft, work.Left, Math.Max(work.Left, work.Right - Width));
            Top = double.IsNaN(_mainTop) ? work.Top + (work.Height - Height) / 2
                : Math.Clamp(_mainTop, work.Top, Math.Max(work.Top, work.Bottom - Height));
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
        DetailsLabel.Text = _details ? T("HideDetails") : T("ShowDetails");
        MiniButton.Content = _mini ? "□" : "–";
        MiniButton.ToolTip = _mini ? T("OpenMain") : T("MinimizeToMini");
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

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LanguageList.SelectedValue is not string code) return;
        _settings.Language = code;
        ApplyLanguage();
        SetCounterFields();
        _tray.Localize();
        foreach (var row in _rows) row.RefreshText();
        RefreshAdapters();
        ApplyLayout();
        UpdateDetailsStatus();
        Feedback.Text = "";
        Save();
    }

    private void ApplyLanguage()
    {
        TextCatalog.Select(_settings.Language);
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(TextCatalog.Culture.IetfLanguageTag);
        FlowDirection = TextCatalog.Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var dictionary = TextCatalog.WindowResources();
        if (Resources.MergedDictionaries.Count == 1)
            Resources.MergedDictionaries.Add(dictionary);
        else
            Resources.MergedDictionaries[1] = dictionary;
        AppColumn0.Header = T("Application");
        AppColumn1.Header = T("DownRate");
        AppColumn2.Header = T("UpRate");
        AppColumn3.Header = T("DownTotal");
        AppColumn4.Header = T("UpTotal");
    }

    private static string T(string key, params object[] args) => TextCatalog.Format(key, args);

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

    private void AutoMeasureChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.AutoMeasureApplications = AutoMeasureCheck.IsChecked == true;
        if (_settings.AutoMeasureApplications) _measurement.Start();
        else
        {
            _measurement.Stop();
            _measureStatus = "ConnectionsOnly";
            _lastAppSample = null;
        }
        UpdateDetailsStatus();
        Save();
    }

    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var enabled = StartupCheck.IsChecked == true;
        if (enabled == _settings.StartWithWindows) return;
        if (!WindowsStartup.SetEnabled(enabled))
        {
            StartupCheck.IsChecked = _settings.StartWithWindows;
            Feedback.Text = T("StartupWriteError");
            return;
        }
        _settings.StartWithWindows = enabled;
        Save();
    }

    private void PreviewSound(object sender, RoutedEventArgs e) => _notifications.Play(_settings.Sound);

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
            Feedback.Text = T("SaveError");
            Debug.WriteLine(ex);
            return false;
        }
    }

    internal void Quit()
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
            if (_wifiQuota is null) _timer.Interval = TrayMonitor.Interval;
            _tray.Notify(T("TrayHidden"));
            return;
        }
        _quit = true;
        _timer.Stop();
        _ping.Dispose();
        _policies.Changed -= RefreshPolicyRows;
        _policies.Failed -= ReportPolicyError;
        if (_isAdmin && _ready) _wifi.Restore(reconnect: false);
        if (_ready) Save();
    }
}
