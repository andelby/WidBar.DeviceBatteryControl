using DeviceBatteryControl.Models;
using Xunit;

namespace BatteryControl.Tests;

public sealed class NativeProtocolTests
{
    [Fact]
    public void SpeakerKindAndProtocolNotesArePreserved()
    {
        var reading = Assert.Single(ProtocolSnapshot.Parse(
            """[{"key":"speaker:one","name":"Speaker","kind":"speaker","percent":-1,"vendorId":13615,"detail":"","note":"No reading yet"}]""",
            DateTimeOffset.UtcNow));
        Assert.Equal(DeviceKind.Speaker, reading.Kind);
        Assert.Equal((ushort)13615, reading.UsbVendorId);
        Assert.Equal("No reading yet", reading.BatteryDetails);
        Assert.Null(reading.Percentage);
    }

    [Fact]
    public void DriverKindTakesPrecedenceOverGenericBluetoothAudioClassification()
    {
        var windows = new BatteryDevice("speaker:one", "Living room", DeviceKind.Headphones,
            true, null, BatterySource.Windows, null);
        var protocol = windows with { Name = "Speaker", Kind = DeviceKind.Speaker, Percentage = 72,
            Source = BatterySource.ProtocolEngine, LastReading = DateTimeOffset.UtcNow };
        var merged = Assert.Single(BatterySnapshotMerger.Merge([windows], [protocol], []));
        Assert.Equal(DeviceKind.Speaker, merged.Kind);
        Assert.Equal("Living room", merged.Name);
        Assert.Equal(72, merged.Percentage);
    }
}
