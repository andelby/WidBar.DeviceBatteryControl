using System.Diagnostics;
using System.Text.Json;
using DeviceBatteryControl.Controls;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Diagnostics;

internal static class WidgetPerformanceProbe
{
    private sealed record Measurement(string Name, int Iterations, double Milliseconds, long AllocatedBytes);

    public static async Task RunAsync(BatteryPreviewView preview, string output)
    {
        var now = DateTimeOffset.UtcNow;
        var devices = Enumerable.Range(0, 10).Select(i => new BatteryDevice("probe-" + i,
            "Sample device " + i, (DeviceKind)(1 + i % 3), true, 70 + i, BatterySource.Windows, now)).ToArray();
        var settings = new BatterySettings { PreviewDeviceCount = 10 };
        preview.Width = settings.GetPreviewWidth(devices);
        preview.Render(devices, settings);
        preview.UpdateLayout();
        var results = new List<Measurement>
        {
            Measure("UnchangedPreview", 1000, _ => { preview.Render(devices, settings); preview.UpdateLayout(); }),
            Measure("ChangedPreview", 250, i =>
            {
                devices[0] = devices[0] with { Percentage = i % 101 };
                preview.Render(devices, settings);
                preview.UpdateLayout();
            })
        };
        var history = new BatteryHistory();
        for (var i = 0; i < BatteryHistory.MaximumSamples; i++)
            history.Add(devices[0] with { Percentage = i % 101 }, now.AddMinutes(-15 * (BatteryHistory.MaximumSamples - i)));
        var samples = history.Get(devices[0].Id, now);
        var series = BatteryChartSeries.Create(samples, now, BatteryChartScale.Automatic);
        results.Add(Measure("NearestPoint", 1000, i => _ = series.Nearest(i / 999d)));
        var cache = new BatteryHistoryCache();
        results.Add(Measure("RepeatedHistoryRead", 128, _ => _ = cache.Get(history, devices[0].Id, now).Count));
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            CapturedUtc = now, Build = typeof(WidgetPerformanceProbe).Assembly.ManifestModule.ModuleVersionId,
            SampleCount = samples.Count, PreviewItemsCreated = preview.CreatedItemCount,
            Notes = "Release UI-thread synthetic measurements; managed allocations only; not a GPU/frame-rate benchmark.",
            Results = results
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Measurement Measure(string name, int iterations, Action<int> action)
    {
        for (var i = 0; i < 16; i++) action(i);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) action(i);
        watch.Stop();
        return new(name, iterations, Math.Round(watch.Elapsed.TotalMilliseconds, 3),
            GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
}
