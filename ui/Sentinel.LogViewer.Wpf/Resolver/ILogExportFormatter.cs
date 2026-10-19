using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Interface for formatting log entries for export
    /// </summary>
    public interface ILogExportFormatter
    {
        /// <summary>
        /// Formats a log entry into a string representation for export
        /// </summary>
        /// <param name="logEntry">The log entry to format</param>
        /// <param name="timeStampResolver">Resolver for timestamp formatting</param>
        /// <param name="loggerNameResolver">Resolver for logger name formatting</param>
        /// <param name="messageResolver">Resolver for message formatting</param>
        /// <returns>The formatted log entry as a string</returns>
        string Format(
            LogEntry logEntry,
            ILogEntryResolver timeStampResolver,
            ILogEntryResolver loggerNameResolver,
            ILogEntryResolver messageResolver);
    }
}


