using System.Collections.Concurrent;
using System.Globalization;
using NLog;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Shows the time of an entry with date and milliseconds in one of the <see cref="TimeStampFormat"/>s
    /// </summary>
    public class TimeStampResolver : ILogEventInfoResolver
    {
        private static readonly ConcurrentDictionary<string, string> SystemPatterns = new();

        public TimeStampResolver(TimeStampFormat format = TimeStampFormat.System)
        {
            Format = format;
        }

        public TimeStampFormat Format { get; }

        public string Resolve(LogEventInfo logEventInfo)
        {
            var timeStamp = logEventInfo.TimeStamp;
            return Format switch
            {
                TimeStampFormat.Iso => timeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                TimeStampFormat.German => timeStamp.ToString("dd.MM.yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture),
                TimeStampFormat.English => timeStamp.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture),
                _ => timeStamp.ToString(GetSystemPattern(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture)
            };
        }

        /// <summary>
        /// The short date and the long time of the culture, with the milliseconds after the seconds
        /// </summary>
        private static string GetSystemPattern(CultureInfo culture) => SystemPatterns.GetOrAdd(culture.Name, _ =>
        {
            var formatInfo = culture.DateTimeFormat;
            var time = formatInfo.LongTimePattern.Contains("ss")
                ? formatInfo.LongTimePattern.Replace("ss", "ss.fff")
                : formatInfo.LongTimePattern + ".fff";
            return formatInfo.ShortDatePattern + " " + time;
        });
    }
}
