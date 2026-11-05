using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Core.Sessions;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Turns the tabs of the application into a session and back
/// </summary>
public static class SessionTabs
{
	/// <summary>
	/// The tabs as a session. Call it on the thread of the window: it copies what can change while the file is written (the entries that are in the
	/// tab now, the filters, the bookmarks). The entries themselves are written later, and they do not change.
	/// </summary>
	public static SessionData Capture(IReadOnlyList<LogTabViewModel> tabs, LogTabViewModel? selected)
	{
		var data = new SessionData { SelectedTab = selected == null ? -1 : tabs.ToList().IndexOf(selected) };
		foreach (var tab in tabs)
		{
			var entries = tab.LogEntries.ToArray();
			data.Tabs.Add(new SessionTab
			{
				Header = tab.Header,
				TargetName = tab.TargetName,
				SenderKey = tab.SenderKey,
				ApplicationName = tab.ApplicationName,
				Sender = tab.Sender,
				ColourIndex = tab.ColourIndex,
				MaxCount = tab.MaxCount,
				RestartBehavior = tab.RestartBehavior.ToString(),
				TakesOverFilters = tab.TakesOverFilters,
				Filters = tab.FilterState?.Clone(),
				Columns = tab.ColumnKeys.ToList(),
				FileTimeZone = tab.FileTimeZone?.Id,
				DisplayTimeZone = tab.DisplayTimeZone?.Id,
				Entries = entries.Select(SessionEntry.From),
				Bookmarks = BookmarksOf(tab.Bookmarks, entries)
			});
		}

		return data;
	}

	// A bookmark is kept by the place of its entry; one whose entry has left the tab is dropped.
	private static List<SessionBookmark> BookmarksOf(EntryBookmarks bookmarks, LogEntry[] entries)
	{
		if (bookmarks.Count == 0)
			return new List<SessionBookmark>();

		var result = new List<SessionBookmark>(bookmarks.Count);
		for (var index = 0; index < entries.Length; index++)
		{
			if (bookmarks.Entries.Contains(entries[index]))
				result.Add(new SessionBookmark { Index = index, Comment = bookmarks.Comments.GetValueOrDefault(entries[index]) });
		}

		return result;
	}

	/// <summary>
	/// The tab of a session with its entries, filters and bookmarks. It does not touch the window, so a big session can be read in the background.
	/// </summary>
	public static LogTabViewModel ToTab(SessionTab saved)
	{
		var tab = new LogTabViewModel
		{
			Header = saved.Header,
			TargetName = saved.TargetName.Length > 0 ? saved.TargetName : $"FileImport_{Guid.NewGuid()}",
			MaxCount = saved.MaxCount > 0 ? saved.MaxCount : int.MaxValue,
			SenderKey = saved.SenderKey,
			ApplicationName = saved.ApplicationName,
			Sender = saved.Sender,
			ColourIndex = saved.ColourIndex,
			TakesOverFilters = saved.TakesOverFilters,
			RestartBehavior = ApplicationTabService.ParseRestartBehavior(saved.RestartBehavior),
			FilterState = saved.Filters,
			FileTimeZone = Zone(saved.FileTimeZone),
			DisplayTimeZone = Zone(saved.DisplayTimeZone)
		};

		if (saved.Columns.Count > 0)
			tab.ColumnKeys = Wpf.LogColumns.Normalize(saved.Columns);

		// shortcut: the entries go in one by one, which is slow for a million of them (the tab has no viewer yet, so nothing but the count listens);
		// upgrade to building the list first and handing it over once when sessions of that size are common
		var entries = new List<LogEntry>();
		foreach (var entry in saved.Entries)
		{
			var logEntry = entry.ToLogEntry();
			entries.Add(logEntry);
			tab.LogEntries.Add(logEntry);
		}

		foreach (var bookmark in saved.Bookmarks.Where(bookmark => bookmark.Index >= 0 && bookmark.Index < entries.Count))
		{
			var entry = entries[bookmark.Index];
			tab.Bookmarks.Entries.Add(entry);
			if (!string.IsNullOrWhiteSpace(bookmark.Comment))
				tab.Bookmarks.Comments[entry] = bookmark.Comment;
		}

		return tab;
	}

	// A zone that this computer does not know (a session from another system) shows the local time instead.
	private static TimeZoneInfo? Zone(string? id) => id != null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
}
