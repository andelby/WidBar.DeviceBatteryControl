namespace DeviceBatteryControl.Models;

/// <summary>Read-only views for one history revision. Invalidate after a sample or identity change.</summary>
public sealed class BatteryHistoryCache
{
    private sealed record View(IReadOnlyList<BatterySample> Samples, DateTimeOffset ReadAt, DateTimeOffset ValidUntil);
    private readonly Dictionary<string, View> _views = new(StringComparer.OrdinalIgnoreCase);
    private BatteryHistory? _source;

    public IReadOnlyList<BatterySample> Get(BatteryHistory history, string id, DateTimeOffset now)
    {
        if (!ReferenceEquals(_source, history)) { _source = history; Invalidate(); }
        if (!history.Devices.TryGetValue(id, out var source)) return [];
        if (_views.TryGetValue(id, out var view) && now >= view.ReadAt && now < view.ValidUntil) return view.Samples;
        var samples = history.Get(id, now);
        var validUntil = DateTimeOffset.MaxValue;
        foreach (var sample in source)
            if (sample.Time > now && sample.Time < validUntil) validUntil = sample.Time;
        _views[id] = new(samples, now, validUntil);
        return samples;
    }

    public void Invalidate() => _views.Clear();
}
