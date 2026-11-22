using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class PerformanceBehaviorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static BatteryDevice Device(int percentage) => new("mouse", "Mouse", DeviceKind.Mouse, true, percentage, BatterySource.Windows, Now);

    [Fact]
    public void HistoryViewsAreReusedUntilSamplesChange()
    {
        var history = new BatteryHistory();
        history.Add(Device(80), Now.AddMinutes(-15));
        var cache = new BatteryHistoryCache();
        var first = cache.Get(history, "mouse", Now);
        Assert.Same(first, cache.Get(history, "MOUSE", Now.AddMinutes(1)));
        history.Add(Device(79), Now);
        cache.Invalidate();
        var next = cache.Get(history, "mouse", Now);
        Assert.NotSame(first, next);
        Assert.Equal(new[] { 80, 79 }, next.Select(x => x.Percentage));
        Assert.Single(first);
    }

    [Fact]
    public void CacheHandlesIdentityRemapsAndReplacingLoadedHistory()
    {
        var history = new BatteryHistory();
        history.Add(Device(80), Now);
        var cache = new BatteryHistoryCache();
        cache.Get(history, "mouse", Now);
        history.Remap(new Dictionary<string, string> { ["mouse"] = "physical-device" });
        cache.Invalidate();
        Assert.Empty(cache.Get(history, "mouse", Now));
        Assert.Single(cache.Get(history, "physical-device", Now));
        var replacement = new BatteryHistory();
        replacement.Add(Device(30), Now);
        Assert.Equal(30, Assert.Single(cache.Get(replacement, "mouse", Now)).Percentage);
    }

    [Fact]
    public void CachedViewsRespectFutureSamplesAndClockChanges()
    {
        var history = new BatteryHistory();
        history.Add(Device(80), Now);
        history.Add(Device(79), Now.AddMinutes(1));
        var cache = new BatteryHistoryCache();
        Assert.Single(cache.Get(history, "mouse", Now));
        Assert.Equal(2, cache.Get(history, "mouse", Now.AddMinutes(1)).Count);
        Assert.Single(cache.Get(history, "mouse", Now));
    }

    [Fact]
    public void BinaryLookupMatchesTheNearestRealReadingAcrossIrregularIntervals()
    {
        var samples = Enumerable.Range(0, 100).Select(i => new BatterySample(Now.AddSeconds(-10_000 + i * i), i % 101));
        var series = BatteryChartSeries.Create(samples, Now, BatteryChartScale.Automatic);
        foreach (var position in Enumerable.Range(-10, 120).Select(i => i / 100d))
        {
            var expected = series.Points.MinBy(x => Math.Abs(x.Position - Math.Clamp(position, 0, 1)));
            Assert.Same(expected, series.Nearest(position));
        }
        Assert.Same(series.Points[0], series.Nearest(double.NaN));
        Assert.Same(series.Points[0], series.Nearest(double.NegativeInfinity));
        Assert.Same(series.Points[^1], series.Nearest(double.PositiveInfinity));
    }

    [Fact]
    public void EqualDistanceSelectsTheEarlierReadingAndDisplayProjectionIsReused()
    {
        var series = BatteryChartSeries.Create([new(Now.AddMinutes(-15), 80), new(Now, 79)], Now, BatteryChartScale.Automatic);
        Assert.Same(series.Points[0], series.Nearest(0.5));
        var longSeries = BatteryChartSeries.Create(Enumerable.Range(0, 2000)
            .Select(i => new BatterySample(Now.AddMinutes(-2000 + i), i % 101)), Now, BatteryChartScale.Automatic);
        Assert.Same(longSeries.DisplayPoints(), longSeries.DisplayPoints());
        Assert.InRange(longSeries.DisplayPoints(100).Count, 2, 100);
        Assert.InRange(longSeries.DisplayPoints().Count, 2, 1024);
        var sparkline = longSeries.ForDisplay(160);
        Assert.InRange(sparkline.Points.Count, 2, 160);
        Assert.Equal(longSeries.Minimum, sparkline.Minimum);
        Assert.Equal(longSeries.Maximum, sparkline.Maximum);
        Assert.Equal(2000, longSeries.Points.Count);
    }
}
