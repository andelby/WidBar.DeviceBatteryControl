using System.Text.Json;

namespace DeviceBatteryControl.Models;

public sealed record BatterySettings
{
    public string? PinnedDeviceId { get; init; }
    public int PreviewDeviceCount { get; init; } = 2;
    public const int RefreshSeconds = 900;
    public string[]? SelectedDeviceIds { get; init; }
    public bool ShowWhenEmpty { get; init; }
    public int LowBatteryThreshold { get; init; } = 20;
    public bool ShowDisconnected { get; init; } = true;
    public bool KeepHistory { get; init; } = true;
    public bool ShowIcons { get; init; } = true;
    public bool ShowPercentSymbol { get; init; } = true;
    public bool HighlightLowBattery { get; init; } = true;
    public PreviewLabelMode LabelMode { get; init; }
    public TaskbarLayout Layout { get; init; }
    public TaskbarDensity Density { get; init; } = TaskbarDensity.Standard;
    public TaskbarLabelPosition LabelPosition { get; init; }
    public TaskbarIconPosition IconPosition { get; init; }
    public int IconSize { get; init; } = 16;
    public int ValueFontSize { get; init; } = 14;
    public int LabelFontSize { get; init; } = 11;
    public int LabelWidth { get; init; } = 80;
    public DeviceAppearance MouseAppearance { get; init; } = new() { IconId = "Mouse" };
    public DeviceAppearance KeyboardAppearance { get; init; } = new() { IconId = "KeyboardStandard" };
    public DeviceAppearance HeadphonesAppearance { get; init; } = new() { IconId = "Headphone" };
    public int PreviewWidth => WidthFor(SelectedDeviceIds?.Length ?? PreviewDeviceCount);
    public int GetPreviewWidth(IEnumerable<BatteryDevice> devices)
    {
        var count = BatterySelection.Preview(devices, this).Count;
        return count == 0 ? 176 : WidthFor(count);
    }
    private int WidthFor(int count) => TaskbarLayoutMetrics.Create(this, count).Width;

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
            var layout = Enum.IsDefined(settings.Layout) ? settings.Layout : TaskbarLayout.Horizontal;
            var position = Enum.IsDefined(settings.LabelPosition) ? settings.LabelPosition : TaskbarLabelPosition.Above;
            if (layout == TaskbarLayout.TwoRows && position is TaskbarLabelPosition.Above or TaskbarLabelPosition.Below)
                position = TaskbarLabelPosition.Before;
            return settings with
            {
                PreviewDeviceCount = Math.Clamp(settings.PreviewDeviceCount, 1, 10),
                SelectedDeviceIds = settings.SelectedDeviceIds?.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(DeviceIdentity.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray(),
                LowBatteryThreshold = Math.Clamp(settings.LowBatteryThreshold, 5, 50),
                LabelMode = Enum.IsDefined(settings.LabelMode) ? settings.LabelMode : PreviewLabelMode.None,
                Layout = layout,
                Density = Enum.IsDefined(settings.Density) ? settings.Density : TaskbarDensity.Standard,
                LabelPosition = position,
                IconPosition = Enum.IsDefined(settings.IconPosition) ? settings.IconPosition : TaskbarIconPosition.Before,
                IconSize = Math.Clamp(settings.IconSize, 12, layout == TaskbarLayout.TwoRows ? 18 : 24),
                ValueFontSize = Math.Clamp(settings.ValueFontSize, 10, layout == TaskbarLayout.TwoRows ? 16 : 18),
                LabelFontSize = Math.Clamp(settings.LabelFontSize, 9, 14),
                LabelWidth = Math.Clamp(settings.LabelWidth, 40, 160),
                MouseAppearance = (settings.MouseAppearance ?? new()).Normalize(DeviceKind.Mouse),
                KeyboardAppearance = (settings.KeyboardAppearance ?? new()).Normalize(DeviceKind.Keyboard),
                HeadphonesAppearance = (settings.HeadphonesAppearance ?? new()).Normalize(DeviceKind.Headphones)
            };
        }
        catch (JsonException) { return new(); }
    }
    public string ToJson() => JsonSerializer.Serialize(this);
}
