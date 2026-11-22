namespace DeviceBatteryControl.Models;

public enum DeviceColor { System, Accent, Blue, Green, Purple, Orange, Custom }
public enum PreviewLabelMode { None, DeviceName, Custom }
public enum BatteryChartScale { Automatic, Full }

public sealed record DeviceAppearance
{
    public string IconId { get; init; } = "Default";
    public DeviceColor Color { get; init; }
    public string CustomColor { get; init; } = "#0078D4";
    public string Label { get; init; } = "";

    public DeviceAppearance Normalize(DeviceKind kind) => this with
    {
        IconId = DeviceIcons.Options(kind).Any(x => x.Id == IconId) ? IconId : DeviceIcons.DefaultId(kind),
        Color = Enum.IsDefined(Color) ? Color : DeviceColor.System,
        CustomColor = IsHexColor(CustomColor) ? CustomColor.ToUpperInvariant() : "#0078D4",
        Label = new string((Label ?? "").Where(c => !char.IsControl(c)).Take(32).ToArray()).Trim()
    };

    public static bool IsHexColor(string? color) => color is { Length: 7 } && color[0] == '#'
        && color.AsSpan(1).ToArray().All(Uri.IsHexDigit);
}

public sealed record DeviceIconOption(string Id, string Glyph, string LabelKey);

public static class DeviceIcons
{
    private static readonly DeviceIconOption[] Mouse = [
        new("Mouse", "\uE962", "IconMouse"), new("Touchpad", "\uEFA5", "IconTouchpad"), new("Bluetooth", "\uE702", "IconBluetooth")];
    private static readonly DeviceIconOption[] Keyboard = [
        new("KeyboardStandard", "\uE92E", "IconKeyboardStandard"), new("KeyboardClassic", "\uE765", "IconKeyboardClassic"), new("KeyboardFull", "\uEC31", "IconKeyboardFull")];
    private static readonly DeviceIconOption[] Headphones = [
        new("Headphone", "\uE7F6", "IconHeadphone"), new("Headset", "\uE95B", "IconHeadset"), new("Headphone1", "\uED31", "IconHeadphoneAlternative")];
    private static readonly DeviceIconOption[] Other = [new("Default", "\uE975", "IconDefault")];

    public static IReadOnlyList<DeviceIconOption> Options(DeviceKind kind) => kind switch
    {
        DeviceKind.Mouse => Mouse, DeviceKind.Keyboard => Keyboard, DeviceKind.Headphones => Headphones, _ => Other
    };
    public static string DefaultId(DeviceKind kind) => Options(kind)[0].Id;
    public static string Glyph(BatteryDevice device, BatterySettings settings) =>
        Options(device.Kind).FirstOrDefault(x => x.Id == settings.AppearanceFor(device.Kind).IconId)?.Glyph is { } glyph
            && device.Kind is DeviceKind.Mouse or DeviceKind.Keyboard or DeviceKind.Headphones ? glyph : device.Glyph;
}
