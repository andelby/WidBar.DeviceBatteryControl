using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DeviceBatteryControl.Controls;

/// <summary>
/// A page's vertical scroll bar that shows only while the pointer moves over
/// the page or the page scrolls, and fades away a moment after it stops.
/// The platform's own indicator lingers for seconds after the last movement;
/// in a flyout this small it read as always there. The bar is only faded,
/// never collapsed: wheel, touch and dragging the thumb work as ever, and a
/// pointer resting on the bar keeps it shown.
/// </summary>
internal sealed class ScrollBarAutoHide
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(1200);

    private readonly ScrollViewer _scroller;
    private readonly DispatcherTimer _idle = new() { Interval = IdleDelay };
    private ScrollBar? _bar;
    private bool _shown = true;
    private bool _pointerOnBar;

    private ScrollBarAutoHide(ScrollViewer scroller)
    {
        _scroller = scroller;
        _idle.Tick += OnIdle;
        scroller.Loaded += (_, _) => EnsureBar();
        // A page that starts collapsed gets its template, and so its bar,
        // only when first shown: its first layout is when to look again.
        scroller.SizeChanged += (_, _) => EnsureBar();
        scroller.Unloaded += (_, _) => _idle.Stop();
        scroller.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnActivity), handledEventsToo: true);
        scroller.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnActivity), handledEventsToo: true);
        scroller.ViewChanged += (_, _) => Show();
        scroller.PointerExited += (_, _) => Restart();
    }

    public static void Attach(ScrollViewer scroller) => _ = new ScrollBarAutoHide(scroller);

    private void EnsureBar()
    {
        if (_bar is not null || FindBar(_scroller) is not { } bar)
        {
            return;
        }

        _bar = bar;
        _bar.PointerEntered += (_, _) => _pointerOnBar = true;
        _bar.PointerExited += (_, _) =>
        {
            _pointerOnBar = false;
            Restart();
        };

        // Out of sight until the pointer or the page moves.
        Fade(0, TimeSpan.Zero);
    }

    private void OnActivity(object sender, PointerRoutedEventArgs e) => Show();

    private void Show()
    {
        EnsureBar();
        Fade(1, TimeSpan.FromMilliseconds(120));
        Restart();
    }

    private void Restart()
    {
        _idle.Stop();
        _idle.Start();
    }

    private void OnIdle(object? sender, object e)
    {
        _idle.Stop();
        if (_pointerOnBar)
        {
            // Resting on the bar, or dragging its thumb: keep it.
            _idle.Start();
            return;
        }

        Fade(0, TimeSpan.FromMilliseconds(360));
    }

    private void Fade(float opacity, TimeSpan duration)
    {
        if (_bar is null || (_shown == (opacity > 0) && duration > TimeSpan.Zero))
        {
            return;
        }

        _shown = opacity > 0;
        var visual = ElementCompositionPreview.GetElementVisual(_bar);
        if (duration == TimeSpan.Zero)
        {
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Opacity = opacity;
            return;
        }

        var compositor = visual.Compositor;
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertExpressionKeyFrame(0f, "this.StartingValue");
        fade.InsertKeyFrame(1f, opacity, compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1f)));
        fade.Duration = duration;
        visual.StartAnimation(nameof(Visual.Opacity), fade);
    }

    private static ScrollBar? FindBar(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollBar { Name: "VerticalScrollBar" } bar)
            {
                return bar;
            }

            // Not into the content: a nested scroller has bars of its own.
            if (child is ScrollContentPresenter)
            {
                continue;
            }

            if (FindBar(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
