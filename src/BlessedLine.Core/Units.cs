using System.Globalization;

namespace BlessedLine.Core;

public static class Units
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static long ParseMegabytes(string text, CultureInfo? culture = null, bool allowZero = false)
    {
        if (text.Any(ch => ch > 127 && char.IsDigit(ch)))
            text = string.Concat(text.Select(ch => char.IsDigit(ch) ? (char)('0' + (int)char.GetNumericValue(ch)) : ch));
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                culture ?? Turkish, out var amount) || amount < 0 || (!allowZero && amount == 0))
            throw new FormatException("Pozitif bir MB değeri girin. Ondalık ayırıcı virgüldür.");

        var bytes = checked((long)decimal.Round(amount * 1_000_000m, 0, MidpointRounding.AwayFromZero));
        return bytes > 0 || (allowZero && amount == 0) ? bytes : throw new FormatException("En az 1 bayt girin.");
    }

    public static string Format(long bytes, CultureInfo? culture = null) => bytes switch
    {
        >= 1_000_000_000 => FormatScaled(bytes, 1_000_000_000, "GB", culture),
        >= 1_000_000 => FormatScaled(bytes, 1_000_000, "MB", culture),
        >= 1_000 => FormatScaled(bytes, 1_000, "KB", culture),
        _ => $"{bytes.ToString("N0", culture ?? Turkish)} B"
    };

    private static string FormatScaled(long bytes, int scale, string suffix, CultureInfo? culture) =>
        $"{((double)bytes / scale).ToString("N2", culture ?? Turkish)} {suffix}";
}
