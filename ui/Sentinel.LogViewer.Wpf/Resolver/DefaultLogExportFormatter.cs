using NLog;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Default implementation of ILogExportFormatter that formats log entries
    /// in the standard format: yyyy-MM-dd HH:mm:ss.ffff | LEVEL | LoggerName | Message
    /// </summary>
    public class DefaultLogExportFormatter : ILogExportFormatter
    {
        /// <summary>
        /// Formats a log event info into a string representation for export
        /// </summary>
        /// <param name="logEventInfo">The log event to format</param>
        /// <param name="timeStampResolver">Resolver for timestamp formatting</param>
        /// <param name="loggerNameResolver">Resolver for logger name formatting</param>
        /// <param name="messageResolver">Resolver for message formatting</param>
        /// <returns>The formatted log entry as a string</returns>
        public string Format(
            LogEventInfo logEventInfo,
            ILogEventInfoResolver timeStampResolver,
            ILogEventInfoResolver loggerNameResolver,
            ILogEventInfoResolver messageResolver)
        {
            // Format: yyyy-MM-dd HH:mm:ss.ffff | LEVEL | LoggerName | Message
            var timestamp = logEventInfo.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            var level = logEventInfo.Level.Name.ToUpper();
            var loggerName = loggerNameResolver?.Resolve(logEventInfo) ?? logEventInfo.LoggerName ?? "Unknown";
            var message = messageResolver?.Resolve(logEventInfo) ?? logEventInfo.FormattedMessage ?? string.Empty;

            return $"{timestamp} | {level} | {loggerName} | {message}";
        }
    }
}


