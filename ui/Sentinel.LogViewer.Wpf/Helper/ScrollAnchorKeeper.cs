using System;
using System.Collections;
using System.Windows.Controls;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Keeps the entry the user is looking at in place when the list's filter changes: the selected entry while it is
    /// on screen, otherwise the entry at the top. Refreshing a filtered view keeps the scroll offset, which would then
    /// show other entries.
    /// </summary>
    internal sealed class ScrollAnchorKeeper
    {
        private object? _anchor;
        private int _anchorRow;
        private bool _anchorSelected;
        private bool _anchorAtEnd;
        private object? _restoredEntry;

        /// <summary>
        /// Remembers the entry to keep in place; call before the filter changes
        /// </summary>
        public void Capture(ListView listView)
        {
            // With item based scrolling the vertical offset is the index of the top item.
            if (ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer)) is not ScrollViewer { CanContentScroll: true } scrollViewer ||
                listView.Items.Count == 0)
            {
                _anchor = null;
                return;
            }

            // A restore from a filter change just before has not been laid out yet.
            listView.UpdateLayout();
            var topIndex = Math.Min((int)scrollViewer.VerticalOffset, listView.Items.Count - 1);

            // While the entry scrolled to by the last restore is still where it was put, the user has not moved on,
            // so the earlier anchor is still the entry being read (e.g. after hiding and showing a level again).
            if (_anchor != null && _restoredEntry != null && IsWhereRestored(listView, scrollViewer, topIndex))
                return;

            var selectedIndex = listView.SelectedItem == null ? -1 : listView.Items.IndexOf(listView.SelectedItem);
            _anchorSelected = selectedIndex >= topIndex && selectedIndex < topIndex + (int)scrollViewer.ViewportHeight;
            var anchorIndex = _anchorSelected ? selectedIndex : topIndex;
            _anchor = listView.Items[anchorIndex];
            _anchorRow = anchorIndex - topIndex;
            _anchorAtEnd = scrollViewer.ScrollableHeight > 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight;
        }

        /// <summary>
        /// Scrolls the remembered entry, or the nearest earlier entry that is still in the list, back to its row
        /// </summary>
        /// <param name="source">All entries in their original order, including the hidden ones</param>
        /// <param name="isInList">Whether an entry passes the list's filter</param>
        public void Restore(ListView listView, IList source, Predicate<object> isInList)
        {
            if (_anchor == null || ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer)) is not ScrollViewer scrollViewer)
                return;

            _restoredEntry = null;
            if (_anchorAtEnd)
            {
                scrollViewer.ScrollToEnd();
                return;
            }

            var target = FindNearestEntryInList(source, _anchor, isInList);
            var index = target == null ? -1 : listView.Items.IndexOf(target);
            if (index < 0)
                return;

            scrollViewer.ScrollToVerticalOffset(Math.Max(0, index - _anchorRow));
            // A hidden entry loses its selection; it gets it back when it is shown again.
            if (_anchorSelected && ReferenceEquals(target, _anchor))
                listView.SelectedItem = target;
            _restoredEntry = target;
        }

        /// <summary>
        /// Returns whether the restored entry is still in its row, or as close to it as the list can scroll
        /// </summary>
        private bool IsWhereRestored(ListView listView, ScrollViewer scrollViewer, int topIndex)
        {
            var index = listView.Items.IndexOf(_restoredEntry);
            var expectedTop = Math.Clamp(index - _anchorRow, 0, Math.Max(0, (int)scrollViewer.ScrollableHeight));
            return index >= 0 && topIndex == expectedTop;
        }

        private static object? FindNearestEntryInList(IList source, object entry, Predicate<object> isInList)
        {
            var position = source.IndexOf(entry);
            if (position < 0)
                return null;

            for (var i = position; i >= 0; i--)
            {
                if (source[i] is { } candidate && isInList(candidate))
                    return candidate;
            }

            // No earlier entry is left, so the first later one takes its place.
            for (var i = position + 1; i < source.Count; i++)
            {
                if (source[i] is { } candidate && isInList(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
