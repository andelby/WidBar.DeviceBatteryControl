using System.Text.Json;

namespace DeviceBatteryControl.Models;

public sealed record BatterySettings
{
    public string? PinnedDeviceId { get; init; }
    public int PreviewDeviceCount { get; init; } = 2;
    public int RefreshSeconds { get; init; } = 120;
    public int LowBatteryThreshold { get; init; } = 20;
    public bool ShowDisconnected { get; init; } = true;
    public bool KeepHistory { get; init; } = true;
    public int PreviewWidth => 24 + 76 * PreviewDeviceCount;

    public static BatterySettings FromJson(string? json)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<BatterySettings>(json ?? "{}") ?? new();
            return settings with
            {
                PreviewDeviceCount = Math.Clamp(settings.PreviewDeviceCount, 1, 3),
                RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 60, 900),
                LowBatteryThreshold = Math.Clamp(settings.LowBatteryThreshold, 5, 50)
            };
        }
        catch (JsonException) { return new(); }
    }
    public string ToJson() => JsonSerializer.Serialize(this);
}
