using System.Collections.Generic;
using System.Windows.Input;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The functions of the viewer that a host calls from its own menus and buttons. The viewer handles them, so a host sends them to the
    /// viewer of the selected tab (<c>command.Execute(parameter, viewer)</c>); without a viewer they cannot be executed. The keys are the ones of the viewer.
    /// </summary>
    public static class LogViewerCommands
    {
        /// <summary>Opens the dialog for a time and selects the first entry from then on (Ctrl+T)</summary>
        public static readonly RoutedUICommand GoToTime = new("Go to time", nameof(GoToTime), typeof(LogViewerCommands),
            new InputGestureCollection { new KeyGesture(Key.T, ModifierKeys.Control) });

        /// <summary>Opens the dialog for the time range (Ctrl+Shift+T)</summary>
        public static readonly RoutedUICommand ChooseTimeRange = new("Choose the time range", nameof(ChooseTimeRange), typeof(LogViewerCommands),
            new InputGestureCollection { new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Shift) });

        /// <summary>Shows all the entries again</summary>
        public static readonly RoutedUICommand ClearTimeRange = new("Clear the time range", nameof(ClearTimeRange), typeof(LogViewerCommands));

        /// <summary>Shows the last period before the last entry; the parameter is a time span such as <c>1:00:00</c></summary>
        public static readonly RoutedUICommand TimeRangeLast = new("Last period", nameof(TimeRangeLast), typeof(LogViewerCommands));

        /// <summary>Selects the next entry of the level that is the parameter (F4 to F9)</summary>
        public static readonly RoutedUICommand NextLevel = new("Next entry of a level", nameof(NextLevel), typeof(LogViewerCommands));

        /// <summary>Selects the previous entry of the level that is the parameter (Shift+F4 to Shift+F9)</summary>
        public static readonly RoutedUICommand PreviousLevel = new("Previous entry of a level", nameof(PreviousLevel), typeof(LogViewerCommands));

        /// <summary>Puts a bookmark on the selected entries or takes it off (Ctrl+B)</summary>
        public static readonly RoutedUICommand ToggleBookmark = new("Toggle bookmark", nameof(ToggleBookmark), typeof(LogViewerCommands),
            new InputGestureCollection { new KeyGesture(Key.B, ModifierKeys.Control) });

        /// <summary>Selects the next entry with a bookmark (F2)</summary>
        public static readonly RoutedUICommand NextBookmark = new("Next bookmark", nameof(NextBookmark), typeof(LogViewerCommands),
            new InputGestureCollection { new KeyGesture(Key.F2) });

        /// <summary>Selects the previous entry with a bookmark (Shift+F2)</summary>
        public static readonly RoutedUICommand PreviousBookmark = new("Previous bookmark", nameof(PreviousBookmark), typeof(LogViewerCommands),
            new InputGestureCollection { new KeyGesture(Key.F2, ModifierKeys.Shift) });

        /// <summary>Opens the dialog for the comment of the bookmark of the selected entry</summary>
        public static readonly RoutedUICommand EditBookmarkComment = new("Bookmark comment", nameof(EditBookmarkComment), typeof(LogViewerCommands));

        /// <summary>Opens the list of the bookmarks</summary>
        public static readonly RoutedUICommand ShowBookmarks = new("Bookmarks", nameof(ShowBookmarks), typeof(LogViewerCommands));

        /// <summary>Takes all the bookmarks off</summary>
        public static readonly RoutedUICommand ClearBookmarks = new("Remove all bookmarks", nameof(ClearBookmarks), typeof(LogViewerCommands));

        /// <summary>Takes the levels, terms, rules for loggers and the time range off; the fields and the format of the time stamp stay</summary>
        public static readonly RoutedUICommand ResetFilter = new("Reset filter", nameof(ResetFilter), typeof(LogViewerCommands));

        /// <summary>
        /// All the commands that the viewer handles, with <see cref="ApplicationCommands.Find"/> (Ctrl+F) that opens the search bar; a host that
        /// forwards them to the viewer of the selected tab binds each of them
        /// </summary>
        public static IReadOnlyList<RoutedCommand> All { get; } = new RoutedCommand[]
        {
            ApplicationCommands.Find, GoToTime, ChooseTimeRange, ClearTimeRange, TimeRangeLast, NextLevel, PreviousLevel, ResetFilter,
            ToggleBookmark, NextBookmark, PreviousBookmark, EditBookmarkComment, ShowBookmarks, ClearBookmarks
        };
    }
}
