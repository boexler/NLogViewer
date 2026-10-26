using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Default implementation of ILogExportFormatter that formats log entries
    /// in the standard format: yyyy-MM-dd HH:mm:ss.ffff | LEVEL | LoggerName | Message
    /// </summary>
    public class DefaultLogExportFormatter : ILogExportFormatter
    {
        /// <summary>
        /// Formats a log entry into a string representation for export
        /// </summary>
        /// <param name="logEntry">The log entry to format</param>
        /// <param name="timeStampResolver">Resolver for timestamp formatting</param>
        /// <param name="loggerNameResolver">Resolver for logger name formatting</param>
        /// <param name="messageResolver">Resolver for message formatting</param>
        /// <returns>The formatted log entry as a string</returns>
        public string Format(
            LogEntry logEntry,
            ILogEntryResolver timeStampResolver,
            ILogEntryResolver loggerNameResolver,
            ILogEntryResolver messageResolver)
        {
            // Format: yyyy-MM-dd HH:mm:ss.ffff | LEVEL | LoggerName | Message
            var shown = timeStampResolver is TimeStampResolver zoned ? zoned.ToDisplay(logEntry) : logEntry.TimeStamp;
            var timestamp = shown.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            var level = logEntry.Level.ToString().ToUpper();
            var loggerName = loggerNameResolver?.Resolve(logEntry) ?? logEntry.LoggerName ?? "Unknown";
            var message = messageResolver?.Resolve(logEntry) ?? logEntry.Message ?? string.Empty;

            return $"{timestamp} | {level} | {loggerName} | {message}";
        }
    }
}


