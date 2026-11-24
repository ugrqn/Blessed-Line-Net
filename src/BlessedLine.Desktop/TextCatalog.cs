using System.Globalization;
using System.IO;
using System.Windows;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal static class TextCatalog
{
    internal sealed record Locale(string Code, string Name, string CultureName)
    {
        public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);
    }

    public static IReadOnlyList<Locale> Languages { get; } =
    [
        new("tr", "Türkçe", "tr-TR"),
        new("en", "English", "en-US"),
        new("zh", "简体中文", "zh-CN"),
        new("hi", "हिन्दी", "hi-IN"),
        new("es", "Español", "es-ES"),
        new("ar", "العربية", "ar-SA"),
        new("fr", "Français", "fr-FR"),
        new("bn", "বাংলা", "bn-BD"),
        new("pt", "Português", "pt-BR"),
        new("ru", "Русский", "ru-RU"),
        new("ur", "اردو", "ur-PK")
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = ReadCatalogs();
    private static Locale _language = Languages[0];
    public static CultureInfo Culture => _language.Culture;

    public static Locale GetLanguage(string? code) => Languages.FirstOrDefault(item => item.Code == code) ?? Languages[0];

    public static void Select(string? code) => _language = GetLanguage(code);

    public static string Format(string key, params object[] args)
    {
        var format = Catalogs[_language.Code][key];
        return args.Length == 0 ? format : string.Format(Culture, format, args);
    }

    public static string Status(string status)
    {
        if (status.Length == 0) return "";
        var fields = status.Split('\t');
        if (fields is ["EventsLost", var count] && long.TryParse(count, CultureInfo.InvariantCulture, out var number))
            return Format("EventsLost", number);
        return Format(fields[0]);
    }

    internal static string StatusOrLiteral(string? status) =>
        status is null ? "" : Catalogs[_language.Code].GetValueOrDefault(status, status);

    public static string Bytes(long bytes) => Units.Format(bytes, Culture);

    public static ResourceDictionary WindowResources()
    {
        var resources = new ResourceDictionary();
        foreach (var (key, value) in Catalogs[_language.Code]) resources.Add($"L_{key}", value);
        return resources;
    }

    private static Dictionary<string, Dictionary<string, string>> ReadCatalogs()
    {
        using var reader = new StreamReader(typeof(TextCatalog).Assembly.GetManifestResourceStream("BlessedLine.Languages.tsv")
            ?? throw new InvalidOperationException("Language catalog is missing."));
        var header = reader.ReadLine()!.Split('\t');
        var catalogs = header.Skip(1).ToDictionary(code => code, _ => new Dictionary<string, string>());
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            for (var column = 1; column < header.Length; column++)
                catalogs[header[column]].Add(fields[0], fields[column]);
        }
        return catalogs;
    }
}
