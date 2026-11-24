using BlessedLine.Core;
using Xunit;

namespace BlessedLine.Core.Tests;

public sealed class WifiQuotaTests
{
    [Fact]
    public void EnforcesExistingOrNewlyCrossedLimitIndependentlyOfWarningFlags()
    {
        var adapter = Guid.NewGuid();
        var counter = new Counter { LimitBytes = 100 };
        var quota = new WifiQuota(counter.Id, adapter);

        counter.Add(99, 0);
        Assert.False(quota.IsDue(counter, adapter));
        Assert.Equal(CounterEvent.Limit, counter.Add(1, 0));
        Assert.True(quota.IsDue(counter, adapter));
        Assert.True(quota.IsDue(counter, adapter));
        counter.Extend(1);
        Assert.False(quota.IsDue(counter, adapter));
    }

    [Fact]
    public void RequiresTheArmedCounterAndAdapterAndARunningCounterWithALimit()
    {
        var adapter = Guid.NewGuid();
        var counter = new Counter { UsedBytes = 100, LimitBytes = 100 };
        var quota = new WifiQuota(counter.Id, adapter);
        Assert.False(quota.IsDue(counter, Guid.NewGuid()));
        Assert.False(quota.IsDue(new Counter { UsedBytes = 100, LimitBytes = 100 }, adapter));
        counter.State = CounterState.Paused;
        Assert.False(quota.IsDue(counter, adapter));
        counter.State = CounterState.Running;
        counter.LimitBytes = null;
        Assert.False(quota.IsDue(counter, adapter));
    }
}
