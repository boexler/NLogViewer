using System;
using System.Collections.Generic;
using System.Globalization;
using NLog;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Targets;

/// <summary>
/// Turns the events of NLog into the log entries of the viewer
/// </summary>
public static class NLogEntryConverter
{
	/// <summary>
	/// Returns the log entry for an event of NLog: the formatted message, the exception as text, the properties of the event and its sequence number
	/// </summary>
	public static LogEntry ToLogEntry(this LogEventInfo logEvent)
	{
		ArgumentNullException.ThrowIfNull(logEvent);

		return new LogEntry(ToLevel(logEvent.Level), logEvent.LoggerName, logEvent.FormattedMessage)
		{
			TimeStamp = logEvent.TimeStamp,
			Exception = logEvent.Exception?.ToString(),
			Properties = ToProperties(logEvent),
			Id = logEvent.SequenceID
		};
	}

	/// <summary>
	/// Returns the level that matches the one of NLog; a level above Fatal (Off) counts as Fatal
	/// </summary>
	public static LogEntryLevel ToLevel(LogLevel level) => (LogEntryLevel)Math.Clamp(level.Ordinal, 0, (int)LogEntryLevel.Fatal);

	private static IReadOnlyDictionary<string, object?> ToProperties(LogEventInfo logEvent)
	{
		if (!logEvent.HasProperties)
			return new Dictionary<string, object?>();

		var properties = new Dictionary<string, object?>(logEvent.Properties.Count, StringComparer.Ordinal);
		foreach (var property in logEvent.Properties)
			properties[Convert.ToString(property.Key, CultureInfo.InvariantCulture) ?? string.Empty] = property.Value;

		return properties;
	}
}
