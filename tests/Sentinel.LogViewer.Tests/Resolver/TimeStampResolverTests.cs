using System;
using System.Globalization;
using NLog;
using Sentinel.LogViewer.Wpf.Models;
using Sentinel.LogViewer.Wpf.Resolver;
using Xunit;

namespace Sentinel.LogViewer.Tests.Resolver;

/// <summary>
/// Tests for <see cref="TimeStampResolver"/>: the formats of the time stamp column
/// </summary>
[Collection("Culture")]
public class TimeStampResolverTests : IDisposable
{
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

	public void Dispose() => CultureInfo.CurrentCulture = _culture;

	private static LogEventInfo Entry(int hour, int minute, int second, int millisecond) =>
		new(LogLevel.Info, "Logger", "message") { TimeStamp = new DateTime(2025, 12, 18, hour, minute, second, millisecond) };

	[Theory]
	[InlineData(15, 11, 48, 385, "2025-12-18 15:11:48.385")]
	[InlineData(3, 11, 48, 715, "2025-12-18 03:11:48.715")]
	[InlineData(0, 5, 9, 7, "2025-12-18 00:05:09.007")]
	public void Iso_UsesThe24HourClock(int hour, int minute, int second, int millisecond, string expected)
	{
		Assert.Equal(expected, new TimeStampResolver(TimeStampFormat.Iso).Resolve(Entry(hour, minute, second, millisecond)));
	}

	[Theory]
	[InlineData(15, 11, 48, 385, "18.12.2025 15:11:48.385")]
	[InlineData(0, 5, 9, 7, "18.12.2025 00:05:09.007")]
	[InlineData(23, 59, 59, 999, "18.12.2025 23:59:59.999")]
	public void German_ShowsDayMonthYearWithThe24HourClock(int hour, int minute, int second, int millisecond, string expected)
	{
		Assert.Equal(expected, new TimeStampResolver(TimeStampFormat.German).Resolve(Entry(hour, minute, second, millisecond)));
	}

	[Theory]
	[InlineData(15, 11, 48, 385, "12/18/2025 03:11:48.385 PM")]
	[InlineData(3, 11, 48, 715, "12/18/2025 03:11:48.715 AM")]
	[InlineData(0, 5, 9, 7, "12/18/2025 12:05:09.007 AM")]
	[InlineData(12, 0, 0, 0, "12/18/2025 12:00:00.000 PM")]
	public void English_ShowsMonthDayYearWithAmAndPm(int hour, int minute, int second, int millisecond, string expected)
	{
		Assert.Equal(expected, new TimeStampResolver(TimeStampFormat.English).Resolve(Entry(hour, minute, second, millisecond)));
	}

	[Theory]
	[InlineData("de-DE", "18.12.2025 15:11:48.385")]
	[InlineData("en-US", "12/18/2025 3:11:48.385 PM")]
	[InlineData("en-GB", "18/12/2025 15:11:48.385")]
	public void System_FollowsTheCultureOfTheComputer(string culture, string expected)
	{
		CultureInfo.CurrentCulture = new CultureInfo(culture);

		Assert.Equal(expected, new TimeStampResolver().Resolve(Entry(15, 11, 48, 385)));
	}

	[Theory]
	[InlineData(TimeStampFormat.Iso)]
	[InlineData(TimeStampFormat.German)]
	[InlineData(TimeStampFormat.English)]
	public void FixedFormats_DoNotDependOnTheCulture(TimeStampFormat format)
	{
		var entry = Entry(15, 11, 48, 385);
		CultureInfo.CurrentCulture = new CultureInfo("de-DE");
		var german = new TimeStampResolver(format).Resolve(entry);

		CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

		Assert.Equal(german, new TimeStampResolver(format).Resolve(entry));
	}
}
