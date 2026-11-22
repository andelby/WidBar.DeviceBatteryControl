using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryPreviewView : UserControl
{
    private string? _signature;
    private IReadOnlyList<BatteryDevice> _devices = [];
    private BatterySettings _settings = new();
    public BatteryPreviewView() { InitializeComponent(); EmptyText.Text = BatteryLoc.Get("NoConnected"); ActualThemeChanged += (_, _) => { _signature = null; Render(_devices, _settings); }; }
    public void Render(IReadOnlyList<BatteryDevice> devices, BatterySettings settings)
    {
        _devices = devices; _settings = settings;
        var selected = BatterySelection.Preview(devices, settings);
        var signature = string.Join('|', selected.Select(x => $"{x.Id}:{x.Name}:{x.Percentage}:{x.Kind}:{x.CoarseLevel}:{x.IsReadingStale}")) + settings.ToJson() + ActualTheme;
        if (signature == _signature) return;
        _signature = signature;
        DevicesPanel.Children.Clear();
        EmptyPanel.Visibility = selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var device in selected)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            if (settings.ShowIcons)
            {
                var icon = new FontIcon { Style = (Style)Resources["TaskbarDeviceIconStyle"] };
                BatteryAppearance.ApplyIcon(icon, device, settings);
                row.Children.Add(icon);
            }
            var level = BatteryPresentation.Level(device);
            if (!settings.ShowPercentSymbol && device.Percentage.HasValue) level = device.Percentage.Value.ToString(BatteryLoc.FormattingCulture);
            var text = new TextBlock { Text = level, FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextLineBounds = TextLineBounds.Tight };
            BatteryAppearance.ApplyValue(text, device, settings);
            var label = BatteryAppearance.Label(device, settings);
            var itemWidth = (settings.PreviewWidth - 20 - Math.Max(0, selected.Count - 1) * 14) / Math.Max(1, selected.Count);
            var values = new StackPanel { MaxWidth = Math.Max(20, itemWidth - (settings.ShowIcons ? 26 : 0)), VerticalAlignment = VerticalAlignment.Center };
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.MaxLines = 1;
            if (label.Length > 0) values.Children.Add(new TextBlock { Text = label, FontSize = 11, Opacity = 0.72, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
            values.Children.Add(text);
            row.Children.Add(values);
            var description = $"{device.Name} · {BatteryPresentation.Level(device)} · {BatteryPresentation.Status(device, settings)}";
            ToolTipService.SetToolTip(row, description);
            AutomationProperties.SetName(row, description);
            DevicesPanel.Children.Add(row);
        }
    }
}
