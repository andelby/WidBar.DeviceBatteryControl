namespace DeviceBatteryControl.Models;

public static class BatterySelection
{
    public static IReadOnlyList<BatteryDevice> Preview(IEnumerable<BatteryDevice> devices, BatterySettings settings) => devices
        .Where(x => x.IsConnected && !x.IsReceiverOnly)
        .OrderByDescending(x => settings.PinnedDeviceId is not null
            && DeviceIdentity.Normalize(x.Id) == DeviceIdentity.Normalize(settings.PinnedDeviceId))
        .ThenBy(x => x.Percentage ?? 101)
        .Take(settings.PreviewDeviceCount).ToArray();
}
