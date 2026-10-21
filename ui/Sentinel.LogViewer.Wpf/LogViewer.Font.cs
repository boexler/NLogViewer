using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The font of the log list: the entries and the column headers, not the tool bars around the list
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>
        /// The font family of the log list
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The font family of the log list")]
        public FontFamily ListFontFamily
        {
            get => (FontFamily)GetValue(ListFontFamilyProperty);
            set => SetValue(ListFontFamilyProperty, value);
        }

        /// <summary>
        /// The <see cref="ListFontFamily"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ListFontFamilyProperty =
            DependencyProperty.Register(nameof(ListFontFamily), typeof(FontFamily), typeof(LogViewer),
                new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, OnListFontChanged));

        /// <summary>
        /// The font size of the log list
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The font size of the log list")]
        public double ListFontSize
        {
            get => (double)GetValue(ListFontSizeProperty);
            set => SetValue(ListFontSizeProperty, value);
        }

        /// <summary>
        /// The <see cref="ListFontSize"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ListFontSizeProperty =
            DependencyProperty.Register(nameof(ListFontSize), typeof(double), typeof(LogViewer),
                new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, OnListFontChanged));

        private static void OnListFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { _columnWidthFitter: { } fitter } viewer)
            {
                // After the list has taken the font, so that the texts are measured with it.
                viewer.Dispatcher.InvokeAsync(() => fitter.Refit(viewer.GetEffectiveEntries()), DispatcherPriority.Loaded);
            }
        }
    }
}
