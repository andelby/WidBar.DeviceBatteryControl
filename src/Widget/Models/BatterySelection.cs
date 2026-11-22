namespace DeviceBatteryControl.Models;

public static class BatterySelection
{
    public static IReadOnlyList<BatteryDevice> Preview(IEnumerable<BatteryDevice> devices, BatterySettings settings) => devices
        .Where(x => x.IsConnected && !x.IsReceiverOnly)
        .Where(x => settings.SelectedDeviceIds is null || settings.SelectedDeviceIds.Contains(DeviceIdentity.Normalize(x.Id), StringComparer.OrdinalIgnoreCase))
        .OrderByDescending(x => settings.PinnedDeviceId is not null
            && DeviceIdentity.Normalize(x.Id) == DeviceIdentity.Normalize(settings.PinnedDeviceId))
        .ThenBy(x => x.Percentage ?? 101)
        .Take(settings.SelectedDeviceIds is null ? settings.PreviewDeviceCount : 10).ToArray();

    public static bool IsVisible(IEnumerable<BatteryDevice> devices, BatterySettings settings) =>
        settings.ShowWhenEmpty || Preview(devices, settings).Count > 0;
}
