using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GameLibrary.Controls;

/// <summary>
/// Hides scroll bars while the viewer is idle and fades them in on wheel/scroll interaction.
/// Attach with <c>controls:ScrollFadeBehavior.Enabled="True"</c>.
/// </summary>
public static class ScrollFadeBehavior
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(850);

    private sealed class State
    {
        public bool Attached;
        public bool Pinned;
        public DispatcherTimer? Timer;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollViewer, State> States = new();

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(ScrollFadeBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer) return;
        var state = States.GetOrCreateValue(viewer);
        if ((bool)e.NewValue)
        {
            if (state.Attached) return;
            state.Attached = true;
            state.Timer = new DispatcherTimer { Interval = IdleDelay };
            state.Timer.Tick += (_, _) => HideIfIdle(viewer, state);
            viewer.Loaded += (_, _) => { if (viewer.IsLoaded) Hide(viewer); };
            viewer.ScrollChanged += (_, args) =>
            {
                if (Math.Abs(args.VerticalChange) > 0.5 || Math.Abs(args.HorizontalChange) > 0.5)
                    Show(viewer, state);
            };
            viewer.MouseEnter += (_, _) => Show(viewer, state);
            viewer.PreviewMouseDown += (_, args) =>
            {
                if (FindAncestor<ScrollBar>(args.OriginalSource as DependencyObject) is not null)
                {
                    state.Pinned = true;
                    Show(viewer, state);
                }
            };
            viewer.PreviewMouseUp += (_, _) =>
            {
                state.Pinned = false;
                RestartTimer(viewer, state);
            };
            viewer.Unloaded += (_, _) =>
            {
                state.Timer?.Stop();
                state.Attached = false;
            };
        }
        else
        {
            state.Timer?.Stop();
            state.Attached = false;
        }
    }

    private static void Show(ScrollViewer viewer, State state)
    {
        foreach (var bar in Descendants<ScrollBar>(viewer))
        {
            bar.IsHitTestVisible = true;
            bar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        }
        RestartTimer(viewer, state);
    }

    private static void RestartTimer(ScrollViewer viewer, State state)
    {
        if (state.Pinned) return;
        if (state.Timer is null) return;
        state.Timer.Stop();
        state.Timer.Start();
    }

    private static void HideIfIdle(ScrollViewer viewer, State state)
    {
        if (state.Pinned || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            RestartTimer(viewer, state);
            return;
        }
        Hide(viewer);
    }

    private static void Hide(ScrollViewer viewer)
    {
        foreach (var bar in Descendants<ScrollBar>(viewer))
        {
            bar.IsHitTestVisible = false;
            bar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
        }
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
