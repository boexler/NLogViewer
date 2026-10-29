using System;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Models
{
    /// <summary>
    /// A row of the list of the bookmarks: the entry with its comment and the texts that the list shows, written like the main list shows them
    /// </summary>
    public sealed class BookmarkItem
    {
        public BookmarkItem(LogEntry entry, string comment, string time, string level, string logger, string message)
        {
            Entry = entry;
            Level = level;
            Comment = comment;
            Time = time;
            Logger = logger;
            Message = message;
        }

        public LogEntry Entry { get; }

        /// <summary>The comment of the bookmark; empty when there is none</summary>
        public string Comment { get; }

        public bool HasComment => Comment.Length > 0;

        /// <summary>The tip of a row: the comment and the whole message</summary>
        public string ToolTipText => HasComment ? Comment + Environment.NewLine + Environment.NewLine + Entry.Message : Entry.Message;

        /// <summary>The time stamp as the main list shows it</summary>
        public string Time { get; }

        /// <summary>The name of the level in the language of the control</summary>
        public string Level { get; }

        public string Logger { get; }

        /// <summary>The first line of the message</summary>
        public string Message { get; }
    }
}
