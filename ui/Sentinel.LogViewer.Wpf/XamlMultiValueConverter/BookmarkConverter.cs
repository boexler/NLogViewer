using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.XamlMultiValueConverter
{
    /// <summary>
    /// The mark of a bookmark in the narrow column of a row. The values are the entry, the viewer and the version of the bookmarks (which only
    /// makes the binding read again). The parameter says what is wanted: <c>Visible</c> for the visibility of the mark, <c>Data</c> for its shape
    /// (with lines in it when there is a comment), <c>Comment</c> and <c>CommentVisible</c> for the comment that is written under the message,
    /// otherwise its tool tip, which is the comment, or the word for a bookmark without one.
    /// </summary>
    public class BookmarkConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var marked = values.Length > 1 && values[0] is LogEntry entry && values[1] is LogViewer viewer && viewer.IsBookmarked(entry);
            if ((string?)parameter == "Visible")
                return marked ? Visibility.Visible : Visibility.Collapsed;

            var comment = marked ? ((LogViewer)values[1]).BookmarkCommentOf((LogEntry)values[0]) : string.Empty;
            switch ((string?)parameter)
            {
                case "Data":
                    return comment.Length > 0 ? BookmarkIcon.RibbonWithNote : BookmarkIcon.Ribbon;
                case "Comment":
                    // A comment of several lines would make the row as high; the first line shows, the tip of the mark has all of it.
                    var lines = comment.Split('\n', 2);
                    return lines.Length > 1 ? lines[0].TrimEnd('\r') + " …" : comment;
                case "CommentVisible":
                    return comment.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!marked)
                return null;

            return comment.Length > 0 ? comment : LogViewerTexts.Get("Bm_Marker");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
