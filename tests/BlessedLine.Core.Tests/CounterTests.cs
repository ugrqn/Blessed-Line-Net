using BlessedLine.Core;
using Xunit;

namespace BlessedLine.Core.Tests;

public class CounterTests
{
    [Fact]
    public void SteamThresholdsUseDecimalBytes()
    {
        var counter = new Counter { LimitBytes = 1_000_000_000, WarningBeforeBytes = 100_000_000 };
        Assert.Null(counter.Add(899_999_999, 0));
        Assert.Equal(CounterEvent.Warning, counter.Add(1, 0));
        Assert.Equal(CounterEvent.Limit, counter.Add(100_000_000, 0));
        Assert.Null(counter.Add(1, 0));
    }

    [Fact]
    public void SingleLargeSampleEmitsOnlyLimit()
    {
        var counter = new Counter { LimitBytes = 1_000_000_000, WarningBeforeBytes = 100_000_000 };
        Assert.Equal(CounterEvent.Limit, counter.Add(1_000_000_000, 0));
        Assert.True(counter.WarningDelivered);
    }

    [Fact]
    public void ExtensionKeepsUsedBytesAndRearmsLimit()
    {
        var counter = new Counter { LimitBytes = 1_000_000_000, WarningBeforeBytes = 100_000_000 };
        counter.Add(1_000_000_000, 0);
        counter.Extend(100_000_000);
        Assert.Equal(1_000_000_000, counter.UsedBytes);
        Assert.Equal(CounterEvent.Limit, counter.Add(100_000_000, 0));
    }

    [Fact]
    public void PausedCounterDoesNotAccumulate()
    {
        var counter = new Counter { State = CounterState.Paused };
        Assert.Null(counter.Add(100, 100));
        Assert.Equal(0, counter.UsedBytes);
    }

    [Fact]
    public void EditableWarningCanBeReevaluatedWithoutNewTraffic()
    {
        var counter = new Counter { LimitBytes = 1_000_000_000 };
        Assert.Equal(100_000_000, counter.WarningBeforeBytes);
        Assert.Null(counter.Add(850_000_000, 0));
        counter.WarningBeforeBytes = 150_000_000;
        Assert.Equal(CounterEvent.Warning, counter.EvaluateThreshold());
        Assert.Null(counter.EvaluateThreshold());
        Assert.Equal(CounterEvent.Limit, counter.Add(150_000_000, 0));
    }

    [Fact]
    public void DisabledWarningStillReportsLimitAndExtensionRearmsIt()
    {
        var counter = new Counter { LimitBytes = 500_000_000, WarningBeforeBytes = 0 };
        Assert.Null(counter.Add(499_999_999, 0));
        Assert.Equal(CounterEvent.Limit, counter.Add(1, 0));
        counter.Extend(200_000_000);
        Assert.Null(counter.EvaluateThreshold());
        Assert.Equal(CounterEvent.Limit, counter.Add(200_000_000, 0));
        Assert.Equal(0, Units.ParseMegabytes("0,0", allowZero: true));
        Assert.Throws<FormatException>(() => Units.ParseMegabytes("0,0"));
    }

    [Fact]
    public void TurkishDecimalInputUsesMegabytes()
    {
        Assert.Equal(1_500_000, Units.ParseMegabytes("1,5"));
    }
}
