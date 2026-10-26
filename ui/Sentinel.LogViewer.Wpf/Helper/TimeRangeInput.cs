using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The times of the time range as the user types and reads them: a clock time in the zone that the viewer shows, kept as a moment in UTC
    /// </summary>
    internal static class TimeRangeInput
    {
        private static readonly Regex WithFraction = new(@"\d{1,2}:\d{2}:\d{2}[.,]\d", RegexOptions.Compiled);
        private static readonly Regex WithSeconds = new(@"\d{1,2}:\d{2}:\d{2}", RegexOptions.Compiled);
        private static readonly Regex WithMinutes = new(@"\d{1,2}:\d{2}", RegexOptions.Compiled);

        /// <summary>
        /// A moment as the text of a box: the date and the time to the second, the milliseconds only when there are some; empty for no moment
        /// </summary>
        public static string Format(DateTime? moment, TimeZoneInfo? zone)
        {
            if (moment is not { } value)
                return string.Empty;

            var shown = EntryTime.ToDisplay(DateTime.SpecifyKind(value, DateTimeKind.Utc), null, zone);
            return shown.ToString(shown.Millisecond == 0 ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads the text of a box. An empty text is no moment. The end of the range is the end of what was typed: with <c>14:30</c> the whole
        /// minute belongs to it, with a date alone the whole day.
        /// </summary>
        public static bool TryParse(string? text, TimeZoneInfo? zone, bool isEnd, out DateTime? moment)
        {
            moment = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.Trim();
            if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var clock) &&
                !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out clock))
                return false;

            clock = DateTime.SpecifyKind(clock, DateTimeKind.Unspecified);
            if (isEnd && !WithFraction.IsMatch(text))
            {
                var step = WithSeconds.IsMatch(text) ? TimeSpan.FromSeconds(1) : WithMinutes.IsMatch(text) ? TimeSpan.FromMinutes(1) : TimeSpan.FromDays(1);
                clock = clock.Add(step).AddTicks(-1);
            }

            moment = EntryTime.ToMoment(clock, zone);
            return true;
        }

        /// <summary>
        /// The range as one line for the viewer, e.g. <c>2025-12-18 14:00:00 – 2025-12-18 15:30:00</c>; with the zone when it is not local time
        /// </summary>
        public static string Describe(DateTime? from, DateTime? to, TimeZoneInfo? zone)
        {
            if (from == null && to == null)
                return string.Empty;

            var text = from != null && to != null
                ? $"{Format(from, zone)} – {Format(to, zone)}"
                : from != null ? $"from {Format(from, zone)}" : $"until {Format(to, zone)}";
            return zone == null || zone.Equals(TimeZoneInfo.Local) ? text : $"{text} ({EntryTime.Label(zone)})";
        }
    }
}
