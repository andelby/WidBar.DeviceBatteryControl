using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryDeviceRow : UserControl
{
    private string? _seriesSignature;
    public string DeviceId { get; }
    public BatteryDeviceRow(string deviceId) { InitializeComponent(); DeviceId = deviceId; }

    internal void Update(BatteryDevice device, BatterySettings settings, IReadOnlyList<BatterySample> samples, bool isFirst)
    {
        Divider.Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible;
        NameText.Text = device.Name;
        StateText.Text = BatteryPresentation.Status(device, settings);
        DeviceIcon.Glyph = device.Glyph;
        PercentageText.Text = BatteryPresentation.Level(device);
        BatteryStateText.Text = device.IsConnected && device.Percentage.HasValue
            ? BatteryLoc.Get(device.Percentage <= settings.LowBatteryThreshold ? "LowBattery" : "BatteryLevel")
            : device.Percentage.HasValue ? BatteryLoc.Get("LastKnown") : BatteryLoc.Get("Unavailable");
        AutomationProperties.SetName(this, $"{device.Name}, {PercentageText.Text}, {StateText.Text}");
        var signature = string.Join('|', samples.Select(x => $"{x.Time.Ticks}:{x.Percentage}"));
        if (_seriesSignature == signature) return;
        _seriesSignature = signature;
        SparkHost.Children.Clear();
        if (samples.Count < 2) return;
        var start = samples[0].Time;
        var duration = Math.Max(1, (samples[^1].Time - start).TotalSeconds);
        Polyline? line = null;
        BatterySample? previous = null;
        foreach (var sample in samples)
        {
            if (previous is null || sample.Time - previous.Time > TimeSpan.FromMinutes(15))
            {
                line = new Polyline { StrokeThickness = 2, Style = (Style)SparkHost.Resources["SparkLineStyle"] };
                SparkHost.Children.Add(line);
            }
            line!.Points.Add(new Point((sample.Time - start).TotalSeconds / duration * 84, 26 - sample.Percentage / 100d * 24));
            previous = sample;
        }
    }
}
