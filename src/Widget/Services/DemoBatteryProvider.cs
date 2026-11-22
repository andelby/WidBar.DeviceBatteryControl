using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>Explicit --demo preview only. Never selected by the actual plugin.</summary>
internal sealed class DemoBatteryProvider : IBatteryProvider
{
    public event EventHandler? DevicesChanged { add { } remove { } }
    public Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<BatteryDevice> devices = [
            new("demo-mouse", "Surface Precision Mouse", DeviceKind.Mouse, true, 76, BatterySource.Windows, now),
            new("demo-headphones", "Surface Headphones", DeviceKind.Headphones, true, 18, BatterySource.BluetoothLE, now),
            new("demo-keyboard", "Designer Compact Keyboard", DeviceKind.Keyboard, true, 92, BatterySource.Windows, now),
            new("demo-controller", "Xbox Wireless Controller", DeviceKind.Controller, false, 54, BatterySource.Windows, now.AddHours(-1)),
            new("demo-unknown", "Wireless Speaker", DeviceKind.Other, true, null, BatterySource.Windows, null)
        ];
        return Task.FromResult(devices);
    }
    public void Dispose() { }
}
