namespace BlessedLine.Core;

public readonly record struct TrafficReading(string Interface, long Received, long Sent, TimeSpan At)
{
    public TrafficDelta? DifferenceFrom(TrafficReading? previous)
    {
        if (previous is not { } baseline || Interface != baseline.Interface)
            return null;

        var elapsed = At - baseline.At;
        if (elapsed <= TimeSpan.Zero || elapsed >= TimeSpan.FromSeconds(10) ||
            Received < baseline.Received || Sent < baseline.Sent)
            return null;

        return new TrafficDelta(Received - baseline.Received, Sent - baseline.Sent, elapsed);
    }
}

public readonly record struct TrafficDelta(long Received, long Sent, TimeSpan Elapsed);

public static class QuotaProgress
{
    public static double Fraction(long usedBytes, long? limitBytes) =>
        limitBytes is > 0 ? Math.Clamp((double)usedBytes / limitBytes.Value, 0, 1) : 0;
}
