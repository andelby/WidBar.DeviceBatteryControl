using System.Globalization;
using System.Xml.Linq;

namespace DeviceBatteryControl.Localization;

/// <summary>Loads English fallback and only the selected RESW locale once.</summary>
internal static class BatteryLoc
{
    private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
    public static CultureInfo FormattingCulture => CultureInfo.CurrentCulture;

    public static async Task InitializeAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Strings");
        await LoadAsync(Path.Combine(root, "en-US", "Resources.resw"));
        if (!Directory.Exists(root)) return;
        var desired = CultureInfo.CurrentUICulture;
        var folders = Directory.GetDirectories(root).Select(Path.GetFileName).OfType<string>().ToArray();
        var language = folders.FirstOrDefault(x => x.Equals(desired.Name, StringComparison.OrdinalIgnoreCase))
            ?? folders.FirstOrDefault(x => x.StartsWith(desired.TwoLetterISOLanguageName + "-", StringComparison.OrdinalIgnoreCase));
        if (language is not null && language != "en-US")
            await LoadAsync(Path.Combine(root, language, "Resources.resw"));
    }

    private static async Task LoadAsync(string path)
    {
        if (!File.Exists(path)) return;
        await using var stream = File.OpenRead(path);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
        foreach (var item in document.Root!.Elements("data"))
            Strings[(string)item.Attribute("name")!] = (string?)item.Element("value") ?? "";
    }

    public static string Get(string key) => Strings.GetValueOrDefault(key, key);
    public static string Format(string key, params object[] values) => string.Format(FormattingCulture, Get(key), values);
}
