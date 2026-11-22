using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>Read the same Windows PnP properties as Settings, including classic
/// Bluetooth parents and battery values stored on child service nodes.</summary>
public static class WindowsBluetoothSnapshotReader
{
    private static readonly Key Container = new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c", 2);
    private static readonly Key FriendlyName = new("a45c254e-df1c-4efd-8020-67d146a850e0", 14);
    private static readonly Key Description = new("a45c254e-df1c-4efd-8020-67d146a850e0", 2);
    private static readonly Key Battery = new("104ea319-6ee2-4701-bd47-8ddbf425bbe5", 2);
    private static readonly Key Connected = new("83da6326-97a6-4088-9453-a1923f573b29", 15);
    private static readonly Key ClassOfDevice = new("2bd67d8b-8beb-48d5-87e0-6cda3428040a", 10);

    public static IReadOnlyList<BluetoothNode> ReadNodes(CancellationToken token)
    {
        using var set = SetupDiGetClassDevsW(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 6);
        if (set.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var nodes = new List<BluetoothNode>();
        for (uint index = 0; ; index++)
        {
            token.ThrowIfCancellationRequested();
            var info = new Info { Size = Marshal.SizeOf<Info>() };
            if (!SetupDiEnumDeviceInfo(set, index, ref info)) break;
            var buffer = new StringBuilder(1024);
            if (!SetupDiGetDeviceInstanceIdW(set, ref info, buffer, buffer.Capacity, out _)) continue;
            var instance = buffer.ToString();
            if (!instance.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase)
                && !instance.StartsWith("BTHLE\\", StringComparison.OrdinalIgnoreCase)
                && !instance.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase)) continue;
            var container = Read(set, ref info, Container, out var type);
            var id = type == 13 && container.Length >= 16 ? new Guid(container.AsSpan(0, 16)).ToString("D") : instance;
            var name = Text(set, ref info, FriendlyName);
            if (name.Length == 0) name = Text(set, ref info, Description);
            var connected = Read(set, ref info, Connected, out type);
            bool? live = type == 17 && connected.Length > 0 ? connected[0] != 0 : null;
            var battery = Read(set, ref info, Battery, out type);
            var percentage = type == 3 && battery.Length > 0 ? BatteryDevice.ValidatePercentage(battery[0]) : null;
            if (CM_Get_DevNode_Status(out var status, out _, info.Instance, 0) == 0 && (status & 0x400) != 0)
            { live = false; percentage = null; }
            var classification = Read(set, ref info, ClassOfDevice, out type);
            uint? cod = type == 7 && classification.Length >= 4 ? BitConverter.ToUInt32(classification) : null;
            var match = Regex.Match(instance, @"DEV_([0-9A-Fa-f]{12})");
            nodes.Add(new(instance, id, name, live, percentage, cod, match.Success ? match.Groups[1].Value.ToUpperInvariant() : null));
        }
        return nodes;
    }

    public static IReadOnlyList<BluetoothSnapshot> Read(CancellationToken token) => BluetoothSnapshotResolver.Resolve(ReadNodes(token));
    private static string Text(Set set, ref Info info, Key key)
    {
        var data = Read(set, ref info, key, out var type);
        return type == 18 ? Encoding.Unicode.GetString(data).TrimEnd('\0') : "";
    }
    private static byte[] Read(Set set, ref Info info, Key key, out uint type)
    {
        var data = new byte[2048];
        if (!SetupDiGetDevicePropertyW(set, ref info, ref key, out type, data, data.Length, out var length, 0))
        { type = 0; return []; }
        return data.AsSpan(0, Math.Min(data.Length, length)).ToArray();
    }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public int Size; public Guid Class; public uint Instance; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct Key
    {
        public Guid Format; public uint Id;
        public Key(string format, uint id) { Format = new(format); Id = id; }
    }
    private sealed class Set : SafeHandleZeroOrMinusOneIsInvalid
    {
        public Set() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern Set SetupDiGetClassDevsW(IntPtr guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr handle);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInfo(Set set, uint index, ref Info info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInstanceIdW(Set set, ref Info info, StringBuilder id, int size, out int required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDevicePropertyW(Set set, ref Info info, ref Key key, out uint type, [Out] byte[] data, int size, out int required, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern int CM_Get_DevNode_Status(out uint status, out uint problem, uint instance, uint flags);
}
