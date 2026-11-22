namespace DeviceBatteryControl.Models;

public sealed record HidDeviceIdentity(ushort VendorId, ushort ProductId, string ContainerId, string Transport);

public static class ProtocolIdentityBindings
{
    public static IReadOnlyList<BatteryDevice> Apply(IEnumerable<BatteryDevice> readings,
        IEnumerable<BatteryDevice> inventory, IEnumerable<HidDeviceIdentity> hid, out Dictionary<string, string> aliases)
    {
        var nativeIds = inventory.Select(x => DeviceIdentity.Normalize(x.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identities = hid.ToArray();
        aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (var group in identities.GroupBy(x => (x.VendorId, x.ProductId)))
        {
            var physical = group.DistinctBy(x => DeviceIdentity.Normalize(x.ContainerId)).ToArray();
            if (physical.Length != 1 || physical[0].Transport != "Bluetooth") continue;
            var id = DeviceIdentity.Normalize(physical[0].ContainerId);
            if (nativeIds.Contains(id)) aliases[$"hid:{group.Key.VendorId:x4}:{group.Key.ProductId:x4}"] = id;
        }
        var result = new List<BatteryDevice>();
        foreach (var reading in readings)
        {
            var bound = reading;
            if (reading.UsbVendorId.HasValue && reading.UsbProductId.HasValue
                && (reading.Id.StartsWith("hid:", StringComparison.OrdinalIgnoreCase)
                    || reading.Transport?.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) == true))
            {
                var candidates = identities.Where(x => x.VendorId == reading.UsbVendorId && x.ProductId == reading.UsbProductId)
                    .DistinctBy(x => DeviceIdentity.Normalize(x.ContainerId)).ToArray();
                // Never match by name or guess between two identical keyboards/controllers.
                if (candidates.Length == 1 && candidates[0].Transport == "Bluetooth")
                {
                    var id = DeviceIdentity.Normalize(candidates[0].ContainerId);
                    if (nativeIds.Contains(id))
                    {
                        aliases[reading.Id] = id;
                        bound = reading with { Id = id, Transport = "Bluetooth HID" };
                    }
                }
            }
            result.Add(bound);
        }
        return result;
    }
}
