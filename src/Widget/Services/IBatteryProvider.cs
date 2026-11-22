using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

public interface IBatteryProvider : IDisposable
{
    event EventHandler? DevicesChanged;
    Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken cancellationToken);
}
