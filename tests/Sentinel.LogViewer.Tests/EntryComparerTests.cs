using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Helper;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for <see cref="EntryComparer"/>: the order of the entries by the value of a column
/// </summary>
public class EntryComparerTests
{
	private static LogEntry Entry(string message, LogEntryLevel level = LogEntryLevel.Info, string logger = "A", string? amount = null, DateTime? time = null) =>
		new(level, logger, message)
		{
			TimeStamp = time ?? new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc),
			Properties = amount == null ? new Dictionary<string, object?>() : new Dictionary<string, object?> { ["Amount"] = amount }
		};

	private static string[] Sorted(string key, bool descending, TimeZoneInfo? zone, params LogEntry[] entries) =>
		entries.OrderBy(entry => entry, Comparer(new EntryComparer(key, descending, zone))).Select(entry => entry.Message).ToArray();

	private static IComparer<LogEntry> Comparer(EntryComparer comparer) => System.Collections.Generic.Comparer<LogEntry>.Create((x, y) => comparer.Compare(x, y));

	[Fact]
	public void NumbersComeFirst_AreComparedAsNumbers_AndTextWithoutRegardToCase()
	{
		var entries = new[] { Entry("ten", amount: "10"), Entry("nine", amount: "9"), Entry("b", amount: "b"), Entry("none"), Entry("A", amount: "A") };

		Assert.Equal(new[] { "nine", "ten", "none", "A", "b" }, Sorted(LogColumns.Property("Amount"), false, null, entries));
		Assert.Equal(new[] { "b", "A", "none", "ten", "nine" }, Sorted(LogColumns.Property("Amount"), true, null, entries));
	}

	[Fact]
	public void EqualValues_KeepTheOrderOfArrival_AlsoWhenTheSortIsDescending()
	{
		var first = Entry("first", logger: "X");
		var second = Entry("second", logger: "X");
		var other = Entry("other", logger: "Y");

		Assert.Equal(new[] { "first", "second", "other" }, Sorted(LogColumns.Logger, false, null, second, other, first));
		Assert.Equal(new[] { "other", "first", "second" }, Sorted(LogColumns.Logger, true, null, second, other, first));
	}

	[Fact]
	public void TheLevelIsComparedByItsSeverity_NotByItsName()
	{
		var entries = new[] { Entry("warn", LogEntryLevel.Warn), Entry("error", LogEntryLevel.Error), Entry("debug", LogEntryLevel.Debug), Entry("fatal", LogEntryLevel.Fatal) };

		Assert.Equal(new[] { "debug", "warn", "error", "fatal" }, Sorted(LogColumns.Level, false, null, entries));
	}

	[Fact]
	public void TimeStamps_AreComparedAsMoments_InTheZoneOfTheFile()
	{
		var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
		// 14:00 in a file written in Tokyo is 05:00 UTC, before 10:00 UTC although the clock says later.
		var written = Entry("written", time: new DateTime(2025, 12, 18, 14, 0, 0));
		var utc = Entry("utc", time: new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc));

		Assert.Equal(new[] { "written", "utc" }, Sorted(LogColumns.TimeStamp, false, tokyo, utc, written));
	}
}