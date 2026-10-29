using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The menu of a row: what can be done with the entry under the mouse, besides what the menu of a cell offers (the logger, the time range,
    /// jumping). The items are made when the right mouse button goes down, so that they know the entry.
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>The mark of the items that this viewer adds to a menu, so that they are replaced and not added again</summary>
        private const string RowActionMark = "RowAction";

        private ICommand? _showInLoggerTreeCommand;

        /// <summary>
        /// Opens the logger tree and selects the logger (the parameter is the logger name) with the loggers above it opened
        /// </summary>
        public ICommand ShowInLoggerTreeCommand => _showInLoggerTreeCommand ??=
            new RelayCommand<string>(ShowInLoggerTree, name => !string.IsNullOrEmpty(name));

        /// <summary>
        /// Opens the logger tree, opens the loggers above the logger and selects it
        /// </summary>
        public void ShowInLoggerTree(string? loggerName)
        {
            var node = LoggerTree.Find(loggerName);
            if (node == null)
                return;

            IsLoggerTreeExpanded = true;
            LoggerTree.SearchText = string.Empty;
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                parent.IsExpanded = true;

            node.IsSelected = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => BringIntoView(node));
        }

        /// <summary>
        /// Scrolls the logger tree to the node
        /// </summary>
        /// <remarks>shortcut: the row of a node far down a long tree may not exist yet (the tree is virtualized); then the node is opened and
        /// selected but not scrolled to. Upgrade: scroll by index when it comes up.</remarks>
        private void BringIntoView(Core.LoggerNode node)
        {
            if (GetTemplateChild("PART_LoggerTreeView") is not TreeView tree)
                return;

            ItemsControl parent = tree;
            var path = new System.Collections.Generic.List<Core.LoggerNode>();
            for (var current = node; current != null; current = current.Parent)
                path.Insert(0, current);

            foreach (var step in path)
            {
                parent.UpdateLayout();
                if (parent.ItemContainerGenerator.ContainerFromItem(step) is not TreeViewItem container)
                    return;

                parent = container;
            }

            ((TreeViewItem)parent).BringIntoView();
        }

        private void OnListViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not FrameworkElement { DataContext: LogEntry entry } source)
                return;

            // The menu of the cell, or of the row for a cell that has none.
            var own = OwnMenuOf(source);
            var menu = own;
            if (menu == null)
            {
                if (FindAncestor<ListViewItem>(source) is not { } row)
                    return;

                menu = row.ContextMenu ??= new ContextMenu();
            }

            AddRowActions(menu, entry);
        }

        private static ContextMenu? OwnMenuOf(DependencyObject source)
        {
            for (var current = source; current != null && current is not ListViewItem; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement { ContextMenu: { } menu })
                    return menu;
            }

            return null;
        }

        /// <summary>
        /// Puts the actions of a row at the end of a menu: the logger, the time range and jumping. A call replaces what an earlier call added.
        /// </summary>
        /// <param name="menu">The menu of the cell, or of the row</param>
        /// <param name="entry">The entry of the row, which the time range starts or ends at</param>
        internal void AddRowActions(ContextMenu menu, LogEntry entry)
        {
            foreach (var old in menu.Items.OfType<Control>().Where(item => Equals(item.Tag, RowActionMark)).ToList())
                menu.Items.Remove(old);

            var items = new System.Collections.Generic.List<Control>();

            // The menu of the logger cell has showing only and hiding the logger already.
            var hasLoggerActions = menu.Items.OfType<MenuItem>().Any(item => item.Header as string == LogViewerTexts.Get("Row_ShowOnly"));
            if (!hasLoggerActions)
            {
                items.Add(Item(LogViewerTexts.Get("Row_ShowOnly"), ShowOnlyLoggerCommand, entry.LoggerName));
                items.Add(Item(LogViewerTexts.Get("Row_Hide"), HideLoggerCommand, entry.LoggerName));
            }

            items.Add(Item(LogViewerTexts.Get("Row_ShowInTree"), ShowInLoggerTreeCommand, entry.LoggerName));
            items.Add(Separator());
            items.Add(Item(LogViewerTexts.Get("Row_TrFrom"), TimeFromHereCommand, entry));
            items.Add(Item(LogViewerTexts.Get("Row_TrUntil"), TimeUntilHereCommand, entry));
            if (HasTimeRange)
                items.Add(Item(LogViewerTexts.Get("Tr_Clear"), ClearTimeRangeCommand, null));

            items.Add(Separator());
            items.Add(Item(LogViewerTexts.Get(IsBookmarked(entry) ? "Row_BookmarkOff" : "Row_Bookmark"), ToggleEntryBookmarkCommand, entry, "Ctrl+B", withIcon: true));
            items.Add(Item(LogViewerTexts.Get("Row_BookmarkComment"), EditBookmarkCommentCommand, entry));
            items.Add(Item(LogViewerTexts.Get("Row_NextBookmark"), NextBookmarkCommand, null, "F2"));
            items.Add(Item(LogViewerTexts.Get("Row_PrevBookmark"), PreviousBookmarkCommand, null, "Shift+F2"));

            items.Add(Separator());
            items.Add(Item(LogViewerTexts.Get("Row_GoTo"), GoToTimeCommand, null, "Ctrl+T"));
            items.Add(Item(LogViewerTexts.Get("Row_NextWarn"), NextLevelCommand, LogEntryLevel.Warn, "F7"));
            items.Add(Item(LogViewerTexts.Get("Row_PrevWarn"), PreviousLevelCommand, LogEntryLevel.Warn, "Shift+F7"));
            items.Add(Item(LogViewerTexts.Get("Row_NextErr"), NextLevelCommand, LogEntryLevel.Error, "F8"));
            items.Add(Item(LogViewerTexts.Get("Row_PrevErr"), PreviousLevelCommand, LogEntryLevel.Error, "Shift+F8"));

            if (menu.Items.Count > 0)
                menu.Items.Add(Separator());

            foreach (var item in items)
                menu.Items.Add(item);

            static Control Separator() => new Separator { Tag = RowActionMark };

            static Control Item(string header, ICommand command, object? parameter, string? gesture = null, bool withIcon = false) =>
                new MenuItem
                {
                    Header = header, Command = command, CommandParameter = parameter, InputGestureText = gesture ?? string.Empty, Tag = RowActionMark,
                    Icon = withIcon ? BookmarkIcon.ForMenu() : null
                };
        }
    }
}
