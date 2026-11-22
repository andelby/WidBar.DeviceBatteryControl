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
    public BatteryPreviewView() { InitializeComponent(); EmptyText.Text = BatteryLoc.Get("NoConnected"); }
    public void Render(IReadOnlyList<BatteryDevice> devices, BatterySettings settings)
    {
        var selected = BatterySelection.Preview(devices, settings);
        var signature = string.Join('|', selected.Select(x => $"{x.Id}:{x.Name}:{x.Percentage}:{x.Kind}:{x.CoarseLevel}:{x.IsReadingStale}")) + settings.LowBatteryThreshold;
        if (signature == _signature) return;
        _signature = signature;
        DevicesPanel.Children.Clear();
        EmptyPanel.Visibility = selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var device in selected)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new FontIcon { Glyph = device.Glyph, Style = (Style)Resources["TaskbarDeviceIconStyle"] });
            var text = new TextBlock { Text = BatteryPresentation.Level(device), FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextLineBounds = TextLineBounds.Tight };
            if (device.Percentage <= settings.LowBatteryThreshold)
                text.Style = (Style)Application.Current.Resources["LowBatteryTextStyle"];
            row.Children.Add(text);
            var description = $"{device.Name} · {BatteryPresentation.Level(device)} · {BatteryPresentation.Status(device, settings)}";
            ToolTipService.SetToolTip(row, description);
            AutomationProperties.SetName(row, description);
            DevicesPanel.Children.Add(row);
        }
    }
}
