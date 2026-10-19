using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Shows the line number of entries imported from a text file as ID, and the number of the entry otherwise
/// </summary>
public class FileLineIdResolver : ILogEntryResolver
{
	private readonly IdResolver _sequenceIdResolver = new();

	public string Resolve(LogEntry logEntry)
	{
		if (logEntry.Properties.TryGetValue(PlainTextParser.FileLinePropertyName, out var fileLine))
		{
			return fileLine?.ToString() ?? string.Empty;
		}

		return _sequenceIdResolver.Resolve(logEntry);
	}
}
