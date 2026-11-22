using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class ChartAndAppearanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecentReadingsFillTheirOwnTimeRangeInsteadOfTheEndOfTwentyFourHours()
    {
        BatterySample[] samples = [new(Now.AddMinutes(-10), 80), new(Now.AddMinutes(-5), 79), new(Now, 78)];
        var series = BatteryChartSeries.Create(samples, Now, BatteryChartScale.Automatic);
        Assert.Equal(new[] { 0d, 0.5, 1d }, series.Points.Select(x => x.Position));
        Assert.Equal(new[] { 80, 79, 78 }, series.Points.Select(x => x.Percentage));
        var later = BatteryChartSeries.Create(samples, Now.AddMinutes(3), BatteryChartScale.Automatic);
        Assert.Equal(series.Points.Select(x => x.Position), later.Points.Select(x => x.Position));
    }

    [Fact]
    public void FirstReadingIsCenteredWithoutInventingAnEarlierValue()
    {
        var series = BatteryChartSeries.Create([new(Now, 92)], Now, BatteryChartScale.Automatic);
        var point = Assert.Single(series.Points);
        Assert.Equal(0.5, point.Position);
        Assert.Equal(92, point.Percentage);
        Assert.True(series.Span >= 10);
    }

    [Fact]
    public void TimeSpacingKeepsRealIntervalsAndBreaksAcrossMissingObservations()
    {
        var series = BatteryChartSeries.Create([new(Now.AddMinutes(-30), 80), new(Now.AddMinutes(-25), 79), new(Now, 75)], Now, BatteryChartScale.Full, 120);
        Assert.Equal(1d / 6, series.Points[1].Position, 8);
        Assert.False(series.Points[1].StartsSegment);
        Assert.True(series.Points[2].StartsSegment);
        Assert.Equal(0, series.Minimum);
        Assert.Equal(100, series.Maximum);
    }

    [Fact]
    public void ChartSortsAndDeduplicatesValidObservations()
    {
        var series = BatteryChartSeries.Create([
            new(Now, 80), new(Now.AddMinutes(-5), 82), new(Now, 79),
            new(Now.AddDays(-2), 90), new(Now.AddMinutes(1), 70), new(Now.AddMinutes(-1), 255)
        ], Now, BatteryChartScale.Automatic);
        Assert.Equal(3, series.Points.Count);
        Assert.Equal(90, series.Points[0].Percentage);
        Assert.Equal(82, series.Points[1].Percentage);
        Assert.Equal(79, series.Points[2].Percentage);
        Assert.Equal(Now, series.Nearest(1)!.Time);
    }

    [Fact]
    public void SlowRefreshAllowsSchedulingJitterButStillBreaksLongGaps()
    {
        var series = BatteryChartSeries.Create([
            new(Now.AddMinutes(-60), 80), new(Now.AddMinutes(-44), 79), new(Now, 75)
        ], Now, BatteryChartScale.Automatic, 900);
        Assert.False(series.Points[1].StartsSegment);
        Assert.True(series.Points[2].StartsSegment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void FlatSeriesHasFiniteBoundedVerticalRange(int percentage)
    {
        var series = BatteryChartSeries.Create([new(Now.AddMinutes(-5), percentage), new(Now, percentage)], Now, BatteryChartScale.Automatic);
        Assert.InRange(series.Minimum, 0, percentage);
        Assert.InRange(series.Maximum, percentage, 100);
        Assert.True(double.IsFinite(series.Span) && series.Span > 0);
    }

    [Fact]
    public void DuplicateAndOutOfOrderReadsDoNotDistortRecordedHistory()
    {
        var device = new BatteryDevice("mouse", "Mouse", DeviceKind.Mouse, true, 80, BatterySource.Windows, Now);
        var history = new BatteryHistory();
        Assert.True(history.Add(device, Now));
        Assert.False(history.Add(device with { Percentage = 70 }, Now));
        Assert.False(history.Add(device with { Percentage = 70 }, Now.AddMinutes(-1)));
        Assert.Equal(80, Assert.Single(history.Get("mouse", Now)).Percentage);
    }

    [Fact]
    public void LegacySettingsKeepTheExistingTaskbarLayout()
    {
        var settings = BatterySettings.FromJson("""{"PreviewDeviceCount":2,"KeepHistory":true}""");
        Assert.True(settings.ShowIcons);
        Assert.Equal(PreviewLabelMode.None, settings.LabelMode);
        Assert.Equal(176, settings.PreviewWidth);
        Assert.Equal("Mouse", settings.MouseAppearance.IconId);
    }

    [Fact]
    public void AppearanceRoundTripsAndNamedPreviewsReserveMoreWidth()
    {
        var settings = new BatterySettings
        {
            LabelMode = PreviewLabelMode.Custom,
            MouseAppearance = new() { IconId = "Touchpad", Color = DeviceColor.Custom, CustomColor = "#AABBCC", Label = "Work mouse" },
            HeadphonesAppearance = new() { IconId = "Headset", Color = DeviceColor.Purple }
        };
        Assert.Equal(settings, BatterySettings.FromJson(settings.ToJson()));
        Assert.True(settings.PreviewWidth > new BatterySettings().PreviewWidth);
        var device = new BatteryDevice("mouse", "Mouse", DeviceKind.Mouse, true, 50, BatterySource.Windows, Now);
        Assert.Equal("\uEFA5", DeviceIcons.Glyph(device, settings));
    }

    [Fact]
    public void InvalidAppearanceDataFallsBackToNativeDefaults()
    {
        var settings = BatterySettings.FromJson("""{"MouseAppearance":{"IconId":"arbitrary","Color":999,"CustomColor":"invalid","Label":"  My\nmouse  "},"KeyboardAppearance":null,"LabelMode":999,"ChartScale":999}""");
        Assert.Equal("Mouse", settings.MouseAppearance.IconId);
        Assert.Equal(DeviceColor.System, settings.MouseAppearance.Color);
        Assert.Equal("#0078D4", settings.MouseAppearance.CustomColor);
        Assert.Equal("Mymouse", settings.MouseAppearance.Label);
        Assert.Equal("KeyboardStandard", settings.KeyboardAppearance.IconId);
        Assert.Equal(PreviewLabelMode.None, settings.LabelMode);
    }
}
