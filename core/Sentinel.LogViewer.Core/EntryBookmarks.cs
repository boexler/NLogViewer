namespace Sentinel.LogViewer.Core;

/// <summary>
/// The bookmarks of the entries of one list, and their comments. The tab owns it and the viewer shows it, so the bookmarks are still there when
/// the viewer of the tab has not been made yet (a session that was just opened) and they can be saved with the tab.
/// </summary>
public sealed class EntryBookmarks
{
	public HashSet<LogEntry> Entries { get; } = new();

	public Dictionary<LogEntry, string> Comments { get; } = new();

	public int Count => Entries.Count;

	public void Clear()
	{
		Entries.Clear();
		Comments.Clear();
	}
}
