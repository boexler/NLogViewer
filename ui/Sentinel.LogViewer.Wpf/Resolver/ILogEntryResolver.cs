using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Default interface to resolve a <see cref="LogEntry"/> for your personal needs
    /// </summary>
    public interface ILogEntryResolver
    {
        string Resolve(LogEntry logEntry);
    }
}
