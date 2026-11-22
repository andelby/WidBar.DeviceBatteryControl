namespace DeviceBatteryControl.Models;

public static class BatterySnapshotMerger
{
    public static IReadOnlyList<BatteryDevice> Merge(IEnumerable<BatteryDevice> windows,
        IEnumerable<BatteryDevice> protocol, IEnumerable<BatteryDevice> usb)
    {
        var result = windows.GroupBy(x => DeviceIdentity.Normalize(x.Id), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(d => d.IsConnected).ThenByDescending(d => d.LastReading).First() with { Id = x.Key }, StringComparer.OrdinalIgnoreCase);
        var protocolDevices = protocol.ToArray();
        foreach (var reading in protocolDevices)
        {
            var id = DeviceIdentity.Normalize(reading.Id);
            if (result.TryGetValue(id, out var inventory))
            {
                // Carry percentage, timestamp and provenance together. A valid
                // connection-only protocol response must not refresh an older
                // Windows value or relabel it as a fresh protocol observation.
                var observation = reading.Percentage.HasValue && !reading.IsReadingStale ? reading
                    : inventory.Percentage.HasValue && !inventory.IsReadingStale ? inventory
                    : new[] { reading, inventory }.Where(x => x.Percentage.HasValue)
                        .OrderByDescending(x => x.LastReading).FirstOrDefault();
                result[id] = reading with
                {
                    Id = id, Name = inventory.Name, Kind = reading.Kind == DeviceKind.Other ? inventory.Kind : reading.Kind,
                    IsConnected = inventory.IsConnected || reading.IsConnected,
                    Percentage = observation?.Percentage,
                    LastReading = observation?.LastReading,
                    IsReadingStale = observation?.IsReadingStale ?? reading.IsReadingStale,
                    Source = observation?.Source ?? reading.Source,
                    Transport = observation?.Transport ?? reading.Transport,
                    BluetoothId = inventory.BluetoothId
                };
            }
            else result[id] = reading with { Id = id };
        }
        foreach (var device in usb)
        {
            if (device.IsReceiverOnly && device.UsbVendorId.HasValue && protocolDevices.Any(x =>
                x.UsbVendorId == device.UsbVendorId && x.UsbProductId == device.UsbProductId)) continue;
            result.TryAdd(device.Id, device);
        }
        return result.Values.OrderByDescending(x => x.IsConnected).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
