using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Default interface to resolve a <see cref="LogEventInfo"/> for your personal needs
    /// </summary>
    public interface ILogEventInfoResolver
    {
        string Resolve(LogEventInfo logEventInfo);
    }
}
