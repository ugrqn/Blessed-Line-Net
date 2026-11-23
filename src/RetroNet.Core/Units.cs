using System.Globalization;

namespace RetroNet.Core;

public static class Units
{
    public static long ParseMegabytes(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var amount) || amount <= 0)
            throw new FormatException("Pozitif bir MB değeri girin.");
        return checked((long)decimal.Round(amount * 1_000_000m, 0, MidpointRounding.AwayFromZero));
    }

    public static string Format(long bytes) => bytes >= 1_000_000_000
        ? $"{bytes / 1_000_000_000d:N2} GB"
        : $"{bytes / 1_000_000d:N1} MB";
}
