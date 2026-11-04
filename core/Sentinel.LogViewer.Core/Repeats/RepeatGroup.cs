namespace Sentinel.LogViewer.Core;

/// <summary>
/// Entries with the same level, logger and message, where GUIDs, dates, times and long numbers are ignored (<see cref="MessageNormalizer"/>)
/// </summary>
public sealed class RepeatGroup
{
	internal RepeatGroup(LogEntry first)
	{
		Level = first.Level;
		Logger = first.LoggerName;
		Sample = first.Message.Split('\n', 2)[0].TrimEnd('\r');
		First = Last = first;
		Count = 1;
	}

	public LogEntryLevel Level { get; }

	public string Logger { get; }

	/// <summary>The first line of the message of the first entry of the group, as it was written</summary>
	public string Sample { get; }

	public int Count { get; private set; }

	/// <summary>The entry with the earliest time stamp of the group</summary>
	public LogEntry First { get; private set; }

	/// <summary>The entry with the latest time stamp of the group</summary>
	public LogEntry Last { get; private set; }

	internal void Add(LogEntry entry)
	{
		Count++;
		if (entry.TimeStamp < First.TimeStamp)
			First = entry;

		if (entry.TimeStamp >= Last.TimeStamp)
			Last = entry;
	}
}
