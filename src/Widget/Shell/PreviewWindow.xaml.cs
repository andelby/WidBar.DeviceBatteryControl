using DeviceBatteryControl.Pages;
using DeviceBatteryControl.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WidBar.SDK;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;

namespace DeviceBatteryControl.Shell;

public sealed partial class PreviewWindow : Window
{
    private readonly BatteryMonitor _monitor;
    private readonly BatteryFlyoutPage _flyout;
    private readonly BatteryPreviewView _preview;
    private BatterySettings _settings = new();
    private bool _showSettings;
    private readonly bool _demo;
    public PreviewWindow()
    {
        InitializeComponent();
        _demo = Environment.GetCommandLineArgs().Contains("--demo");
        Title = "Device Battery Control" + (_demo ? " · " + BatteryLoc.Get("Demo") : "");
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 940));
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WidBar.DeviceBatteryControl", _demo ? "Demo" : "Preview");
        _monitor = new BatteryMonitor(_demo ? new DemoBatteryProvider() : new ConnectedBatteryProvider(), directory);
        _monitor.UpdateClient(this, _settings, true);
        _preview = new BatteryPreviewView { Width = _settings.PreviewWidth };
        _flyout = new BatteryFlyoutPage(_monitor, _settings);
        TaskbarContent.Content = _preview;
        FlyoutContent.Content = _flyout;
        SettingsButton.Content = BatteryLoc.Get("Settings");
        ThemePicker.ItemsSource = new[] { BatteryLoc.Get("SystemTheme"), BatteryLoc.Get("LightTheme"), BatteryLoc.Get("DarkTheme") };
        ThemePicker.SelectedIndex = 0;
        _monitor.Changed += OnChanged;
        Closed += OnClosed;
        _ = StartAsync();
    }
    private async Task StartAsync()
    {
        await _monitor.LoadHistoryAsync();
        if (_demo)
        {
            _monitor.History.Devices.Clear();
            var provider = new DemoBatteryProvider();
            var devices = await provider.ReadAsync(CancellationToken.None);
            foreach (var device in devices.Where(x => x.IsConnected && x.Percentage.HasValue))
                for (var i = 0; i < 672; i++)
                    _monitor.History.Add(device with { Percentage = Math.Min(100, device.Percentage!.Value + (671 - i) / 96) },
                        DateTimeOffset.UtcNow.AddMinutes(-10080 + i * 15));
        }
        await _monitor.RefreshAsync();
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Contains("--settings")) OnSettings(this, new RoutedEventArgs());
        var deviceIndex = Array.IndexOf(arguments, "--device");
        if (deviceIndex >= 0 && deviceIndex + 1 < arguments.Length)
            _flyout.OpenDevice(arguments[deviceIndex + 1]);
        if (Environment.GetCommandLineArgs().Contains("--diagnostics"))
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WidBar.DeviceBatteryControl", "preview-snapshot.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new
            {
                CapturedUtc = DateTimeOffset.UtcNow, _monitor.Error, _monitor.LastRefreshMilliseconds, _monitor.Devices
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private void OnChanged(object? sender, EventArgs args)
    {
        _preview.Width = _settings.GetPreviewWidth(_monitor.Devices);
        _preview.Render(_monitor.Devices, _settings);
    }
    private void OnThemeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (Root is not null) Root.RequestedTheme = ThemePicker.SelectedIndex switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
    }
    private void OnSettings(object sender, RoutedEventArgs args)
    {
        _showSettings = !_showSettings;
        FlyoutContent.Content = _showSettings ? new BatterySettingsPage(new PreviewSettingsContext(this), _monitor) : _flyout;
        SettingsButton.Content = BatteryLoc.Get(_showSettings ? "Back" : "Settings");
    }
    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _flyout.Dispose(); _monitor.Changed -= OnChanged; await _monitor.DisposeAsync();
    }
    private sealed class PreviewSettingsContext(PreviewWindow owner) : IWidgetSettingsContext
    {
        public string InstanceId => "preview";
        public string SettingsJson => owner._settings.ToJson();
        public void SaveSettings(string json)
        {
            owner._settings = BatterySettings.FromJson(json);
            owner._preview.Width = owner._settings.GetPreviewWidth(owner._monitor.Devices);
            owner._flyout.SetSettings(owner._settings);
            owner._monitor.UpdateClient(owner, owner._settings, true);
            owner._preview.Render(owner._monitor.Devices, owner._settings);
        }
        public void RequestPreviewRefresh() { }
    }
}
