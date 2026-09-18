using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    public class IdResolver : ILogEventInfoResolver
    {
        public string Resolve(LogEventInfo logEventInfo)
        {
            return logEventInfo.SequenceID.ToString();
        }
    }
}