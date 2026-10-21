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

        private bool _refitPending;

        /// <summary>How often the columns were measured again for a new font</summary>
        internal int FontRefits { get; private set; }

        private static void OnListFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer { _columnWidthFitter: not null, _refitPending: false } viewer)
                return;

            // Every wheel notch of a zoom changes the font, and measuring all entries takes long: one measuring for all the changes
            // that come before the list has taken the font.
            viewer._refitPending = true;
            viewer.Dispatcher.InvokeAsync(() =>
            {
                viewer._refitPending = false;
                viewer.FontRefits++;
                viewer._columnWidthFitter?.Refit(viewer.GetEffectiveEntries());
            }, DispatcherPriority.Loaded);
        }
    }
}
