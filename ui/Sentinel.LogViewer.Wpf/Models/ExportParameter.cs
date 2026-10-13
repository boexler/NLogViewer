using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Wpf.Models;

/// <summary>
/// Represents the export format options for log export functionality
/// </summary>
public enum ExportFormat
{
	/// <summary>
	/// Standard log file format (*.log)
	/// </summary>
	Log
}

/// <summary>
/// Parameters for exporting log entries to a file
/// </summary>
public class ExportParameter
{
	/// <summary>
	/// Gets or sets the target file path for the export
	/// </summary>
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the export format to use
	/// </summary>
	public ExportFormat Format { get; set; } = ExportFormat.Log;

	/// <summary>
	/// Gets or sets an optional custom formatter to override the default export formatter for this export operation
	/// </summary>
	public ILogExportFormatter? CustomFormatter { get; set; }
}

