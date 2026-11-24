using System.IO;
using System.Text.Json;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed class WindowStateData
{
    public Counter? Counter { get; set; }
    public List<Counter>? SavedCounters { get; set; }
    public string? AdapterId { get; set; }
    public string Theme { get; set; } = "XP";
    public bool MainDetails { get; set; } = true;
    public bool MiniDetails { get; set; }
    public WifiQuota? ArmedWifi { get; set; }
    public WifiRecovery? WifiRecovery { get; set; }
    public string Language { get; set; } = "tr";
    public List<string> FavoriteApps { get; set; } = [];
    public Dictionary<string, AppPolicyRule> AppRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool ColorizeUsage { get; set; } = true;
    public bool AutoMeasureApplications { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public int Sound { get; set; }
}

internal sealed class SettingsStore
{
    private const long MaximumStateBytes = 16L * 1024 * 1024;
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _file = Path.Combine(Local, "BlessedLine", "state.json");

    public WindowStateData Load()
    {
        if (File.Exists(_file))
            return Read<WindowStateData>(_file) ?? new();

        var legacy = Path.Combine(Local, "RetroNet", "counters.json");
        if (!File.Exists(legacy))
            return new();

        var counters = Read<List<Counter>>(legacy);
        return new WindowStateData { Counter = counters?.FirstOrDefault(), SavedCounters = counters };
    }

    private static T? Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > MaximumStateBytes) throw new JsonException("Settings size limit");
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<T>(reader.ReadToEnd());
    }

    public void Save(WindowStateData state)
    {
        var directory = Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Path.GetRandomFileName());
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            try { JsonSerializer.Serialize(stream, state); }
            catch
            {
                stream.Dispose();
                File.Delete(temporary);
                throw;
            }
        }
        try { File.Move(temporary, _file, overwrite: true); }
        finally { File.Delete(temporary); }
    }
}
