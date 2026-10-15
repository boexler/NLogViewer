using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Parsers;

/// <summary>
/// How the day and the month are ordered in a date like <c>01/02/2025</c>
/// </summary>
public enum DateOrder
{
	/// <summary>
	/// Not known; a date is read like the culture of the computer reads it, and a number above 12 decides when there is one
	/// </summary>
	Auto,

	/// <summary>
	/// <c>18/12/2025</c> is 18 December
	/// </summary>
	DayFirst,

	/// <summary>
	/// <c>12/18/2025</c> is 18 December
	/// </summary>
	MonthFirst
}

/// <summary>
/// A timestamp that was read, and which parts of it the text contained
/// </summary>
public readonly record struct ParsedTimestamp(DateTime Value, bool HasDate, bool HasTime);

/// <summary>
/// Reads the timestamps of text log files: ISO and numeric dates (day first or month first), dates with month names,
/// times with <c>AM</c> / <c>PM</c>, and the formats of ctime (<c>Thu Dec 18 15:11:48 2025</c>) and syslog (<c>Dec 18 15:11:48</c>).
/// Does not depend on the culture of the computer, except where a date cannot be told apart and the order is not given.
/// </summary>
public static class TimestampParser
{
	/// <summary>
	/// The largest number of columns a timestamp can be spread over, e.g. <c>Thu Dec 18 15:11:48 2025</c>
	/// </summary>
	public const int MaxColumns = 5;

	private const string Time = @"(?<h>\d{1,2}):(?<min>\d{2})(?::(?<s>\d{2})(?:[.,](?<f>\d{1,7}))?)?(?:\s?(?<ap>[AaPp][Mm])\b)?";
	private const string YearMonthDay = @"(?<y>\d{4})[-/.](?<mo>\d{1,2})[-/.](?<d>\d{1,2})";
	private const string NumericDate = @"(?<a>\d{1,2})[-/.](?<b>\d{1,2})[-/.](?<y>\d{4})";
	private const string Month = @"(?<mon>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?";
	private const string Weekday = @"(?:(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)[a-z]*,?\s+)?";
	private const string TimeAfterDate = @"(?:[ T,]+" + Time + ")?";

	private static readonly string[] MonthNames = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

	// Longer shapes come first: ctime has the year after the time, which syslog does not have.
	private static readonly string[] Shapes =
	{
		Weekday + Month + @"\s+(?<d>\d{1,2})\s+" + Time + @"\s+(?<y>\d{4})",
		Weekday + Month + @"\s+(?<d>\d{1,2})(?:st|nd|rd|th)?,?\s+(?<y>\d{4})" + TimeAfterDate,
		Weekday + @"(?<d>\d{1,2})(?:st|nd|rd|th)?\s+" + Month + @",?\s+(?<y>\d{4})" + TimeAfterDate,
		Weekday + YearMonthDay + TimeAfterDate,
		Weekday + NumericDate + TimeAfterDate,
		Month + @"\s+(?<d>\d{1,2})\s+" + Time,
		Time
	};

	private static readonly Regex[] FullMatchers = CreateMatchers(@"\s*$");
	private static readonly Regex[] StartMatchers = CreateMatchers(string.Empty);
	private static readonly Regex NumericDateAnywhere = new(@"(?<!\d)(?<a>\d{1,2})[-/.](?<b>\d{1,2})[-/.]\d{4}(?!\d)", RegexOptions.Compiled);

	/// <summary>
	/// Resolves <see cref="DateOrder.Auto"/> to the order of the culture, <c>DayFirst</c> when that has the year first
	/// </summary>
	public static DateOrder ResolveOrder(DateOrder order, CultureInfo? culture = null)
	{
		if (order != DateOrder.Auto)
			return order;

		var pattern = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.ShortDatePattern;
		var month = pattern.IndexOf('M');
		var startsWithYear = pattern.StartsWith("y", StringComparison.OrdinalIgnoreCase);
		return !startsWithYear && month >= 0 && month < pattern.IndexOf('d') ? DateOrder.MonthFirst : DateOrder.DayFirst;
	}

	/// <summary>
	/// Reads a timestamp
	/// </summary>
	/// <param name="text">The whole text is the timestamp</param>
	/// <param name="order">How day and month are ordered in a date like 01/02/2025</param>
	/// <param name="reference">The time that a date without a year belongs to, e.g. when the file was written; the current time when not given</param>
	public static bool TryParse(string text, DateOrder order, DateTime? reference, out ParsedTimestamp result)
	{
		foreach (var matcher in FullMatchers)
		{
			var match = matcher.Match(text.Trim());
			if (match.Success && TryBuild(match, order, reference, out result))
				return true;
		}

		result = default;
		return false;
	}

	/// <summary>
	/// Returns whether a line starts with a date and a time, and how long that part is. A date or a time alone does not count.
	/// </summary>
	public static bool TryMatchAtStart(string line, out int length)
	{
		foreach (var matcher in StartMatchers)
		{
			var match = matcher.Match(line);
			if (match.Success && match.Groups["h"].Success && (match.Groups["y"].Success || match.Groups["mon"].Success))
			{
				length = match.Length;
				return true;
			}
		}

		length = 0;
		return false;
	}

	/// <summary>
	/// Finds out from the dates themselves whether the day or the month comes first: a first number above 12 can only be a day,
	/// a second one only a day of a month first date. <see cref="DateOrder.Auto"/> when the dates do not tell, or contradict each other.
	/// </summary>
	public static DateOrder DetectOrder(IEnumerable<string> texts)
	{
		var dayFirst = false;
		var monthFirst = false;
		foreach (var text in texts)
		{
			foreach (Match match in NumericDateAnywhere.Matches(text))
			{
				var first = int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
				var second = int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture);
				dayFirst |= first > 12 && second <= 12;
				monthFirst |= second > 12 && first <= 12;
			}
		}

		return dayFirst == monthFirst ? DateOrder.Auto : dayFirst ? DateOrder.DayFirst : DateOrder.MonthFirst;
	}

	private static Regex[] CreateMatchers(string end)
	{
		var matchers = new Regex[Shapes.Length];
		for (var i = 0; i < Shapes.Length; i++)
			matchers[i] = new Regex("^" + Shapes[i] + end, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		return matchers;
	}

	private static bool TryBuild(Match match, DateOrder order, DateTime? reference, out ParsedTimestamp result)
	{
		result = default;
		var now = reference ?? DateTime.Now;
		var hasTime = match.Groups["h"].Success;
		var hasDate = match.Groups["y"].Success || match.Groups["mon"].Success;

		var time = TimeSpan.Zero;
		if (hasTime && !TryReadTime(match, out time))
			return false;

		if (!hasDate)
		{
			result = new ParsedTimestamp(DateTime.Today + time, false, true);
			return true;
		}

		int day, month, year;
		if (match.Groups["mon"].Success)
		{
			month = Array.IndexOf(MonthNames, match.Groups["mon"].Value.ToLowerInvariant()) + 1;
			day = Number(match, "d");
		}
		else if (match.Groups["mo"].Success)
		{
			month = Number(match, "mo");
			day = Number(match, "d");
		}
		else
		{
			(day, month) = OrderDayAndMonth(Number(match, "a"), Number(match, "b"), order);
		}

		try
		{
			if (match.Groups["y"].Success)
			{
				year = Number(match, "y");
				result = new ParsedTimestamp(new DateTime(year, month, day) + time, true, hasTime);
				return true;
			}

			// Without a year (syslog) the date is the latest one that is not later than the reference.
			var date = new DateTime(now.Year, month, day) + time;
			if (reference != null && date > now.AddDays(1))
				date = new DateTime(now.Year - 1, month, day) + time;

			result = new ParsedTimestamp(date, true, true);
			return true;
		}
		catch (ArgumentOutOfRangeException)
		{
			return false;
		}
	}

	/// <summary>
	/// Day and month of a date like 01/02/2025; a number above 12 can only be the day, whatever the order says
	/// </summary>
	private static (int Day, int Month) OrderDayAndMonth(int first, int second, DateOrder order)
	{
		if (first > 12)
			return (first, second);

		if (second > 12)
			return (second, first);

		return ResolveOrder(order) == DateOrder.MonthFirst ? (second, first) : (first, second);
	}

	private static bool TryReadTime(Match match, out TimeSpan time)
	{
		time = default;
		var hour = Number(match, "h");
		var minute = Number(match, "min");
		var second = match.Groups["s"].Success ? Number(match, "s") : 0;
		if (match.Groups["ap"].Success)
		{
			if (hour is < 1 or > 12)
				return false;

			hour = hour % 12 + (match.Groups["ap"].Value.StartsWith("p", StringComparison.OrdinalIgnoreCase) ? 12 : 0);
		}

		if (hour > 23 || minute > 59 || second > 59)
			return false;

		var ticks = 0L;
		if (match.Groups["f"].Success)
			ticks = long.Parse(match.Groups["f"].Value.PadRight(7, '0'), CultureInfo.InvariantCulture);

		time = new TimeSpan(hour, minute, second) + TimeSpan.FromTicks(ticks);
		return true;
	}

	private static int Number(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
}
