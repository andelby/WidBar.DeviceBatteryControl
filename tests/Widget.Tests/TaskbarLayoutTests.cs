using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class TaskbarLayoutTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    public void CompactTwoRowLayoutUsesLessWidthAndFitsTheTaskbar(int count)
    {
        var normal = TaskbarLayoutMetrics.Create(new(), count);
        var compact = TaskbarLayoutMetrics.Create(new() { Layout = TaskbarLayout.TwoRows, Density = TaskbarDensity.Compact }, count);
        Assert.True(compact.Width < normal.Width);
        Assert.Equal((count + 1) / 2, compact.Columns);
        Assert.Equal(2, compact.Rows);
        Assert.InRange(compact.Height, 1, 48);
    }

    [Fact]
    public void TwoRowsNormalizeFontAndIconSizesAndKeepLabelsBesideValues()
    {
        var settings = BatterySettings.FromJson("""{"Layout":1,"LabelMode":1,"LabelPosition":1,"IconSize":24,"ValueFontSize":18,"LabelFontSize":14}""");
        Assert.Equal(TaskbarLabelPosition.Before, settings.LabelPosition);
        Assert.Equal(18, settings.IconSize);
        Assert.Equal(16, settings.ValueFontSize);
        var metrics = TaskbarLayoutMetrics.Create(settings, 10);
        Assert.InRange(metrics.Height, 1, 48);
    }

    [Fact]
    public void SideLabelsReserveMoreWidthThanLabelsAboveOrBelow()
    {
        var settings = new BatterySettings { LabelMode = PreviewLabelMode.DeviceName };
        var above = TaskbarLayoutMetrics.Create(settings, 2);
        var below = TaskbarLayoutMetrics.Create(settings with { LabelPosition = TaskbarLabelPosition.Below }, 2);
        var before = TaskbarLayoutMetrics.Create(settings with { LabelPosition = TaskbarLabelPosition.Before }, 2);
        var after = TaskbarLayoutMetrics.Create(settings with { LabelPosition = TaskbarLabelPosition.After }, 2);
        Assert.Equal(above.Width, below.Width);
        Assert.Equal(before.Width, after.Width);
        Assert.True(before.Width > above.Width);
        Assert.True(TaskbarLayoutMetrics.Create(settings with { LabelWidth = 120 }, 2).Width > above.Width);
    }

    [Fact]
    public void HiddenLabelsAndIconsDoNotReserveUnneededWidth()
    {
        var settings = new BatterySettings();
        var standard = TaskbarLayoutMetrics.Create(settings, 2);
        Assert.Equal(176, standard.Width);
        Assert.Equal(standard.Width, TaskbarLayoutMetrics.Create(settings with { LabelWidth = 160 }, 2).Width);
        Assert.True(TaskbarLayoutMetrics.Create(settings with { ShowIcons = false }, 2).Width < standard.Width);
        Assert.True(TaskbarLayoutMetrics.Create(settings with { ShowPercentSymbol = false }, 2).Width < standard.Width);
        Assert.Equal(standard.Width, TaskbarLayoutMetrics.Create(settings with { IconPosition = TaskbarIconPosition.After }, 2).Width);
    }

    [Fact]
    public void CustomLayoutRoundTripsWithoutLosingAppearanceChoices()
    {
        var settings = new BatterySettings { Layout = TaskbarLayout.Horizontal, Density = TaskbarDensity.Comfortable,
            LabelMode = PreviewLabelMode.Custom, LabelPosition = TaskbarLabelPosition.Below, IconPosition = TaskbarIconPosition.After,
            IconSize = 22, ValueFontSize = 17, LabelFontSize = 13, LabelWidth = 120 };
        Assert.Equal(settings, BatterySettings.FromJson(settings.ToJson()));
    }

    [Fact]
    public void InvalidLayoutSettingsFallBackToSafeDefaultsAndBounds()
    {
        var settings = BatterySettings.FromJson("""{"Layout":999,"Density":999,"LabelPosition":999,"IconPosition":999,"IconSize":-1,"ValueFontSize":999,"LabelFontSize":0,"LabelWidth":999}""");
        Assert.Equal(TaskbarLayout.Horizontal, settings.Layout);
        Assert.Equal(TaskbarDensity.Standard, settings.Density);
        Assert.Equal(TaskbarLabelPosition.Above, settings.LabelPosition);
        Assert.Equal(TaskbarIconPosition.Before, settings.IconPosition);
        Assert.Equal(12, settings.IconSize);
        Assert.Equal(18, settings.ValueFontSize);
        Assert.Equal(9, settings.LabelFontSize);
        Assert.Equal(160, settings.LabelWidth);
    }

    [Fact]
    public void LargestSupportedSizesFitAllLayoutsAndDeviceCounts()
    {
        foreach (var layout in Enum.GetValues<TaskbarLayout>())
        foreach (var density in Enum.GetValues<TaskbarDensity>())
        foreach (var position in Enum.GetValues<TaskbarLabelPosition>())
        foreach (var icons in new[] { true, false })
        for (var count = 1; count <= 10; count++)
        {
            var settings = new BatterySettings { Layout = layout, Density = density, LabelMode = PreviewLabelMode.DeviceName,
                LabelPosition = position, ShowIcons = icons, IconSize = 24, ValueFontSize = 18, LabelFontSize = 14, LabelWidth = 160 };
            var metrics = TaskbarLayoutMetrics.Create(BatterySettings.FromJson(settings.ToJson()), count);
            Assert.InRange(metrics.Height, 1, 48);
            Assert.True(metrics.Width > 0);
        }
    }
}
