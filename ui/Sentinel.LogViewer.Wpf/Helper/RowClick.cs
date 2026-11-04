using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Finds the row of a list that a double click hit
    /// </summary>
    internal static class RowClick
    {
        /// <returns>The row and the element in it that was hit; null for a click on the header or on the empty space</returns>
        public static (ListViewItem Row, DependencyObject Source)? Of(ListView list, MouseButtonEventArgs e)
        {
            // The double click of a control may name the control itself as its source; then the element under the mouse tells where it hit.
            var source = e.OriginalSource as DependencyObject;
            if (source == null || ReferenceEquals(source, list))
                source = e.MouseDevice.DirectlyOver as DependencyObject;

            return source != null && ItemsControl.ContainerFromElement(list, source) is ListViewItem row ? (row, source) : null;
        }
    }
}
