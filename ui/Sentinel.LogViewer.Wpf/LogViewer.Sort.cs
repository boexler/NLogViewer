using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Sorting the list by a column. It is only a view: the entries stay in the order in which they came in, and the third click on a header
    /// (or <see cref="ResetSortCommand"/>) shows them in that order again.
    /// </summary>
    public partial class LogViewer
    {
        private const string AscendingMark = " ▲";
        private const string DescendingMark = " ▼";

        private ICommand? _resetSortCommand;
        private bool? _autoScrollBeforeSort;

        /// <summary>
        /// The key of the column that the list is sorted by (see <see cref="LogColumns"/>), or null for the order in which the entries came in
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The key of the column that the list is sorted by, or null for the order in which the entries came in")]
        public string? SortKey
        {
            get => (string?)GetValue(SortKeyProperty);
            set => SetValue(SortKeyProperty, value);
        }

        /// <summary>
        /// The <see cref="SortKey"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SortKeyProperty =
            DependencyProperty.Register(nameof(SortKey), typeof(string), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSortChanged));

        /// <summary>
        /// Whether the sort by <see cref="SortKey"/> goes from the largest value to the smallest
        /// </summary>
        [Category("LogViewerColumns")]
        public bool SortDescending
        {
            get => (bool)GetValue(SortDescendingProperty);
            set => SetValue(SortDescendingProperty, value);
        }

        /// <summary>
        /// The <see cref="SortDescending"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SortDescendingProperty =
            DependencyProperty.Register(nameof(SortDescending), typeof(bool), typeof(LogViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSortChanged));

        private static readonly DependencyPropertyKey SortTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(SortText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="SortText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SortTextProperty = SortTextPropertyKey.DependencyProperty;

        /// <summary>
        /// What the result row says about the sort, e.g. "Sorted by LoggerName ▲"; empty while the list is not sorted
        /// </summary>
        [Category("LogViewerColumns")]
        public string SortText => (string)GetValue(SortTextProperty);

        /// <summary>
        /// Shows the entries in the order in which they came in again
        /// </summary>
        public ICommand ResetSortCommand => _resetSortCommand ??= new RelayCommand(ResetSort, () => SortKey != null);

        /// <summary>
        /// The next step of the sort by a column, as a click on its header does it: ascending, then descending, then not sorted
        /// </summary>
        /// <param name="key">The key of the column, see <see cref="LogColumns"/></param>
        public void CycleSort(string key)
        {
            if (SortKey != key)
            {
                SortDescending = false;
                SortKey = key;
            }
            else if (!SortDescending)
            {
                SortDescending = true;
            }
            else
            {
                ResetSort();
            }
        }

        /// <summary>
        /// Shows the entries in the order in which they came in
        /// </summary>
        public void ResetSort()
        {
            SortDescending = false;
            SortKey = null;
        }

        private static void OnSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as LogViewer)?.ApplySort();

        /// <summary>
        /// The ID, logger and message columns are sorted by what their resolvers show, so a new resolver sorts again
        /// </summary>
        private static void OnSortResolverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { SortKey: not null } viewer)
                viewer.SetViewSort();
        }

        /// <summary>
        /// Puts the list in the order of <see cref="SortKey"/>. A new entry takes its place in that order. Auto-scroll is switched off while the
        /// list is sorted, because the end of the list is not where the new entries are, and comes back when the sort is reset.
        /// </summary>
        private void ApplySort()
        {
            SetViewSort();

            if (SortKey != null && _autoScrollBeforeSort == null)
            {
                _autoScrollBeforeSort = AutoScroll;
                AutoScroll = false;
            }
            else if (SortKey == null && _autoScrollBeforeSort != null)
            {
                AutoScroll = _autoScrollBeforeSort.Value;
                _autoScrollBeforeSort = null;
            }

            UpdateHeaders();
            ShowSelectedOrTop();
            UpdateBookmarkLabel();
        }

        /// <summary>
        /// Gives the view of the list the comparer of the sort; a new view (another source of entries) and a new zone of the file need it again
        /// </summary>
        private void SetViewSort()
        {
            if (LogEvents?.View is ListCollectionView view)
                view.CustomSort = SortKey == null ? null : new EntryComparer(SortKey, SortDescending, FileTimeZone, IdResolver, LoggerNameResolver, MessageResolver);
        }

        /// <summary>
        /// Writes the headers of the columns in the language of the control, with the mark of the sort on the header of its column; tells
        /// <see cref="SortText"/>
        /// </summary>
        private void UpdateHeaders()
        {
            var text = string.Empty;
            foreach (var (column, key) in _columnKeys)
            {
                var mark = key == SortKey ? (SortDescending ? DescendingMark : AscendingMark) : string.Empty;
                column.Header = ColumnTitle(key, withZone: true) + mark;
                if (mark.Length > 0)
                    text = LogViewerTexts.Format("Sort_By", ColumnTitle(key, withZone: false) + mark);
            }

            SetValue(SortTextPropertyKey, text);
        }

        /// <summary>
        /// The title of a column in the language of the control; the time stamp column says which zones are converted, e.g. <c>TimeStamp (UTC+9 → local)</c>
        /// </summary>
        private string ColumnTitle(string key, bool withZone)
        {
            switch (key)
            {
                case LogColumns.Id: return LogViewerTexts.Get("Col_Id");
                case LogColumns.Level: return LogViewerTexts.Get("Col_Level");
                case LogColumns.TimeStamp:
                    var label = withZone ? (TimeStampResolver as Resolver.TimeStampResolver)?.ZoneLabel : null;
                    return LogViewerTexts.Get("Col_TimeStamp") + (string.IsNullOrEmpty(label) ? string.Empty : $" ({label})");
                case LogColumns.Logger: return LogViewerTexts.Get("Col_Logger");
                case LogColumns.Thread: return LogViewerTexts.Get("Col_Thread");
                case LogColumns.Message: return LogViewerTexts.Get("Col_Message");
                default: return LogColumns.IsProperty(key, out var name) ? name : key;
            }
        }

        private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader { Role: not GridViewColumnHeaderRole.Padding, Column: { } column }
                && _columnKeys.TryGetValue(column, out var key))
            {
                CycleSort(key);
            }
        }

        /// <summary>
        /// After a change of the order the list shows the selected entry, or the top
        /// </summary>
        private void ShowSelectedOrTop()
        {
            if (PART_ListView is not { } listView)
                return;

            if (listView.SelectedItem != null)
                listView.ScrollIntoView(listView.SelectedItem);
            else if (ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer)) is ScrollViewer scrollViewer)
                scrollViewer.ScrollToTop();
        }
    }
}
