namespace DeviceBatteryControl.Models;

public static class DeviceIdentity
{
    public static string Normalize(string value)
    {
        var text = value.Trim();
        var candidate = text.StartsWith("bt:", StringComparison.OrdinalIgnoreCase) ? text[3..] : text;
        if (Guid.TryParse(candidate, out var id)) return id.ToString("D");
        return text.ToLowerInvariant();
    }

    public static string? Address(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace(":", "").Replace("-", "").Trim();
        return text.Length == 12 && text.All(Uri.IsHexDigit) ? text.ToUpperInvariant() : null;
    }
}
