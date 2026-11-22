using System.Text.Json;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

public sealed class BatteryHistoryStore(string directory)
{
    private readonly string _path = Path.Combine(directory, "battery-history.json");

    public async Task<BatteryHistory> LoadAsync(DateTimeOffset now)
    {
        if (!File.Exists(_path)) return new();
        if (new FileInfo(_path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Battery history exceeds the storage limit.");
        await using var stream = File.OpenRead(_path);
        var history = await JsonSerializer.DeserializeAsync<BatteryHistory>(stream) ?? new();
        history.Normalize(now);
        return history;
    }

    public async Task SaveAsync(BatteryHistory history)
    {
        Directory.CreateDirectory(directory);
        await using (var stream = new FileStream(_path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, true))
            await JsonSerializer.SerializeAsync(stream, history);
        File.Move(_path + ".tmp", _path, true);
    }
}
