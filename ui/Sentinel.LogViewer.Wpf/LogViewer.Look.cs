using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>How the level of an entry is shown in the level column</summary>
    public enum LevelDisplayMode
    {
        /// <summary>As the symbol of the level</summary>
        Symbol,

        /// <summary>As the name of the level</summary>
        Text,

        /// <summary>As the symbol and the name next to it</summary>
        SymbolAndText
    }

    /// <summary>How much room a row of the list takes</summary>
    public enum RowHeightMode
    {
        Normal,

        /// <summary>No border and a smaller symbol, so more rows fit on the screen</summary>
        Compact
    }

    /// <summary>
    /// The look of the rows of the list: how the level is shown and how high the rows are. Both are inherited by what the list shows, so the rows
    /// and the cells read them from themselves; a search for the viewer above them would fail while a row is made, before it is in the window,
    /// and fill the output with binding errors.
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>
        /// How the level column shows the level of an entry
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("How the level column shows the level of an entry")]
        public LevelDisplayMode LevelDisplay
        {
            get => (LevelDisplayMode)GetValue(LevelDisplayProperty);
            set => SetValue(LevelDisplayProperty, value);
        }

        /// <summary>
        /// The <see cref="LevelDisplay"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LevelDisplayProperty =
            DependencyProperty.RegisterAttached(nameof(LevelDisplay), typeof(LevelDisplayMode), typeof(LogViewer),
                new FrameworkPropertyMetadata(LevelDisplayMode.Symbol, FrameworkPropertyMetadataOptions.Inherits, OnLevelDisplayChanged));

        public static LevelDisplayMode GetLevelDisplay(DependencyObject element) => (LevelDisplayMode)element.GetValue(LevelDisplayProperty);

        public static void SetLevelDisplay(DependencyObject element, LevelDisplayMode value) => element.SetValue(LevelDisplayProperty, value);

        /// <summary>
        /// How high the rows of the list are
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("How high the rows of the list are")]
        public RowHeightMode RowHeight
        {
            get => (RowHeightMode)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        /// <summary>
        /// The <see cref="RowHeight"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.RegisterAttached(nameof(RowHeight), typeof(RowHeightMode), typeof(LogViewer),
                new FrameworkPropertyMetadata(RowHeightMode.Normal, FrameworkPropertyMetadataOptions.Inherits));

        public static RowHeightMode GetRowHeight(DependencyObject element) => (RowHeightMode)element.GetValue(RowHeightProperty);

        public static void SetRowHeight(DependencyObject element, RowHeightMode value) => element.SetValue(RowHeightProperty, value);

        private static void OnLevelDisplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer || !viewer._columnsByKey.TryGetValue(LogColumns.Level, out var column))
                return;

            // The column is as wide as its cells; assigning the same width again is ignored, so leave it first to get it measured anew.
            viewer.Dispatcher.InvokeAsync(() =>
            {
                column.Width = column.ActualWidth;
                column.Width = double.NaN;
            }, DispatcherPriority.Loaded);
        }
    }
}
