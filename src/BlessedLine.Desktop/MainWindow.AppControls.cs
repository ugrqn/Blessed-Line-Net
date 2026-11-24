using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace BlessedLine.Desktop;

public partial class MainWindow
{
    private AppDetailsWindow? _appDetails;
    private WifiInfoWindow? _wifiInfo;
    private readonly Ping _ping = new();
    private static readonly IPAddress PingTarget = IPAddress.Parse("1.1.1.1");
    private bool _pingPending, _quickPolicyPending;
    private string _sortProperty = nameof(ApplicationRow.Name);
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    private void SetupAppControls()
    {
        UsageColors.IsChecked = _settings.ColorizeUsage;
        _policies.Changed += RefreshPolicyRows;
        _policies.Failed += ReportPolicyError;
        foreach (var path in _settings.FavoriteApps.Concat(_settings.AppRules.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Take(2048))
        {
            if (!ApplicationPolicy.IsControllable(path)) continue;
            AddAppRow(path);
        }
        SortApps();
        UpdateAppRows(_policies.TakeTrayTotals(), 0);
    }

    private void ReportPolicyError(string message) => Feedback.Text = message;

    private ApplicationRow AddAppRow(string? path)
    {
        var row = new ApplicationRow(path, _fallbackIcon);
        SyncRow(row);
        _byApplication.Add(path ?? UnknownApplication, row);
        _rows.Add(row);
        return row;
    }

    private void SyncRow(ApplicationRow row) =>
        row.Sync(row.Path is not null && _settings.FavoriteApps.Contains(row.Path, StringComparer.OrdinalIgnoreCase),
            _settings.ColorizeUsage, _policies.Find(row.Path));

    private void RefreshPolicyRows()
    {
        foreach (var row in _rows) SyncRow(row);
    }

    private void SortApps()
    {
        if (CollectionViewSource.GetDefaultView(_rows) is not ListCollectionView view) return;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(nameof(ApplicationRow.Favorite), ListSortDirection.Descending));
            view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            if (_sortProperty != nameof(ApplicationRow.Name))
                view.SortDescriptions.Add(new SortDescription(nameof(ApplicationRow.Name), ListSortDirection.Ascending));
        }
    }

    private void SortApplicationColumn(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(e.Column.SortMemberPath)) return;
        _sortProperty = e.Column.SortMemberPath;
        _sortDirection = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending : ListSortDirection.Ascending;
        foreach (var column in Applications.Columns) column.SortDirection = null;
        e.Column.SortDirection = _sortDirection;
        SortApps();
    }

    private void ToggleFavorite(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ApplicationRow { Path: { } path } row || !row.CanControl) return;
        if (row.Favorite)
            _settings.FavoriteApps.RemoveAll(value => value.Equals(path, StringComparison.OrdinalIgnoreCase));
        else
            _settings.FavoriteApps.Add(path);
        SyncRow(row);
        SortApps();
        Save();
    }

    private void UsageColorsChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.ColorizeUsage = UsageColors.IsChecked == true;
        RefreshPolicyRows();
        Save();
    }

    private void OpenRowDetails(object sender, MouseButtonEventArgs e)
    {
        for (var child = e.OriginalSource as Visual; child is not null; child = VisualTreeHelper.GetParent(child) as Visual)
        {
            if (child is Button) return;
            if (child is DataGridRow) break;
        }
        if (((FrameworkElement)sender).DataContext is ApplicationRow row) ShowApplication(row);
    }

    private void OpenRowMenuDetails(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ApplicationRow row) ShowApplication(row);
    }

    private void AppListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.OriginalSource is not Button && Applications.SelectedItem is ApplicationRow row)
        {
            e.Handled = true;
            ShowApplication(row);
        }
    }

    private void ShowApplication(ApplicationRow row)
    {
        if (_appDetails?.Row == row)
        {
            _appDetails.Activate();
            return;
        }
        _appDetails?.Close();
        var detail = new AppDetailsWindow(row, _policies, _settings.Theme) { Owner = this };
        detail.Closed += (_, _) => _appDetails = null;
        _appDetails = detail;
        detail.Show();
    }

    private async void QuickAppPolicy(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ApplicationRow { Path: { } path, CanControl: true } || _quickPolicyPending)
            return;
        e.Handled = true;
        await ShowPolicyResultAsync(() => _policies.ToggleAsync(path));
    }

    private async void ClearAllPolicies(object sender, RoutedEventArgs e)
    {
        if (_quickPolicyPending) return;
        await ShowPolicyResultAsync(_policies.ClearAllAsync);
    }

    private async Task ShowPolicyResultAsync(Func<Task<PolicyResult>> action)
    {
        _quickPolicyPending = true;
        ClearPoliciesButton.IsEnabled = false;
        Feedback.Text = T("PolicyPending");
        try { Feedback.Text = AppPolicyController.Describe(await action()); }
        finally
        {
            _quickPolicyPending = false;
            ClearPoliciesButton.IsEnabled = true;
        }
    }

    private async void UpdatePing()
    {
        if (_quit || _pingPending || !IsVisible || _adapter is null) return;
        _pingPending = true;
        try
        {
            var result = await _ping.SendPingAsync(PingTarget, 1250);
            if (!_quit)
                PingValue.Text = result.Status == IPStatus.Success ? $"{result.RoundtripTime:N0} ms" : "—";
        }
        catch (Exception ex) when (ex is PingException or ObjectDisposedException or InvalidOperationException)
        {
            if (!_quit) PingValue.Text = "—";
        }
        finally { _pingPending = false; }
    }

    private void OpenWifiInfo(object sender, RoutedEventArgs e)
    {
        if (_wifiInfo is not null)
        {
            _wifiInfo.Activate();
            return;
        }
        var info = new WifiInfoWindow(_adapter?.Id, _settings.Theme) { Owner = this };
        info.Closed += (_, _) => _wifiInfo = null;
        _wifiInfo = info;
        info.Show();
    }
}
