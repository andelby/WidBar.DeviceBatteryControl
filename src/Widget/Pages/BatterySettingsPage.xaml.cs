using DeviceBatteryControl.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WidBar.SDK;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;

namespace DeviceBatteryControl.Pages;

public sealed record DeviceChoice(string? Id, string Name);
public sealed partial class BatterySettingsPage : UserControl
{
    private readonly IWidgetSettingsContext _context;
    private readonly BatteryMonitor _monitor;
    private readonly BatteryPreviewView _sample = new();
    private bool _ready;
    private readonly int[] _intervals = [60, 120, 300, 900];
    public BatterySettingsPage(IWidgetSettingsContext context, BatteryMonitor monitor)
    {
        _context = context;
        _monitor = monitor;
        InitializeComponent();
        var settings = BatterySettings.FromJson(context.SettingsJson);
        LivePreviewHeader.Text = BatteryLoc.Get("LivePreview");
        LivePreviewNote.Text = BatteryLoc.Get("SettingsDescription");
        PreviewSectionText.Text = BatteryLoc.Get("TaskbarPreview");
        DataSectionText.Text = BatteryLoc.Get("DataSection");
        CountCard.Header = BatteryLoc.Get("PreviewCount");
        PreviewCount.ItemsSource = Enumerable.Range(1, 3).Select(x => BatteryLoc.Format("DeviceCount", x)).ToArray();
        PreviewCount.SelectedIndex = settings.PreviewDeviceCount - 1;
        PinnedCard.Header = BatteryLoc.Get("PinnedDevice");
        var choices = new List<DeviceChoice> { new(null, BatteryLoc.Get("Automatic")) };
        choices.AddRange(monitor.Devices.Select(x => new DeviceChoice(x.Id, x.Name)));
        if (settings.PinnedDeviceId is not null && choices.All(x => x.Id != settings.PinnedDeviceId))
            choices.Add(new(settings.PinnedDeviceId, BatteryLoc.Get("SavedDevice")));
        PinnedDevice.ItemsSource = choices;
        PinnedDevice.SelectedItem = choices.FirstOrDefault(x => x.Id == settings.PinnedDeviceId) ?? choices[0];
        RefreshCard.Header = BatteryLoc.Get("RefreshInterval");
        RefreshInterval.ItemsSource = _intervals.Select(x => BatteryLoc.Format("Minutes", x / 60)).ToArray();
        RefreshInterval.SelectedIndex = Math.Max(0, Array.IndexOf(_intervals, settings.RefreshSeconds));
        ThresholdCard.Header = BatteryLoc.Get("LowThreshold");
        Threshold.Value = settings.LowBatteryThreshold;
        DisconnectedCard.Header = BatteryLoc.Get("ShowDisconnected");
        DisconnectedToggle.IsOn = settings.ShowDisconnected;
        HistoryCard.Header = BatteryLoc.Get("KeepHistory");
        HistoryCard.Description = BatteryLoc.Get("HistoryDescription");
        HistoryToggle.IsOn = settings.KeepHistory;
        SampleHost.Content = _sample;
        RenderSample(settings);
        _ready = true;
    }
    private void OnChanged(object sender, SelectionChangedEventArgs args) => SaveDraft();
    private void OnNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SaveDraft();
    private void OnToggled(object sender, RoutedEventArgs args) => SaveDraft();
    private void RenderSample(BatterySettings settings)
    {
        _sample.Width = settings.PreviewWidth;
        _sample.Render(_monitor.Devices, settings);
        PreviewWidthText.Text = BatteryLoc.Format("PreviewWidth", settings.PreviewWidth);
    }
    private void SaveDraft()
    {
        if (!_ready || double.IsNaN(Threshold.Value)) return;
        var settings = new BatterySettings
        {
            PreviewDeviceCount = PreviewCount.SelectedIndex + 1,
            PinnedDeviceId = (PinnedDevice.SelectedItem as DeviceChoice)?.Id,
            RefreshSeconds = _intervals[Math.Max(0, RefreshInterval.SelectedIndex)],
            LowBatteryThreshold = (int)Threshold.Value,
            ShowDisconnected = DisconnectedToggle.IsOn,
            KeepHistory = HistoryToggle.IsOn
        };
        RenderSample(settings);
        _context.SaveSettings(settings.ToJson());
        _context.RequestPreviewRefresh();
    }
}
