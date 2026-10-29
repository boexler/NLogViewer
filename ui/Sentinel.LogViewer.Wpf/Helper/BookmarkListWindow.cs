using System.Windows;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The window with the list of the bookmarks of a viewer; it stays open next to the main window
    /// </summary>
    internal sealed class BookmarkListWindow : Window
    {
        public BookmarkListWindow(LogViewer viewer)
        {
            Title = LogViewerTexts.Get("Bm_ListTitle");
            Width = 1100;
            Height = 420;
            MinWidth = 520;
            MinHeight = 240;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Content = new BookmarkList { DataContext = viewer };
        }
    }
}
