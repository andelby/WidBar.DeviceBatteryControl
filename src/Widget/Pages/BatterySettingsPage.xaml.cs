using DeviceBatteryControl.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WidBar.SDK;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;

namespace DeviceBatteryControl.Pages;

public sealed record DeviceChoice(string Id, string Name, string Glyph);

public sealed partial class BatterySettingsPage : UserControl
{
    private readonly IWidgetSettingsContext _context;
    private readonly BatteryMonitor _monitor;
    private readonly BatteryPreviewView _sample = new();
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
        PreviewCount.ItemsSource = Enumerable.Range(1, 10).Select(x => BatteryLoc.Format("DeviceCount", x)).ToArray();
        PreviewCount.SelectedIndex = _current.PreviewDeviceCount - 1;
        DevicesCard.Header = BatteryLoc.Get("TaskbarDevices");
        AutomaticCard.Header = BatteryLoc.Get("AutomaticSelection");
        AutomaticToggle.IsOn = _current.SelectedDeviceIds is null;
        EmptyCard.Header = BatteryLoc.Get("ShowWhenEmpty");
        EmptyToggle.IsOn = _current.ShowWhenEmpty;
        UpdateDevices();
        ThresholdCard.Header = BatteryLoc.Get("LowThreshold");
        Threshold.Value = _current.LowBatteryThreshold;
        DisconnectedCard.Header = BatteryLoc.Get("ShowDisconnected");
        DisconnectedToggle.IsOn = _current.ShowDisconnected;
        HistoryCard.Header = BatteryLoc.Get("KeepHistory");
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
        MouseEditor.Configure(DeviceKind.Mouse, _current.MouseAppearance);
        KeyboardEditor.Configure(DeviceKind.Keyboard, _current.KeyboardAppearance);
        HeadphonesEditor.Configure(DeviceKind.Headphones, _current.HeadphonesAppearance);
        MouseEditor.Changed += OnAppearanceChanged;
        KeyboardEditor.Changed += OnAppearanceChanged;
        HeadphonesEditor.Changed += OnAppearanceChanged;
        SampleHost.Content = _sample;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _ready = true;
        UpdateSelectionState();
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
    private void OnAutomaticToggled(object sender, RoutedEventArgs args)
    {
        if (!_ready) return;
        if (!AutomaticToggle.IsOn && DevicePicker.SelectedItems.Count == 0)
        {
            _ready = false;
            var ids = BatterySelection.Preview(_monitor.Devices, _current).Select(x => DeviceIdentity.Normalize(x.Id)).ToHashSet();
            foreach (var item in DevicePicker.Items.OfType<DeviceChoice>().Where(x => ids.Contains(DeviceIdentity.Normalize(x.Id))))
                DevicePicker.SelectedItems.Add(item);
            _ready = true;
        }
        SaveDraft();
        UpdateSelectionState();
    }

    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready) return;
        _ready = false;
        foreach (var item in args.AddedItems)
            if (DevicePicker.SelectedItems.Count > 10) DevicePicker.SelectedItems.Remove(item);
        _ready = true;
        SaveDraft();
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        DevicePicker.IsEnabled = !AutomaticToggle.IsOn;
        CountCard.IsEnabled = AutomaticToggle.IsOn;
        SelectionCount.Text = AutomaticToggle.IsOn ? BatteryLoc.Get("Automatic") : BatteryLoc.Format("SelectedCount", DevicePicker.SelectedItems.Count);
    }

    private void UpdateDevices()
    {
        var known = _monitor.History.KnownDevices.Select(x => new BatteryDevice(x.Key, x.Value.Name, x.Value.Kind, false, null, BatterySource.Windows, null));
        var choices = known.Concat(_monitor.Devices.Where(x => !x.IsReceiverOnly))
            .GroupBy(x => DeviceIdentity.Normalize(x.Id)).Select(x => x.Last())
            .Select(x => new DeviceChoice(x.Id, x.Name, DeviceIcons.Glyph(x, _current))).ToList();
        foreach (var id in _current.SelectedDeviceIds ?? [])
            if (choices.All(x => DeviceIdentity.Normalize(x.Id) != id)) choices.Add(new(id, BatteryLoc.Get("SavedDevice"), "\uE975"));
        choices = choices.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var signature = string.Join('|', choices.Select(x => x.Id + x.Name + x.Glyph));
        if (_deviceSignature == signature && DevicePicker.ItemsSource is not null) return;
        _deviceSignature = signature;
        var wasReady = _ready;
        _ready = false;
        DevicePicker.ItemsSource = choices;
        foreach (var choice in choices.Where(x => _current.SelectedDeviceIds?.Contains(DeviceIdentity.Normalize(x.Id)) == true))
            DevicePicker.SelectedItems.Add(choice);
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
        _sample.Width = _current.GetPreviewWidth(live ? _monitor.Devices : examples);
        _sample.Render(live ? _monitor.Devices : examples, _current);
        LivePreviewNote.Text = BatteryLoc.Get(live ? "SettingsDescription" : "PreviewSampleNote");
        PreviewWidthText.Text = BatteryLoc.Format("PreviewWidth", (int)_sample.Width);
    }

    private void SaveDraft()
    {
        if (!_ready || double.IsNaN(Threshold.Value)) return;
        _current = BatterySettings.FromJson((_current with
        {
            PreviewDeviceCount = PreviewCount.SelectedIndex + 1,
            SelectedDeviceIds = AutomaticToggle.IsOn ? null : DevicePicker.SelectedItems.OfType<DeviceChoice>().Select(x => x.Id).ToArray(),
            ShowWhenEmpty = EmptyToggle.IsOn,
            LowBatteryThreshold = (int)Threshold.Value,
            ShowDisconnected = DisconnectedToggle.IsOn,
            KeepHistory = HistoryToggle.IsOn,
            ShowIcons = IconsToggle.IsOn,
            ShowPercentSymbol = PercentToggle.IsOn,
            HighlightLowBattery = HighlightToggle.IsOn,
            LabelMode = (PreviewLabelMode)Math.Max(0, LabelModePicker.SelectedIndex),
            MouseAppearance = MouseEditor.Value,
            KeyboardAppearance = KeyboardEditor.Value,
            HeadphonesAppearance = HeadphonesEditor.Value
        }).ToJson());
        RenderSample();
        _context.SaveSettings(_current.ToJson());
    }
}
