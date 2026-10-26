using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the time zones of the time stamps: how the parsers mark them, and what is shown for a zone of the file and a display zone
/// </summary>
public class EntryTimeTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
	private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

	[Fact]
	public void ATimeWithoutAZone_IsReadInTheZoneOfTheFile_AndShownInTheDisplayZone()
	{
		var written = new DateTime(2025, 12, 18, 14, 2, 11, 385);

		var shown = EntryTime.ToDisplay(written, Tokyo, TimeZoneInfo.Utc);

		Assert.Equal(new DateTime(2025, 12, 18, 5, 2, 11, 385), shown);
	}

	[Fact]
	public void WhenTheZoneOfTheFileIsTheDisplayZone_TheTimeIsShownAsItIs()
	{
		var written = new DateTime(2025, 12, 18, 14, 2, 11);

		Assert.Equal(written, EntryTime.ToDisplay(written, Tokyo, Tokyo));
		Assert.Equal(written, EntryTime.ToDisplay(written, null, TimeZoneInfo.Local));
	}

	[Fact]
	public void AMomentIgnoresTheZoneOfTheFile()
	{
		var utc = new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc);
		var local = new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Local);

		Assert.Equal(new DateTime(2025, 12, 18, 19, 0, 0), EntryTime.ToDisplay(utc, Berlin, Tokyo));
		Assert.Equal(local.ToUniversalTime(), EntryTime.ToDisplay(local, Tokyo, TimeZoneInfo.Utc));
	}

	[Fact]
	public void SummerTimeIsTakenIntoAccount_AndAMissingHourDoesNotThrow()
	{
		Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0), EntryTime.ToDisplay(new DateTime(2026, 7, 1, 12, 0, 0), Berlin, TimeZoneInfo.Utc));
		Assert.Equal(new DateTime(2026, 1, 15, 11, 0, 0), EntryTime.ToDisplay(new DateTime(2026, 1, 15, 12, 0, 0), Berlin, TimeZoneInfo.Utc));

		// 02:30 on 29 March 2026 does not exist in Berlin; a log can still say so.
		var shown = EntryTime.ToDisplay(new DateTime(2026, 3, 29, 2, 30, 0), Berlin, TimeZoneInfo.Utc);
		Assert.Equal(new DateTime(2026, 3, 29), shown.Date);
	}

	[Fact]
	public void TheLabelNamesTheZoneShortly()
	{
		Assert.Equal("local", EntryTime.Label(null));
		Assert.Equal("local", EntryTime.Label(TimeZoneInfo.Local));
		Assert.Equal("UTC", EntryTime.Label(TimeZoneInfo.Utc));
		Assert.Equal("UTC+9", EntryTime.Label(Tokyo));
		Assert.Equal("UTC+5:30", EntryTime.Label(TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")));
		Assert.Equal("UTC-8", EntryTime.Label(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")));
	}

	[Fact]
	public void AnUnknownZoneId_IsLocalTime()
	{
		Assert.Null(EntryTime.FindZone(null));
		Assert.Null(EntryTime.FindZone(""));
		Assert.Null(EntryTime.FindZone("No Such Standard Time"));
		Assert.Equal(Tokyo, EntryTime.FindZone("Tokyo Standard Time"));
	}

	[Theory]
	[InlineData("2025-12-18T15:11:48Z")]
	[InlineData("2025-12-18 15:11:48Z")]
	[InlineData("2025-12-18T16:11:48+01:00")]
	[InlineData("2025-12-18 16:11:48+01:00")]
	[InlineData("2025-12-18 16:11:48 +01:00")]
	public void ATimeWithAZone_IsTheSameMomentHoweverItIsWritten(string text)
	{
		Assert.True(PlainTextParser.TryParseTimestamp(text, out var parsed));

		Assert.Equal(DateTimeKind.Utc, parsed.Kind);
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 48, DateTimeKind.Utc), parsed);
	}

	[Fact]
	public void ATimeWithoutAZone_KeepsItsClockTime_WithoutAKind()
	{
		Assert.True(PlainTextParser.TryParseTimestamp("2025-12-18 15:11:48.385", out var parsed));

		Assert.Equal(DateTimeKind.Unspecified, parsed.Kind);
		Assert.Equal(new DateTime(2025, 12, 18, 15, 11, 48, 385), parsed);
	}

	[Fact]
	public void ADateAlone_IsNotTakenForATimeWithAnOffset()
	{
		Assert.True(PlainTextParser.TryParseTimestamp("2025-12-18", out var parsed));

		Assert.Equal(DateTimeKind.Unspecified, parsed.Kind);
		Assert.Equal(new DateTime(2025, 12, 18), parsed);
	}

	[Fact]
	public void ALogEventThatCameOverTheNetwork_IsAMoment()
	{
		var logEvent = new Log4JEvent { Logger = "L", Level = Log4JLevel.Info, Timestamp = 1234567890000, Message = "m" }.ToLogEvent("sender");

		Assert.Equal(DateTimeKind.Utc, logEvent.Entry.TimeStamp.Kind);
		Assert.Equal(new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc), logEvent.Entry.TimeStamp);
	}
}
