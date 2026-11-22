namespace DeviceBatteryControl.Models;

public sealed record BatteryChartPoint(DateTimeOffset Time, int Percentage, double Position, bool StartsSegment);

public sealed class BatteryChartSeries
{
    public IReadOnlyList<BatteryChartPoint> Points { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Span => Maximum - Minimum;

    private BatteryChartSeries(IReadOnlyList<BatteryChartPoint> points, double minimum, double maximum)
    {
        Points = points; Minimum = minimum; Maximum = maximum;
    }

    public static BatteryChartSeries Create(IEnumerable<BatterySample> samples, DateTimeOffset now, BatteryChartScale scale, int refreshSeconds = 120)
    {
        var valid = samples.Where(x => x.Time >= now.AddHours(-24) && x.Time <= now && x.Percentage is >= 0 and <= 100)
            .OrderBy(x => x.Time).GroupBy(x => x.Time).Select(x => x.Last()).TakeLast(BatteryHistory.MaximumSamples).ToArray();
        if (valid.Length == 0) return new([], 0, 100);
        var duration = (valid[^1].Time - valid[0].Time).TotalSeconds;
        var gapLimit = TimeSpan.FromSeconds(Math.Max(900, Math.Clamp(refreshSeconds, 60, 900) * 2.5));
        var points = valid.Select((sample, i) => new BatteryChartPoint(sample.Time, sample.Percentage,
            duration == 0 ? 0.5 : (sample.Time - valid[0].Time).TotalSeconds / duration,
            i == 0 || sample.Time - valid[i - 1].Time > gapLimit)).ToArray();
        if (scale == BatteryChartScale.Full) return new(points, 0, 100);
        var low = valid.Min(x => x.Percentage);
        var high = valid.Max(x => x.Percentage);
        var span = Math.Max(10, high - low);
        var center = (high + low) / 2d;
        var minimum = Math.Max(0, Math.Floor((center - span * 0.6) / 5) * 5);
        var maximum = Math.Min(100, Math.Ceiling((center + span * 0.6) / 5) * 5);
        return new(points, minimum, Math.Max(minimum + 1, maximum));
    }

    public BatteryChartPoint? Nearest(double position) => Points.Count == 0 ? null
        : Points.MinBy(x => Math.Abs(x.Position - Math.Clamp(position, 0, 1)));
}
