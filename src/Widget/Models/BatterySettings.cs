using System.Text.Json;

namespace DeviceBatteryControl.Models;

public sealed record BatterySettings
{
    public string? PinnedDeviceId { get; init; }
    public int PreviewDeviceCount { get; init; } = 2;
    public int RefreshSeconds { get; init; } = 120;
    public int LowBatteryThreshold { get; init; } = 20;
    public bool ShowDisconnected { get; init; } = true;
    public bool KeepHistory { get; init; } = true;
    public bool ShowIcons { get; init; } = true;
    public bool ShowPercentSymbol { get; init; } = true;
    public bool HighlightLowBattery { get; init; } = true;
    public PreviewLabelMode LabelMode { get; init; }
    public BatteryChartScale ChartScale { get; init; }
    public DeviceAppearance MouseAppearance { get; init; } = new() { IconId = "Mouse" };
    public DeviceAppearance KeyboardAppearance { get; init; } = new() { IconId = "KeyboardStandard" };
    public DeviceAppearance HeadphonesAppearance { get; init; } = new() { IconId = "Headphone" };
    public int PreviewWidth => 24 + (76 + (LabelMode == PreviewLabelMode.None ? 0 : 64) - (ShowIcons ? 0 : 22)) * PreviewDeviceCount;

    public DeviceAppearance AppearanceFor(DeviceKind kind) => kind switch
    {
        DeviceKind.Mouse => MouseAppearance, DeviceKind.Keyboard => KeyboardAppearance,
        DeviceKind.Headphones => HeadphonesAppearance, _ => new DeviceAppearance()
    };

    public static BatterySettings FromJson(string? json)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<BatterySettings>(json ?? "{}") ?? new();
            return settings with
            {
                PreviewDeviceCount = Math.Clamp(settings.PreviewDeviceCount, 1, 3),
                RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 60, 900),
                LowBatteryThreshold = Math.Clamp(settings.LowBatteryThreshold, 5, 50),
                LabelMode = Enum.IsDefined(settings.LabelMode) ? settings.LabelMode : PreviewLabelMode.None,
                ChartScale = Enum.IsDefined(settings.ChartScale) ? settings.ChartScale : BatteryChartScale.Automatic,
                MouseAppearance = (settings.MouseAppearance ?? new()).Normalize(DeviceKind.Mouse),
                KeyboardAppearance = (settings.KeyboardAppearance ?? new()).Normalize(DeviceKind.Keyboard),
                HeadphonesAppearance = (settings.HeadphonesAppearance ?? new()).Normalize(DeviceKind.Headphones)
            };
        }
        catch (JsonException) { return new(); }
    }
    public string ToJson() => JsonSerializer.Serialize(this);
}
