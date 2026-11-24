using System.Globalization;
using System.Windows;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public partial class AppDetailsWindow : Window
{
    internal ApplicationRow Row { get; }
    private readonly AppPolicyController _policies;
    private readonly RateGraph _graph = new();
    private bool _busy;

    internal AppDetailsWindow(ApplicationRow row, AppPolicyController policies, string theme)
    {
        Row = row;
        _policies = policies;
        InitializeComponent();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/BlessedLine;component/Themes/{(theme == "Win7" ? "Win7" : "XP")}.xaml", UriKind.Relative)
        });
        Resources.MergedDictionaries.Add(TextCatalog.WindowResources());
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(TextCatalog.Culture.IetfLanguageTag);
        FlowDirection = TextCatalog.Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        GraphHost.Child = _graph;
        ProgramIcon.Source = row.Icon;
        ProgramName.Text = row.Name;
        Title = $"{TextCatalog.Format("AppDetails")} · {row.Name}";
        ProgramPath.Text = row.Path is { } path ? System.IO.Path.GetFileName(path) : TextCatalog.Format("InvalidAppPolicy");
        _policies.Changed += RefreshControls;
        Closed += (_, _) => _policies.Changed -= RefreshControls;
        RefreshControls();
        Sample();
    }

    internal void Sample()
    {
        _graph.Add(Row.DownloadRate, Row.UploadRate);
        RateSummary.Text = $"↓ {Row.DownloadRateText}    ↑ {Row.UploadRateText}";
        var policy = _policies.Find(Row.Path);
        if (policy?.QuotaBytes > 0)
            PolicyState.Text = TextCatalog.Format("AppQuotaProgress", TextCatalog.Bytes(policy.UsedBytes), TextCatalog.Bytes(policy.QuotaBytes));
    }

    private void RefreshControls()
    {
        var rule = _policies.Find(Row.Path);
        BlockPolicyButton.Content = TextCatalog.Format(rule?.IsBlocked == true ? "UnblockApp" : "BlockApp");
        var canApply = Row.CanControl && !_busy;
        BlockPolicyButton.IsEnabled = RestorePolicyButton.IsEnabled = canApply;
        UploadCapInput.IsEnabled = AppQuotaInput.IsEnabled = ApplyUploadCap.IsEnabled = ApplyAppQuota.IsEnabled = canApply;
        if (!UploadCapInput.IsKeyboardFocused)
            UploadCapInput.Text = (rule?.UploadKbps ?? 0).ToString(TextCatalog.Culture);
        if (!AppQuotaInput.IsKeyboardFocused)
            AppQuotaInput.Text = ((rule?.QuotaBytes ?? 0) / 1_000_000m).ToString("0.######", TextCatalog.Culture);
        if (rule?.QuotaBytes > 0)
            PolicyState.Text = TextCatalog.Format("AppQuotaProgress", TextCatalog.Bytes(rule.UsedBytes), TextCatalog.Bytes(rule.QuotaBytes));
        else
            PolicyState.Text = TextCatalog.Format(Row.CanControl ? "PolicyZeroDisables" : "InvalidAppPolicy");
    }

    private async Task RunAsync(Func<string, Task<PolicyResult>> action)
    {
        if (_busy || !Row.CanControl || Row.Path is not { } path) return;
        _busy = true;
        ActionStatus.Text = TextCatalog.Format("PolicyPending");
        RefreshControls();
        try
        {
            ActionStatus.Text = AppPolicyController.Describe(await action(path));
        }
        finally
        {
            _busy = false;
            RefreshControls();
        }
    }

    private async void Toggle(object sender, RoutedEventArgs e) => await RunAsync(_policies.ToggleAsync);
    private async void Clear(object sender, RoutedEventArgs e) => await RunAsync(_policies.ClearOneAsync);

    private async void ApplyUpload(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(UploadCapInput.Text, NumberStyles.None, TextCatalog.Culture, out var rate) ||
            rate < 0 || rate > ApplicationPolicy.MaximumUploadKbps)
        {
            ActionStatus.Text = TextCatalog.Format("InvalidPolicyValue");
            return;
        }
        await RunAsync(path => _policies.SetUploadAsync(path, rate));
    }

    private async void ApplyQuota(object sender, RoutedEventArgs e)
    {
        long quota;
        try { quota = Units.ParseMegabytes(AppQuotaInput.Text, TextCatalog.Culture, allowZero: true); }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            ActionStatus.Text = TextCatalog.Format("InvalidPolicyValue");
            return;
        }
        await RunAsync(path => _policies.SetQuotaAsync(path, quota));
    }
}
