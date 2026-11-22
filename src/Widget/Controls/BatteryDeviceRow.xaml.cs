using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryDeviceRow : UserControl
{
    private BatteryChartSeries _series = BatteryChartSeries.Create([], DateTimeOffset.UtcNow, BatteryChartScale.Automatic);
    private ElementTheme _seriesTheme;
    private BatteryDevice? _device;
    private BatterySettings _settings = new();
    private IReadOnlyList<BatterySample> _samples = [];
    private bool _isFirst;
    public string DeviceId { get; }
    public BatteryDeviceRow(string deviceId)
    {
        InitializeComponent();
        DeviceId = deviceId;
        ActualThemeChanged += (_, _) => { if (_device is not null) Update(_device, _settings, _samples, _isFirst); };
    }

    internal void Update(BatteryDevice device, BatterySettings settings, IReadOnlyList<BatterySample> samples, bool isFirst)
    {
        var dataChanged = !ReferenceEquals(samples, _samples);
        var appearanceChanged = _device?.Kind != device.Kind || _settings.AppearanceFor(device.Kind) != settings.AppearanceFor(device.Kind)
            || _seriesTheme != ActualTheme;
        if (!dataChanged && !appearanceChanged && _device == device && _settings == settings && _isFirst == isFirst) return;
        _device = device; _settings = settings; _samples = samples; _isFirst = isFirst;
        Divider.Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible;
        NameText.Text = device.Name;
        StateText.Text = BatteryPresentation.Status(device, settings);
        BatteryAppearance.ApplyIcon(DeviceIcon, device, settings);
        PercentageText.Text = BatteryPresentation.Level(device);
        BatteryAppearance.ApplyValue(PercentageText, device, settings);
        BatteryStateText.Text = device.IsConnected && device.Percentage.HasValue
            ? BatteryLoc.Get(device.Percentage <= settings.LowBatteryThreshold ? "LowBattery" : "BatteryLevel")
            : device.Percentage.HasValue ? BatteryLoc.Get("LastKnown") : BatteryLoc.Get("Unavailable");
        AutomationProperties.SetName(this, $"{device.Name}, {PercentageText.Text}, {StateText.Text}");
        if (dataChanged) _series = BatteryChartSeries.Create(samples, DateTimeOffset.UtcNow, BatteryChartScale.Automatic).ForDisplay(160);
        if (!dataChanged && !appearanceChanged) return;
        _seriesTheme = ActualTheme;
        BatteryChartRenderer.Draw(SparkHost, _series, BatteryAppearance.ColorFor(device.Kind, settings, ActualTheme), false, false);
    }
}
