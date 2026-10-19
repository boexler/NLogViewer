using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class LoggerNameResolver : ILogEntryResolver
    {
        public string Resolve(LogEntry logEntry)
        {
            return logEntry.LoggerName;
        }
    }
}
