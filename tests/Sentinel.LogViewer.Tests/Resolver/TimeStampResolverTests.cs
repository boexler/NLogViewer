using System;
using System.Globalization;
using NLog;
using Sentinel.LogViewer.Wpf.Resolver;
using Xunit;

namespace Sentinel.LogViewer.Tests.Resolver;

/// <summary>
/// Tests for <see cref="TimeStampResolver"/>: the time of the timestamp column uses the 24-hour clock
/// </summary>
[Collection("Culture")]
public class TimeStampResolverTests : IDisposable
{
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

	public TimeStampResolverTests()
	{
		// The separators of the time follow the culture.
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
	}

	public void Dispose() => CultureInfo.CurrentCulture = _culture;

	[Theory]
	[InlineData(2025, 12, 18, 15, 11, 48, 385, "18-12-2025 15:11:48.385")]
	[InlineData(2025, 12, 18, 3, 11, 48, 715, "18-12-2025 03:11:48.715")]
	[InlineData(2025, 12, 18, 0, 5, 9, 7, "18-12-2025 00:05:09.007")]
	[InlineData(2025, 12, 18, 12, 0, 0, 0, "18-12-2025 12:00:00.000")]
	[InlineData(2025, 12, 18, 23, 59, 59, 999, "18-12-2025 23:59:59.999")]
	public void Resolve_ShowsDateAndTimeWithThe24HourClock(int year, int month, int day, int hour, int minute, int second, int millisecond, string expected)
	{
		var entry = new LogEventInfo(LogLevel.Info, "Logger", "message")
		{
			TimeStamp = new DateTime(year, month, day, hour, minute, second, millisecond)
		};

		Assert.Equal(expected, new TimeStampResolver().Resolve(entry));
	}
}
