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
    public WifiRecovery? WifiRecovery { get; set; }
    public string Language { get; set; } = "tr";
    public bool StartWithWindows { get; set; } = true;
    public int Sound { get; set; }
}

internal sealed class SettingsStore
{
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _file = Path.Combine(Local, "BlessedLine", "state.json");

    public WindowStateData Load()
    {
        if (File.Exists(_file))
            return JsonSerializer.Deserialize<WindowStateData>(File.ReadAllText(_file)) ?? new();

        var legacy = Path.Combine(Local, "RetroNet", "counters.json");
        if (!File.Exists(legacy))
            return new();

        var counters = JsonSerializer.Deserialize<List<Counter>>(File.ReadAllText(legacy));
        return new WindowStateData { Counter = counters?.FirstOrDefault(), SavedCounters = counters };
    }

    public void Save(WindowStateData state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temporary = _file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, _file, overwrite: true);
    }
}
