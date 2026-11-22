using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DeviceBatteryControl;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Normal activation uses MSIX identity. The explicit preview is also
        // available unpackaged, with live data unless --demo was requested.
        uint packageNameLength = 0;
        if (args.Contains("--preview") && GetCurrentPackageFullName(ref packageNameLength, IntPtr.Zero) == 15700)
            Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00020003);
        var languageIndex = Array.IndexOf(args, "--lang");
        if (languageIndex >= 0 && languageIndex + 1 < args.Length)
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(args[languageIndex + 1]);
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
