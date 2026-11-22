using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

public sealed record UsbHidCollection(string ContainerId, ushort VendorId, ushort ProductId,
    string Product, string Manufacturer, ushort UsagePage, ushort Usage,
    int BatteryFields, int? Percentage, bool Digitizer, string Path, string Transport = "USB");

/// <summary>Native metadata discovery and standard HID battery reports only.
/// No SetFeature, output reports, ReadFile, pairing or vendor command guessing.</summary>
public static class UsbHidReader
{
    private const int Success = 0x00110000;
    private static DevPropKey ContainerKey = new() { Format = new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), Id = 2 };

    public static IReadOnlyList<UsbHidCollection> Scan(CancellationToken token, bool readBattery = true, bool includeBluetooth = false)
    {
        HidD_GetHidGuid(out var hidGuid);
        using var devices = SetupDiGetClassDevsW(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (devices.IsInvalid) return [];
        var candidates = new List<UsbHidCollection>();
        for (uint index = 0; ; index++)
        {
            token.ThrowIfCancellationRequested();
            var data = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
            if (!SetupDiEnumDeviceInterfaces(devices, IntPtr.Zero, ref hidGuid, index, ref data)) break;
            var info = new DeviceInfo { Size = Marshal.SizeOf<DeviceInfo>() };
            SetupDiGetDeviceInterfaceDetailW(devices, ref data, IntPtr.Zero, 0, out var needed, ref info);
            if (needed < 8 || needed > 65536) continue;
            var detail = Marshal.AllocHGlobal((int)needed);
            try
            {
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetailW(devices, ref data, detail, needed, out _, ref info)) continue;
                var transport = GetTransport(info.Instance);
                if (transport is null || (!includeBluetooth && transport != "USB")) continue;
                var path = Marshal.PtrToStringUni(detail + 4);
                if (path is null) continue;
                using var handle = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle.IsInvalid) continue;
                var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
                if (!HidD_GetAttributes(handle, ref attributes) || !HidD_GetPreparsedData(handle, out var preparsed)) continue;
                try
                {
                    if (HidP_GetCaps(preparsed, out var caps) != Success) continue;
                    var container = new byte[16];
                    var id = SetupDiGetDevicePropertyW(devices, ref info, ref ContainerKey, out var propertyType, container, 16, out _, 0)
                        && propertyType == 13 ? new Guid(container).ToString() : UsbIdentity(path);
                    var fields = Fields(preparsed, caps);
                    candidates.Add(new(id, attributes.Vendor, attributes.Product,
                        StringProperty(handle, true), StringProperty(handle, false), caps.UsagePage, caps.Usage,
                        fields.Count, null, caps.UsagePage == 0x0d, path, transport));
                }
                finally { HidD_FreePreparsedData(preparsed); }
            }
            finally { Marshal.FreeHGlobal(detail); }
        }

        if (!readBattery) return candidates;
        var excluded = candidates.Where(x => x.Digitizer).Select(x => x.ContainerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<UsbHidCollection>(candidates.Count);
        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            int? percentage = null;
            if (candidate.Transport == "USB" && !excluded.Contains(candidate.ContainerId) && candidate.BatteryFields > 0 && candidate.UsagePage is not (0x84 or 0x85))
                percentage = ReadBattery(candidate.Path, candidate.VendorId, token);
            result.Add(candidate with { Percentage = percentage });
        }
        return result;
    }

    private static int? ReadBattery(string path, ushort vendorId, CancellationToken token)
    {
        using var handle = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid || !HidD_GetPreparsedData(handle, out var preparsed)) return null;
        try
        {
            if (HidP_GetCaps(preparsed, out var caps) != Success) return null;
            // Cancel pending driver I/O on shutdown; no foreground/UI work waits here.
            using var cancellation = token.Register(() => CancelIoEx(handle, IntPtr.Zero));
            foreach (var (type, field, usage) in Fields(preparsed, caps).Take(4))
            {
                token.ThrowIfCancellationRequested();
                var length = type == 2 ? caps.FeatureLength : caps.InputLength;
                if (length is < 1 or > 4096) continue;
                var report = new byte[length];
                report[0] = field.ReportId;
                var read = type == 2 ? HidD_GetFeature(handle, report, report.Length) : HidD_GetInputReport(handle, report, report.Length);
                if (read && HidP_GetUsageValue(type, field.UsagePage, field.LinkCollection, usage, out var value, preparsed, report, report.Length) == Success)
                {
                    var percentage = HidBatteryValue.ToPercentage(value, field.LogicalMinimum, field.LogicalMaximum, vendorId);
                    if (percentage.HasValue) return percentage;
                }
            }
        }
        finally { HidD_FreePreparsedData(preparsed); }
        return null;
    }

    private static List<(int Type, ValueCaps Field, ushort Usage)> Fields(IntPtr preparsed, HidCaps caps)
    {
        var result = new List<(int, ValueCaps, ushort)>();
        foreach (var type in new[] { 2, 0 })
        {
            var count = type == 2 ? caps.FeatureValueCount : caps.InputValueCount;
            if (count is 0 or > 512) continue;
            var fields = new ValueCaps[count];
            if (HidP_GetValueCaps(type, fields, ref count, preparsed) != Success) continue;
            for (var i = 0; i < count; i++)
            {
                var field = fields[i];
                var usage = HidBatteryValue.BatteryUsage(field.UsagePage, field.UsageMinimum,
                    field.IsRange == 0 ? field.UsageMinimum : field.UsageMaximum);
                if (usage.HasValue && field.ReportCount == 1 && field.LogicalMinimum >= 0 && field.LogicalMaximum > 1)
                    result.Add((type, field, usage.Value));
            }
        }
        return result;
    }
    private static string? GetTransport(uint instance)
    {
        for (var depth = 0; depth < 8; depth++)
        {
            var name = new StringBuilder(512);
            if (CM_Get_Device_IDW(instance, name, 512, 0) != 0) return null;
            var id = name.ToString();
            if (id.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase)) return "USB";
            if (id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) return "Bluetooth";
            if (CM_Get_Parent(out var parent, instance, 0) != 0) return null;
            instance = parent;
        }
        return null;
    }
    private static string UsbIdentity(string path) => path.ToLowerInvariant().Split("&mi_")[0];
    private static string StringProperty(SafeFileHandle handle, bool product)
    {
        var buffer = new byte[512];
        var success = product ? HidD_GetProductString(handle, buffer, buffer.Length) : HidD_GetManufacturerString(handle, buffer, buffer.Length);
        return success ? Encoding.Unicode.GetString(buffer).TrimEnd('\0') : "";
    }

    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData { public int Size; public Guid Class; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInfo { public int Size; public Guid Class; public uint Instance; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct DevPropKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] private struct HidAttributes { public int Size; public ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Explicit, Size = 64)] private struct HidCaps
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort UsagePage;
        [FieldOffset(4)] public ushort InputLength;
        [FieldOffset(8)] public ushort FeatureLength;
        [FieldOffset(48)] public ushort InputValueCount;
        [FieldOffset(60)] public ushort FeatureValueCount;
    }
    [StructLayout(LayoutKind.Explicit, Size = 72)] private struct ValueCaps
    {
        [FieldOffset(0)] public ushort UsagePage;
        [FieldOffset(2)] public byte ReportId;
        [FieldOffset(6)] public ushort LinkCollection;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(20)] public ushort ReportCount;
        [FieldOffset(40)] public int LogicalMinimum;
        [FieldOffset(44)] public int LogicalMaximum;
        [FieldOffset(56)] public ushort UsageMinimum;
        [FieldOffset(58)] public ushort UsageMaximum;
    }
    private sealed class DeviceInfoHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DeviceInfoHandle() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }
    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);
    [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int type, [Out] ValueCaps[] caps, ref ushort length, IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int type, ushort page, ushort collection, ushort usage, out uint value, IntPtr data, byte[] report, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetFeature(SafeFileHandle handle, [In, Out] byte[] report, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetInputReport(SafeFileHandle handle, [In, Out] byte[] report, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetProductString(SafeFileHandle handle, [Out] byte[] buffer, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetManufacturerString(SafeFileHandle handle, [Out] byte[] buffer, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll")] private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern DeviceInfoHandle SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr handle);
    [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInterfaces(DeviceInfoHandle handle, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetailW(DeviceInfoHandle handle, ref InterfaceData data, IntPtr detail, uint size, out uint required, ref DeviceInfo device);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDevicePropertyW(DeviceInfoHandle handle, ref DeviceInfo device, ref DevPropKey key, out uint type, [Out] byte[] buffer, uint size, out uint required, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_Device_IDW(uint instance, StringBuilder buffer, int length, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern int CM_Get_Parent(out uint parent, uint instance, uint flags);
}
