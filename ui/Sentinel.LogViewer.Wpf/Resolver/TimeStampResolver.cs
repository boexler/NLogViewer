using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class TimeStampResolver : ILogEventInfoResolver
    {
        public string Resolve(LogEventInfo logEventInfo)
        {
            return logEventInfo.TimeStamp.ToString("dd-MM-yyyy HH:mm:ss.fff");
        }
    }
}