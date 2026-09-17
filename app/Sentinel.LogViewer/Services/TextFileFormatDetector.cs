using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sentinel.LogViewer.Models;

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
			HasHeaderLine = hasHeaderLine,
			StartLineIndex = startLineIndex,
			ColumnCount = columnCount,
			ColumnMapping = columnMapping
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
		var hasTimestamp = _timestampRegex.IsMatch(firstLine);
		var hasLevel = _levelRegex.IsMatch(firstLine);

		// If first line has neither timestamp nor level, it's likely a header
		if (!hasTimestamp && !hasLevel)
		{
			// But check if any other line has these patterns
			var hasDataPattern = lines.Skip(1).Any(line =>
				_timestampRegex.IsMatch(line) || _levelRegex.IsMatch(line));

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
				.Select(line => SplitLine(line, separator).Length)
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

	/// <summary>
	/// Gets the column count for the detected separator
	/// </summary>
	private int GetColumnCount(List<string> dataLines, string separator)
	{
		var columnCounts = dataLines
			.Select(line => SplitLine(line, separator).Length)
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
				.Select(line => SplitLine(line, separator))
				.Where(parts => parts.Length > col)
				.Select(parts => parts[col].Trim())
				.ToList();

			columnSamples.Add(columnData);
		}

		// Find timestamp column
		for (int col = 0; col < columnSamples.Count; col++)
		{
			var timestampMatches = columnSamples[col].Count(sample =>
				_timestampRegex.IsMatch(sample) && TryParseTimestamp(sample, out _));

			if (timestampMatches >= columnSamples[col].Count * 0.8) // 80% match rate
			{
				mapping.TimestampColumn = col;
				break;
			}
		}

		// Find level column
		for (int col = 0; col < columnSamples.Count; col++)
		{
			if (col == mapping.TimestampColumn)
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
			if (col == mapping.TimestampColumn || col == mapping.LevelColumn)
				continue;

			var loggerMatches = columnSamples[col].Count(sample =>
				sample.Contains('.') && sample.Length > 10);

			if (loggerMatches >= columnSamples[col].Count * 0.5) // 50% match rate
			{
				mapping.LoggerColumn = col;
				break;
			}
		}

		// Message column is typically the last column or the remaining one
		// Try to find a column that's not mapped and contains longer text
		for (int col = columnSamples.Count - 1; col >= 0; col--)
		{
			if (col == mapping.TimestampColumn || col == mapping.LevelColumn || col == mapping.LoggerColumn)
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
				if (col != mapping.TimestampColumn && col != mapping.LevelColumn && col != mapping.LoggerColumn)
				{
					mapping.MessageColumn = col;
					break;
				}
			}
		}

		return mapping;
	}

	/// <summary>
	/// Splits a line by the separator, handling edge cases
	/// </summary>
	private string[] SplitLine(string line, string separator)
	{
		if (separator == "  ") // Multiple spaces
		{
			return line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}

		return line.Split(new[] { separator }, StringSplitOptions.None);
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
			"yyyy-MM-dd HH:mm:ss.ffffK",     // With timezone
			"yyyy-MM-dd HH:mm:ss.fffK",      // With timezone
			"yyyy-MM-dd HH:mm:ssK"           // With timezone
		};

		// Try parsing with exact formats first (using InvariantCulture to avoid locale issues)
		foreach (var format in formats)
		{
			if (DateTime.TryParseExact(timestampStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
			{
				return true;
			}
		}

		// Fallback: Try parsing with InvariantCulture (handles ISO formats better)
		if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
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
}

