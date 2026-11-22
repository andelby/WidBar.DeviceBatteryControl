using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;
using Xunit;

namespace BatteryControl.Tests;

public sealed class HistoryAndVisibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static BatteryDevice Device(string id, bool connected = true) => new(id, "Mouse " + id,
        DeviceKind.Mouse, connected, 80, BatterySource.Windows, Now);

    [Fact]
    public async Task SamplesAndDeviceNamesSurviveAColdReloadAfterSeveralWeeks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatteryHistory-" + Guid.NewGuid().ToString("N"));
        try
        {
            var history = new BatteryHistory();
            var device = Device("mouse");
            history.Remember([device], Now);
            history.Add(device, Now.AddDays(-45));
            history.Add(device with { Percentage = 75 }, Now.AddDays(-7));
            history.Add(device with { Percentage = 70 }, Now);
            await new BatteryHistoryStore(directory).SaveAsync(history);
            var reloaded = await new BatteryHistoryStore(directory).LoadAsync(Now.AddDays(2));
            Assert.Equal(new[] { 80, 75, 70 }, reloaded.Get("mouse", Now.AddDays(2)).Select(x => x.Percentage));
            Assert.Equal(device.Name, reloaded.KnownDevices["mouse"].Name);
            Assert.Equal(3, BatteryChartSeries.Create(reloaded.Get("mouse", Now), Now, BatteryChartScale.Automatic).Points.Count);
            Assert.False(File.Exists(Path.Combine(directory, "battery-history.json.tmp")));
        }
        finally
        {
            foreach (var filename in new[] { "battery-history.json", "battery-history.json.tmp" })
                File.Delete(Path.Combine(directory, filename));
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public void TaskbarIsHiddenByDefaultWithoutAConnectedRealDevice()
    {
        var settings = new BatterySettings();
        Assert.False(settings.ShowWhenEmpty);
        Assert.False(BatterySelection.IsVisible([], settings));
        Assert.False(BatterySelection.IsVisible([Device("off", false)], settings));
        Assert.False(BatterySelection.IsVisible([Device("receiver") with { IsReceiverOnly = true }], settings));
        Assert.True(BatterySelection.IsVisible([Device("on")], settings));
        Assert.True(BatterySelection.IsVisible([], settings with { ShowWhenEmpty = true }));
    }

    [Fact]
    public void ExplicitSelectionShowsOnlyChosenConnectedDevicesAndSizesTheSlot()
    {
        var settings = BatterySettings.FromJson("""{"SelectedDeviceIds":[" MOUSE-B ","mouse-b","keyboard"],"PreviewDeviceCount":1}""");
        Assert.Equal(new[] { "mouse-b", "keyboard" }, settings.SelectedDeviceIds);
        BatteryDevice[] devices = [Device("mouse-a"), Device("mouse-b"), Device("keyboard")];
        Assert.Equal(2, BatterySelection.Preview(devices, settings).Count);
        Assert.Equal(176, settings.GetPreviewWidth(devices));
        Assert.False(BatterySelection.IsVisible([devices[0]], settings));
        Assert.Equal(settings.SelectedDeviceIds, BatterySettings.FromJson(settings.ToJson()).SelectedDeviceIds);
    }

    [Fact]
    public void TenDevicesAreSupportedInAutomaticAndExplicitSelections()
    {
        var devices = Enumerable.Range(1, 12).Select(x => Device("mouse-" + x)).ToArray();
        var automatic = BatterySettings.FromJson("""{"PreviewDeviceCount":10,"RefreshSeconds":60}""");
        Assert.Equal(10, BatterySelection.Preview(devices, automatic).Count);
        var explicitSettings = BatterySettings.FromJson((automatic with { SelectedDeviceIds = devices.Select(x => x.Id).ToArray() }).ToJson());
        Assert.Equal(10, explicitSettings.SelectedDeviceIds!.Length);
        Assert.Equal(10, BatterySelection.Preview(devices, explicitSettings).Count);
        Assert.Equal(900, BatterySettings.RefreshSeconds);
        Assert.DoesNotContain("RefreshSeconds", automatic.ToJson());
        Assert.DoesNotContain("ChartScale", automatic.ToJson());
        Assert.False(BatterySelection.IsVisible(devices, automatic with { SelectedDeviceIds = [] }));
    }

    [Fact]
    public void LongHistoriesKeepAllObservationsButBoundDrawingAndPreserveExtremes()
    {
        var samples = Enumerable.Range(0, BatteryHistory.MaximumSamples)
            .Select(i => new BatterySample(Now.AddMinutes(-15 * (BatteryHistory.MaximumSamples - i)), i == 4321 ? 1 : i == 6321 ? 100 : 70)).ToArray();
        var series = BatteryChartSeries.Create(samples, Now, BatteryChartScale.Automatic);
        Assert.Equal(samples.Length, series.Points.Count);
        var display = series.DisplayPoints();
        Assert.InRange(display.Count, 2, 1024);
        Assert.Equal(series.Points[0], display[0]);
        Assert.Equal(series.Points[^1], display[^1]);
        Assert.Contains(display, x => x.Percentage == 1);
        Assert.Contains(display, x => x.Percentage == 100);
        Assert.Equal(samples[4321].Time, series.Nearest(series.Points[4321].Position)!.Time);
    }
}
