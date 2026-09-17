using System;
using System.Collections.Generic;

namespace Sentinel.LogViewer.Models;

/// <summary>
/// Represents the detected format of a text log file
/// </summary>
public class TextFileFormat
{
	/// <summary>
	/// Gets or sets the separator string used to split columns
	/// </summary>
	public string Separator { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether the first line is a header/info line that should be skipped
	/// </summary>
	public bool HasHeaderLine { get; set; }

	/// <summary>
	/// Gets or sets the line index where actual data starts (0-based)
	/// </summary>
	public int StartLineIndex { get; set; }

	/// <summary>
	/// Gets or sets the column mapping configuration
	/// </summary>
	public ColumnMapping ColumnMapping { get; set; } = new();

	/// <summary>
	/// Gets or sets the number of columns detected in the file
	/// </summary>
	public int ColumnCount { get; set; }
}

/// <summary>
/// Maps column indices to LogEventInfo fields
/// </summary>
public class ColumnMapping
{
	/// <summary>
	/// Gets or sets the column index for the timestamp field (-1 if not mapped)
	/// </summary>
	public int TimestampColumn { get; set; } = -1;

	/// <summary>
	/// Gets or sets the column index for the level field (-1 if not mapped)
	/// </summary>
	public int LevelColumn { get; set; } = -1;

	/// <summary>
	/// Gets or sets the column index for the logger field (-1 if not mapped)
	/// </summary>
	public int LoggerColumn { get; set; } = -1;

	/// <summary>
	/// Gets or sets the column index for the message field (-1 if not mapped)
	/// </summary>
	public int MessageColumn { get; set; } = -1;

	/// <summary>
	/// Gets all mapped column indices
	/// </summary>
	public IEnumerable<int> GetMappedColumns()
	{
		if (TimestampColumn >= 0) yield return TimestampColumn;
		if (LevelColumn >= 0) yield return LevelColumn;
		if (LoggerColumn >= 0) yield return LoggerColumn;
		if (MessageColumn >= 0) yield return MessageColumn;
	}

	/// <summary>
	/// Checks if the mapping is valid (at least one field is mapped)
	/// </summary>
	public bool IsValid()
	{
		return TimestampColumn >= 0 || LevelColumn >= 0 || LoggerColumn >= 0 || MessageColumn >= 0;
	}
}

/// <summary>
/// Configuration for text file format per file pattern
/// </summary>
public class TextFileFormatConfig
{
	/// <summary>
	/// Gets or sets the file pattern (filename or pattern matching)
	/// </summary>
	public string FilePattern { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the format configuration for this pattern
	/// </summary>
	public TextFileFormat Format { get; set; } = new();
}

/// <summary>
/// Collection of text file format configurations
/// </summary>
public class TextFileFormatConfigCollection
{
	/// <summary>
	/// Gets or sets the list of format configurations
	/// </summary>
	public List<TextFileFormatConfig> Formats { get; set; } = new();
}

