using System.Text.Json;
using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class ConnectionAndProtocolTests
{
    private const string Id = "aabbccdd-1234-5678-90ab-aabbccddeeff";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConnectedParentWinsOverDisconnectedHidService()
    {
        var devices = BluetoothSnapshotResolver.Resolve([
            new("BTHENUM\\DEV_AABBCCDDEEFF", "{" + Id.ToUpperInvariant() + "}", "Keychron K2", true, null, 0x2540),
            new("BTHENUM\\{SERVICE}", Id, "HID service", false, 65)
        ]);
        var device = Assert.Single(devices);
        Assert.True(device.Connected);
        Assert.Equal(65, device.Percentage);
        Assert.Equal(DeviceKind.Keyboard, device.Kind);
        Assert.Equal("Keychron K2", device.Name);
    }

    [Fact]
    public void ConnectedClassicAndLeParentsMergeRegardlessOfEnumerationOrder()
    {
        var classic = new BluetoothNode("BTHENUM\\DEV_AABBCCDDEEFF", Id, "Device", true, null);
        var le = new BluetoothNode("BTHLE\\DEV_AABBCCDDEEFF", Id, "Device", false, 38);
        Assert.True(Assert.Single(BluetoothSnapshotResolver.Resolve([classic, le])).Connected);
        Assert.True(Assert.Single(BluetoothSnapshotResolver.Resolve([le, classic])).Connected);
    }

    [Fact]
    public void DisconnectedParentDoesNotExposeAnOldServicePercentageAsCurrent()
    {
        var device = Assert.Single(BluetoothSnapshotResolver.Resolve([
            new("BTHENUM\\DEV_AABBCCDDEEFF", Id, "Keychron K2", false, null),
            new("BTHENUM\\{SERVICE}", Id, "HID service", true, 90)
        ]));
        Assert.False(device.Connected);
        Assert.Null(device.Percentage);
    }

    [Fact]
    public void ProtocolAndWindowsUseOnePhysicalDeviceIdentity()
    {
        var windows = new BatteryDevice(Id, "Keychron K2", DeviceKind.Keyboard, true, null, BatterySource.Windows, null);
        var protocol = ProtocolSnapshot.Parse("""[{"key":"bt:{AABBCCDD-1234-5678-90AB-AABBCCDDEEFF}","name":"Keyboard","kind":"other","percent":65,"connected":true,"charging":false}]""", Now);
        var device = Assert.Single(BatterySnapshotMerger.Merge([windows], protocol, []));
        Assert.Equal("Keychron K2", device.Name);
        Assert.Equal(65, device.Percentage);
        Assert.Equal(DeviceKind.Keyboard, device.Kind);
        Assert.Null(device.IsCharging); // false is not evidence that charging status is supported.
    }

    [Fact]
    public void ConnectedKeyboardRemainsVisibleWithoutProtocolPercentage()
    {
        var keyboard = new BatteryDevice(Id, "Keychron K2", DeviceKind.Keyboard, true, null, BatterySource.Windows, null);
        var result = BatterySnapshotMerger.Merge([keyboard], [], []);
        Assert.True(Assert.Single(result).IsConnected);
        Assert.Null(result[0].Percentage);
    }

    [Fact]
    public void IdenticalControllerNamesKeepSeparateStableIdentities()
    {
        var devices = ProtocolSnapshot.Parse("""[{"key":"xbox:one","name":"Xbox Controller","kind":"gamepad","percent":70},{"key":"xbox:two","name":"Xbox Controller","kind":"gamepad","percent":80}]""", Now);
        Assert.Equal(2, BatterySnapshotMerger.Merge([], devices, []).Count);
    }

    [Fact]
    public void CoarseAndInvalidValuesArePreservedWithoutInventingPercentages()
    {
        var devices = ProtocolSnapshot.Parse("""[{"key":"one","name":"Controller","kind":"gamepad","percent":-1,"level":"Low"},{"key":"two","name":"Device","percent":255},{"key":"three","name":"Device","percent":"75"},null]""", Now);
        Assert.Equal(3, devices.Count);
        Assert.All(devices, x => Assert.Null(x.Percentage));
        Assert.Equal("Low", devices[0].CoarseLevel);
    }

    [Fact]
    public void SleepingReadingsNeverCreateFreshHistorySamples()
    {
        var device = Assert.Single(ProtocolSnapshot.Parse("""[{"key":"one","name":"Mouse","percent":70,"asleep":true}]""", Now));
        Assert.True(device.IsReadingStale);
        Assert.Null(device.LastReading);
        Assert.False(new BatteryHistory().Add(device, Now));
    }

    [Fact]
    public void InvalidSnapshotRootIsRejected()
    {
        Assert.Throws<JsonException>(() => ProtocolSnapshot.Parse("{}", Now));
    }

    [Fact]
    public void PreviewIncludesConnectedKeyboardWithoutPercentage()
    {
        var keyboard = new BatteryDevice(Id, "Keychron K2", DeviceKind.Keyboard, true, null, BatterySource.Windows, null);
        var mouse = new BatteryDevice("mouse", "Mouse", DeviceKind.Mouse, true, 38, BatterySource.Windows, Now);
        var selected = BatterySelection.Preview([keyboard, mouse], new BatterySettings());
        Assert.Equal(2, selected.Count);
        Assert.Contains(keyboard, selected);
        Assert.Null(selected.Single(x => x.Kind == DeviceKind.Keyboard).Percentage);
    }

    [Fact]
    public void GenericBroadcomHidReadingIsBoundToPhysicalKeychronContainer()
    {
        var keyboard = new BatteryDevice(Id, "Keychron K2", DeviceKind.Keyboard, true, null, BatterySource.Windows, null);
        var reading = Assert.Single(ProtocolSnapshot.Parse("""[{"key":"hid:05AC:024F","name":"Broadcom Bluetooth Wireless Keyboard","via":"HID","kind":"keyboard","percent":90}]""", Now));
        var bound = ProtocolIdentityBindings.Apply([reading], [keyboard], [new(0x05ac, 0x024f, Id, "Bluetooth")], out var aliases);
        var device = Assert.Single(BatterySnapshotMerger.Merge([keyboard], bound, []));
        Assert.Equal("Keychron K2", device.Name);
        Assert.Equal(90, device.Percentage);
        Assert.Equal(Id, aliases["hid:05ac:024f"]);
    }

    [Fact]
    public void IdenticalHidModelsAreNotAssignedToAnArbitraryBluetoothDevice()
    {
        var keyboard = new BatteryDevice(Id, "Keyboard", DeviceKind.Keyboard, true, null, BatterySource.Windows, null);
        var reading = new BatteryDevice("hid:05ac:024f", "Keyboard", DeviceKind.Keyboard, true, 90, BatterySource.ProtocolEngine,
            Now, UsbVendorId: 0x05ac, UsbProductId: 0x024f);
        var bound = ProtocolIdentityBindings.Apply([reading], [keyboard],
            [new(0x05ac, 0x024f, Id, "Bluetooth"), new(0x05ac, 0x024f, "other-container", "USB")], out var aliases);
        Assert.Equal("hid:05ac:024f", bound[0].Id);
        Assert.Empty(aliases);
    }

    [Fact]
    public void LastKnownReadingSurvivesIntermittentPollWithoutCreatingFreshHistory()
    {
        var history = new BatteryHistory();
        var old = new BatteryDevice("hid:05ac:024f", "Broadcom Keyboard", DeviceKind.Keyboard, true, 90, BatterySource.ProtocolEngine, Now, Transport: "HID");
        history.Add(old, Now);
        var keyboard = old with { Id = Id, Name = "Keychron K2", Percentage = null, LastReading = null, Source = BatterySource.Windows };
        ProtocolIdentityBindings.Apply([], [keyboard], [new(0x05ac, 0x024f, Id, "Bluetooth")], out var aliases);
        Assert.True(history.Remap(aliases));
        var restored = Assert.Single(history.WithLastKnown([keyboard], Now.AddMinutes(2)));
        Assert.Equal(90, restored.Percentage);
        Assert.Equal(Now, restored.LastReading);
        Assert.True(restored.IsReadingStale);
        Assert.False(history.Add(restored, Now.AddMinutes(2)));
        Assert.Null(Assert.Single(history.WithLastKnown([keyboard], Now.AddMinutes(31))).Percentage);
    }

    [Fact]
    public void AppleCompatiblePercentageDescriptorDoesNotDivideTheLevelBy255()
    {
        Assert.Equal(90, HidBatteryValue.ToPercentage(90, 0, 255, 0x05ac));
        Assert.Equal(35, HidBatteryValue.ToPercentage(90, 0, 255, 0x1234));
    }

    [Fact]
    public void ConnectionOnlyResponseDoesNotRefreshOrRelabelCachedWindowsPercentage()
    {
        var windows = new BatteryDevice(Id, "Keyboard", DeviceKind.Keyboard, true, 50,
            BatterySource.Windows, Now.AddMinutes(-5), IsReadingStale: true);
        var protocol = windows with { Percentage = null, LastReading = null, Source = BatterySource.ProtocolEngine, IsReadingStale = false };
        var device = Assert.Single(BatterySnapshotMerger.Merge([windows], [protocol], []));
        Assert.Equal(50, device.Percentage);
        Assert.Equal(windows.LastReading, device.LastReading);
        Assert.Equal(BatterySource.Windows, device.Source);
        Assert.True(device.IsReadingStale);
        Assert.False(new BatteryHistory().Add(device, Now));
    }

    [Fact]
    public void NewestCachedObservationKeepsItsTimestampAndSource()
    {
        var windows = new BatteryDevice(Id, "Keyboard", DeviceKind.Keyboard, true, 50,
            BatterySource.Windows, Now.AddMinutes(-5), IsReadingStale: true);
        var protocol = windows with { Percentage = 90, LastReading = Now.AddMinutes(-1), Source = BatterySource.ProtocolEngine };
        var device = Assert.Single(BatterySnapshotMerger.Merge([windows], [protocol], []));
        Assert.Equal(90, device.Percentage);
        Assert.Equal(protocol.LastReading, device.LastReading);
        Assert.Equal(BatterySource.ProtocolEngine, device.Source);
        Assert.True(device.IsReadingStale);
    }

    [Fact]
    public void ProviderCacheAlsoExpiresAfterThirtyMinutes()
    {
        var old = new BatteryDevice(Id, "Keyboard", DeviceKind.Keyboard, true, 90,
            BatterySource.Windows, Now.AddMinutes(-31), IsReadingStale: true);
        var device = Assert.Single(new BatteryHistory().WithLastKnown([old], Now));
        Assert.Null(device.Percentage);
        Assert.False(device.IsReadingStale);
        Assert.Equal(old.LastReading, device.LastReading);
    }
}
