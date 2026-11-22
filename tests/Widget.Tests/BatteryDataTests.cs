using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class BatteryDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private static BatteryDevice Device(int? percentage, bool connected = true) =>
        new("one", "Mouse", DeviceKind.Mouse, connected, percentage, BatterySource.Windows, Now);

    [Fact]
    public void UnknownAndInvalidLevelsAreNeverShownAsZero()
    {
        Assert.Null(BatteryDevice.ValidatePercentage(null));
        Assert.Null(BatteryDevice.ValidatePercentage((byte)255));
        Assert.Null(BatteryDevice.ValidatePercentage(-1));
        Assert.Null(BatteryDevice.ValidatePercentage("50"));
        Assert.Equal(0, BatteryDevice.ValidatePercentage((byte)0));
        Assert.Equal(100, BatteryDevice.ValidatePercentage((uint)100));
    }

    [Fact]
    public void DisconnectedOrUnknownReadingsDoNotFabricateHistory()
    {
        var history = new BatteryHistory();
        Assert.False(history.Add(Device(50, false), Now));
        Assert.False(history.Add(Device(null), Now));
        Assert.False(history.Add(Device(255), Now));
        Assert.Empty(history.Devices);
    }

    [Fact]
    public void RepeatedRefreshesAreCoalescedButBatteryChangesAreRetained()
    {
        var history = new BatteryHistory();
        Assert.True(history.Add(Device(50), Now));
        Assert.False(history.Add(Device(50), Now.AddSeconds(30)));
        Assert.True(history.Add(Device(49), Now.AddMinutes(1)));
        Assert.True(history.Add(Device(49), Now.AddMinutes(6)));
        Assert.Equal(3, history.Get("one", Now.AddMinutes(6)).Count);
    }

    [Fact]
    public void HistoryStaysBoundedAndExpires()
    {
        var history = new BatteryHistory();
        for (var i = 0; i < 400; i++) history.Add(Device(i % 100), Now.AddMinutes(i));
        Assert.Equal(BatteryHistory.MaximumSamples, history.Devices["one"].Count);
        Assert.Empty(history.Get("one", Now.AddDays(2)));
        for (var i = 0; i < 40; i++) history.Add(Device(50) with { Id = "device-" + i }, Now.AddDays(2));
        Assert.Equal(BatteryHistory.MaximumDevices, history.Devices.Count);
    }

    [Fact]
    public void CorruptAndOutOfRangeSettingsUseSafeBounds()
    {
        Assert.Equal(new BatterySettings(), BatterySettings.FromJson("broken"));
        var settings = BatterySettings.FromJson("""{"PreviewDeviceCount":99,"RefreshSeconds":0,"LowBatteryThreshold":255}""");
        Assert.Equal(3, settings.PreviewDeviceCount);
        Assert.Equal(60, settings.RefreshSeconds);
        Assert.Equal(50, settings.LowBatteryThreshold);
        Assert.Equal(settings, BatterySettings.FromJson(settings.ToJson()));
    }

    [Fact]
    public void DiskHistoryRejectsInvalidAndFutureValues()
    {
        var history = new BatteryHistory();
        history.Devices["one"] = [new(Now.AddDays(-2), 60), new(Now.AddHours(1), 50), new(Now, 255), new(Now, 42)];
        history.Normalize(Now);
        Assert.Equal(new BatterySample(Now, 42), Assert.Single(history.Devices["one"]));
    }

    [Fact]
    public void OnlyStandardHidBatteryUsagesCanBeQueried()
    {
        Assert.Equal((ushort)0x20, HidBatteryValue.BatteryUsage(0x06, 0x20, 0x20));
        Assert.Equal((ushort)0x65, HidBatteryValue.BatteryUsage(0x85, 0x60, 0x70));
        Assert.Null(HidBatteryValue.BatteryUsage(0xff02, 0, ushort.MaxValue));
        Assert.Null(HidBatteryValue.BatteryUsage(0x01, 0, ushort.MaxValue));
        Assert.Null(HidBatteryValue.BatteryUsage(0x06, 0x21, 0x30));
    }

    [Fact]
    public void HidNullValuesAndLowBatteryFlagsDoNotBecomePercentages()
    {
        Assert.Null(HidBatteryValue.ToPercentage(255, 0, 100));
        Assert.Null(HidBatteryValue.ToPercentage(1, 0, 1));
        Assert.Null(HidBatteryValue.ToPercentage(0, 10, 100));
        Assert.Null(HidBatteryValue.ToPercentage(100, 100, 100));
        Assert.Equal(0, HidBatteryValue.ToPercentage(0, 0, 100));
        Assert.Equal(100, HidBatteryValue.ToPercentage(100, 0, 100));
        Assert.Equal(50, HidBatteryValue.ToPercentage(5, 0, 10));
    }
}
