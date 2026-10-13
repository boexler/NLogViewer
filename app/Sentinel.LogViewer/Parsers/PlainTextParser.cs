using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
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

	/// <summary>
	/// Parses lines using the specified format configuration
	/// </summary>
	/// <param name="lines">Lines to parse</param>
	/// <param name="format">Format configuration to use</param>
	/// <returns>List of parsed log events</returns>
	public List<LogEventInfo> Parse(string[] lines, TextFileFormat? format)
	{
		if (format != null && format.ColumnMapping.IsValid())
		{
			return ParseWithFormat(lines, format);
		}

		// Fallback to regex-based parsing
		return Parse(lines);
	}

	/// <summary>
	/// Parses lines using regex-based detection (legacy method)
	/// </summary>
	public List<LogEventInfo> Parse(string[] lines)
	{
		var results = new List<LogEventInfo>();
		LogEventInfo? currentEvent = null;

		foreach (var line in lines)
		{
			// Check if this line starts a new log entry (has a timestamp at the beginning)
			var isNewEntry = IsNewLogEntry(line);

			if (isNewEntry)
			{
				// Save previous event if exists
				if (currentEvent != null)
				{
					results.Add(currentEvent);
				}

				// Parse new log entry
				currentEvent = ParseLine(line);
			}
			else if (currentEvent != null)
			{
				// This is a continuation line - append to current message
				if (string.IsNullOrWhiteSpace(line))
				{
					// Preserve empty lines in multi-line messages (e.g., stack traces)
					currentEvent.Message += Environment.NewLine;
				}
				else
				{
					currentEvent.Message += Environment.NewLine + line;
				}
			}
			// If no current event and line doesn't start new entry, skip it (orphaned continuation line)
		}

		// Add the last event if exists
		if (currentEvent != null)
		{
			results.Add(currentEvent);
		}

		return results;
	}

	/// <summary>
	/// Determines if a line starts a new log entry by checking for timestamp pattern at the beginning
	/// </summary>
	private bool IsNewLogEntry(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
			return false;

		// Check if line starts with a timestamp pattern
		var trimmedLine = line.TrimStart();
		var timestampMatch = _timestampRegex.Match(trimmedLine);
		
		// Timestamp must be at the start of the line (after trimming whitespace)
		return timestampMatch.Success && timestampMatch.Index == 0;
	}

	/// <summary>
	/// Parses lines using the format configuration
	/// </summary>
	private List<LogEventInfo> ParseWithFormat(string[] lines, TextFileFormat format)
	{
		var results = new List<LogEventInfo>();
		var dataLines = lines.Skip(format.StartLineIndex).ToList();
		LogEventInfo? currentEvent = null;

		foreach (var line in dataLines)
		{
			// Check if this line starts a new log entry
			var isNewEntry = IsNewLogEntryWithFormat(line, format);

			if (isNewEntry)
			{
				// Save previous event if exists
				if (currentEvent != null)
				{
					results.Add(currentEvent);
				}

				// Parse new log entry
				currentEvent = ParseLineWithFormat(line, format);
			}
			else if (currentEvent != null)
			{
				// This is a continuation line - append to current message
				if (string.IsNullOrWhiteSpace(line))
				{
					// Preserve empty lines in multi-line messages (e.g., stack traces)
					currentEvent.Message += Environment.NewLine;
				}
				else
				{
					currentEvent.Message += Environment.NewLine + line;
				}
			}
			// If no current event and line doesn't start new entry, skip it (orphaned continuation line)
		}

		// Add the last event if exists
		if (currentEvent != null)
		{
			results.Add(currentEvent);
		}

		return results;
	}

	/// <summary>
	/// Determines if a line starts a new log entry when using format-based parsing
	/// </summary>
	private bool IsNewLogEntryWithFormat(string line, TextFileFormat format)
	{
		if (string.IsNullOrWhiteSpace(line))
			return false;

		// If timestamp column is mapped, check if first column matches timestamp pattern
		if (format.ColumnMapping.TimestampColumn >= 0)
		{
			var parts = SplitLine(line, format.Separator);
			if (format.ColumnMapping.TimestampColumn < parts.Length)
			{
				var timestampStr = parts[format.ColumnMapping.TimestampColumn].Trim();
				return TryParseTimestamp(timestampStr, out _);
			}
		}

		// Fallback to general timestamp detection
		return IsNewLogEntry(line);
	}

	/// <summary>
	/// Parses a single line using the format configuration
	/// </summary>
	private LogEventInfo? ParseLineWithFormat(string line, TextFileFormat format)
	{
		try
		{
			var parts = SplitLine(line, format.Separator);
			if (parts.Length == 0)
				return null;

			// Extract timestamp
			var timestamp = DateTime.Now;
			if (format.ColumnMapping.TimestampColumn >= 0 && 
			    format.ColumnMapping.TimestampColumn < parts.Length)
			{
				var timestampStr = parts[format.ColumnMapping.TimestampColumn].Trim();
				if (TryParseTimestamp(timestampStr, out var dt))
				{
					timestamp = dt;
				}
			}

			// Extract level
			var level = LogLevel.Info;
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

			return new LogEventInfo(level, loggerName, message)
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
	/// Splits a line by the separator
	/// </summary>
	private string[] SplitLine(string line, string separator)
	{
		if (separator == "  ") // Multiple spaces
		{
			return line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}

		return line.Split(new[] { separator }, StringSplitOptions.None);
	}

	private LogEventInfo? ParseLine(string line)
	{
		try
		{
			// Check if line uses pipe-separated format
			if (line.Contains(" | "))
			{
				return ParsePipeSeparatedLine(line);
			}

			// Try to extract timestamp
			var timestampMatch = _timestampRegex.Match(line);
			var timestamp = timestampMatch.Success && TryParseTimestamp(timestampMatch.Value, out var dt)
				? dt
				: DateTime.Now;

			// Try to extract log level
			var levelMatch = _levelRegex.Match(line);
			var level = levelMatch.Success
				? ParseLogLevel(levelMatch.Value)
				: LogLevel.Info;

			// Extract logger name (try to find pattern like [LoggerName] or (LoggerName))
			var loggerName = "Unknown";
			var loggerMatch = Regex.Match(line, @"[\[\(]([^\])]+)[\]\)]");
			if (loggerMatch.Success)
			{
				loggerName = loggerMatch.Groups[1].Value;
			}

			// Message is the entire line (or remaining part after timestamp/level)
			var message = line;

			return new LogEventInfo(level, loggerName, message)
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
	private LogEventInfo? ParsePipeSeparatedLine(string line)
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

			return new LogEventInfo(level, loggerName, message)
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
	/// Attempts to parse a timestamp string using various common formats
	/// </summary>
	/// <param name="timestampStr">The timestamp string to parse</param>
	/// <param name="result">The parsed DateTime if successful</param>
	/// <returns>True if parsing was successful</returns>
	private bool TryParseTimestamp(string timestampStr, out DateTime result)
	{
		// Common timestamp formats
		var formats = new[]
		{
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

	private LogLevel ParseLogLevel(string levelString)
	{
		return levelString.ToUpperInvariant() switch
		{
			"TRACE" => LogLevel.Trace,
			"DEBUG" => LogLevel.Debug,
			"INFO" => LogLevel.Info,
			"WARN" or "WARNING" => LogLevel.Warn,
			"ERROR" => LogLevel.Error,
			"FATAL" => LogLevel.Fatal,
			_ => LogLevel.Info
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