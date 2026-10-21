using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.Parsers;

/// <summary>
/// Parser for plain text log files
/// </summary>
public class PlainTextParser : IDisposable
{
	private readonly Regex _timestampRegex = new Regex(
		@"(\d{4}-\d{2}-\d{2}[\sT]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private readonly Regex _levelRegex = new Regex(
		@"\b(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|FATAL)\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private bool _disposed;

	/// <summary>Name of the event property that holds the 1-based line number of the entry in its file.</summary>
	public const string FileLinePropertyName = "FileLine";

	/// <summary>
	/// Parses lines using the specified format configuration
	/// </summary>
	/// <param name="lines">All lines of the file, in order</param>
	/// <param name="format">Format configuration to use</param>
	/// <returns>List of parsed log events</returns>
	/// <param name="referenceTime">The time that dates without a year belong to, e.g. when the file was written</param>
	public List<LogEntry> Parse(IEnumerable<string> lines, TextFileFormat? format, DateTime? referenceTime = null)
	{
		if (format != null && format.ColumnMapping.IsValid())
		{
			return ParseWithFormat(lines, format, referenceTime);
		}

		// Fallback to regex-based parsing
		return ParseWithoutFormat(lines, referenceTime);
	}

	/// <summary>
	/// Parses lines using regex-based detection (legacy method)
	/// </summary>
	public List<LogEntry> Parse(IEnumerable<string> lines) => ParseWithoutFormat(lines, null);

	private List<LogEntry> ParseWithoutFormat(IEnumerable<string> lines, DateTime? referenceTime)
	{
		var numberedLines = lines.Select((line, index) => (line, index + 1));
		return ParseEntries(numberedLines, IsNewLogEntry, line => ParseLine(line, referenceTime));
	}

	/// <summary>
	/// Turns records into log entries; a record that does not start a new entry continues the previous message
	/// </summary>
	private static List<LogEntry> ParseEntries(
		IEnumerable<(string Text, int LineNumber)> records,
		Func<string, bool> isNewEntry,
		Func<string, LogEntry?> parseEntry)
	{
		var results = new List<LogEntry>();
		LogEntry? current = null;
		var currentLine = 0;
		StringBuilder? continuation = null;

		void Finish()
		{
			if (current != null)
				results.Add(WithFileLine(current, currentLine, continuation));

			current = null;
			continuation = null;
		}

		foreach (var (text, lineNumber) in records)
		{
			if (isNewEntry(text))
			{
				Finish();

				// Parse new log entry
				current = parseEntry(text);
				currentLine = lineNumber;
			}
			else if (current != null)
			{
				// This is a continuation line - append to current message
				continuation ??= new StringBuilder();
				if (string.IsNullOrWhiteSpace(text))
				{
					// Preserve empty lines in multi-line messages (e.g., stack traces)
					continuation.Append(Environment.NewLine);
				}
				else
				{
					continuation.Append(Environment.NewLine).Append(text);
				}
			}
			// If no current event and line doesn't start new entry, skip it (orphaned continuation line)
		}

		// Add the last event if exists
		Finish();

		return results;
	}

	/// <summary>
	/// Returns the entry with the number of its line in the file and the lines that continue its message
	/// </summary>
	private static LogEntry WithFileLine(LogEntry entry, int lineNumber, StringBuilder? continuation) =>
		new(entry.Level, entry.LoggerName, continuation == null ? entry.Message : entry.Message + continuation)
		{
			TimeStamp = entry.TimeStamp,
			Exception = entry.Exception,
			Id = entry.Id,
			Properties = new Dictionary<string, object?>(entry.Properties) { [FileLinePropertyName] = lineNumber }
		};

	/// <summary>
	/// Determines if a line starts a new log entry by checking for timestamp pattern at the beginning
	/// </summary>
	private bool IsNewLogEntry(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
			return false;

		// Timestamp must be at the start of the line (after trimming whitespace)
		var trimmedLine = line.TrimStart();
		return TimestampParser.TryMatchAtStart(trimmedLine, out _);
	}

	/// <summary>
	/// Parses lines using the format configuration
	/// </summary>
	private List<LogEntry> ParseWithFormat(IEnumerable<string> lines, TextFileFormat format, DateTime? referenceTime)
	{
		var columnNames = format.HasHeaderLine && lines.FirstOrDefault() is { } header
			? SplitLine(header, format).Select(name => name.Trim()).ToArray()
			: Array.Empty<string>();
		return ParseEntries(
			ReadRecords(lines, format),
			record => IsNewLogEntryWithFormat(record, format),
			record => ParseLineWithFormat(record, format, referenceTime, columnNames));
	}

	/// <summary>
	/// Skips the header lines and joins the lines of a quoted multi-line field into one record
	/// </summary>
	private IEnumerable<(string Text, int LineNumber)> ReadRecords(IEnumerable<string> lines, TextFileFormat format)
	{
		StringBuilder? openRecord = null;
		var openRecordLine = 0;
		var lineNumber = 0;

		foreach (var line in lines)
		{
			lineNumber++;
			if (lineNumber <= format.StartLineIndex)
				continue;

			if (openRecord != null)
			{
				// An unterminated quote must not swallow the entries that follow it.
				var startsNewEntry = IsNewLogEntryWithFormat(line, format);
				if (!startsNewEntry)
				{
					openRecord.Append(Environment.NewLine).Append(line);
					if (TextLineSplitter.EndsInsideQuotedField(line, format.Separator, true))
						continue;
				}

				yield return (openRecord.ToString(), openRecordLine);
				openRecord = null;
				if (!startsNewEntry)
					continue;
			}

			if (format.QuotedFields && TextLineSplitter.EndsInsideQuotedField(line, format.Separator, false))
			{
				openRecord = new StringBuilder(line);
				openRecordLine = lineNumber;
			}
			else
			{
				yield return (line, lineNumber);
			}
		}

		if (openRecord != null)
			yield return (openRecord.ToString(), openRecordLine);
	}

	/// <summary>
	/// Determines if a line starts a new log entry when using format-based parsing
	/// </summary>
	private bool IsNewLogEntryWithFormat(string line, TextFileFormat format)
	{
		if (string.IsNullOrWhiteSpace(line))
			return false;

		var parts = SplitLine(line, format);
		if (format.ColumnMapping.TimestampColumn >= 0)
		{
			var timestampText = GetTimestampText(parts, format.ColumnMapping);
			return timestampText != null && TryParseTimestamp(timestampText, format.DateOrder, null, out _);
		}

		// Without a timestamp column, having all mapped columns is the only hint for a new entry.
		return parts.Length > format.ColumnMapping.GetMappedColumns().Max();
	}

	/// <summary>
	/// Parses a single line using the format configuration
	/// </summary>
	private LogEntry? ParseLineWithFormat(string line, TextFileFormat format, DateTime? referenceTime, string[] columnNames)
	{
		try
		{
			var parts = SplitLine(line, format);
			if (parts.Length == 0)
				return null;

			// Extract timestamp
			var timestamp = DateTime.Now;
			var timestampText = GetTimestampText(parts, format.ColumnMapping);
			if (timestampText != null && TryParseTimestamp(timestampText, format.DateOrder, referenceTime, out var dt))
			{
				timestamp = dt;
			}

			// Extract level
			var level = LogEntryLevel.Info;
			if (format.ColumnMapping.LevelColumn >= 0 && 
			    format.ColumnMapping.LevelColumn < parts.Length)
			{
				var levelStr = parts[format.ColumnMapping.LevelColumn].Trim();
				level = ParseLogLevel(levelStr);
			}

			// Extract logger
			var loggerName = "Unknown";
			if (format.ColumnMapping.LoggerColumn >= 0 && 
			    format.ColumnMapping.LoggerColumn < parts.Length)
			{
				loggerName = parts[format.ColumnMapping.LoggerColumn].Trim();
			}

			// Extract message
			var message = string.Empty;
			if (format.ColumnMapping.MessageColumn >= 0 && 
			    format.ColumnMapping.MessageColumn < parts.Length)
			{
				message = parts[format.ColumnMapping.MessageColumn].Trim();
			}
			else
			{
				// If no message column mapped, use the entire line
				message = line;
			}

			return new LogEntry(level, loggerName, message)
			{
				TimeStamp = timestamp,
				Properties = ExtraColumns(parts, format.ColumnMapping, columnNames)
			};
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// The columns of a line that are not mapped to the timestamp, the level, the logger or the message, as properties: named after the header
	/// line when there is one, otherwise <c>Column N</c>; empty values are left out
	/// </summary>
	private static Dictionary<string, object?> ExtraColumns(string[] parts, ColumnMapping mapping, string[] columnNames)
	{
		var extra = new Dictionary<string, object?>();
		var timestampEnd = mapping.TimestampColumn < 0 ? -1 : mapping.TimestampColumn + Math.Max(1, mapping.TimestampColumnCount);
		for (var i = 0; i < parts.Length; i++)
		{
			var isMapped = (i >= mapping.TimestampColumn && i < timestampEnd) || i == mapping.LevelColumn || i == mapping.LoggerColumn || i == mapping.MessageColumn;
			var value = parts[i].Trim();
			if (isMapped || value.Length == 0)
				continue;

			var name = i < columnNames.Length && columnNames[i].Length > 0 ? columnNames[i] : $"Column {i + 1}";
			extra[name] = value;
		}

		return extra;
	}

	/// <summary>
	/// Returns the text of the timestamp of a split line: the timestamp column and the columns that belong to it, joined with a space.
	/// Null when the format has no timestamp column or the line has too few columns.
	/// </summary>
	public static string? GetTimestampText(string[] parts, ColumnMapping mapping)
	{
		var first = mapping.TimestampColumn;
		var count = Math.Max(1, mapping.TimestampColumnCount);
		if (first < 0 || first + count > parts.Length)
			return null;

		return string.Join(" ", parts.Skip(first).Take(count).Select(part => part.Trim()));
	}

	/// <summary>
	/// Splits a line into the columns of the format
	/// </summary>
	public static string[] SplitLine(string line, TextFileFormat format)
	{
		// The message is free text: when no mapped column follows it, the rest of the line belongs to it.
		// Quoted fields already protect separators inside the message.
		var mapping = format.ColumnMapping;
		var messageTakesRest = !format.QuotedFields && mapping.MessageColumn >= 0 &&
		                       mapping.MessageColumn == mapping.GetMappedColumns().Max();
		return TextLineSplitter.Split(line, format.Separator, format.QuotedFields,
			messageTakesRest ? mapping.MessageColumn + 1 : 0);
	}

	private LogEntry? ParseLine(string line, DateTime? referenceTime)
	{
		try
		{
			// Check if line uses pipe-separated format
			if (line.Contains(" | "))
			{
				return ParsePipeSeparatedLine(line);
			}

			// Try to extract timestamp
			var trimmedLine = line.TrimStart();
			var timestamp = TimestampParser.TryMatchAtStart(trimmedLine, out var length) &&
			                TryParseTimestamp(trimmedLine[..length], DateOrder.Auto, referenceTime, out var dt)
				? dt
				: DateTime.Now;

			// Try to extract log level
			var levelMatch = _levelRegex.Match(line);
			var level = levelMatch.Success
				? ParseLogLevel(levelMatch.Value)
				: LogEntryLevel.Info;

			// Extract logger name (try to find pattern like [LoggerName] or (LoggerName))
			var loggerName = "Unknown";
			var loggerMatch = Regex.Match(line, @"[\[\(]([^\])]+)[\]\)]");
			if (loggerMatch.Success)
			{
				loggerName = loggerMatch.Groups[1].Value;
			}

			// Message is the entire line (or remaining part after timestamp/level)
			var message = line;

			return new LogEntry(level, loggerName, message)
			{
				TimeStamp = timestamp
			};
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Parses a pipe-separated log line: timestamp | level | logger | message
	/// </summary>
	private LogEntry? ParsePipeSeparatedLine(string line)
	{
		try
		{
			var parts = line.Split(new[] { " | " }, StringSplitOptions.None);
			
			if (parts.Length < 4)
			{
				// Not enough parts for pipe format, fall back to regular parsing
				return null;
			}

			// Extract timestamp (first part)
			var timestampStr = parts[0].Trim();
			var timestamp = TryParseTimestamp(timestampStr, out var dt) ? dt : DateTime.Now;

			// Extract level (second part)
			var levelStr = parts[1].Trim();
			var level = ParseLogLevel(levelStr);

			// Extract logger (third part)
			var loggerName = parts[2].Trim();
			if (string.IsNullOrWhiteSpace(loggerName))
			{
				loggerName = "Unknown";
			}

			// Extract message (fourth part and beyond, in case message contains " | ")
			var message = string.Join(" | ", parts.Skip(3)).Trim();

			return new LogEntry(level, loggerName, message)
			{
				TimeStamp = timestamp
			};
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Attempts to parse a timestamp string using various common formats; a date like 01/02/2025 is read as the culture of the computer reads it
	/// </summary>
	/// <param name="timestampStr">The timestamp string to parse</param>
	/// <param name="result">The parsed DateTime if successful</param>
	public static bool TryParseTimestamp(string timestampStr, out DateTime result) =>
		TryParseTimestamp(timestampStr, DateOrder.Auto, null, out result);

	/// <summary>
	/// Attempts to parse a timestamp string using various common formats
	/// </summary>
	/// <param name="timestampStr">The timestamp string to parse</param>
	/// <param name="order">How day and month are ordered in a date like 01/02/2025</param>
	/// <param name="referenceTime">The time that a date without a year belongs to</param>
	/// <param name="result">The parsed DateTime if successful</param>
	public static bool TryParseTimestamp(string timestampStr, DateOrder order, DateTime? referenceTime, out DateTime result)
	{
		// Common timestamp formats
		var formats = new[]
		{
			"dd.MM.yyyy HH:mm:ss.fff",       // 27.11.2025 14:53:05.260
			"dd.MM.yyyy HH:mm:ss",           // 27.11.2025 14:53:05
			"yyyy-MM-dd HH:mm:ss.ffff",      // 2025-11-27 14:53:05.2609
			"yyyy-MM-dd HH:mm:ss.fff",       // 2025-11-27 14:53:05.260
			"yyyy-MM-dd HH:mm:ss.ff",        // 2025-11-27 14:53:05.26
			"yyyy-MM-dd HH:mm:ss.f",         // 2025-11-27 14:53:05.2
			"yyyy-MM-dd HH:mm:ss",           // 2025-11-27 14:53:05
			"yyyy-MM-ddTHH:mm:ss.ffff",     // ISO 8601 with microseconds
			"yyyy-MM-ddTHH:mm:ss.fff",       // ISO 8601 with milliseconds
			"yyyy-MM-ddTHH:mm:ss",           // ISO 8601
			"yyyy-MM-ddTHH:mm:ss.ffffZ",     // ISO 8601 with microseconds and UTC (Z)
			"yyyy-MM-ddTHH:mm:ss.fffZ",      // ISO 8601 with milliseconds and UTC (Z)
			"yyyy-MM-ddTHH:mm:ssZ",          // ISO 8601 with UTC (Z)
			"yyyy-MM-dd HH:mm:ss.ffffK",     // With timezone
			"yyyy-MM-dd HH:mm:ss.fffK",      // With timezone
			"yyyy-MM-dd HH:mm:ssK"           // With timezone
		};

		// Try parsing with exact formats first (using InvariantCulture to avoid locale issues)
		foreach (var format in formats)
		{
			// Use RoundtripKind for formats with 'Z' to preserve UTC
			var styles = format.EndsWith("Z")
				? DateTimeStyles.RoundtripKind
				: DateTimeStyles.None;

			if (DateTime.TryParseExact(timestampStr, format, CultureInfo.InvariantCulture, styles, out result))
			{
				return true;
			}
		}

		// Dates with the day or the month first, month names, AM / PM, ctime and syslog
		if (TimestampParser.TryParse(timestampStr, order, referenceTime, out var parsed))
		{
			result = parsed.Value;
			return true;
		}

		// Fallback: Try parsing with InvariantCulture (handles ISO formats better)
		// Use RoundtripKind to preserve UTC for timestamps ending with 'Z'
		var fallbackStyles = timestampStr.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
			? DateTimeStyles.RoundtripKind
			: DateTimeStyles.None;

		if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, fallbackStyles, out result))
		{
			return true;
		}

		// Last resort: Try with current culture (but this may cause issues)
		if (DateTime.TryParse(timestampStr, out result))
		{
			return true;
		}

		result = DateTime.Now;
		return false;
	}

	private LogEntryLevel ParseLogLevel(string levelString)
	{
		return levelString.ToUpperInvariant() switch
		{
			"TRACE" => LogEntryLevel.Trace,
			"DEBUG" => LogEntryLevel.Debug,
			"INFO" => LogEntryLevel.Info,
			"WARN" or "WARNING" => LogEntryLevel.Warn,
			"ERROR" => LogEntryLevel.Error,
			"FATAL" => LogEntryLevel.Fatal,
			_ => LogEntryLevel.Info
		};
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
		}
	}
}