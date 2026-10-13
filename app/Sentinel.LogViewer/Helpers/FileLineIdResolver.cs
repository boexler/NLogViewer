using NLog;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Shows the line number of entries imported from a text file as ID, and the NLog sequence id otherwise
/// </summary>
public class FileLineIdResolver : ILogEventInfoResolver
{
	private readonly IdResolver _sequenceIdResolver = new();

	public string Resolve(LogEventInfo logEventInfo)
	{
		// HasProperties avoids allocating the property dictionary for every live event.
		if (logEventInfo.HasProperties &&
		    logEventInfo.Properties.TryGetValue(PlainTextParser.FileLinePropertyName, out var fileLine))
		{
			return fileLine?.ToString() ?? string.Empty;
		}

		return _sequenceIdResolver.Resolve(logEventInfo);
	}
}
