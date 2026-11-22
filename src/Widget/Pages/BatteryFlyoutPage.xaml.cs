using DeviceBatteryControl.Controls;
using System.Collections.ObjectModel;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;

namespace DeviceBatteryControl.Pages;

public sealed record BatteryStatRow(string Label, string Value);

public sealed partial class BatteryFlyoutPage : UserControl, IDisposable
{
    private readonly BatteryMonitor _monitor;
    private readonly ObservableCollection<BatteryDeviceRow> _rows = [];
    private BatterySettings _settings;
    private string? _selectedId;
    private bool _active;
    private bool _detail;
    private Storyboard? _refreshAnimation;
    private string _pillSignature = "";
    private bool _detailLoaded;
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    private IReadOnlyList<BatteryStatRow> _stats = [];

    public BatteryFlyoutPage(BatteryMonitor monitor, BatterySettings settings)
    {
        _monitor = monitor;
        _settings = settings;
        InitializeComponent();
        DeviceList.ItemsSource = _rows;
        ScrollBarAutoHide.Attach(HomePage);
        HeaderTitle.Text = BatteryLoc.Get("WidgetName");
        DevicesHeading.Text = BatteryLoc.Get("Devices");
        DevicesHint.Text = BatteryLoc.Get("SelectDevice");
        SearchBox.PlaceholderText = BatteryLoc.Get("SearchDevices");
        AutomationProperties.SetName(SearchBox, BatteryLoc.Get("SearchDevices"));
        EmptyTitle.Text = BatteryLoc.Get("NoDevices");
        EmptyHint.Text = BatteryLoc.Get("EmptyHint");
        BluetoothSettings.Content = BatteryLoc.Get("OpenBluetooth");
        HomeNote.Text = Environment.GetCommandLineArgs().Contains("--demo") ? BatteryLoc.Get("Demo") : BatteryLoc.Get("HomeNote");
        Describe(RefreshButton, "Refresh");
        Describe(BackButton, "BackToDevices");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => { if (_active) Render(); };
        Render();
    }

    private static void Describe(FrameworkElement element, string key)
    {
        ToolTipService.SetToolTip(element, BatteryLoc.Get(key));
        AutomationProperties.SetName(element, BatteryLoc.Get(key));
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => SetActive(true);
    private void OnUnloaded(object sender, RoutedEventArgs args) => SetActive(false);
    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (active) { _monitor.Changed += OnChanged; Render(); }
        else { _monitor.Changed -= OnChanged; StopRefreshAnimation(); }
    }
    public void SetSettings(BatterySettings settings) { _settings = settings; if (_active) Render(); }
    private void OnChanged(object? sender, EventArgs args) => Render();
    private async void OnRefresh(object sender, RoutedEventArgs args) => await _monitor.RefreshAsync();
    private async void OnOpenSettings(object sender, RoutedEventArgs args) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => RenderRows();

    private BatteryDevice[] VisibleDevices() => _monitor.Devices
        .Where(x => _settings.ShowDisconnected || x.IsConnected || x.IsReceiverOnly).ToArray();

    private void Render()
    {
        RefreshButton.IsEnabled = !_monitor.IsRefreshing;
        if (_active && _monitor.IsRefreshing && _uiSettings.AnimationsEnabled)
            StartRefreshAnimation();
        else StopRefreshAnimation();
        ErrorBar.IsOpen = _monitor.Error is not null;
        ErrorBar.Message = _monitor.Error is null ? "" : BatteryLoc.Get(_monitor.Error);
        if (!_detail) RenderRows();
        if (_detail)
        {
            var selected = VisibleDevices().FirstOrDefault(x => x.Id == _selectedId);
            if (selected is null) ShowHome();
            else RenderDetails(selected);
        }
        else HeaderSubtitle.Text = BatteryLoc.Format("ConnectedCount", _monitor.Devices.Count(x => x.IsConnected));
    }

    private void RenderRows()
    {
        if (DeviceList is null) return;
        var query = SearchBox.Text?.Trim() ?? "";
        var devices = VisibleDevices().Where(x => query.Length == 0 || x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        var byId = _rows.ToDictionary(x => x.DeviceId, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var position = 0; position < devices.Length; position++)
        {
            var device = devices[position];
            seen.Add(device.Id);
            if (!byId.TryGetValue(device.Id, out var row))
            {
                row = new BatteryDeviceRow(device.Id);
                _rows.Insert(position, row);
            }
            else if (_rows.IndexOf(row) != position) _rows.Move(_rows.IndexOf(row), position);
            row.Update(device, _settings, _settings.KeepHistory ? _monitor.GetHistory(device.Id) : [], position == 0);
        }
        for (var i = _rows.Count - 1; i >= 0; i--)
            if (!seen.Contains(_rows[i].DeviceId)) _rows.RemoveAt(i);
        EmptyPanel.Visibility = devices.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        DeviceList.Visibility = devices.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = BatteryLoc.Get(query.Length > 0 ? "NoSearchResults" : "NoDevices");
    }

    private void OnDeviceClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is BatteryDeviceRow row) OpenDevice(row.DeviceId);
    }
    internal void OpenDevice(string id)
    {
        var device = VisibleDevices().FirstOrDefault(x => x.Id == id);
        if (device is null) return;
        EnsureDetail();
        _selectedId = id;
        _detail = true;
        HomePage.Visibility = Visibility.Collapsed;
        DetailPage.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        RenderDetails(device);
        HistoryChart.PlayEntrance();
        DetailPage.ChangeView(null, 0, null, true);
        if (_uiSettings.AnimationsEnabled)
            Motion.Enter(new UIElement[] { HeroSection, HistorySection, DetailsSection }, new Vector3(24, 0, 0));
    }
    private void OnBackClicked(object sender, RoutedEventArgs args) => ShowHome();
    private void EnsureDetail()
    {
        if (_detailLoaded) return;
        _ = FindName(nameof(DetailPage));
        ScrollBarAutoHide.Attach(DetailPage);
        HistoryHeading.Text = BatteryLoc.Get("History");
        DetailsHeading.Text = BatteryLoc.Get("Details");
        SupportHint.Text = BatteryLoc.Get("SupportHint");
        _detailLoaded = true;
    }
    private void ShowHome()
    {
        _detail = false;
        RenderRows();
        DetailPage.Visibility = Visibility.Collapsed;
        HomePage.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Collapsed;
        HeaderTitle.Text = BatteryLoc.Get("WidgetName");
        HeaderSubtitle.Text = BatteryLoc.Format("ConnectedCount", _monitor.Devices.Count(x => x.IsConnected));
        if (_active && _uiSettings.AnimationsEnabled)
            Motion.Enter(new UIElement[] { SearchBox, DevicesSection, HomeNote }, new Vector3(-24, 0, 0));
    }

    private void RenderDetails(BatteryDevice device)
    {
        HeaderTitle.Text = device.Name;
        HeaderSubtitle.Text = BatteryPresentation.Source(device);
        HeroValue.Text = device.Percentage?.ToString(BatteryLoc.FormattingCulture) ?? BatteryPresentation.Level(device);
        HeroUnit.Visibility = device.Percentage.HasValue ? Visibility.Visible : Visibility.Collapsed;
        BatteryAppearance.ApplyIcon(HeroIcon, device, _settings);
        HeroStatus.Text = BatteryPresentation.Status(device, _settings);
        HeroUpdated.Text = device.LastReading is null ? BatteryLoc.Get("NoBatteryReading")
            : BatteryLoc.Format("UpdatedAt", device.LastReading.Value.ToLocalTime().ToString("g", BatteryLoc.FormattingCulture));
        HistorySection.Visibility = _settings.KeepHistory && !device.IsReceiverOnly ? Visibility.Visible : Visibility.Collapsed;
        var samples = _monitor.GetHistory(device.Id);
        HistoryRange.Text = BatteryLoc.Format("SampleCount", samples.Count);
        HistoryChart.SetSamples(samples, _settings, device.Kind);
        HistoryHint.Text = BatteryLoc.Get(samples.Count == 1 ? "HistorySingle" : samples.Count == 0 ? "HistoryEmpty" : "HistoryHint");
        var stats = new List<BatteryStatRow>
        {
            new(BatteryLoc.Get("Connection"), BatteryLoc.Get(device.IsReceiverOnly ? "ReceiverPresent" : device.IsConnected ? "Connected" : "DisconnectedOnly")),
            new(BatteryLoc.Get("Source"), BatteryPresentation.Source(device)),
            new(BatteryLoc.Get("LastReading"), device.LastReading?.ToLocalTime().ToString("g", BatteryLoc.FormattingCulture) ?? "—"),
            new(BatteryLoc.Get("ChargeState"), BatteryLoc.Get(device.IsCharging == true ? "Charging" : "StateUnavailable"))
        };
        if (!string.IsNullOrWhiteSpace(device.BatteryDetails)) stats.Add(new(BatteryLoc.Get("DeviceBatteryDetails"), device.BatteryDetails));
        if (device.Millivolts.HasValue) stats.Add(new(BatteryLoc.Get("Voltage"), device.Millivolts.Value.ToString(BatteryLoc.FormattingCulture) + " mV"));
        if (!_stats.SequenceEqual(stats)) { _stats = stats; DeviceStats.ItemsSource = stats; }
        RenderPills();
    }
    private void RenderPills()
    {
        var devices = VisibleDevices();
        var signature = string.Join('|', devices.Select(x => x.Id + ":" + x.Name)) + _selectedId;
        if (_pillSignature == signature) return;
        _pillSignature = signature;
        PillsPanel.Children.Clear();
        foreach (var device in devices)
        {
            var button = new Button
            {
                Content = device.Name,
                Style = (Style)Resources[device.Id == _selectedId ? "ActivePillStyle" : "PillStyle"]
            };
            AutomationProperties.SetName(button, device.Name);
            button.Click += (_, _) => OpenDevice(device.Id);
            PillsPanel.Children.Add(button);
        }
    }
    private void StartRefreshAnimation()
    {
        if (_refreshAnimation is not null) return;
        var animation = new DoubleAnimation { From = 0, To = 360, Duration = new Duration(TimeSpan.FromMilliseconds(900)), RepeatBehavior = RepeatBehavior.Forever };
        Storyboard.SetTarget(animation, RefreshIconRotation);
        Storyboard.SetTargetProperty(animation, "Angle");
        _refreshAnimation = new Storyboard();
        _refreshAnimation.Children.Add(animation);
        _refreshAnimation.Begin();
    }
    private void StopRefreshAnimation()
    {
        _refreshAnimation?.Stop();
        _refreshAnimation = null;
        RefreshIconRotation.Angle = 0;
    }
    public void Dispose()
    {
        SetActive(false);
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }
}
