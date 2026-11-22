using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeviceBatteryControl.Models;

/// <summary>Reads protocol results without converting coarse levels into percentages.</summary>
public static class ProtocolSnapshot
{
    public static IReadOnlyList<BatteryDevice> Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected a device array.");
        if (document.RootElement.GetArrayLength() > 256) throw new JsonException("Too many protocol devices.");
        var devices = new List<BatteryDevice>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var key = Text(item, "key");
            var name = Text(item, "name");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name)) continue;
            var percentage = item.TryGetProperty("percent", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? BatteryDevice.ValidatePercentage(number) : null;
            var connected = Boolean(item, "connected") ?? true;
            var asleep = Boolean(item, "asleep") ?? false;
            var kind = Text(item, "kind") switch
            {
                "mouse" => DeviceKind.Mouse, "keyboard" => DeviceKind.Keyboard, "headset" => DeviceKind.Headphones,
                "gamepad" => DeviceKind.Controller, "phone" => DeviceKind.Phone, "microphone" => DeviceKind.Microphone,
                "speaker" => DeviceKind.Speaker,
                _ => DeviceKind.Other
            };
            var millivolts = item.TryGetProperty("millivolts", out value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number) && number > 0 ? (int?)number : null;
            var usb = Regex.Match(key, @"(?:^|:)([0-9a-fA-F]{4}):([0-9a-fA-F]{4})(?:$|[:#])");
            var details = Text(item, "detail");
            if (string.IsNullOrWhiteSpace(details)) details = Text(item, "note");
            ushort? vendorId = usb.Success ? Convert.ToUInt16(usb.Groups[1].Value, 16) : null;
            if (vendorId is null && item.TryGetProperty("vendorId", out value) && value.ValueKind == JsonValueKind.Number
                && value.TryGetUInt16(out var vendor) && vendor > 0)
                vendorId = vendor;
            devices.Add(new(DeviceIdentity.Normalize(key), name, kind, connected, percentage, BatterySource.ProtocolEngine,
                connected && !asleep && percentage.HasValue ? now : null,
                Transport: Text(item, "via"), BatteryDetails: details,
                IsCharging: Boolean(item, "charging") == true ? true : null,
                IsAsleep: asleep, CoarseLevel: Text(item, "level"), Millivolts: millivolts,
                IsReadingStale: asleep || !connected,
                UsbVendorId: vendorId,
                UsbProductId: usb.Success ? Convert.ToUInt16(usb.Groups[2].Value, 16) : null));
        }
        return devices;
    }
    private static string? Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() : null;
    private static bool? Boolean(JsonElement item, string key) => item.TryGetProperty(key, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}
