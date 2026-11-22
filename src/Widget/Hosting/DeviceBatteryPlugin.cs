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
    public override string Id => "com.widbar.devicebatterycontrol";
    public override string Name => "Device Battery Control";
    public override string Version => "0.3.0";
    public override int PreviewLogicalWidth => _settings.PreviewWidth;
    public override int FlyoutWidth => 620;
    public override int FlyoutHeight => 650;
    public override int TileHeight => 590;

    public override async Task InitializeAsync(IWidgetContext context)
    {
        await base.InitializeAsync(context);
        _settings = BatterySettings.FromJson(context.SettingsJson);
        _monitor = BatteryMonitor.Join(this, context.SharedDataDirectory, _settings, context.IsPreviewVisible && !context.IsBoardTile);
        _monitor.Changed += OnChanged;
        context.PreviewVisibilityChanged += OnVisibilityChanged;
        // Local disk and device discovery must not hold the SDK handshake up.
        _ = InitializeDataAsync();
    }
    private async Task InitializeDataAsync()
    {
        await _monitor!.LoadHistoryAsync();
        if (!_disposed && (_detailsVisible || (Context?.IsPreviewVisible == true && !Context.IsBoardTile)) &&
            (_monitor.LastRefresh is null || DateTimeOffset.UtcNow - _monitor.LastRefresh > TimeSpan.FromMinutes(1)))
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
        _flyout?.SetSettings(_settings);
        UpdateActivity();
        Render();
        Context?.RequestPreviewRefresh();
    }
    private void OnChanged(object? sender, EventArgs args)
    {
        if (Context?.IsPreviewVisible == true) Render();
    }
    private void Render() => _preview?.Render(_monitor?.Devices ?? [], _settings);
    private void OnVisibilityChanged(object? sender, bool visible)
    {
        UpdateActivity();
        if (visible) { Render(); _ = InitializeDataAsync(); }
    }
    private void UpdateActivity() => _monitor?.UpdateClient(this, _settings,
        _detailsVisible || (Context?.IsPreviewVisible == true && Context.IsBoardTile == false));
    public void OnFlyoutShown() { _detailsVisible = true; _flyout?.SetActive(true); UpdateActivity(); _ = InitializeDataAsync(); }
    public void OnFlyoutHidden() { _detailsVisible = false; _flyout?.SetActive(false); UpdateActivity(); }
    public void OnTileShown() => OnFlyoutShown();
    public void OnTileHidden() => OnFlyoutHidden();
    public override async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (Context is not null) Context.PreviewVisibilityChanged -= OnVisibilityChanged;
        _flyout?.Dispose();
        if (_monitor is not null)
        {
            _monitor.Changed -= OnChanged;
            await _monitor.LeaveAsync(this);
        }
    }
}
