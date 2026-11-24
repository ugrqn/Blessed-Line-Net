namespace BlessedLine.Core;

public sealed record WifiQuota(Guid Counter, Guid Adapter)
{
    public bool IsDue(Counter counter, Guid adapter) =>
        Counter == counter.Id && Adapter == adapter && counter.State == CounterState.Running &&
        counter.LimitBytes is > 0 && counter.UsedBytes >= counter.LimitBytes;
}
