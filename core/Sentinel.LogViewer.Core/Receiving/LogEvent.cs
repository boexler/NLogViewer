
namespace Sentinel.LogViewer.Core;

public class LogEvent
{
	public required AppInfo AppInfo { get; set; }
	public required LogEntry Entry { get; set; }
}