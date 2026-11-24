using System.Windows;
using System.Windows.Media;
using System.Globalization;

namespace BlessedLine.Desktop;

internal sealed class RateGraph : FrameworkElement
{
    private readonly (long Down, long Up)[] _samples = new (long, long)[60];
    private int _count, _next;
    private static readonly Pen Grid = CreatePen(0xD5, 0xDF, 0xE5);
    private static readonly Pen Download = CreatePen(0x27, 0x6C, 0xA3);
    private static readonly Pen Upload = CreatePen(0x3C, 0x89, 0x63);

    internal void Add(long down, long up)
    {
        _samples[_next] = (down, up);
        _next = (_next + 1) % _samples.Length;
        _count = Math.Min(_count + 1, _samples.Length);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = RenderSize.Width;
        var height = RenderSize.Height;
        dc.DrawRectangle(Brushes.White, Grid, new Rect(0, 0, width, height));
        if (width < 20 || height < 20) return;

        const double margin = 10;
        var chartHeight = height - 2 * margin;
        for (var row = 1; row < 4; row++)
        {
            var y = margin + chartHeight * row / 4;
            dc.DrawLine(Grid, new Point(margin, y), new Point(width - margin, y));
        }

        long maximum = 1;
        for (var i = 0; i < _count; i++)
            maximum = Math.Max(maximum, Math.Max(_samples[i].Down, _samples[i].Up));

        var interval = (width - 2 * margin) / (_samples.Length - 1);
        for (var i = 1; i < _count; i++)
        {
            var a = _samples[(_next - _count + i - 1 + _samples.Length) % _samples.Length];
            var b = _samples[(_next - _count + i + _samples.Length) % _samples.Length];
            var x = margin + interval * (_samples.Length - _count + i - 1);
            dc.DrawLine(Download,
                new Point(x, height - margin - a.Down / (double)maximum * chartHeight),
                new Point(x + interval, height - margin - b.Down / (double)maximum * chartHeight));
            dc.DrawLine(Upload,
                new Point(x, height - margin - a.Up / (double)maximum * chartHeight),
                new Point(x + interval, height - margin - b.Up / (double)maximum * chartHeight));
        }

        var text = new FormattedText(TextCatalog.Bytes(maximum) + TextCatalog.Format("PerSecond"),
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.DimGray,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(margin + 2, 4));
    }

    private static Pen CreatePen(byte r, byte g, byte b)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(r, g, b)), 1);
        pen.Freeze();
        return pen;
    }
}
