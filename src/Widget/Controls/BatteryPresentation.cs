using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

internal static class BatteryPresentation
{
    public static string Status(BatteryDevice device, BatterySettings settings) =>
        device.IsReceiverOnly ? BatteryLoc.Get("ReceiverBatteryUnavailable") :
        !device.IsConnected ? BatteryLoc.Get(device.Percentage.HasValue ? "Disconnected" : "DisconnectedOnly") :
        device.IsAsleep ? BatteryLoc.Get("Asleep") :
        device.IsReadingStale ? BatteryLoc.Get("LastKnown") :
        device.IsCharging == true ? BatteryLoc.Get("Charging") :
        !device.Percentage.HasValue && !string.IsNullOrEmpty(device.CoarseLevel) ? BatteryLoc.Get("CoarseBattery") :
        !device.Percentage.HasValue ? BatteryLoc.Get("BatteryUnavailable") :
        device.Percentage <= settings.LowBatteryThreshold ? BatteryLoc.Get("LowBattery") : BatteryLoc.Get("Connected");

    public static string Source(BatteryDevice device) => device.Source == BatterySource.ProtocolEngine
        ? device.Transport?.Trim().ToLowerInvariant() switch
        {
            "bluetooth" => BatteryLoc.Get("WindowsSource"), "usb cable" => BatteryLoc.Get("UsbCable"),
            "2.4 ghz receiver" => BatteryLoc.Get("WirelessReceiver"), "usb dongle" => BatteryLoc.Get("WirelessReceiver"),
            null or "" => BatteryLoc.Get("ProtocolSource"), _ => device.Transport
        } : BatteryLoc.Get(device.Source switch
    {
        BatterySource.BluetoothLE => "BleSource", BatterySource.UsbHid => "UsbSource", _ => "WindowsSource"
    });

    public static string Level(BatteryDevice device) => device.Percentage.HasValue
        ? device.Percentage.Value.ToString(BatteryLoc.FormattingCulture) + "%"
        : device.CoarseLevel?.Trim().ToLowerInvariant() switch
        {
            "low" => BatteryLoc.Get("LevelLow"), "critical" => BatteryLoc.Get("LevelCritical"),
            "medium" => BatteryLoc.Get("LevelMedium"), "high" => BatteryLoc.Get("LevelHigh"),
            "full" => BatteryLoc.Get("LevelFull"), "empty" => BatteryLoc.Get("LevelEmpty"),
            null or "" => "—", _ => device.CoarseLevel
        };
}
