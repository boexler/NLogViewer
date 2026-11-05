using System.Globalization;
using System.Text.Json;

namespace Sentinel.LogViewer.Core.Sessions;

/// <summary>
/// What a session file holds: the tabs of the application with their entries, filters and bookmarks. The settings of the application (ports,
/// language, theme) are not part of it.
/// </summary>
public sealed class SessionData
{
	/// <summary>The version of the file format that this program writes and reads</summary>
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;

	public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

	/// <summary>The index of the tab that was selected; -1 when none</summary>
	public int SelectedTab { get; set; } = -1;

	public List<SessionTab> Tabs { get; set; } = new();
}

/// <summary>
/// One tab of a session
/// </summary>
public sealed class SessionTab
{
	public string Header { get; set; } = string.Empty;

	public string TargetName { get; set; } = string.Empty;

	/// <summary>The key of the application of a tab that receives logs; null for the tab of a file</summary>
	public string? SenderKey { get; set; }

	public string ApplicationName { get; set; } = string.Empty;

	public string Sender { get; set; } = string.Empty;

	public int ColourIndex { get; set; } = -1;

	public int MaxCount { get; set; }

	/// <summary>What happens when the application starts again, as the name of the setting</summary>
	public string RestartBehavior { get; set; } = string.Empty;

	public bool TakesOverFilters { get; set; } = true;

	public LogViewerFilterState? Filters { get; set; }

	/// <summary>The keys of the columns in their order</summary>
	public List<string> Columns { get; set; } = new();

	/// <summary>The id of the time zone of the file; null for the local time</summary>
	public string? FileTimeZone { get; set; }

	/// <summary>The id of the time zone in which the times are shown; null for the local time</summary>
	public string? DisplayTimeZone { get; set; }

	/// <summary>The entries in the order of the list. Written one after the other, so a list of a million entries is not made twice in memory.</summary>
	public IEnumerable<SessionEntry> Entries { get; set; } = Array.Empty<SessionEntry>();

	public List<SessionBookmark> Bookmarks { get; set; } = new();
}

/// <summary>
/// A bookmark of a tab: the place of the entry in <see cref="SessionTab.Entries"/> and the comment
/// </summary>
public sealed class SessionBookmark
{
	public int Index { get; set; }

	public string? Comment { get; set; }
}

/// <summary>
/// One entry in a session file
/// </summary>
public sealed class SessionEntry
{
	public LogEntryLevel Level { get; set; }

	public string Logger { get; set; } = string.Empty;

	public string Message { get; set; } = string.Empty;

	public DateTime Time { get; set; }

	public string? Exception { get; set; }

	public string? Application { get; set; }

	public string? Sender { get; set; }

	public long Id { get; set; }

	public Dictionary<string, object?>? Properties { get; set; }

	public static SessionEntry From(LogEntry entry) => new()
	{
		Level = entry.Level,
		Logger = entry.LoggerName,
		Message = entry.Message,
		Time = entry.TimeStamp,
		Exception = entry.Exception,
		Application = entry.Application.Length == 0 ? null : entry.Application,
		Sender = entry.Sender.Length == 0 ? null : entry.Sender,
		Id = entry.Id,
		Properties = entry.Properties.Count == 0 ? null : entry.Properties.ToDictionary(pair => pair.Key, pair => pair.Value)
	};

	public LogEntry ToLogEntry() => new(Level, Logger, Message)
	{
		TimeStamp = Time,
		Exception = Exception,
		Application = string.Intern(Application ?? string.Empty),
		Sender = string.Intern(Sender ?? string.Empty),
		Id = Id,
		Properties = Properties == null ? new Dictionary<string, object?>() : Properties.ToDictionary(pair => pair.Key, pair => Plain(pair.Value))
	};

	/// <summary>
	/// A value that was read from the file is a JSON element; it becomes the text, number or switch that it was written from
	/// </summary>
	private static object? Plain(object? value)
	{
		if (value is not JsonElement element)
			return value;

		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			// Both arms are boxed: a plain conditional of a long and a double would make the whole number a double.
			JsonValueKind.Number => element.TryGetInt64(out var whole) ? (object)whole : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null => null,
			_ => element.GetRawText()
		};
	}
}
