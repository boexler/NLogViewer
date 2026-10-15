using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Service for detecting text file formats and column structures
/// </summary>
public class TextFileFormatDetector
{
	private readonly Regex _timestampRegex = new(
		@"(\d{4}-\d{2}-\d{2}[\sT]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private readonly Regex _levelRegex = new(
		@"\b(TRACE|DEBUG|INFO|WARN|WARNING|ERROR|FATAL)\b",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// Detects the format of a text file
	/// </summary>
	/// <param name="filePath">Path to the text file</param>
	/// <returns>Detected format or null if detection fails</returns>
	public TextFileFormat? DetectFormat(string filePath)
	{
		if (!File.Exists(filePath))
			return null;

		var lines = File.ReadLines(filePath).Take(100).ToList(); // Sample first 100 lines
		if (lines.Count == 0)
			return null;

		// Detect header line
		var hasHeaderLine = DetectHeaderLine(lines);
		var startLineIndex = hasHeaderLine ? 1 : 0;
		var dataLines = lines.Skip(startLineIndex).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

		// Continuation lines (stack traces, quoted multi-line fields) must not count against a separator.
		var entryLines = dataLines.Where(StartsWithTimestamp).ToList();
		if (entryLines.Count > 0)
			dataLines = entryLines;

		if (dataLines.Count == 0)
			return null;

		// Detect separator
		var separator = DetectSeparator(dataLines);
		if (string.IsNullOrEmpty(separator))
			return null;

		// Analyze column structure
		var columnCount = GetColumnCount(dataLines, separator);
		if (columnCount < 1)
			return null;

		// Auto-map columns
		var columnMapping = AutoMapColumns(dataLines, separator, columnCount);

		return new TextFileFormat
		{
			Separator = separator,
			QuotedFields = DetectQuotedFields(dataLines, separator),
			HasHeaderLine = hasHeaderLine,
			StartLineIndex = startLineIndex,
			ColumnCount = columnCount,
			ColumnMapping = columnMapping,
			DateOrder = DetectDateOrder(dataLines, separator, columnMapping)
		};
	}

	/// <summary>
	/// Detects if the first line is a header/info line
	/// </summary>
	private bool DetectHeaderLine(List<string> lines)
	{
		if (lines.Count == 0)
			return false;

		var firstLine = lines[0].Trim();

		// Check if line starts with special characters (header indicators)
		if (firstLine.StartsWith("#") || firstLine.StartsWith("=") || firstLine.StartsWith("-"))
			return true;

		// Check if line contains only special characters or is very short
		if (firstLine.Length < 10 && firstLine.All(c => !char.IsLetterOrDigit(c)))
			return true;

		// Check if first line doesn't match data pattern (no timestamp, no level)
		var hasTimestamp = HasTimestamp(firstLine);
		var hasLevel = _levelRegex.IsMatch(firstLine);

		// If first line has neither timestamp nor level, it's likely a header
		if (!hasTimestamp && !hasLevel)
		{
			// But check if any other line has these patterns
			var hasDataPattern = lines.Skip(1).Any(line =>
				HasTimestamp(line) || _levelRegex.IsMatch(line));

			if (hasDataPattern)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Detects the separator used in the file
	/// </summary>
	private string DetectSeparator(List<string> dataLines)
	{
		if (dataLines.Count == 0)
			return string.Empty;

		// Test different separators
		var separators = new[]
		{
			(" | ", "Pipe with spaces"),
			("|", "Pipe"),
			("\t", "Tab"),
			(",", "Comma"),
			(";", "Semicolon"),
			("  ", "Multiple spaces")
		};

		var bestSeparator = string.Empty;
		var bestScore = 0;
		var bestConsistency = 0.0;

		foreach (var (separator, _) in separators)
		{
			var columnCounts = dataLines
				.Select(line => TextLineSplitter.Split(line, separator).Length)
				.Where(count => count > 1)
				.ToList();

			if (columnCounts.Count == 0)
				continue;

			var avgColumns = columnCounts.Average();
			var consistency = 1.0 - (columnCounts.Select(c => Math.Abs(c - avgColumns)).Average() / avgColumns);
			var score = columnCounts.Count(c => Math.Abs(c - avgColumns) < 1);

			// Prefer separators with high consistency and many matching lines
			if (score > bestScore || (score == bestScore && consistency > bestConsistency))
			{
				bestScore = score;
				bestConsistency = consistency;
				bestSeparator = separator;
			}
		}

		// If we found a good separator, return it
		if (bestScore >= dataLines.Count * 0.7) // At least 70% of lines match
		{
			return bestSeparator;
		}

		return string.Empty;
	}

	private bool HasTimestamp(string line) =>
		_timestampRegex.IsMatch(line) || TimestampParser.TryMatchAtStart(line.TrimStart(), out _);

	private bool StartsWithTimestamp(string line)
	{
		return TimestampParser.TryMatchAtStart(line, out _);
	}

	/// <summary>
	/// Detects whether fields are wrapped in double quotes (CSV style)
	/// </summary>
	public static bool DetectQuotedFields(IEnumerable<string> dataLines, string separator)
	{
		// Only single-character separators are written by CSV-style layouts.
		return separator.Length == 1 &&
		       dataLines.Any(line => line.StartsWith('"') || line.Contains(separator + "\""));
	}

	/// <summary>
	/// Gets the column count for the detected separator
	/// </summary>
	private int GetColumnCount(List<string> dataLines, string separator)
	{
		var columnCounts = dataLines
			.Select(line => TextLineSplitter.Split(line, separator).Length)
			.Where(count => count > 1)
			.ToList();

		if (columnCounts.Count == 0)
			return 0;

		// Return the most common column count
		return columnCounts
			.GroupBy(c => c)
			.OrderByDescending(g => g.Count())
			.First()
			.Key;
	}

	/// <summary>
	/// Finds out from the dates of the sample lines whether the day or the month comes first; Auto when they do not tell
	/// </summary>
	private static DateOrder DetectDateOrder(List<string> dataLines, string separator, ColumnMapping mapping)
	{
		if (mapping.TimestampColumn < 0)
			return DateOrder.Auto;

		var timestamps = dataLines
			.Select(line => PlainTextParser.GetTimestampText(TextLineSplitter.Split(line, separator), mapping))
			.Where(text => text != null)
			.Select(text => text!);
		return TimestampParser.DetectOrder(timestamps);
	}

	private static bool IsTimestampColumn(ColumnMapping mapping, int column) =>
		mapping.TimestampColumn >= 0 && column >= mapping.TimestampColumn && column < mapping.TimestampColumn + mapping.TimestampColumnCount;

	/// <summary>
	/// Automatically maps columns to LogEventInfo fields
	/// </summary>
	private ColumnMapping AutoMapColumns(List<string> dataLines, string separator, int columnCount)
	{
		var mapping = new ColumnMapping();

		// Analyze each column across sample lines
		var sampleLines = dataLines.Take(Math.Min(20, dataLines.Count)).ToList();
		var columnSamples = new List<List<string>>();

		for (int col = 0; col < columnCount; col++)
		{
			var columnData = sampleLines
				.Select(line => TextLineSplitter.Split(line, separator))
				.Where(parts => parts.Length > col)
				.Select(parts => parts[col].Trim())
				.ToList();

			columnSamples.Add(columnData);
		}

		// Find timestamp column: the first columns that together are a date and a time. The longest such run wins,
		// so that "2025-12-18 15:11:48 PM" is not cut off after the time, and a time alone is the last resort.
		var rows = sampleLines.Select(line => TextLineSplitter.Split(line, separator)).ToList();
		for (int col = 0; col < columnSamples.Count && mapping.TimestampColumn < 0; col++)
		{
			for (var count = Math.Min(TimestampParser.MaxColumns, columnSamples.Count - col); count >= 1; count--)
			{
				var complete = rows.Count(parts => parts.Length >= col + count &&
				                                   TimestampParser.TryParse(string.Join(" ", parts.Skip(col).Take(count).Select(part => part.Trim())),
					                                   DateOrder.Auto, null, out var parsed) &&
				                                   parsed.HasDate && parsed.HasTime);
				if (complete >= columnSamples[col].Count * 0.8) // 80% match rate
				{
					mapping.TimestampColumn = col;
					mapping.TimestampColumnCount = count;
					break;
				}
			}
		}

		for (int col = 0; col < columnSamples.Count && mapping.TimestampColumn < 0; col++)
		{
			var timestampMatches = columnSamples[col].Count(sample =>
				sample.Contains(':') && PlainTextParser.TryParseTimestamp(sample, out _));

			if (timestampMatches >= columnSamples[col].Count * 0.8) // 80% match rate
			{
				mapping.TimestampColumn = col;
			}
		}

		// Find level column
		for (int col = 0; col < columnSamples.Count; col++)
		{
			if (IsTimestampColumn(mapping, col))
				continue;

			var levelMatches = columnSamples[col].Count(sample =>
				_levelRegex.IsMatch(sample));

			if (levelMatches >= columnSamples[col].Count * 0.7) // 70% match rate
			{
				mapping.LevelColumn = col;
				break;
			}
		}

		// Find logger column (typically contains dots, namespaces, longer strings)
		for (int col = 0; col < columnSamples.Count; col++)
		{
			if (IsTimestampColumn(mapping, col) || col == mapping.LevelColumn)
				continue;

			var loggerMatches = columnSamples[col].Count(sample =>
				sample.Contains('.') && sample.Length > 10);

			if (loggerMatches >= columnSamples[col].Count * 0.5) // 50% match rate
			{
				mapping.LoggerColumn = col;
				break;
			}
		}

		// With spaces as the separator the words of the message are columns of their own: the message begins
		// right after the other fields and takes the rest of the line.
		if (separator == TextLineSplitter.SpacesSeparator)
		{
			var timestampEnd = mapping.TimestampColumn >= 0 ? mapping.TimestampColumn + mapping.TimestampColumnCount - 1 : -1;
			var firstMessageColumn = Math.Max(timestampEnd, Math.Max(mapping.LevelColumn, mapping.LoggerColumn)) + 1;
			if (firstMessageColumn < columnCount)
				mapping.MessageColumn = firstMessageColumn;
		}

		// Message column is typically the last column or the remaining one
		// Try to find a column that's not mapped and contains longer text
		for (int col = columnSamples.Count - 1; col >= 0 && mapping.MessageColumn < 0; col--)
		{
			if (IsTimestampColumn(mapping, col) || col == mapping.LevelColumn || col == mapping.LoggerColumn)
				continue;

			var avgLength = columnSamples[col].Average(s => s.Length);
			if (avgLength > 10) // Average length > 10 characters
			{
				mapping.MessageColumn = col;
				break;
			}
		}

		// If no message column found, use the last unmapped column
		if (mapping.MessageColumn < 0)
		{
			for (int col = columnSamples.Count - 1; col >= 0; col--)
			{
				if (!IsTimestampColumn(mapping, col) && col != mapping.LevelColumn && col != mapping.LoggerColumn)
				{
					mapping.MessageColumn = col;
					break;
				}
			}
		}

		return mapping;
	}
}
