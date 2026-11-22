using System.Diagnostics;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>Combines protocol readings with the Windows device inventory.</summary>
public sealed class ConnectedBatteryProvider : IBatteryProvider
{
    private readonly WindowsBatteryProvider _windows = new();
    private readonly UsbHidBatteryProvider _usb = new();
    private readonly DeviceProtocolProvider _protocol = new();
    public string? WarningKey { get; private set; }
    public IReadOnlyDictionary<string, string> IdentityAliases { get; private set; } = new Dictionary<string, string>();
    public event EventHandler? DevicesChanged;
    public ConnectedBatteryProvider()
    {
        _windows.DevicesChanged += OnChanged;
        _usb.DevicesChanged += OnChanged;
    }
    private void OnChanged(object? sender, EventArgs args) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    public async Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken token)
    {
        WarningKey = null;
        // Only one provider sends HID battery commands: never query a receiver
        // through generic HID while the upstream vendor driver is using it.
        var windowsTask = CaptureAsync(_windows, token);
        var protocol = await CaptureAsync(_protocol, token);
        _usb.ReadStandardReports = _protocol.LastError is not null;
        var usb = await CaptureAsync(_usb, token);
        var windows = await windowsTask;
        var bound = ProtocolIdentityBindings.Apply(protocol, windows, _usb.DeviceIdentities, out var aliases);
        IdentityAliases = aliases;
        return BatterySnapshotMerger.Merge(windows, bound, usb);
    }
    private async Task<IReadOnlyList<BatteryDevice>> CaptureAsync(IBatteryProvider provider, CancellationToken token)
    {
        try { return await provider.ReadAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            WarningKey = provider is DeviceProtocolProvider ? "ProtocolUnavailable" : WarningKey ?? "PartialData";
            Trace.WriteLine($"Battery provider {provider.GetType().Name}: {ex}");
            return [];
        }
    }
    public void Dispose()
    {
        _windows.DevicesChanged -= OnChanged;
        _usb.DevicesChanged -= OnChanged;
        _windows.Dispose(); _usb.Dispose(); _protocol.Dispose();
    }
}
