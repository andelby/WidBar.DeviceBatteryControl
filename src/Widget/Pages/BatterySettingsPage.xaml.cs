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
    private readonly int[] _intervals = [60, 120, 300, 900];
    private BatterySettings _current;
    private bool _ready;
    private bool _listening;
    private string _deviceSignature = "";

    public BatterySettingsPage(IWidgetSettingsContext context, BatteryMonitor monitor)
    {
        _context = context;
        _monitor = monitor;
        _current = BatterySettings.FromJson(context.SettingsJson);
        InitializeComponent();
        LivePreviewHeader.Text = BatteryLoc.Get("LivePreview");
        PreviewSectionText.Text = BatteryLoc.Get("TaskbarPreview");
        AppearanceSectionText.Text = BatteryLoc.Get("AppearanceSection");
        DataSectionText.Text = BatteryLoc.Get("DataSection");
        CountCard.Header = BatteryLoc.Get("PreviewCount");
        PreviewCount.ItemsSource = Enumerable.Range(1, 3).Select(x => BatteryLoc.Format("DeviceCount", x)).ToArray();
        PreviewCount.SelectedIndex = _current.PreviewDeviceCount - 1;
        PinnedCard.Header = BatteryLoc.Get("PinnedDevice");
        UpdateDevices();
        RefreshCard.Header = BatteryLoc.Get("RefreshInterval");
        RefreshInterval.ItemsSource = _intervals.Select(x => BatteryLoc.Format("Minutes", x / 60)).ToArray();
        RefreshInterval.SelectedIndex = Math.Max(0, Array.IndexOf(_intervals, _current.RefreshSeconds));
        ThresholdCard.Header = BatteryLoc.Get("LowThreshold");
        Threshold.Value = _current.LowBatteryThreshold;
        DisconnectedCard.Header = BatteryLoc.Get("ShowDisconnected");
        DisconnectedToggle.IsOn = _current.ShowDisconnected;
        HistoryCard.Header = BatteryLoc.Get("KeepHistory");
        HistoryCard.Description = BatteryLoc.Get("HistoryDescription");
        HistoryToggle.IsOn = _current.KeepHistory;
        IconsCard.Header = BatteryLoc.Get("ShowPreviewIcons");
        IconsToggle.IsOn = _current.ShowIcons;
        LabelModeCard.Header = BatteryLoc.Get("PreviewLabels");
        LabelModePicker.ItemsSource = new[] { BatteryLoc.Get("LabelNone"), BatteryLoc.Get("LabelDeviceName"), BatteryLoc.Get("LabelCustom") };
        LabelModePicker.SelectedIndex = (int)_current.LabelMode;
        PercentCard.Header = BatteryLoc.Get("ShowPercentSymbol");
        PercentToggle.IsOn = _current.ShowPercentSymbol;
        HighlightCard.Header = BatteryLoc.Get("HighlightLowBattery");
        HighlightCard.Description = BatteryLoc.Get("HighlightDescription");
        HighlightToggle.IsOn = _current.HighlightLowBattery;
        ChartScaleCard.Header = BatteryLoc.Get("ChartScale");
        ChartScaleCard.Description = BatteryLoc.Get("ChartScaleDescription");
        ChartScalePicker.ItemsSource = new[] { BatteryLoc.Get("ScaleAutomatic"), BatteryLoc.Get("ScaleFull") };
        ChartScalePicker.SelectedIndex = (int)_current.ChartScale;
        MouseEditor.Configure(DeviceKind.Mouse, _current.MouseAppearance);
        KeyboardEditor.Configure(DeviceKind.Keyboard, _current.KeyboardAppearance);
        HeadphonesEditor.Configure(DeviceKind.Headphones, _current.HeadphonesAppearance);
        MouseEditor.Changed += OnAppearanceChanged;
        KeyboardEditor.Changed += OnAppearanceChanged;
        HeadphonesEditor.Changed += OnAppearanceChanged;
        ForkCredit.Text = BatteryLoc.Get("ForkCredit");
        ProjectLink.Content = BatteryLoc.Get("ProjectSource");
        UpstreamLink.Content = BatteryLoc.Get("UpstreamProject");
        SampleHost.Content = _sample;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _ready = true;
        RenderSample();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _ready = true;
        if (!_listening) { _monitor.Changed += OnMonitorChanged; _listening = true; }
        UpdateDevices();
        RenderSample();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_listening) { _monitor.Changed -= OnMonitorChanged; _listening = false; }
        _ready = false;
    }
    private void OnMonitorChanged(object? sender, EventArgs args) { UpdateDevices(); RenderSample(); }
    private void OnChanged(object sender, SelectionChangedEventArgs args) => SaveDraft();
    private void OnNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => SaveDraft();
    private void OnToggled(object sender, RoutedEventArgs args) => SaveDraft();
    private void OnAppearanceChanged(object? sender, EventArgs args) => SaveDraft();

    private void UpdateDevices()
    {
        var signature = string.Join('|', _monitor.Devices.Select(x => x.Id + x.Name)) + _current.PinnedDeviceId;
        if (_deviceSignature == signature && PinnedDevice.ItemsSource is not null) return;
        _deviceSignature = signature;
        var wasReady = _ready;
        _ready = false;
        var choices = new List<DeviceChoice> { new(null, BatteryLoc.Get("Automatic")) };
        choices.AddRange(_monitor.Devices.Select(x => new DeviceChoice(x.Id, x.Name)));
        if (_current.PinnedDeviceId is not null && choices.All(x => x.Id != _current.PinnedDeviceId))
            choices.Add(new(_current.PinnedDeviceId, BatteryLoc.Get("SavedDevice")));
        PinnedDevice.ItemsSource = choices;
        PinnedDevice.SelectedItem = choices.FirstOrDefault(x => x.Id == _current.PinnedDeviceId) ?? choices[0];
        _ready = wasReady;
    }

    private void RenderSample()
    {
        var live = _monitor.Devices.Any(x => x.IsConnected && !x.IsReceiverOnly);
        IReadOnlyList<BatteryDevice> examples = [
            new("sample-mouse", BatteryLoc.Get("DeviceMouse"), DeviceKind.Mouse, true, 76, BatterySource.Windows, null),
            new("sample-keyboard", BatteryLoc.Get("DeviceKeyboard"), DeviceKind.Keyboard, true, 92, BatterySource.Windows, null),
            new("sample-headphones", BatteryLoc.Get("DeviceHeadphones"), DeviceKind.Headphones, true, 18, BatterySource.Windows, null)
        ];
        _sample.Width = _current.PreviewWidth;
        _sample.Render(live ? _monitor.Devices : examples, _current);
        LivePreviewNote.Text = BatteryLoc.Get(live ? "SettingsDescription" : "PreviewSampleNote");
        PreviewWidthText.Text = BatteryLoc.Format("PreviewWidth", _current.PreviewWidth);
    }

    private void SaveDraft()
    {
        if (!_ready || double.IsNaN(Threshold.Value)) return;
        _current = BatterySettings.FromJson((_current with
        {
            PreviewDeviceCount = PreviewCount.SelectedIndex + 1,
            PinnedDeviceId = (PinnedDevice.SelectedItem as DeviceChoice)?.Id,
            RefreshSeconds = _intervals[Math.Max(0, RefreshInterval.SelectedIndex)],
            LowBatteryThreshold = (int)Threshold.Value,
            ShowDisconnected = DisconnectedToggle.IsOn,
            KeepHistory = HistoryToggle.IsOn,
            ShowIcons = IconsToggle.IsOn,
            ShowPercentSymbol = PercentToggle.IsOn,
            HighlightLowBattery = HighlightToggle.IsOn,
            LabelMode = (PreviewLabelMode)Math.Max(0, LabelModePicker.SelectedIndex),
            ChartScale = (BatteryChartScale)Math.Max(0, ChartScalePicker.SelectedIndex),
            MouseAppearance = MouseEditor.Value,
            KeyboardAppearance = KeyboardEditor.Value,
            HeadphonesAppearance = HeadphonesEditor.Value
        }).ToJson());
        RenderSample();
        _context.SaveSettings(_current.ToJson());
    }
}
