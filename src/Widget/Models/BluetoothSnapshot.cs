namespace DeviceBatteryControl.Models;

public sealed record BluetoothNode(string InstanceId, string ContainerId, string Name,
    bool? Connected, int? Percentage, uint? ClassOfDevice = null, string? Address = null)
{
    public bool IsMain => InstanceId.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase)
        || InstanceId.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase);
}

public sealed record BluetoothSnapshot(string Id, string Name, bool Connected, int? Percentage,
    DeviceKind Kind, string? Address);

public static class BluetoothSnapshotResolver
{
    public static IReadOnlyList<BluetoothSnapshot> Resolve(IEnumerable<BluetoothNode> nodes) => nodes
        .GroupBy(x => DeviceIdentity.Normalize(x.ContainerId), StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            var mains = group.Where(x => x.IsMain).ToArray();
            var states = mains.Where(x => x.Connected.HasValue).ToArray();
            // A Hands-Free/GATT service can say false while its main device is
            // connected. Classic and LE parents sharing a container are ORed.
            var connected = states.Length > 0 ? states.Any(x => x.Connected == true) : group.Any(x => x.Connected == true);
            var anchor = mains.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name))
                ?? group.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name)) ?? group.First();
            var reading = connected ? group.Where(x => x.Percentage is >= 0 and <= 100)
                .OrderByDescending(x => x.Connected == true).FirstOrDefault()?.Percentage : null;
            var cod = mains.FirstOrDefault(x => x.ClassOfDevice.HasValue)?.ClassOfDevice;
            var kind = cod.HasValue ? Classify(cod.Value) : DeviceKind.Other;
            if (kind == DeviceKind.Other) kind = BatteryDevice.Classify(anchor.Name);
            return new BluetoothSnapshot(group.Key, anchor.Name, connected, reading, kind,
                mains.Select(x => x.Address).FirstOrDefault(x => x is not null));
        }).ToArray();

    private static DeviceKind Classify(uint cod) => ((cod >> 8) & 0x1f) switch
    {
        2 => DeviceKind.Phone,
        4 => DeviceKind.Headphones,
        5 when (cod & 0x40) != 0 => DeviceKind.Keyboard,
        5 when (cod & 0x80) != 0 => DeviceKind.Mouse,
        5 when ((cod >> 2) & 0xf) is 1 or 2 => DeviceKind.Controller,
        _ => DeviceKind.Other
    };
}
