namespace DeviceBatteryControl.Models;

/// <summary>Only a descriptor-declared standard battery usage can become a percentage.</summary>
public static class HidBatteryValue
{
    public static ushort? BatteryUsage(ushort page, ushort minimum, ushort maximum) => page switch
    {
        0x06 when minimum <= 0x20 && maximum >= 0x20 => 0x20,
        0x85 when minimum <= 0x65 && maximum >= 0x65 => 0x65,
        _ => null
    };

    public static int? ToPercentage(uint value, int minimum, int maximum, ushort vendorId = 0)
    {
        // A boolean "battery low" flag cannot establish a percentage.
        if (minimum < 0 || maximum <= minimum || maximum <= 1 || value < minimum || value > maximum) return null;
        // Apple-compatible HID firmware reports percent despite a 0..255 descriptor.
        if (vendorId == 0x05ac && value <= 100) return (int)value;
        return (int)Math.Round(((double)value - minimum) * 100 / (maximum - (double)minimum));
    }
}
