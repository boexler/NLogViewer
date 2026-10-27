using System;
using System.Globalization;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The times of the time range as the viewer shows them: a clock time in the zone of the display, kept as a moment in UTC
    /// </summary>
    internal static class TimeRangeInput
    {
        /// <summary>
        /// The clock time of a moment in the zone of the display
        /// </summary>
        public static DateTime ToClock(DateTime moment, TimeZoneInfo? zone) =>
            EntryTime.ToDisplay(DateTime.SpecifyKind(moment, DateTimeKind.Utc), null, zone);

        /// <summary>
        /// A moment as one line, the date and the time to the second, the milliseconds only when there are some; empty for no moment
        /// </summary>
        public static string Format(DateTime? moment, TimeZoneInfo? zone)
        {
            if (moment is not { } value)
                return string.Empty;

            var shown = ToClock(value, zone);
            return shown.ToString(shown.Millisecond == 0 ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The start of the range from a clock time that was picked (to the second). The moment that was there before stays when it lies in that
        /// second, so that a dialog that is closed with OK without a change does not change the range.
        /// </summary>
        public static DateTime StartOf(DateTime clock, TimeZoneInfo? zone, DateTime? before)
        {
            var picked = EntryTime.ToMoment(clock, zone);
            return before is { } old && FloorToSecond(old) == picked ? old : picked;
        }

        /// <summary>
        /// The end of the range from a clock time that was picked: the end of that second, so that the entries of that second are in. The moment
        /// that was there before stays when it lies in that second.
        /// </summary>
        public static DateTime EndOf(DateTime clock, TimeZoneInfo? zone, DateTime? before)
        {
            var picked = EntryTime.ToMoment(clock, zone).AddSeconds(1).AddTicks(-1);
            return before is { } old && FloorToSecond(old).AddSeconds(1).AddTicks(-1) == picked ? old : picked;
        }

        private static DateTime FloorToSecond(DateTime moment) => new(moment.Ticks - moment.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

        /// <summary>
        /// The range as one line for the viewer, e.g. <c>2025-12-18 14:00:00 – 2025-12-18 15:30:00</c>; with the zone when it is not local time
        /// </summary>
        /// <param name="format">Writes a clock time like the list does; without it the date and the time are written as in the example</param>
        public static string Describe(DateTime? from, DateTime? to, TimeZoneInfo? zone, Func<DateTime, string>? format = null)
        {
            if (from == null && to == null)
                return string.Empty;

            string Show(DateTime? moment) => format != null ? format(ToClock(moment!.Value, zone)) : Format(moment, zone);

            var text = from != null && to != null
                ? $"{Show(from)} – {Show(to)}"
                : from != null ? $"from {Show(from)}" : $"until {Show(to)}";
            // Compared by instance: on a computer that runs in UTC, the local zone is equal to UTC, and a chosen UTC must still say so.
            return zone == null || ReferenceEquals(zone, TimeZoneInfo.Local) ? text : $"{text} ({EntryTime.Label(zone)})";
        }
    }
}
