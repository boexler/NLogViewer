using System.Windows;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Every element in the template of the viewer knows the viewer. The rows and the cells of the list read the colours and the resolvers
    /// from it. They are made, and their bindings are set, before they are in the window; a search for the viewer above them with
    /// <c>FindAncestor</c> fails then and fills the output with binding errors for every new entry. This property is empty until the element is
    /// in the viewer and then gives the viewer, without an error.
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>
        /// The <see cref="LogViewer"/> in which an element is shown; inherited by everything the viewer shows
        /// </summary>
        public static readonly DependencyProperty OwnerProperty =
            DependencyProperty.RegisterAttached("Owner", typeof(LogViewer), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        public static LogViewer? GetOwner(DependencyObject element) => (LogViewer?)element.GetValue(OwnerProperty);

        public static void SetOwner(DependencyObject element, LogViewer? value) => element.SetValue(OwnerProperty, value);
    }
}
