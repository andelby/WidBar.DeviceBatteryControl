using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Controls;

internal static class BatteryChartRenderer
{
    public const double Inset = 5;
    public static double MapX(double position, double width) => Inset + Math.Max(0, width - Inset * 2) * position;
    public static double MapY(double value, BatteryChartSeries series, double height) =>
        height - 2 - (value - series.Minimum) / series.Span * Math.Max(0, height - 4);

    public static void Draw(Canvas canvas, BatteryChartSeries series, Color color, bool fill, bool currentLine)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || series.Points.Count == 0) return;
        canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        var brush = new SolidColorBrush(color);
        var segments = new List<List<Point>>();
        foreach (var sample in series.DisplayPoints())
        {
            if (sample.StartsSegment) segments.Add([]);
            segments[^1].Add(new(MapX(sample.Position, width), MapY(sample.Percentage, series, height)));
        }
        foreach (var segment in segments)
        {
            if (segment.Count == 1)
            {
                AddMarker(canvas, segment[0], brush, 4);
                continue;
            }
            if (fill)
            {
                var area = new PointCollection { new(segment[0].X, height) };
                foreach (var point in segment) area.Add(point);
                area.Add(new(segment[^1].X, height));
                canvas.Children.Add(new Polygon
                {
                    Points = area,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new(0, 0), EndPoint = new(0, 1),
                        GradientStops = { new() { Color = Color.FromArgb(48, color.R, color.G, color.B), Offset = 0 }, new() { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 1 } }
                    }
                });
            }
            var points = new PointCollection();
            foreach (var point in segment) points.Add(point);
            canvas.Children.Add(new Polyline { Points = points, Stroke = brush, StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        }
        var last = series.Points[^1];
        var end = new Point(MapX(last.Position, width), MapY(last.Percentage, series, height));
        if (currentLine && series.Points.Count > 1)
            canvas.Children.Add(new Line { X1 = Inset, X2 = width - Inset, Y1 = end.Y, Y2 = end.Y,
                Stroke = brush, Opacity = 0.55, StrokeThickness = 1, StrokeDashArray = [5d, 4d] });
        AddMarker(canvas, end, brush, fill ? 7 : 4);
    }

    private static void AddMarker(Canvas canvas, Point point, Brush brush, double size)
    {
        var marker = new Ellipse { Width = size, Height = size, Fill = brush };
        Canvas.SetLeft(marker, point.X - size / 2); Canvas.SetTop(marker, point.Y - size / 2);
        canvas.Children.Add(marker);
    }
}
