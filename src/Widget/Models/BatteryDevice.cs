namespace DeviceBatteryControl.Models;

public enum DeviceKind { Other, Mouse, Keyboard, Headphones, Controller, Phone, Microphone, Speaker }
public enum BatterySource { Windows, BluetoothLE, UsbHid, ProtocolEngine }

public sealed record BatteryDevice(
    string Id, string Name, DeviceKind Kind, bool IsConnected, int? Percentage,
    BatterySource Source, DateTimeOffset? LastReading, string? BluetoothId = null, bool IsReceiverOnly = false,
    string? Transport = null, string? BatteryDetails = null, bool? IsCharging = null, bool IsAsleep = false,
    string? CoarseLevel = null, int? Millivolts = null, bool IsReadingStale = false,
    ushort? UsbVendorId = null, ushort? UsbProductId = null)
{
    public string Glyph => Kind switch
    {
        DeviceKind.Mouse => "\uE962", DeviceKind.Keyboard => "\uE92E",
        DeviceKind.Headphones => "\uE7F6", DeviceKind.Controller => "\uE7FC",
        DeviceKind.Phone => "\uE8EA", DeviceKind.Microphone => "\uE720", DeviceKind.Speaker => "\uE7F5", _ => "\uE975"
    };

    public static int? ValidatePercentage(object? value)
    {
        // Accept numeric properties only. Missing, malformed and >100 are not 0%.
        var number = value switch { byte v => (int)v, ushort v => v, uint v when v <= 100 => (int)v, int v => v, _ => -1 };
        return number is >= 0 and <= 100 ? number : null;
    }

    public static DeviceKind Classify(string name, IEnumerable<string>? categories = null)
    {
        var text = (name + " " + string.Join(' ', categories ?? [])).ToLowerInvariant();
        if (text.Contains("mouse") || text.Contains("mice")) return DeviceKind.Mouse;
        if (text.Contains("keyboard")) return DeviceKind.Keyboard;
        if (text.Contains("head") || text.Contains("audio") || text.Contains("buds") || text.Contains("airpods")) return DeviceKind.Headphones;
        if (text.Contains("controller") || text.Contains("gamepad") || text.Contains("xbox") || text.Contains("dualsense")) return DeviceKind.Controller;
        if (text.Contains("phone")) return DeviceKind.Phone;
        return DeviceKind.Other;
    }
}
