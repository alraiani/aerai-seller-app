using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AERai.Seller.Desktop.Behaviors;

/// <summary>
/// A DataGrid (and other list controls) has its own internal ScrollViewer that swallows
/// MouseWheel input even once it has nothing left to scroll, so a DataGrid nested inside a
/// page-level ScrollViewer blocks wheel input meant to scroll the whole page. Attach
/// BubbleScroll="True" to redirect the DataGrid's wheel input to its nearest ancestor
/// ScrollViewer instead — appropriate for small, non-independently-scrolling grids embedded
/// in a scrollable page (not for a grid that's the page's only/primary scrollable content).
/// </summary>
public static class ScrollBehavior
{
    public static readonly DependencyProperty BubbleScrollProperty =
        DependencyProperty.RegisterAttached(
            "BubbleScroll",
            typeof(bool),
            typeof(ScrollBehavior),
            new PropertyMetadata(false, OnBubbleScrollChanged));

    public static bool GetBubbleScroll(DependencyObject element) => (bool)element.GetValue(BubbleScrollProperty);

    public static void SetBubbleScroll(DependencyObject element, bool value) => element.SetValue(BubbleScrollProperty, value);

    private static void OnBubbleScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not DependencyObject source)
        {
            return;
        }

        var scrollViewer = FindAncestorScrollViewer(source);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? FindAncestorScrollViewer(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent is not null and not ScrollViewer)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as ScrollViewer;
    }
}
