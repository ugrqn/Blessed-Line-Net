using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal enum QuotaAction { Continue, Extend, WifiOff }

public partial class QuotaAlertWindow : Window
{
    private readonly Func<Counter, QuotaAction, long, string?> _apply;
    private readonly Counter _counter;

    internal QuotaAlertWindow(Counter counter, Func<Counter, QuotaAction, long, string?> apply, string theme)
    {
        _counter = counter;
        _apply = apply;
        InitializeComponent();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"/BlessedLine;component/Themes/{(theme == "Win7" ? "Win7" : "XP")}.xaml", UriKind.Relative)
        });
        Resources.MergedDictionaries.Add(TextCatalog.WindowResources());
        FontFamily = (System.Windows.Media.FontFamily)FindResource("BodyFont");
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(TextCatalog.Culture.IetfLanguageTag);
        FlowDirection = TextCatalog.Culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    internal void Update(CounterEvent notification, bool canCutWifi)
    {
        Heading.Text = TextCatalog.Format(notification == CounterEvent.Limit ? "LimitReached" : "QuotaLowTitle");
        var remaining = Math.Max(0, (_counter.LimitBytes ?? 0) - _counter.UsedBytes);
        Remaining.Text = $"{_counter.Name} · {TextCatalog.Format("QuotaLeft", TextCatalog.Bytes(remaining))}";
        SetWifiAvailable(canCutWifi);
    }

    internal void SetWifiAvailable(bool available) => WifiOffButton.IsEnabled = available;

    private void Continue(object sender, RoutedEventArgs e) => Apply(QuotaAction.Continue);

    private void WifiOff(object sender, RoutedEventArgs e) => Apply(QuotaAction.WifiOff);

    private void Extend(object sender, RoutedEventArgs e)
    {
        var amount = long.Parse((string)((Button)sender).Tag, CultureInfo.InvariantCulture) * 1_000_000;
        Apply(QuotaAction.Extend, amount);
    }

    private void Apply(QuotaAction action, long bytes = 0)
    {
        var error = _apply(_counter, action, bytes);
        if (error is null) Close();
        else Error.Text = error;
    }
}
