using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryPreviewView : UserControl
{
    private readonly Dictionary<string, BatteryPreviewItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private string[] _order = [];
    private TaskbarLayoutMetrics? _metrics;
    private IReadOnlyList<BatteryDevice> _devices = [];
    private BatterySettings _settings = new();
    public int CreatedItemCount { get; private set; }

    public BatteryPreviewView()
    {
        InitializeComponent();
        EmptyText.Text = BatteryLoc.Get("NoConnected");
        ActualThemeChanged += (_, _) => Render(_devices, _settings);
    }

    public void Render(IReadOnlyList<BatteryDevice> devices, BatterySettings settings)
    {
        _devices = devices; _settings = settings;
        var selected = BatterySelection.Preview(devices, settings);
        var metrics = TaskbarLayoutMetrics.Create(settings, selected.Count);
        var layoutChanged = _metrics != metrics;
        var orderChanged = _order.Length != selected.Count;
        for (var i = 0; !orderChanged && i < selected.Count; i++)
            orderChanged = !StringComparer.OrdinalIgnoreCase.Equals(_order[i], selected[i].Id);
        if (layoutChanged)
        {
            _metrics = metrics;
            DevicesPanel.ColumnDefinitions.Clear();
            DevicesPanel.RowDefinitions.Clear();
            PreviewRoot.Margin = new Thickness(metrics.OuterPadding, 0, metrics.OuterPadding, 0);
            DevicesPanel.ColumnSpacing = metrics.ColumnGap;
            DevicesPanel.RowSpacing = metrics.RowGap;
            for (var i = 0; i < metrics.Columns; i++) DevicesPanel.ColumnDefinitions.Add(new() { Width = new GridLength(metrics.CellWidth) });
            for (var i = 0; i < metrics.Rows; i++) DevicesPanel.RowDefinitions.Add(new() { Height = new GridLength(metrics.CellHeight) });
            EmptyIcon.FontSize = metrics.IconSize;
            EmptyIcon.Width = EmptyIcon.Height = metrics.IconSize + 4;
            EmptyText.FontSize = metrics.ValueSize;
        }
        EmptyPanel.Visibility = selected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyIcon.Visibility = settings.ShowIcons ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.ColumnSpacing = settings.ShowIcons ? 8 : 0;
        if (orderChanged)
        {
            foreach (var id in _items.Keys.ToArray())
                if (!selected.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x.Id, id)))
                {
                    DevicesPanel.Children.Remove(_items[id]);
                    _items.Remove(id);
                }
            _order = selected.Select(x => x.Id).ToArray();
        }
        for (var index = 0; index < selected.Count; index++)
        {
            var device = selected[index];
            if (!_items.TryGetValue(device.Id, out var item))
            {
                _items[device.Id] = item = new BatteryPreviewItem();
                CreatedItemCount++;
                DevicesPanel.Children.Add(item);
            }
            item.Update(device, settings, metrics, ActualTheme);
            if (layoutChanged || orderChanged)
            {
                Grid.SetColumn(item, index / metrics.Rows);
                Grid.SetRow(item, index % metrics.Rows);
                Grid.SetRowSpan(item, metrics.Rows == 2 && selected.Count % 2 == 1 && index == selected.Count - 1 ? 2 : 1);
            }
        }
    }
}
