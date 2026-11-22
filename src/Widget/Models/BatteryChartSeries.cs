namespace DeviceBatteryControl.Models;

public sealed record BatteryChartPoint(DateTimeOffset Time, int Percentage, double Position, bool StartsSegment);

public sealed class BatteryChartSeries
{
    private IReadOnlyList<BatteryChartPoint>? _displayPoints;
    public IReadOnlyList<BatteryChartPoint> Points { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Span => Maximum - Minimum;

    private BatteryChartSeries(IReadOnlyList<BatteryChartPoint> points, double minimum, double maximum)
    {
        Points = points; Minimum = minimum; Maximum = maximum;
    }

    public static BatteryChartSeries Create(IEnumerable<BatterySample> samples, DateTimeOffset now, BatteryChartScale scale, int refreshSeconds = BatterySettings.RefreshSeconds)
    {
        var valid = samples.Where(x => x.Time <= now && x.Percentage is >= 0 and <= 100)
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

    public BatteryChartPoint? Nearest(double position)
    {
        if (Points.Count == 0) return null;
        var target = double.IsNaN(position) ? 0 : Math.Clamp(position, 0, 1);
        var low = 0;
        var high = Points.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Points[middle].Position < target) low = middle + 1;
            else high = middle;
        }
        if (low == 0) return Points[0];
        if (low == Points.Count) return Points[^1];
        return target - Points[low - 1].Position <= Points[low].Position - target ? Points[low - 1] : Points[low];
    }

    public IReadOnlyList<BatteryChartPoint> DisplayPoints(int limit = 1024)
    {
        limit = Math.Max(4, limit);
        if (Points.Count <= limit) return Points;
        if (limit == 1024 && _displayPoints is not null) return _displayPoints;
        var indices = new SortedSet<int> { 0, Points.Count - 1 };
        var bucketSize = (int)Math.Ceiling((Points.Count - 2d) / ((limit - 2) / 2));
        for (var start = 1; start < Points.Count - 1; start += bucketSize)
        {
            var minimum = start;
            var maximum = start;
            for (var i = start + 1; i < Math.Min(start + bucketSize, Points.Count - 1); i++)
            {
                if (Points[i].Percentage < Points[minimum].Percentage) minimum = i;
                if (Points[i].Percentage > Points[maximum].Percentage) maximum = i;
            }
            indices.Add(minimum); indices.Add(maximum);
        }
        var display = new List<BatteryChartPoint>(indices.Count);
        var previous = -1;
        foreach (var index in indices)
        {
            var gap = previous < 0;
            for (var i = previous + 1; i <= index; i++) gap |= Points[i].StartsSegment;
            display.Add(Points[index] with { StartsSegment = gap });
            previous = index;
        }
        if (limit == 1024) _displayPoints = display;
        return display;
    }

    public BatteryChartSeries ForDisplay(int limit) => new(DisplayPoints(limit), Minimum, Maximum);
}
