using System;
using System.Globalization;
using Sentinel.LogViewer.Parsers;
using Xunit;

namespace Sentinel.LogViewer.Tests.Parsers;

/// <summary>
/// Tests for <see cref="TimestampParser"/>: dates with the day or the month first, month names, AM / PM, ctime and syslog
/// </summary>
[Collection("Culture")]
public class TimestampParserTests : IDisposable
{
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

	public void Dispose() => CultureInfo.CurrentCulture = _culture;

	private static DateTime Parse(string text, DateOrder order = DateOrder.Auto, DateTime? reference = null)
	{
		Assert.True(TimestampParser.TryParse(text, order, reference, out var result), $"'{text}' was not read");
		Assert.True(result.HasDate);
		Assert.True(result.HasTime);
		return result.Value;
	}

	[Theory]
	[InlineData("12/18/2025 3:11:48 PM", "2025-12-18 15:11:48.000")]
	[InlineData("12/18/2025 15:11:48", "2025-12-18 15:11:48.000")]
	[InlineData("18/12/2025 15:11:48", "2025-12-18 15:11:48.000")]
	[InlineData("18.12.2025 15:11:48.385", "2025-12-18 15:11:48.385")]
	[InlineData("18-12-2025 15:11:48.385", "2025-12-18 15:11:48.385")]
	[InlineData("2025/12/18 15:11:48", "2025-12-18 15:11:48.000")]
	[InlineData("2025-12-18 15:11:48,385", "2025-12-18 15:11:48.385")]
	[InlineData("2025-12-18 03:11:48 PM", "2025-12-18 15:11:48.000")]
	[InlineData("Dec 18 2025 15:11:48", "2025-12-18 15:11:48.000")]
	[InlineData("18 Dec 2025 15:11:48", "2025-12-18 15:11:48.000")]
	[InlineData("18th December 2025 15:11", "2025-12-18 15:11:00.000")]
	[InlineData("Dec 18, 2025 3:11:48 PM", "2025-12-18 15:11:48.000")]
	[InlineData("Thu Dec 18 15:11:48 2025", "2025-12-18 15:11:48.000")]
	[InlineData("Thursday, December 18, 2025 3:11 PM", "2025-12-18 15:11:00.000")]
	[InlineData("12/18/2025 3:11:48PM", "2025-12-18 15:11:48.000")]
	[InlineData("12/18/2025 12:00:00 AM", "2025-12-18 00:00:00.000")]
	[InlineData("12/18/2025 12:00:00 PM", "2025-12-18 12:00:00.000")]
	public void TryParse_ReadsTheCommonFormats_WhateverTheCultureOfTheComputer(string text, string expected)
	{
		foreach (var culture in new[] { "de-DE", "en-US", "fr-FR", "ja-JP" })
		{
			CultureInfo.CurrentCulture = new CultureInfo(culture);

			Assert.Equal(expected, Parse(text).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
		}
	}

	[Theory]
	[InlineData("01/02/2025 10:00:00", DateOrder.DayFirst, "2025-02-01")]
	[InlineData("01/02/2025 10:00:00", DateOrder.MonthFirst, "2025-01-02")]
	[InlineData("18/02/2025 10:00:00", DateOrder.MonthFirst, "2025-02-18")]
	[InlineData("02/18/2025 10:00:00", DateOrder.DayFirst, "2025-02-18")]
	public void TryParse_UsesTheGivenOrder_AndAnImpossibleNumberDecidesByItself(string text, DateOrder order, string expectedDate)
	{
		Assert.Equal(expectedDate, Parse(text, order).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}

	[Theory]
	[InlineData("de-DE", "2025-02-01")]
	[InlineData("en-GB", "2025-02-01")]
	[InlineData("en-US", "2025-01-02")]
	[InlineData("sv-SE", "2025-02-01")]
	public void TryParse_WithoutAnOrder_ReadsAnAmbiguousDateLikeTheCultureOfTheComputer(string culture, string expectedDate)
	{
		CultureInfo.CurrentCulture = new CultureInfo(culture);

		Assert.Equal(expectedDate, Parse("01/02/2025 10:00:00").ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}

	[Theory]
	[InlineData("Dec 18 15:11:48", "2025-12-20", "2025-12-18")]
	[InlineData("Dec 18 15:11:48", "2026-01-02", "2025-12-18")]
	[InlineData("Jan 1 10:00:00", "2026-01-02", "2026-01-01")]
	[InlineData("Dec 18 15:11:48", "2026-12-18", "2026-12-18")]
	public void TryParse_ASyslogDateWithoutAYear_BelongsToTheYearOfTheReference(string text, string reference, string expectedDate)
	{
		var result = Parse(text, reference: DateTime.Parse(reference + " 12:00:00", CultureInfo.InvariantCulture));

		Assert.Equal(expectedDate, result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		Assert.Equal(new TimeSpan(result.Hour, result.Minute, result.Second), result.TimeOfDay);
	}

	[Fact]
	public void TryParse_ATimeAlone_HasNoDate()
	{
		Assert.True(TimestampParser.TryParse("15:11:48.385", DateOrder.Auto, null, out var result));

		Assert.False(result.HasDate);
		Assert.True(result.HasTime);
		Assert.Equal(new TimeSpan(0, 15, 11, 48, 385), result.Value.TimeOfDay);
	}

	[Fact]
	public void TryParse_ADateAlone_HasNoTime()
	{
		Assert.True(TimestampParser.TryParse("12/18/2025", DateOrder.Auto, null, out var result));

		Assert.True(result.HasDate);
		Assert.False(result.HasTime);
		Assert.Equal(new DateTime(2025, 12, 18), result.Value);
	}

	[Theory]
	[InlineData("")]
	[InlineData("nonsense")]
	[InlineData("13/13/2025 10:00:00")]
	[InlineData("Dec 32 2025 10:00:00")]
	[InlineData("12/18/2025 25:00:00")]
	[InlineData("12/18/2025 13:00:00 PM")]
	[InlineData("12/18/2025 10:61:00")]
	[InlineData("1766070708")]
	[InlineData("Dec 18 15:11:48 and more")]
	public void TryParse_RejectsWhatIsNotATimestamp(string text)
	{
		Assert.False(TimestampParser.TryParse(text, DateOrder.Auto, null, out _));
	}

	[Theory]
	[InlineData("2025-12-18 15:11:48.385 INFO Acme.Shop message", 23)]
	[InlineData("12/18/2025 3:11:48 PM INFO Acme.Shop message", 21)]
	[InlineData("Dec 18 2025 15:11:48 INFO message", 20)]
	[InlineData("Dec 18 15:11:48 host app: message", 15)]
	[InlineData("Thu Dec 18 15:11:48 2025 INFO message", 24)]
	[InlineData("2025-12-18 15:11:48 Amazon went down", 19)]
	public void TryMatchAtStart_FindsTheDateAndTimeAtTheStartOfALine(string line, int expectedLength)
	{
		Assert.True(TimestampParser.TryMatchAtStart(line, out var length));
		Assert.Equal(expectedLength, length);
	}

	[Theory]
	[InlineData("   at Acme.Shop.Orders.Process() in Orders.cs:line 12")]
	[InlineData("15:11:48 INFO a time without a date")]
	[InlineData("2025-12-18 a date without a time")]
	[InlineData("Exception: something went wrong")]
	[InlineData("")]
	public void TryMatchAtStart_DoesNotTakeContinuationLinesForEntries(string line)
	{
		Assert.False(TimestampParser.TryMatchAtStart(line, out _));
	}

	[Theory]
	[InlineData(new[] { "18/12/2025 10:00:00", "01/02/2025 10:00:00" }, DateOrder.DayFirst)]
	[InlineData(new[] { "12/18/2025 10:00:00", "01/02/2025 10:00:00" }, DateOrder.MonthFirst)]
	[InlineData(new[] { "01/02/2025 10:00:00", "03/04/2025 10:00:00" }, DateOrder.Auto)]
	[InlineData(new[] { "18/12/2025 10:00:00", "12/18/2025 10:00:00" }, DateOrder.Auto)]
	[InlineData(new[] { "2025-12-18 10:00:00", "Dec 18 2025 10:00:00" }, DateOrder.Auto)]
	[InlineData(new string[0], DateOrder.Auto)]
	public void DetectOrder_ReadsTheOrderFromTheDates_AndLeavesItOpenWhenTheyDoNotTell(string[] texts, DateOrder expected)
	{
		Assert.Equal(expected, TimestampParser.DetectOrder(texts));
	}
}
