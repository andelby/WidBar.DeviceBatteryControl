using Microsoft.UI.Xaml;
using WidBar.SDK;
using WidBar.SDK.Hosting;
using DeviceBatteryControl.Localization;

namespace DeviceBatteryControl;

public partial class App : WidgetHostApplication
{
    private Shell.PreviewWindow? _preview;
    public App()
    {
        UnhandledException += (_, args) => WriteStartupError(args.Exception);
        try { InitializeComponent(); }
        catch (Exception ex) { WriteStartupError(ex); throw; }
    }

    private static void WriteStartupError(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WidBar.DeviceBatteryControl");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "last-error.log"), exception.ToString());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await BatteryLoc.InitializeAsync();
        if (Environment.GetCommandLineArgs().Contains("--preview"))
        {
            _preview = new Shell.PreviewWindow();
            _preview.Activate();
            return;
        }
        base.OnLaunched(args);
    }

    protected override IWidgetPlugin CreatePlugin() => new Hosting.DeviceBatteryPlugin();
}
