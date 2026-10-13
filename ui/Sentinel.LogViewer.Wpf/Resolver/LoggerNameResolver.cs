using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class LoggerNameResolver : ILogEventInfoResolver
    {
        public string Resolve(LogEventInfo logEventInfo)
        {
            return logEventInfo.LoggerName;
        }
    }
}