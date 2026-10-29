using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The list of the bookmarks of a viewer (the data context): every bookmark with its time, level, logger, message and comment
    /// </summary>
    public partial class BookmarkList : UserControl
    {
        // The entry of the selected row; a change of the bookmarks makes a new list, and the row of this entry is selected again.
        private LogEntry? _selected;

        public BookmarkList()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.OfType<BookmarkItem>().FirstOrDefault() is { } item)
                _selected = item.Entry;
        }

        private void OnItemsChanged(object? sender, DataTransferEventArgs e)
        {
            if (e.Property == ItemsControl.ItemsSourceProperty && _selected != null)
                BookmarkRows.SelectedItem = BookmarkRows.Items.OfType<BookmarkItem>().FirstOrDefault(item => item.Entry == _selected);
        }

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only a double click on a row goes to the entry, not one on the header or the empty space.
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(BookmarkRows, source) is ListViewItem { Content: BookmarkItem item }
                && DataContext is LogViewer viewer)
                viewer.GoToBookmarkCommand.Execute(item);
        }
    }
}
