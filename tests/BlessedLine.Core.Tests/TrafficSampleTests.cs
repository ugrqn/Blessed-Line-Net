using Xunit;

namespace BlessedLine.Core.Tests;

public sealed class TrafficSampleTests
{
    [Fact]
    public void RatesUseElapsedTimeAndDifferentiateDirections()
    {
        var baseline = new TrafficReading("Wi-Fi", 1_000, 2_000, TimeSpan.FromSeconds(1));
        var current = new TrafficReading("Wi-Fi", 3_000, 2_500, TimeSpan.FromSeconds(3));
        var delta = Assert.IsType<TrafficDelta>(current.DifferenceFrom(baseline));
        Assert.Equal(2_000, delta.Received);
        Assert.Equal(500, delta.Sent);
        Assert.Equal(TimeSpan.FromSeconds(2), delta.Elapsed);
    }

    [Theory]
    [InlineData("Kablolu", 1000, 2)]
    [InlineData("Wi-Fi", 20, 2)]
    [InlineData("Wi-Fi", 1000, 12)]
    [InlineData("Wi-Fi", 1000, 1)]
    public void NewAdapterResetOrSamplingGapReestablishesBaseline(string adapter, long received, int seconds)
    {
        var baseline = new TrafficReading("Wi-Fi", 100, 200, TimeSpan.FromSeconds(1));
        var current = new TrafficReading(adapter, received, 200, TimeSpan.FromSeconds(seconds));
        Assert.Null(current.DifferenceFrom(baseline));
    }

    [Theory]
    [InlineData(500L, 1000L, 0.5)]
    [InlineData(1000L, 1000L, 1)]
    [InlineData(1500L, 1000L, 1)]
    [InlineData(100, null, 0)]
    public void QuotaMarkerStopsAtTheWall(long used, long? limit, double position) =>
        Assert.Equal(position, QuotaProgress.Fraction(used, limit));

    [Fact]
    public void SmallTrafficRemainsVisible()
    {
        Assert.Equal("350 B", Units.Format(350));
        Assert.Equal("1,25 KB", Units.Format(1250));
    }
}
