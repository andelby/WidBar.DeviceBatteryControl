using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

internal static class BatteryAppearance
{
    private static readonly Windows.UI.ViewManagement.AccessibilitySettings Accessibility = new();

    public static Color ColorFor(DeviceKind kind, BatterySettings settings, ElementTheme theme)
    {
        var appearance = settings.AppearanceFor(kind);
        if (Accessibility.HighContrast || appearance.Color is DeviceColor.System or DeviceColor.Accent)
            return (Application.Current.Resources["AccentFillColorDefaultBrush"] as SolidColorBrush)?.Color ?? Hex("#0078D4");
        var dark = theme == ElementTheme.Dark;
        return appearance.Color switch
        {
            DeviceColor.Blue => Hex(dark ? "#60CDFF" : "#0067C0"),
            DeviceColor.Green => Hex(dark ? "#6CCB5F" : "#107C10"),
            DeviceColor.Purple => Hex(dark ? "#C895FF" : "#744DA9"),
            DeviceColor.Orange => Hex(dark ? "#F8B34E" : "#9D5D00"),
            _ => Hex(appearance.CustomColor)
        };
    }

    public static void ApplyIcon(FontIcon icon, BatteryDevice device, BatterySettings settings, ElementTheme? theme = null)
    {
        icon.Glyph = DeviceIcons.Glyph(device, settings);
        if (Accessibility.HighContrast || settings.AppearanceFor(device.Kind).Color == DeviceColor.System)
            icon.ClearValue(IconElement.ForegroundProperty);
        else icon.Foreground = new SolidColorBrush(ColorFor(device.Kind, settings, theme ?? icon.ActualTheme));
    }

    public static void ApplyValue(TextBlock text, BatteryDevice device, BatterySettings settings, ElementTheme? theme = null)
    {
        text.Style = settings.HighlightLowBattery && device.Percentage <= settings.LowBatteryThreshold
            ? (Style)Application.Current.Resources["LowBatteryTextStyle"] : null;
        text.ClearValue(TextBlock.ForegroundProperty);
        if (text.Style is null && !Accessibility.HighContrast && settings.AppearanceFor(device.Kind).Color != DeviceColor.System)
            text.Foreground = new SolidColorBrush(ColorFor(device.Kind, settings, theme ?? text.ActualTheme));
    }

    public static string Label(BatteryDevice device, BatterySettings settings) => settings.LabelMode switch
    {
        PreviewLabelMode.None => "",
        PreviewLabelMode.DeviceName => device.Name,
        _ => string.IsNullOrWhiteSpace(settings.AppearanceFor(device.Kind).Label)
            ? BatteryLoc.Get(device.Kind switch { DeviceKind.Mouse => "DeviceMouse", DeviceKind.Keyboard => "DeviceKeyboard", DeviceKind.Headphones => "DeviceHeadphones", _ => "Devices" })
            : settings.AppearanceFor(device.Kind).Label
    };

    public static Color Hex(string value)
    {
        if (!DeviceAppearance.IsHexColor(value)) value = "#0078D4";
        var rgb = uint.Parse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
