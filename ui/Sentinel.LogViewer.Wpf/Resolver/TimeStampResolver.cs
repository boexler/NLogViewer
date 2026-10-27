using System.Collections.Concurrent;
using System.Globalization;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf.Resolver
{
    /// <summary>
    /// Shows the time of an entry with date and milliseconds in one of the <see cref="TimeStampFormat"/>s
    /// </summary>
    public class TimeStampResolver : ILogEntryResolver
    {
        private static readonly ConcurrentDictionary<string, string> SystemPatterns = new();

        public TimeStampResolver(TimeStampFormat format = TimeStampFormat.System, TimeZoneInfo? fileZone = null, TimeZoneInfo? displayZone = null)
        {
            Format = format;
            FileZone = fileZone;
            DisplayZone = displayZone;
        }

        public TimeStampFormat Format { get; }

        /// <summary>The zone in which a time without a zone was written; null for the local time of the computer</summary>
        public TimeZoneInfo? FileZone { get; }

        /// <summary>The zone in which the times are shown; null for the local time of the computer</summary>
        public TimeZoneInfo? DisplayZone { get; }

        /// <summary>Whether the times are shown in another zone than they were written in</summary>
        public bool IsConverting => !IsLocal(FileZone) || !IsLocal(DisplayZone);

        /// <summary>The time of an entry in the display zone</summary>
        public DateTime ToDisplay(LogEntry logEntry) => EntryTime.ToDisplay(logEntry.TimeStamp, FileZone, DisplayZone);

        /// <summary>The time as it is in the entry, with its zone, e.g. <c>2025-12-18 14:02:11.385 (UTC+9)</c></summary>
        public string Original(LogEntry logEntry) =>
            logEntry.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
            " (" + EntryTime.Label(EntryTime.ZoneOf(logEntry.TimeStamp, FileZone)) + ")";

        /// <summary>The label of the zones for the header: <c>UTC</c>, <c>UTC+9 → local</c>, or nothing while nothing is converted</summary>
        public string ZoneLabel
        {
            get
            {
                if (!IsConverting)
                    return string.Empty;

                var display = EntryTime.Label(DisplayZone);
                return IsLocal(FileZone) || Equals(FileZone, DisplayZone) ? display : EntryTime.Label(FileZone) + " \u2192 " + display;
            }
        }

        private static bool IsLocal(TimeZoneInfo? zone) => zone == null || zone.Equals(TimeZoneInfo.Local);

        public string Resolve(LogEntry logEntry) => FormatClock(ToDisplay(logEntry));

        /// <summary>
        /// A clock time (already in the display zone) in the format of this resolver, e.g. for the time range, so that it reads like the list
        /// </summary>
        public string FormatClock(DateTime timeStamp)
        {
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
