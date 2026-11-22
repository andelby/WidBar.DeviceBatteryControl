using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>Windows device properties first; standard BLE Battery Service as fallback.
/// No pairing, advertisement scan, persistent GATT connection or vendor HID commands.</summary>
public sealed class WindowsBatteryProvider : IBatteryProvider
{
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string ConnectedKey = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    private const string ContainerKey = "System.Devices.ContainerId";
    private const string AepContainerKey = "System.Devices.Aep.ContainerId";
    private const string Connected = "System.Devices.Aep.IsConnected";
    private static readonly string[] AepProperties = [AepContainerKey, Connected, "System.Devices.Aep.Category", "System.Devices.Aep.DeviceAddress"];
    private readonly List<DeviceWatcher> _watchers = [];
    private readonly Dictionary<string, DateTimeOffset> _bleRetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BatteryDevice> _last = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    public event EventHandler? DevicesChanged;

    public WindowsBatteryProvider()
    {
        AddWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true), AepProperties, DeviceInformationKind.AssociationEndpoint);
        AddWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), AepProperties, DeviceInformationKind.AssociationEndpoint);
        AddWatcher("System.Devices.DeviceInstanceId:~<\"BTH\"", [BatteryKey, ConnectedKey, ContainerKey], DeviceInformationKind.Device);
    }

    private void AddWatcher(string selector, string[] properties, DeviceInformationKind kind)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, properties, kind);
        watcher.Added += OnAdded;
        watcher.Updated += OnUpdated;
        watcher.Removed += OnRemoved;
        _watchers.Add(watcher);
        watcher.Start();
    }
    private void OnAdded(DeviceWatcher sender, DeviceInformation args) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate args) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public async Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshots = await Task.Run(() => WindowsBluetoothSnapshotReader.Read(cancellationToken), cancellationToken);
        var byId = snapshots.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var byAddress = snapshots.Where(x => x.Address is not null)
            .GroupBy(x => x.Address!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, BatteryDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
        {
            _last.TryGetValue(snapshot.Id, out var previous);
            result[snapshot.Id] = new(snapshot.Id, snapshot.Name, snapshot.Kind, snapshot.Connected,
                snapshot.Percentage ?? previous?.Percentage, BatterySource.Windows,
                snapshot.Percentage.HasValue ? now : previous?.LastReading,
                IsReadingStale: !snapshot.Percentage.HasValue && previous?.Percentage is not null);
        }

        // PnP inventory remains usable even if WinRT discovery/access fails.
        DeviceInformationCollection? classic = null, ble = null;
        try
        {
            classic = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                AepProperties, DeviceInformationKind.AssociationEndpoint).AsTask(cancellationToken);
            ble = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                AepProperties, DeviceInformationKind.AssociationEndpoint).AsTask(cancellationToken);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Trace.WriteLine($"Bluetooth WinRT discovery: {ex.Message}"); }

        var bleIds = ble?.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var probes = 0;
        foreach (var info in (classic?.AsEnumerable() ?? []).Concat(ble?.AsEnumerable() ?? []))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(info.Name)) continue;
            var container = Property(info, AepContainerKey)?.ToString();
            var address = DeviceIdentity.Address(Property(info, "System.Devices.Aep.DeviceAddress")?.ToString());
            BluetoothSnapshot? snapshot = null;
            if (!string.IsNullOrWhiteSpace(container)) byId.TryGetValue(DeviceIdentity.Normalize(container), out snapshot);
            if (snapshot is null && address is not null) byAddress.TryGetValue(address, out snapshot);
            var id = snapshot?.Id ?? (!string.IsNullOrWhiteSpace(container) && Guid.TryParse(container, out var guid) && guid != Guid.Empty
                ? guid.ToString("D") : address ?? info.Id);
            result.TryGetValue(id, out var existing);
            _last.TryGetValue(id, out var previous);
            var connected = snapshot?.Connected ?? Property(info, Connected) is true;
            var level = snapshot?.Percentage;
            var source = BatterySource.Windows;
            var isBle = bleIds.Contains(info.Id);
            if (connected && level is null && isBle && probes < 4 &&
                (!_bleRetry.TryGetValue(info.Id, out var retry) || now >= retry))
            {
                probes++;
                level = await ReadBleAsync(info.Id, cancellationToken);
                _bleRetry[info.Id] = now.AddMinutes(level.HasValue ? 1 : 15);
                if (level.HasValue) source = BatterySource.BluetoothLE;
            }
            var cached = existing ?? previous;
            var device = new BatteryDevice(id, snapshot?.Name ?? info.Name,
                snapshot?.Kind ?? BatteryDevice.Classify(info.Name), connected,
                level ?? cached?.Percentage, level.HasValue ? source : cached?.Source ?? source,
                level.HasValue ? now : cached?.LastReading, isBle ? info.Id : cached?.BluetoothId,
                IsReadingStale: !level.HasValue && cached?.Percentage is not null);
            if (existing is not null && existing.LastReading == now && !level.HasValue)
                device = existing with { BluetoothId = isBle ? info.Id : existing.BluetoothId };
            result[id] = device;
        }
        _last.Clear();
        foreach (var (id, device) in result) _last[id] = device;
        foreach (var id in _bleRetry.Keys.Where(id => !bleIds.Contains(id)).ToArray()) _bleRetry.Remove(id);
        return result.Values.OrderByDescending(x => x.IsConnected).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static object? Property(DeviceInformation info, string key) => info.Properties.TryGetValue(key, out var value) ? value : null;

    private static async Task<int?> ReadBleAsync(string id, CancellationToken token)
    {
        try
        {
            // This method starts on the UI thread: FromIdAsync may request consent.
            using var device = await BluetoothLEDevice.FromIdAsync(id).AsTask(token);
            if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected) return null;
            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached).AsTask(token);
            try
            {
                if (services.Status != GattCommunicationStatus.Success) return null;
                foreach (var service in services.Services)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Cached).AsTask(token);
                    if (characteristics.Status != GattCommunicationStatus.Success) continue;
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read)) continue;
                        var value = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(token);
                        if (value.Status != GattCommunicationStatus.Success || value.Value.Length < 1) continue;
                        using var reader = DataReader.FromBuffer(value.Value);
                        return BatteryDevice.ValidatePercentage(reader.ReadByte());
                    }
                }
            }
            finally { foreach (var service in services.Services) service.Dispose(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Trace.WriteLine($"Battery BLE: {ex.Message}"); }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var watcher in _watchers)
        {
            watcher.Added -= OnAdded;
            watcher.Updated -= OnUpdated;
            watcher.Removed -= OnRemoved;
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
        }
        _watchers.Clear();
    }
}
