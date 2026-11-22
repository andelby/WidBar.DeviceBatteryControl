namespace DeviceBatteryControl.Models;

public sealed record BatterySample(DateTimeOffset Time, int Percentage, BatterySource? Source = null, string? Transport = null);

/// <summary>Bounded, real observations. Gaps and reconnections remain gaps.</summary>
public sealed class BatteryHistory
{
    public const int MaximumDevices = 32;
    public const int MaximumSamples = 288;
    private static readonly TimeSpan LastKnownAge = TimeSpan.FromMinutes(30);
    public Dictionary<string, List<BatterySample>> Devices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Add(BatteryDevice device, DateTimeOffset now)
    {
        if (!device.IsConnected || device.IsAsleep || device.IsReadingStale || device.Percentage is not >= 0 or > 100) return false;
        if (!Devices.TryGetValue(device.Id, out var samples))
        {
            if (Devices.Count >= MaximumDevices)
                Devices.Remove(Devices.MinBy(x => x.Value.LastOrDefault()?.Time ?? DateTimeOffset.MinValue).Key);
            Devices[device.Id] = samples = [];
        }
        samples.RemoveAll(x => now - x.Time > TimeSpan.FromHours(24));
        var last = samples.LastOrDefault();
        if (last is not null && now <= last.Time) return false;
        if (last is not null && now - last.Time < TimeSpan.FromMinutes(5) && last.Percentage == device.Percentage)
            return false;
        samples.Add(new(now, device.Percentage.Value, device.Source, device.Transport));
        if (samples.Count > MaximumSamples) samples.RemoveRange(0, samples.Count - MaximumSamples);
        return true;
    }

    public IReadOnlyList<BatterySample> Get(string id, DateTimeOffset now) => Devices.TryGetValue(id, out var samples)
        ? samples.Where(x => x.Time >= now.AddHours(-24) && x.Time <= now)
            .OrderBy(x => x.Time).GroupBy(x => x.Time).Select(x => x.Last()).ToArray() : [];

    public bool Remap(IReadOnlyDictionary<string, string> aliases)
    {
        var changed = false;
        foreach (var (oldId, newId) in aliases)
        {
            if (oldId.Equals(newId, StringComparison.OrdinalIgnoreCase) || !Devices.Remove(oldId, out var samples)) continue;
            if (!Devices.TryGetValue(newId, out var existing)) Devices[newId] = samples;
            else Devices[newId] = existing.Concat(samples).Distinct().OrderBy(x => x.Time).TakeLast(MaximumSamples).ToList();
            changed = true;
        }
        return changed;
    }

    public IReadOnlyList<BatteryDevice> WithLastKnown(IEnumerable<BatteryDevice> devices, DateTimeOffset now) => devices.Select(device =>
    {
        if (device.IsReadingStale && device.LastReading is { } timestamp && now - timestamp > LastKnownAge)
            device = device with { Percentage = null, IsReadingStale = false };
        if (device.Percentage.HasValue) return device;
        var last = Get(device.Id, now).LastOrDefault();
        return last is not null && now - last.Time <= LastKnownAge
            ? device with
            {
                Percentage = last.Percentage, LastReading = last.Time, IsReadingStale = true,
                Source = last.Source ?? device.Source,
                Transport = last.Transport == "HID" && device.Source == BatterySource.Windows ? "Bluetooth HID" : last.Transport ?? device.Transport
            }
            : device;
    }).ToArray();

    public void Normalize(DateTimeOffset now)
    {
        Devices ??= new(StringComparer.OrdinalIgnoreCase);
        Devices = Devices.Where(x => !string.IsNullOrEmpty(x.Key) && x.Value is not null)
            .Select(x => new KeyValuePair<string, List<BatterySample>>(x.Key, x.Value
                .Where(s => s is not null && s.Percentage is >= 0 and <= 100 && s.Time >= now.AddHours(-24) && s.Time <= now)
                .OrderBy(s => s.Time).GroupBy(s => s.Time).Select(s => s.Last()).TakeLast(MaximumSamples).ToList()))
            .Where(x => x.Value.Count > 0).OrderByDescending(x => x.Value[^1].Time).Take(MaximumDevices)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
