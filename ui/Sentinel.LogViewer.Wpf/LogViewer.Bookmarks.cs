using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Bookmarks (Ctrl+B, F2 and Shift+F2) and copying the selected rows (Ctrl+C). A bookmark marks an entry in the narrow column at the left of
    /// the list and can have a comment. Bookmarks belong to the viewer and stay while the entry is in the list, also when a filter hides it.
    /// </summary>
    public partial class LogViewer
    {
        private readonly HashSet<LogEntry> _bookmarks = new();
        private readonly Dictionary<LogEntry, string> _bookmarkComments = new();

        // Where a jump stopped at the end of the bookmarks; the same jump again goes on at the other end.
        private (bool Forward, LogEntry Entry)? _bookmarkEnd;

        private GridViewColumn? _bookmarkColumn;
        private BookmarkListWindow? _bookmarkWindow;

        /// <summary>
        /// Marks the narrow column of the bookmarks in the template of the viewer; a template without it has no bookmark column
        /// </summary>
        public static readonly DependencyProperty IsBookmarkColumnProperty =
            DependencyProperty.RegisterAttached("IsBookmarkColumn", typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        public static bool GetIsBookmarkColumn(DependencyObject column) => (bool)column.GetValue(IsBookmarkColumnProperty);

        public static void SetIsBookmarkColumn(DependencyObject column, bool value) => column.SetValue(IsBookmarkColumnProperty, value);

        private ICommand? _toggleBookmarkCommand;
        private ICommand? _toggleEntryBookmarkCommand;
        private ICommand? _nextBookmarkCommand;
        private ICommand? _previousBookmarkCommand;
        private ICommand? _editBookmarkCommentCommand;
        private ICommand? _clearBookmarksCommand;
        private ICommand? _showBookmarksCommand;
        private ICommand? _goToBookmarkCommand;
        private ICommand? _removeBookmarkItemCommand;

        private static readonly DependencyPropertyKey BookmarkItemsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BookmarkItems), typeof(IReadOnlyList<BookmarkItem>), typeof(LogViewer),
                new PropertyMetadata(Array.Empty<BookmarkItem>()));

        /// <summary>
        /// The <see cref="BookmarkItems"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BookmarkItemsProperty = BookmarkItemsPropertyKey.DependencyProperty;

        /// <summary>
        /// The bookmarks as the list shows them, in the order of the entries; made when the list is opened and while it is open
        /// </summary>
        [Category("LogViewerControls")]
        public IReadOnlyList<BookmarkItem> BookmarkItems => (IReadOnlyList<BookmarkItem>)GetValue(BookmarkItemsProperty);

        /// <summary>
        /// Opens the window with the list of the bookmarks, or brings it to the front when it is open
        /// </summary>
        public ICommand ShowBookmarksCommand => _showBookmarksCommand ??= new RelayCommand(ShowBookmarkList);

        /// <summary>
        /// Selects the entry of a row of the list of the bookmarks (the parameter) in the main list; an entry that a filter hides cannot be selected
        /// and <see cref="JumpText"/> says so
        /// </summary>
        public ICommand GoToBookmarkCommand => _goToBookmarkCommand ??= new RelayCommand<BookmarkItem?>(GoToBookmark, item => item != null);

        /// <summary>
        /// Takes the bookmark of a row of the list (the parameter) off
        /// </summary>
        public ICommand RemoveBookmarkItemCommand => _removeBookmarkItemCommand ??= new RelayCommand<BookmarkItem?>(item =>
        {
            if (item == null)
                return;

            RemoveBookmark(item.Entry);
            BookmarksChanged();
        }, item => item != null);

        private static readonly DependencyPropertyKey BookmarkVersionPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BookmarkVersion), typeof(int), typeof(LogViewer), new PropertyMetadata(0));

        /// <summary>
        /// The <see cref="BookmarkVersion"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BookmarkVersionProperty = BookmarkVersionPropertyKey.DependencyProperty;

        /// <summary>
        /// Counts the changes of the bookmarks and their comments; the marks of the rows read the bookmarks again when it changes
        /// </summary>
        [Category("LogViewerControls")]
        public int BookmarkVersion => (int)GetValue(BookmarkVersionProperty);

        private static readonly DependencyPropertyKey BookmarkLabelPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BookmarkLabel), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="BookmarkLabel"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BookmarkLabelProperty = BookmarkLabelPropertyKey.DependencyProperty;

        /// <summary>
        /// The text of the bookmark button: the number of bookmarks, e.g. "3", and "2/3" while the selected entry is the second of them; empty without any
        /// </summary>
        [Category("LogViewerControls")]
        public string BookmarkLabel => (string)GetValue(BookmarkLabelProperty);

        private static readonly DependencyPropertyKey BookmarkCountPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(BookmarkCount), typeof(int), typeof(LogViewer), new PropertyMetadata(0));

        /// <summary>
        /// The <see cref="BookmarkCount"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BookmarkCountProperty = BookmarkCountPropertyKey.DependencyProperty;

        /// <summary>
        /// How many entries have a bookmark
        /// </summary>
        [Category("LogViewerControls")]
        public int BookmarkCount => (int)GetValue(BookmarkCountProperty);

        /// <summary>
        /// Puts a bookmark on the selected entries, or takes it off when all of them have one (Ctrl+B)
        /// </summary>
        public ICommand ToggleBookmarkCommand => _toggleBookmarkCommand ??= new RelayCommand(ToggleBookmarks, () => PART_ListView is { SelectedItems.Count: > 0 });

        /// <summary>
        /// Puts a bookmark on the entry that is the parameter, or takes it off; the menu of a row uses it, as the entry under the mouse is not
        /// always the selected one
        /// </summary>
        public ICommand ToggleEntryBookmarkCommand => _toggleEntryBookmarkCommand ??= new RelayCommand<LogEntry?>(entry =>
        {
            if (entry == null)
                return;

            if (!_bookmarks.Add(entry))
                RemoveBookmark(entry);

            BookmarksChanged();
        });

        /// <summary>
        /// Selects the next entry with a bookmark among the entries that are shown (F2)
        /// </summary>
        public ICommand NextBookmarkCommand => _nextBookmarkCommand ??= new RelayCommand(() => JumpToBookmark(forward: true));

        /// <summary>
        /// Selects the previous entry with a bookmark among the entries that are shown (Shift+F2)
        /// </summary>
        public ICommand PreviousBookmarkCommand => _previousBookmarkCommand ??= new RelayCommand(() => JumpToBookmark(forward: false));

        /// <summary>
        /// Opens the dialog for the comment of the bookmark of an entry (the parameter), or of the selected entry without one
        /// </summary>
        public ICommand EditBookmarkCommentCommand => _editBookmarkCommentCommand ??= new RelayCommand<LogEntry?>(EditBookmarkComment);

        /// <summary>
        /// Takes all the bookmarks off; when some have a comment it asks first, as the comments are lost
        /// </summary>
        public ICommand ClearBookmarksCommand => _clearBookmarksCommand ??= new RelayCommand(AskAndClearBookmarks, () => BookmarkCount > 0);

        /// <summary>
        /// Whether the entry has a bookmark
        /// </summary>
        public bool IsBookmarked(LogEntry entry) => _bookmarks.Contains(entry);

        /// <summary>
        /// The comment of the bookmark of an entry; empty when there is none
        /// </summary>
        public string BookmarkCommentOf(LogEntry entry) => _bookmarkComments.GetValueOrDefault(entry) ?? string.Empty;

        /// <summary>
        /// Puts a bookmark on the selected entries, or takes it off when all of them have one
        /// </summary>
        public void ToggleBookmarks()
        {
            var entries = SelectedEntries();
            if (entries.Count == 0)
                return;

            if (entries.All(_bookmarks.Contains))
            {
                foreach (var entry in entries)
                    RemoveBookmark(entry);
            }
            else
            {
                foreach (var entry in entries)
                    _bookmarks.Add(entry);
            }

            BookmarksChanged();
        }

        /// <summary>
        /// Gives the bookmark of an entry a comment (it is bookmarked if it was not); an empty text takes the comment off
        /// </summary>
        public void SetBookmarkComment(LogEntry entry, string? comment)
        {
            _bookmarks.Add(entry);
            if (string.IsNullOrWhiteSpace(comment))
                _bookmarkComments.Remove(entry);
            else
                _bookmarkComments[entry] = comment.Trim();

            BookmarksChanged();
        }

        /// <summary>
        /// Takes all the bookmarks off
        /// </summary>
        public void ClearBookmarks()
        {
            _bookmarks.Clear();
            _bookmarkComments.Clear();
            _bookmarkEnd = null;
            BookmarksChanged();
        }

        /// <summary>
        /// Selects the next or the previous entry with a bookmark in the order that the list shows, starting at the selected entry, or at the top or the
        /// bottom of the list when none is selected. At the end of the bookmarks the selection stays and <see cref="JumpText"/> says so; the same jump
        /// again goes on at the other end.
        /// </summary>
        /// <returns>Whether an entry was selected</returns>
        public bool JumpToBookmark(bool forward)
        {
            if (PART_ListView is not { } listView)
                return false;

            var items = listView.Items;
            var selected = listView.SelectedItem as LogEntry;
            var step = forward ? 1 : -1;
            var start = selected != null ? items.IndexOf(selected) : forward ? -1 : items.Count;
            for (var i = start + step; i >= 0 && i < items.Count; i += step)
            {
                if (items[i] is LogEntry { } entry && _bookmarks.Contains(entry))
                {
                    _bookmarkEnd = null;
                    SelectForJump(entry);
                    return true;
                }
            }

            // Only here, so a jump to a near bookmark does not pass through the whole list.
            var shown = items.OfType<LogEntry>().Where(_bookmarks.Contains).ToList();
            if (shown.Count == 0)
            {
                ShowJumpText(LogViewerTexts.Get("Jump_NoBookmarks"));
                return false;
            }

            // At the end: the first time it says so, the next time of the same jump it goes on at the other end.
            if (selected != null && _bookmarkEnd is { } end && end.Forward == forward && ReferenceEquals(end.Entry, selected))
            {
                _bookmarkEnd = null;
                SelectForJump(forward ? shown[0] : shown[^1]);
                return true;
            }

            _bookmarkEnd = selected == null ? null : (forward, selected);
            ShowJumpText(LogViewerTexts.Get(forward ? "Jump_BookmarkEnd" : "Jump_BookmarkStart"));
            return false;
        }

        /// <summary>
        /// Ctrl+B puts a bookmark on the selected entries, F2 and Shift+F2 jump to the next and the previous one
        /// </summary>
        private void AddBookmarkKeyBindings()
        {
            InputBindings.Add(new KeyBinding(ToggleBookmarkCommand, Key.B, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(NextBookmarkCommand, Key.F2, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(PreviousBookmarkCommand, Key.F2, ModifierKeys.Shift));
        }

        private void GoToBookmark(BookmarkItem? item)
        {
            if (item == null || PART_ListView is not { } listView)
                return;

            if (listView.Items.Contains(item.Entry))
            {
                SelectForJump(item.Entry);
                // The list is a window of its own; the entry is in the main window, so that comes to the front.
                Window.GetWindow(this)?.Activate();
            }
            else
            {
                ShowJumpText(LogViewerTexts.Get("Bm_Hidden"));
            }
        }

        private void AskAndClearBookmarks()
        {
            if (_bookmarkComments.Count > 0
                && MessageBox.Show(DialogOwner(), LogViewerTexts.Format("Bm_ClearAsk", _bookmarks.Count, _bookmarkComments.Count),
                    LogViewerTexts.Get("Bm_ListTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ClearBookmarks();
        }

        // The list window owns a dialog only while the user works in it; a minimized owner would leave the dialog out of sight.
        private Window? DialogOwner() => _bookmarkWindow is { IsActive: true } list ? list : Window.GetWindow(this);

        private void ShowBookmarkList()
        {
            if (_bookmarkWindow == null)
            {
                RefreshBookmarkItems();
                _bookmarkWindow = new BookmarkListWindow(this) { Owner = Window.GetWindow(this) };
                _bookmarkWindow.Closed += (_, _) =>
                {
                    _bookmarkWindow = null;
                    Unloaded -= CloseBookmarkList;
                };
                // The list shows the bookmarks of this viewer, so it goes when the viewer leaves the screen, e.g. with the tab.
                Unloaded += CloseBookmarkList;
                _bookmarkWindow.Show();
            }
            else
            {
                if (_bookmarkWindow.WindowState == WindowState.Minimized)
                    _bookmarkWindow.WindowState = WindowState.Normal;
                _bookmarkWindow.Activate();
            }
        }

        private void CloseBookmarkList(object sender, RoutedEventArgs e) => _bookmarkWindow?.Close();

        /// <summary>
        /// Makes the rows of the list of the bookmarks: every bookmark in the order of the entries, also the ones that a filter hides
        /// </summary>
        internal void RefreshBookmarkItems()
        {
            var time = TimeStampResolver ?? new Resolver.TimeStampResolver();
            var items = new List<BookmarkItem>(_bookmarks.Count);
            foreach (var entry in GetEffectiveEntries())
            {
                if (!_bookmarks.Contains(entry))
                    continue;

                var line = entry.Message.Split('\n', 2)[0].TrimEnd('\r');
                items.Add(new BookmarkItem(entry, BookmarkCommentOf(entry), time.Resolve(entry), LogViewerTexts.Get("Level_" + entry.Level), entry.LoggerName, line));
            }

            SetValue(BookmarkItemsPropertyKey, items);
        }

        private void EditBookmarkComment(LogEntry? entry)
        {
            entry ??= SelectedEntries().FirstOrDefault();
            if (entry == null)
                return;

            var dialog = new CommentDialog(BookmarkCommentOf(entry)) { Owner = DialogOwner() };
            // An empty text on an entry without a bookmark would only make an empty bookmark.
            if (dialog.ShowDialog() == true && (_bookmarks.Contains(entry) || !string.IsNullOrWhiteSpace(dialog.Comment)))
                SetBookmarkComment(entry, dialog.Comment);
        }

        /// <summary>
        /// The selected entries in the order that the list shows them (the selection itself is in the order of the clicks)
        /// </summary>
        private List<LogEntry> SelectedEntries()
        {
            if (PART_ListView is not { SelectedItems.Count: > 0 } listView)
                return new List<LogEntry>();

            var selected = listView.SelectedItems.OfType<LogEntry>().ToHashSet();
            return listView.Items.OfType<LogEntry>().Where(selected.Contains).ToList();
        }

        private void RemoveBookmark(LogEntry entry)
        {
            _bookmarks.Remove(entry);
            _bookmarkComments.Remove(entry);
        }

        private void BookmarksChanged()
        {
            SetValue(BookmarkVersionPropertyKey, BookmarkVersion + 1);
            SetValue(BookmarkCountPropertyKey, _bookmarks.Count);
            UpdateBookmarkLabel();
            if (_bookmarkWindow != null)
                RefreshBookmarkItems();

            ((RelayCommand?)_toggleBookmarkCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand?)_clearBookmarksCommand)?.RaiseCanExecuteChanged();
        }

        private void UpdateBookmarkLabel()
        {
            var label = _bookmarks.Count == 0 ? string.Empty : _bookmarks.Count.ToString(CultureInfo.InvariantCulture);
            if (PART_ListView is { SelectedItems.Count: 1, SelectedItem: LogEntry selected } && _bookmarks.Contains(selected))
            {
                // The place in the order that the list shows, like F2 jumps; a bookmark that a filter hides only counts in the number.
                // One pass up to the selected entry, only for a selected bookmark.
                var place = 0;
                foreach (var entry in PART_ListView.Items.OfType<LogEntry>())
                {
                    if (_bookmarks.Contains(entry))
                        place++;

                    if (ReferenceEquals(entry, selected))
                        break;
                }

                label = $"{place.ToString(CultureInfo.InvariantCulture)}/{label}";
            }

            SetValue(BookmarkLabelPropertyKey, label);
        }

        /// <summary>
        /// A double click on the row of a bookmark opens its comment, to read it or to change it
        /// </summary>
        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(PART_ListView, source) is ListViewItem { Content: LogEntry entry }
                && _bookmarks.Contains(entry))
            {
                e.Handled = true;
                EditBookmarkComment(entry);
            }
        }

        /// <summary>
        /// Entries that left the list lose their bookmark
        /// </summary>
        private void ForgetBookmarks(NotifyCollectionChangedEventArgs e)
        {
            if (_bookmarks.Count == 0)
                return;

            if (e.Action == NotifyCollectionChangedAction.Reset)
                ClearBookmarks();
            else if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace && e.OldItems != null)
            {
                var any = false;
                foreach (LogEntry old in e.OldItems)
                    any |= _bookmarks.Contains(old);

                foreach (LogEntry old in e.OldItems)
                    RemoveBookmark(old);

                if (any)
                    BookmarksChanged();
            }
        }

        /// <summary>
        /// Ctrl+C in the list copies the selected rows, one line per entry, written like the filtered list that is saved to a file
        /// </summary>
        private void AddCopyBinding(ListView listView)
        {
            listView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
                (_, e) =>
                {
                    CopySelectedRows();
                    e.Handled = true;
                },
                (_, e) =>
                {
                    e.CanExecute = listView.SelectedItems.Count > 0;
                    e.Handled = true;
                }));
        }

        /// <summary>
        /// Puts the selected rows on the clipboard, in the order of the list
        /// </summary>
        /// <returns>The text that was copied; empty when no row is selected</returns>
        public string CopySelectedRows()
        {
            var entries = SelectedEntries();
            if (entries.Count == 0)
                return string.Empty;

            // shortcut: all rows are joined on the UI thread, so a copy of a million rows freezes the app for a while; upgrade when a user copies huge lists
            var text = string.Join(Environment.NewLine, FormatForExport(entries));
            try
            {
                // The clipboard may be in use by another program for a moment; the data object keeps the text after the program ends.
                Clipboard.SetDataObject(text, copy: true);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // shortcut: a clipboard that stays locked loses the copy silently; upgrade: tell the user when it comes up
            }

            return text;
        }
    }
}
