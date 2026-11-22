using Windows.Devices.Enumeration;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

public sealed class UsbHidBatteryProvider : IBatteryProvider
{
    private readonly DeviceWatcher _watcher;
    public bool ReadStandardReports { get; set; } = true;
    public IReadOnlyList<HidDeviceIdentity> DeviceIdentities { get; private set; } = [];
    public event EventHandler? DevicesChanged;
    public UsbHidBatteryProvider()
    {
        _watcher = DeviceInformation.CreateWatcher("System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\"");
        _watcher.Added += OnAdded;
        _watcher.Removed += OnRemoved;
        _watcher.Start();
    }
    private void OnAdded(DeviceWatcher sender, DeviceInformation args) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    public Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken token) => Task.Run<IReadOnlyList<BatteryDevice>>(() =>
    {
        var collections = UsbHidReader.Scan(token, readBattery: ReadStandardReports, includeBluetooth: true);
        DeviceIdentities = collections.Select(x => new HidDeviceIdentity(x.VendorId, x.ProductId, x.ContainerId, x.Transport))
            .Distinct().ToArray();
        var devices = new List<BatteryDevice>();
        foreach (var group in collections.Where(x => x.Transport == "USB").GroupBy(x => x.ContainerId, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Any(x => x.Digitizer)) continue;
            var reading = group.FirstOrDefault(x => x.Percentage.HasValue);
            if (reading is not null)
            {
                var kind = group.Any(x => x.UsagePage == 1 && x.Usage == 2) ? DeviceKind.Mouse
                    : group.Any(x => x.UsagePage == 1 && x.Usage == 6) ? DeviceKind.Keyboard : BatteryDevice.Classify(reading.Product);
                devices.Add(new("usb:" + group.Key, Name(reading), kind, true, reading.Percentage, BatterySource.UsbHid, DateTimeOffset.UtcNow,
                    UsbVendorId: reading.VendorId, UsbProductId: reading.ProductId));
            }
            else if (group.FirstOrDefault(x => IsReceiver(x.Product)) is { } receiver)
            {
                // A plugged-in dongle is not evidence of a live wireless device.
                devices.Add(new("usb:" + group.Key, Name(receiver), DeviceKind.Other, false, null,
                    BatterySource.UsbHid, null, IsReceiverOnly: true, UsbVendorId: receiver.VendorId, UsbProductId: receiver.ProductId));
            }
        }
        return devices;
    }, token);
    private static string Name(UsbHidCollection device) => string.IsNullOrWhiteSpace(device.Product)
        ? $"USB HID {device.VendorId:X4}:{device.ProductId:X4}" : device.Product;
    private static bool IsReceiver(string product) => new[] { "receiver", "dongle", "2.4g" }
        .Any(word => product.Contains(word, StringComparison.OrdinalIgnoreCase));
    public void Dispose()
    {
        _watcher.Added -= OnAdded;
        _watcher.Removed -= OnRemoved;
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) _watcher.Stop();
    }
}
