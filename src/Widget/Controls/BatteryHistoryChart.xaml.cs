using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryHistoryChart : UserControl
{
    private IReadOnlyList<BatterySample> _samples = [];
    public BatteryHistoryChart()
    {
        InitializeComponent();
        StartLabel.Text = BatteryLoc.Get("HoursAgo"); EndLabel.Text = BatteryLoc.Get("Now");
        ActualThemeChanged += (_, _) => Draw();
    }
    public void SetSamples(IReadOnlyList<BatterySample> samples) { _samples = samples; Draw(); }
    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => Draw();
    private void Draw()
    {
        Plot.Children.Clear();
        if (Plot.ActualWidth <= 0 || Plot.ActualHeight <= 0) return;
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-24);
        Polyline? line = null;
        BatterySample? previous = null;
        foreach (var sample in _samples)
        {
            if (sample.Time < start || sample.Time > now) continue;
            var point = new Point((sample.Time - start).TotalHours / 24 * Plot.ActualWidth,
                (1 - sample.Percentage / 100d) * (Plot.ActualHeight - 4) + 2);
            if (previous is null || sample.Time - previous.Time > TimeSpan.FromMinutes(15))
            {
                line = new Polyline { Style = (Style)Application.Current.Resources["ChartLineStyle"], StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
                Plot.Children.Add(line);
            }
            line!.Points.Add(point);
            // Visible isolated observations, no invented interpolation through gaps.
            var dot = new Ellipse { Width = 4, Height = 4, Style = (Style)Application.Current.Resources["ChartDotStyle"] };
            Canvas.SetLeft(dot, point.X - 2); Canvas.SetTop(dot, point.Y - 2);
            ToolTipService.SetToolTip(dot, $"{sample.Time.ToLocalTime():t} · {sample.Percentage}%");
            Plot.Children.Add(dot);
            previous = sample;
        }
        AutomationProperties.SetName(this, _samples.Count == 0 ? BatteryLoc.Get("HistoryEmpty")
            : BatteryLoc.Format("ChartSummary", _samples.Min(x => x.Percentage), _samples.Max(x => x.Percentage), _samples.Count));
    }
}
