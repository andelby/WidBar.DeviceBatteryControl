using DeviceBatteryControl.Pages;
using Microsoft.UI.Xaml;
using WidBar.SDK;
using DeviceBatteryControl.Models;
using DeviceBatteryControl.Services;
using DeviceBatteryControl.Controls;

namespace DeviceBatteryControl.Hosting;

public sealed class DeviceBatteryPlugin : WidgetPluginBase, IConfigurableWidgetPlugin, IWidgetFlyoutLifecycle, IWidgetTileLifecycle
{
    private BatteryMonitor? _monitor;
    private BatterySettings _settings = new();
    private BatteryPreviewView? _preview;
    private BatteryFlyoutPage? _flyout;
    private bool _detailsVisible;
    private bool _disposed;
    private Timer? _hideTimer;
    private DateTimeOffset _lastConnected = DateTimeOffset.MinValue;
    private static readonly TimeSpan HideGrace = TimeSpan.FromSeconds(5);
    public override string Id => "com.widbar.devicebatterycontrol";
    public override string Name => "Device Battery Control";
    public override string Version => "0.5.0";
    public override int PreviewLogicalWidth => _settings.GetPreviewWidth(_monitor?.Devices ?? []);
    public override bool IsPreviewVisible => _detailsVisible || BatterySelection.IsVisible(_monitor?.Devices ?? [], _settings)
        || DateTimeOffset.UtcNow - _lastConnected < HideGrace;
    public override int FlyoutWidth => 620;
    public override int FlyoutHeight => 650;
    public override int TileHeight => 590;

    public override async Task InitializeAsync(IWidgetContext context)
    {
        await base.InitializeAsync(context);
        _settings = BatterySettings.FromJson(context.SettingsJson);
        _hideTimer = new Timer(_ => Context?.RequestPreviewRefresh(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _monitor = BatteryMonitor.Join(this, context.SharedDataDirectory, _settings, !context.IsBoardTile);
        _monitor.Changed += OnChanged;
        context.PreviewVisibilityChanged += OnVisibilityChanged;
        // Local disk and device discovery must not hold the SDK handshake up.
        _ = InitializeDataAsync();
    }
    private async Task InitializeDataAsync()
    {
        await _monitor!.LoadHistoryAsync();
        if (!_disposed && (_detailsVisible || Context?.IsBoardTile == false) &&
            (_monitor.LastRefresh is null || DateTimeOffset.UtcNow - _monitor.LastRefresh > TimeSpan.FromSeconds(BatterySettings.RefreshSeconds)))
            await _monitor.RefreshAsync();
    }
    public override UIElement? CreatePreviewContent()
    {
        _preview = new BatteryPreviewView();
        Render();
        return _preview;
    }
    public override UIElement? CreateFlyoutContent() => _flyout = new BatteryFlyoutPage(_monitor!, _settings);
    public override UIElement? CreateTileContent() => CreateFlyoutContent();
    public UIElement? CreateSettingsContent(IWidgetSettingsContext context) => new BatterySettingsPage(context, _monitor!);
    public override void OnSettingsDraftChanged(string json)
    {
        _settings = BatterySettings.FromJson(json);
        if (BatterySelection.Preview(_monitor?.Devices ?? [], _settings).Count == 0)
            _lastConnected = DateTimeOffset.MinValue;
        _flyout?.SetSettings(_settings);
        UpdateActivity();
        Render();
        Context?.RequestPreviewRefresh();
    }
    private void OnChanged(object? sender, EventArgs args)
    {
        if (BatterySelection.Preview(_monitor?.Devices ?? [], _settings).Count > 0)
        {
            _lastConnected = DateTimeOffset.UtcNow;
            _hideTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        else if (!_settings.ShowWhenEmpty && DateTimeOffset.UtcNow - _lastConnected < HideGrace)
            _hideTimer?.Change(HideGrace, Timeout.InfiniteTimeSpan);
        if (Context?.IsPreviewVisible == true) Render();
        Context?.RequestPreviewRefresh();
    }
    private void Render() => _preview?.Render(_monitor?.Devices ?? [], _settings);
    private void OnVisibilityChanged(object? sender, bool visible)
    {
        UpdateActivity();
        if (visible) { Render(); _ = InitializeDataAsync(); }
    }
    private void UpdateActivity() => _monitor?.UpdateClient(this, _settings,
        _detailsVisible || Context?.IsBoardTile == false);
    public void OnFlyoutShown() { _detailsVisible = true; _flyout?.SetActive(true); UpdateActivity(); _ = InitializeDataAsync(); }
    public void OnFlyoutHidden() { _detailsVisible = false; _flyout?.SetActive(false); UpdateActivity(); Context?.RequestPreviewRefresh(); }
    public void OnTileShown() => OnFlyoutShown();
    public void OnTileHidden() => OnFlyoutHidden();
    public override async ValueTask DisposeAsync()
    {
        _disposed = true;
        _hideTimer?.Dispose();
        if (Context is not null) Context.PreviewVisibilityChanged -= OnVisibilityChanged;
        _flyout?.Dispose();
        if (_monitor is not null)
        {
            _monitor.Changed -= OnChanged;
            await _monitor.LeaveAsync(this);
        }
    }
}
