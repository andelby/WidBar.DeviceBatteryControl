using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryPreviewItem : UserControl
{
    private BatterySettings _settings = new();
    private ElementTheme _theme;
    private DeviceKind _kind;
    private BatteryDevice? _device;
    public TaskbarLayoutMetrics Metrics { get; private set; } = TaskbarLayoutMetrics.Create(new(), 1);
    public string Level { get; private set; } = "";
    public string Label { get; private set; } = "";
    public string Glyph { get; private set; } = "";
    public string Description { get; private set; } = "";
    public int IconExtent => Metrics.IconSize + 4;
    public int IconGap => _settings.ShowIcons ? Metrics.ContentGap : 0;
    public int LabelGap => Label.Length > 0 && Metrics.LabelPosition is TaskbarLabelPosition.Before or TaskbarLabelPosition.After ? Metrics.ContentGap : 0;
    public int IconColumn => _settings.IconPosition == TaskbarIconPosition.After ? 1 : 0;
    public int ContentColumn => _settings.ShowIcons && _settings.IconPosition == TaskbarIconPosition.Before ? 1 : 0;
    public int LabelRow => Metrics.LabelPosition == TaskbarLabelPosition.Below ? 1 : 0;
    public int LabelColumn => Metrics.LabelPosition == TaskbarLabelPosition.After ? 1 : 0;
    public int ValueRow => Label.Length > 0 && Metrics.LabelPosition == TaskbarLabelPosition.Above ? 1 : 0;
    public int ValueColumn => Label.Length > 0 && Metrics.LabelPosition == TaskbarLabelPosition.Before ? 1 : 0;
    public int LabelLineHeight => Metrics.LabelSize + 2;
    public int ValueLineHeight => Metrics.ValueSize + 2;
    public Visibility IconVisibility => _settings.ShowIcons ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LabelVisibility => Label.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public BatteryPreviewItem() => InitializeComponent();

    internal void Update(BatteryDevice device, BatterySettings settings, TaskbarLayoutMetrics metrics, ElementTheme theme)
    {
        if (_device == device && _settings == settings && Metrics == metrics && _theme == theme) return;
        _device = device;
        var level = !settings.ShowPercentSymbol && device.Percentage.HasValue
            ? device.Percentage.Value.ToString(Localization.BatteryLoc.FormattingCulture) : BatteryPresentation.Level(device);
        var label = BatteryAppearance.Label(device, settings);
        var glyph = DeviceIcons.Glyph(device, settings);
        var description = $"{device.Name} · {BatteryPresentation.Level(device)} · {BatteryPresentation.Status(device, settings)}";
        if (Level == level && Label == label && Glyph == glyph && Description == description && _kind == device.Kind
            && _settings == settings && Metrics == metrics && _theme == theme) return;
        _settings = settings; _theme = theme; _kind = device.Kind;
        Metrics = metrics; Level = level; Label = label; Glyph = glyph; Description = description;
        Bindings.Update();
        BatteryAppearance.ApplyIcon(DeviceIcon, device, settings, theme);
        BatteryAppearance.ApplyValue(ValueText, device, settings, theme);
    }
}
