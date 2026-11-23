using System.Globalization;

namespace BlessedLine.Core;

public static class Units
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static long ParseMegabytes(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                Turkish, out var amount) || amount <= 0)
            throw new FormatException("Pozitif bir MB değeri girin. Ondalık ayırıcı virgüldür.");

        var bytes = checked((long)decimal.Round(amount * 1_000_000m, 0, MidpointRounding.AwayFromZero));
        return bytes > 0 ? bytes : throw new FormatException("En az 1 bayt girin.");
    }

    public static string Format(long bytes) => bytes switch
    {
        >= 1_000_000_000 => FormatScaled(bytes, 1_000_000_000, "GB"),
        >= 1_000_000 => FormatScaled(bytes, 1_000_000, "MB"),
        >= 1_000 => FormatScaled(bytes, 1_000, "KB"),
        _ => $"{bytes.ToString("N0", Turkish)} B"
    };

    private static string FormatScaled(long bytes, int scale, string suffix) =>
        $"{((double)bytes / scale).ToString("N2", Turkish)} {suffix}";
}
