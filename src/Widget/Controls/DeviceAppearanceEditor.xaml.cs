using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed record AppearanceIconOption(string Id, string Glyph, string Name);
public sealed record AppearanceColorOption(DeviceColor Id, string Name);

public sealed partial class DeviceAppearanceEditor : UserControl
{
    private DeviceKind _kind;
    private string _customColor = "#0078D4";
    private bool _ready;
    private ColorPicker? _picker;
    private Flyout? _flyout;
    public event EventHandler? Changed;

    public DeviceAppearanceEditor() => InitializeComponent();

    public void Configure(DeviceKind kind, DeviceAppearance appearance)
    {
        _ready = false;
        _kind = kind;
        appearance = appearance.Normalize(kind);
        Heading.Header = BatteryLoc.Get(kind switch { DeviceKind.Mouse => "DeviceMouse", DeviceKind.Keyboard => "DeviceKeyboard", _ => "DeviceHeadphones" });
        Heading.Description = BatteryLoc.Get("DeviceAppearanceDescription");
        IconCard.Header = BatteryLoc.Get("DeviceIcon");
        ColorCard.Header = BatteryLoc.Get("DeviceColor");
        LabelCard.Header = BatteryLoc.Get("CustomLabel");
        LabelCard.Description = BatteryLoc.Get("CustomLabelDescription");
        CustomLabel.PlaceholderText = Heading.Header;
        ToolTipService.SetToolTip(CustomColorButton, BatteryLoc.Get("PickColor"));
        var icons = DeviceIcons.Options(kind).Select(x => new AppearanceIconOption(x.Id, x.Glyph, BatteryLoc.Get(x.LabelKey))).ToArray();
        IconPicker.ItemsSource = icons;
        IconPicker.SelectedItem = icons.First(x => x.Id == appearance.IconId);
        var colors = Enum.GetValues<DeviceColor>().Select(x => new AppearanceColorOption(x, BatteryLoc.Get("Color" + x))).ToArray();
        ColorMode.ItemsSource = colors;
        ColorMode.SelectedItem = colors.First(x => x.Id == appearance.Color);
        CustomLabel.Text = appearance.Label;
        _customColor = appearance.CustomColor;
        UpdatePreview();
        _ready = true;
    }

    public DeviceAppearance Value => new DeviceAppearance
    {
        IconId = (IconPicker.SelectedItem as AppearanceIconOption)?.Id ?? DeviceIcons.DefaultId(_kind),
        Color = (ColorMode.SelectedItem as AppearanceColorOption)?.Id ?? DeviceColor.System,
        CustomColor = _customColor,
        Label = CustomLabel.Text
    }.Normalize(_kind);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready) return;
        UpdatePreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void OnLabelChanged(object sender, TextChangedEventArgs args)
    {
        if (_ready) Changed?.Invoke(this, EventArgs.Empty);
    }
    private void UpdatePreview()
    {
        Heading.Glyph = (IconPicker.SelectedItem as AppearanceIconOption)?.Glyph ?? "";
        CustomColorButton.Visibility = (ColorMode.SelectedItem as AppearanceColorOption)?.Id == DeviceColor.Custom ? Visibility.Visible : Visibility.Collapsed;
        ColorSwatch.Fill = new SolidColorBrush(BatteryAppearance.Hex(_customColor));
    }
    private void OnCustomColorClicked(object sender, RoutedEventArgs args)
    {
        if (_picker is null)
        {
            _picker = new ColorPicker { Width = 300, IsAlphaEnabled = false, IsAlphaSliderVisible = false, IsAlphaTextInputVisible = false };
            _picker.ColorChanged += (_, change) =>
            {
                _customColor = $"#{change.NewColor.R:X2}{change.NewColor.G:X2}{change.NewColor.B:X2}";
                UpdatePreview();
                Changed?.Invoke(this, EventArgs.Empty);
            };
            _flyout = new Flyout { Content = _picker };
        }
        _picker.Color = BatteryAppearance.Hex(_customColor);
        _flyout!.ShowAt(CustomColorButton);
    }
}
