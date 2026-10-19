using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Models;

public class LogEvent
{
	public required AppInfo AppInfo { get; set; }
	public required LogEntry Entry { get; set; }
}