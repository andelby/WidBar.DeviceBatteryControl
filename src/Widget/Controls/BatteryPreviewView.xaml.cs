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
        DevicesPanel.ColumnDefinitions.Clear();
        DevicesPanel.RowDefinitions.Clear();
        var metrics = TaskbarLayoutMetrics.Create(settings, selected.Count);
        PreviewRoot.Margin = new Thickness(metrics.OuterPadding, 0, metrics.OuterPadding, 0);
        DevicesPanel.ColumnSpacing = metrics.ColumnGap;
        DevicesPanel.RowSpacing = metrics.RowGap;
        for (var i = 0; i < metrics.Columns; i++) DevicesPanel.ColumnDefinitions.Add(new() { Width = new GridLength(metrics.CellWidth) });
        for (var i = 0; i < metrics.Rows; i++) DevicesPanel.RowDefinitions.Add(new() { Height = new GridLength(metrics.CellHeight) });
        EmptyPanel.Visibility = selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyIcon.Visibility = settings.ShowIcons ? Visibility.Visible : Visibility.Collapsed;
        EmptyIcon.FontSize = metrics.IconSize;
        EmptyIcon.Width = EmptyIcon.Height = metrics.IconSize + 4;
        EmptyText.FontSize = metrics.ValueSize;
        for (var index = 0; index < selected.Count; index++)
        {
            var device = selected[index];
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.ContentGap,
                VerticalAlignment = VerticalAlignment.Center, MaxWidth = metrics.CellWidth };
            FontIcon? icon = null;
            if (settings.ShowIcons)
            {
                icon = new FontIcon { Style = (Style)Resources["TaskbarDeviceIconStyle"],
                    FontSize = metrics.IconSize, Width = metrics.IconSize + 4, Height = metrics.IconSize + 4 };
                BatteryAppearance.ApplyIcon(icon, device, settings, ActualTheme);
                if (settings.IconPosition == TaskbarIconPosition.Before) row.Children.Add(icon);
            }
            var level = BatteryPresentation.Level(device);
            if (!settings.ShowPercentSymbol && device.Percentage.HasValue) level = device.Percentage.Value.ToString(BatteryLoc.FormattingCulture);
            var text = new TextBlock { Text = level, Width = metrics.ValueWidth, FontSize = metrics.ValueSize, LineHeight = metrics.ValueSize + 2,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextLineBounds = TextLineBounds.Tight };
            BatteryAppearance.ApplyValue(text, device, settings, ActualTheme);
            var label = BatteryAppearance.Label(device, settings);
            var vertical = metrics.LabelPosition is TaskbarLabelPosition.Above or TaskbarLabelPosition.Below;
            var values = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                Spacing = vertical ? 0 : metrics.ContentGap, VerticalAlignment = VerticalAlignment.Center };
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.MaxLines = 1;
            var caption = new TextBlock { Text = label, FontSize = metrics.LabelSize, LineHeight = metrics.LabelSize + 2,
                Width = metrics.LabelWidth, Opacity = 0.72, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
                VerticalAlignment = VerticalAlignment.Center };
            if (label.Length > 0 && metrics.LabelPosition is TaskbarLabelPosition.Above or TaskbarLabelPosition.Before) values.Children.Add(caption);
            values.Children.Add(text);
            if (label.Length > 0 && metrics.LabelPosition is TaskbarLabelPosition.Below or TaskbarLabelPosition.After) values.Children.Add(caption);
            row.Children.Add(values);
            if (icon is not null && settings.IconPosition == TaskbarIconPosition.After) row.Children.Add(icon);
            var description = $"{device.Name} · {BatteryPresentation.Level(device)} · {BatteryPresentation.Status(device, settings)}";
            ToolTipService.SetToolTip(row, description);
            AutomationProperties.SetName(row, description);
            Grid.SetColumn(row, index / metrics.Rows);
            Grid.SetRow(row, index % metrics.Rows);
            if (metrics.Rows == 2 && selected.Count % 2 == 1 && index == selected.Count - 1) Grid.SetRowSpan(row, 2);
            DevicesPanel.Children.Add(row);
        }
    }
}
