using System.Diagnostics;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed class AdapterMeter
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TrafficReading? _previous;
    internal AdapterChoice? Selected { get; private set; }

    internal void Select(AdapterChoice? adapter)
    {
        if (Selected?.Id != adapter?.Id) Reset();
        Selected = adapter;
    }

    internal void Reset() => _previous = null;

    internal TrafficDelta? Read()
    {
        if (Selected is not { } adapter) return null;
        var statistics = adapter.Interface.GetIPStatistics();
        var reading = new TrafficReading(adapter.Id, statistics.BytesReceived, statistics.BytesSent, _clock.Elapsed);
        var delta = reading.DifferenceFrom(_previous);
        _previous = reading;
        return delta;
    }

    internal static AdapterChoice? Preferred(IReadOnlyList<AdapterChoice> adapters, string? id, bool pin) =>
        adapters.FirstOrDefault(adapter => adapter.Id == id) ?? (pin ? null : adapters.FirstOrDefault());
}
