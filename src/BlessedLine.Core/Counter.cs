namespace BlessedLine.Core;

public enum TrafficDirection { Download, Upload, Both }
public enum CounterState { Running, Paused }

public sealed class Counter
{
    public const long DefaultWarningBytes = 100_000_000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Yeni Sayaç";
    public TrafficDirection Direction { get; set; } = TrafficDirection.Both;
    public CounterState State { get; set; } = CounterState.Running;
    public long UsedBytes { get; set; }
    public long? LimitBytes { get; set; }
    public long WarningBeforeBytes { get; set; } = DefaultWarningBytes;
    public bool WarningDelivered { get; set; }
    public bool LimitDelivered { get; set; }

    public CounterEvent? Add(long received, long sent)
    {
        if (State == CounterState.Paused) return null;
        var increase = Direction switch
        {
            TrafficDirection.Download => received,
            TrafficDirection.Upload => sent,
            _ => checked(received + sent)
        };
        UsedBytes = checked(UsedBytes + increase);
        return EvaluateThreshold();
    }

    public CounterEvent? EvaluateThreshold()
    {
        if (State == CounterState.Paused || LimitBytes is not > 0) return null;
        var limit = LimitBytes.Value;
        if (UsedBytes >= limit && !LimitDelivered)
        {
            LimitDelivered = true;
            WarningDelivered = true;
            return CounterEvent.Limit;
        }
        if (WarningBeforeBytes > 0 && UsedBytes < limit && UsedBytes >= limit - WarningBeforeBytes && !WarningDelivered)
        {
            WarningDelivered = true;
            return CounterEvent.Warning;
        }
        return null;
    }

    public void Extend(long bytes)
    {
        if (LimitBytes is not long current || bytes <= 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        LimitBytes = checked(current + bytes);
        LimitDelivered = UsedBytes >= LimitBytes;
        WarningDelivered = LimitDelivered || (WarningBeforeBytes > 0 && UsedBytes >= LimitBytes - WarningBeforeBytes);
    }

    public void Reset()
    {
        UsedBytes = 0;
        WarningDelivered = false;
        LimitDelivered = false;
    }
}

public enum CounterEvent { Warning, Limit }
