using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class IdResolver : ILogEntryResolver
    {
        public string Resolve(LogEntry logEntry)
        {
            return logEntry.Id.ToString();
        }
    }
}
