namespace Sentinel.LogViewer.Core;

/// <summary>
/// Groups entries that say the same, the most frequent group first
/// </summary>
public static class RepeatFinder
{
	/// <summary>How many entries are grouped before the token is looked at and the progress is reported</summary>
	private const int Chunk = 1024;

	/// <param name="entries">The entries to group, in any order</param>
	/// <param name="cancellation">Stops the work; the result is then empty</param>
	/// <param name="progress">Called every chunk with the number of entries that are done</param>
	/// <returns>Groups by count, the highest first; groups of the same count by their latest entry, the newest first</returns>
	public static IReadOnlyList<RepeatGroup> Group(IEnumerable<LogEntry> entries, CancellationToken cancellation = default, Action<int>? progress = null)
	{
		var groups = new Dictionary<(LogEntryLevel, string, string), RepeatGroup>();
		var done = 0;
		foreach (var entry in entries)
		{
			var key = (entry.Level, entry.LoggerName, MessageNormalizer.Normalize(entry.Message));
			if (groups.TryGetValue(key, out var group))
				group.Add(entry);
			else
				groups[key] = new RepeatGroup(entry);

			if (++done % Chunk == 0)
			{
				if (cancellation.IsCancellationRequested)
					return Array.Empty<RepeatGroup>();

				progress?.Invoke(done);
			}
		}

		return groups.Values.OrderByDescending(group => group.Count).ThenByDescending(group => group.Last.TimeStamp).ToList();
	}
}
