using NLog;

namespace Sentinel.LogViewer.Models;

public class LogEvent
{
	public required AppInfo AppInfo { get; set; }
	public required LogEventInfo LogEventInfo { get; set; }
}