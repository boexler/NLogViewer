using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Interface for formatting log entries for export
    /// </summary>
    public interface ILogExportFormatter
    {
        /// <summary>
        /// Formats a log event info into a string representation for export
        /// </summary>
        /// <param name="logEventInfo">The log event to format</param>
        /// <param name="timeStampResolver">Resolver for timestamp formatting</param>
        /// <param name="loggerNameResolver">Resolver for logger name formatting</param>
        /// <param name="messageResolver">Resolver for message formatting</param>
        /// <returns>The formatted log entry as a string</returns>
        string Format(
            LogEventInfo logEventInfo,
            ILogEventInfoResolver timeStampResolver,
            ILogEventInfoResolver loggerNameResolver,
            ILogEventInfoResolver messageResolver);
    }
}


