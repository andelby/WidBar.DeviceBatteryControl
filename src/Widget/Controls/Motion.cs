using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace DeviceBatteryControl.Controls;

/// <summary>
/// The flyout's entrances and exits, run by the compositor off the UI thread.
/// Every animation names its start value and applies it before its delay, so
/// an element never shows one frame where it was before moving: the flash
/// the implicit transitions gave, whose first frame still had the old value.
/// Never mixed on one element with the UIElement Opacity/Translation facades.
/// </summary>
internal static class Motion
{
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(100);

    // Which call last moved an element: a finished exit only acts if nothing
    // has moved the element since.
    private static readonly ConditionalWeakTable<UIElement, StrongBox<int>> Generations = new();

    /// <summary>
    /// The elements fade in one after another, <paramref name="stagger"/>
    /// apart, each gliding from <paramref name="offset"/> into place.
    /// Collapsed elements are skipped and take no turn.
    /// </summary>
    public static void Enter(
        IEnumerable<UIElement?> elements,
        Vector3 offset,
        TimeSpan delay = default,
        TimeSpan? stagger = null)
    {
        var step = stagger ?? TimeSpan.FromMilliseconds(45);
        var turn = 0;
        foreach (var element in elements)
        {
            if (element is not { Visibility: Visibility.Visible })
            {
                continue;
            }

            Enter(element, offset, delay + (step * turn));
            turn++;
        }
    }

    public static void Enter(UIElement element, Vector3 offset, TimeSpan delay = default)
    {
        Next(element);
        var visual = Prepare(element);
        var compositor = visual.Compositor;
        var easing = Decelerate(compositor);

        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, easing);
        fade.Duration = FadeDuration;
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", fade);

        if (offset == Vector3.Zero)
        {
            visual.StopAnimation("Translation");
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            return;
        }

        var slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0f, offset);
        slide.InsertKeyFrame(1f, Vector3.Zero, easing);
        slide.Duration = SlideDuration;
        slide.DelayTime = delay;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>
    /// Only the glide, no fade: an element already on screen that has to
    /// move, drawn at <paramref name="offset"/> from its new place first so
    /// it seems to travel there from where it was.
    /// </summary>
    public static void Glide(UIElement element, Vector3 offset, TimeSpan? duration = null)
    {
        Next(element);
        var visual = Prepare(element);
        var compositor = visual.Compositor;
        var slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0f, offset);
        slide.InsertKeyFrame(1f, Vector3.Zero, Decelerate(compositor));
        slide.Duration = duration ?? TimeSpan.FromMilliseconds(300);
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>
    /// A quick fade out; <paramref name="completed"/> runs after it unless
    /// something moved the element again in the meantime.
    /// </summary>
    public static void Exit(UIElement element, Action completed)
    {
        var generation = Next(element);
        var visual = Prepare(element);
        var compositor = visual.Compositor;

        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertExpressionKeyFrame(0f, "this.StartingValue");
        fade.InsertKeyFrame(1f, 0f, compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.7f, 0f),
            new Vector2(1f, 0.5f)));
        fade.Duration = ExitDuration;

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", fade);
        batch.End();
        batch.Completed += (_, _) =>
        {
            if (Generations.TryGetValue(element, out var current) && current.Value == generation)
            {
                completed();
            }
        };
    }

    /// <summary>Back to rest at once: fully shown, in place.</summary>
    public static void Reset(UIElement element)
    {
        Next(element);
        var visual = Prepare(element);
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Translation");
        visual.Opacity = 1f;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

    /// <summary>Hidden at once, for an entrance that has yet to start.</summary>
    public static void Hide(UIElement element)
    {
        Next(element);
        var visual = Prepare(element);
        visual.StopAnimation("Opacity");
        visual.Opacity = 0f;
    }

    private static Visual Prepare(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        return ElementCompositionPreview.GetElementVisual(element);
    }

    private static int Next(UIElement element) =>
        ++Generations.GetValue(element, _ => new StrongBox<int>()).Value;

    // Fluent's decelerate curve: quick off the mark, a long soft landing.
    private static CompositionEasingFunction Decelerate(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
}
