using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using DeviceBatteryControl.Localization;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

public sealed partial class BatteryHistoryChart : UserControl
{
    private BatteryChartSeries _series = BatteryChartSeries.Create([], DateTimeOffset.UtcNow, BatteryChartScale.Automatic);
    private BatterySettings _settings = new();
    private DeviceKind _kind;
    private string? _signature;
    private int _entranceGeneration;

    public BatteryHistoryChart()
    {
        InitializeComponent();
        EmptyMessage.Text = BatteryLoc.Get("HistoryEmpty");
        ActualThemeChanged += (_, _) => Draw();
    }

    public void SetSamples(IReadOnlyList<BatterySample> samples, BatterySettings settings, DeviceKind kind)
    {
        var signature = string.Join('|', samples.Select(x => $"{x.Time.Ticks}:{x.Percentage}")) + settings.ToJson() + kind;
        if (signature == _signature) return;
        _signature = signature;
        _settings = settings;
        _kind = kind;
        _series = BatteryChartSeries.Create(samples, DateTimeOffset.UtcNow, settings.ChartScale, settings.RefreshSeconds);
        var labels = new[] { Axis0, Axis1, Axis2, Axis3 };
        for (var i = 0; i < labels.Length; i++)
            labels[i].Text = _series.Points.Count == 0 ? "" : (_series.Maximum - _series.Span * i / 3).ToString("0.#", BatteryLoc.FormattingCulture) + "%";
        StartLabel.Text = EndLabel.Text = "";
        if (_series.Points.Count > 0)
        {
            var first = _series.Points[0].Time.ToLocalTime();
            var last = _series.Points[^1].Time.ToLocalTime();
            var format = first.Date == last.Date ? "t" : "g";
            StartLabel.Text = first.ToString(format, BatteryLoc.FormattingCulture);
            StartLabel.HorizontalAlignment = _series.Points.Count == 1 ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            if (_series.Points.Count > 1) EndLabel.Text = last.ToString(format, BatteryLoc.FormattingCulture);
        }
        AutomationProperties.SetName(this, _series.Points.Count == 0 ? BatteryLoc.Get("HistoryEmpty")
            : BatteryLoc.Format("ChartSummary", _series.Points.Min(x => x.Percentage), _series.Points.Max(x => x.Percentage), _series.Points.Count));
        HideCrosshair();
        Draw();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => Draw();
    private void Draw()
    {
        GridLines.Children.Clear();
        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        if (width <= 0 || height <= 0) return;
        for (var i = 1; i < 4; i++)
            GridLines.Children.Add(new Line { X1 = BatteryChartRenderer.Inset, X2 = width - BatteryChartRenderer.Inset,
                Y1 = height * i / 4, Y2 = height * i / 4,
                Stroke = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"], StrokeThickness = 1, Opacity = 0.55 });
        var color = BatteryAppearance.ColorFor(_kind, _settings, ActualTheme);
        BatteryChartRenderer.Draw(Plot, _series, color, true, true);
        CursorDot.Fill = new SolidColorBrush(color);
        EmptyMessage.Visibility = _series.Points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void PlayEntrance()
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var generation = ++_entranceGeneration;
        var visual = ElementCompositionPreview.GetElementVisual(RevealHost);
        var compositor = visual.Compositor;
        var width = (float)Math.Max(ActualWidth, 600);
        var clip = compositor.CreateInsetClip(-12, -12, width + 12, -12);
        visual.Clip = clip;
        var reveal = compositor.CreateScalarKeyFrameAnimation();
        reveal.InsertKeyFrame(0, width + 12);
        reveal.InsertKeyFrame(1, -12, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.6f), new Vector2(0.3f, 1f)));
        reveal.Duration = TimeSpan.FromMilliseconds(700);
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        clip.StartAnimation(nameof(InsetClip.RightInset), reveal);
        batch.End();
        batch.Completed += (_, _) => { if (generation == _entranceGeneration) visual.Clip = null; };
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args) => UpdateCrosshair(args.GetCurrentPoint(Overlay).Position);
    private void OnPointerPressed(object sender, PointerRoutedEventArgs args) => UpdateCrosshair(args.GetCurrentPoint(Overlay).Position);
    private void OnPointerExited(object sender, PointerRoutedEventArgs args) => HideCrosshair();
    private void UpdateCrosshair(Point position)
    {
        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        if (width <= 10 || height <= 0) return;
        var point = _series.Nearest((position.X - BatteryChartRenderer.Inset) / (width - BatteryChartRenderer.Inset * 2));
        if (point is null) return;
        var x = BatteryChartRenderer.MapX(point.Position, width);
        var y = BatteryChartRenderer.MapY(point.Percentage, _series, height);
        Cursor.X1 = Cursor.X2 = x;
        Cursor.Y1 = 0; Cursor.Y2 = height;
        Cursor.Visibility = CursorDot.Visibility = Tooltip.Visibility = Visibility.Visible;
        Canvas.SetLeft(CursorDot, x - 4.5); Canvas.SetTop(CursorDot, y - 4.5);
        ValueText.Text = point.Percentage.ToString(BatteryLoc.FormattingCulture) + "%";
        TimeText.Text = point.Time.ToLocalTime().ToString("g", BatteryLoc.FormattingCulture);
        Tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Tooltip.DesiredSize;
        Canvas.SetLeft(Tooltip, Math.Clamp(x - size.Width / 2, 0, Math.Max(0, width - size.Width)));
        Canvas.SetTop(Tooltip, Math.Clamp(y - size.Height - 14, 0, Math.Max(0, height - size.Height)));
        Canvas.SetZIndex(Tooltip, 90);
    }

    private void HideCrosshair() => Cursor.Visibility = CursorDot.Visibility = Tooltip.Visibility = Visibility.Collapsed;
}
