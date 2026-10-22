namespace Sentinel.LogViewer.Extensions.Logging;

/// <summary>
/// The settings of <c>AddLogViewer()</c>
/// </summary>
public sealed class LogViewerLoggerOptions
{
	/// <summary>
	/// The most entries that the buffer holds. A value above zero gives the logging its own buffer of that size, which becomes
	/// <see cref="Sentinel.LogViewer.Core.LogBuffer.Default"/>; with zero the default buffer stays as it is.
	/// </summary>
	public int Capacity { get; set; }
}
