using System;
using System.Linq;
using System.Threading;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests for <see cref="MessageNormalizer"/> and <see cref="RepeatFinder"/>: which messages count as the same, and how they are grouped
/// </summary>
public class RepeatTests
{
	[Theory]
	[InlineData("Order 4f9c2a10-1b2c-4d3e-8f90-a1b2c3d4e5f6 shipped", "Order {guid} shipped")]
	[InlineData("Started on 2026-05-15 at 10:04:37.123", "Started on {date} at {time}")]
	[InlineData("Started on 15.05.2026 at 9:04", "Started on {date} at {time}")]
	[InlineData("Started 2026-05-15T10:04:37Z, ended 2026-05-16T11:09:12.5+02:00", "Started {date}, ended {date}")]
	[InlineData("Session 1523487 closed after 20260515 ms", "Session {number} closed after {number} ms")]
	// Short numbers stay: an HTTP code, a year or a count says something.
	[InlineData("HTTP 404 in 2026 for 3 items", "HTTP 404 in 2026 for 3 items")]
	[InlineData("nothing variable here", "nothing variable here")]
	[InlineData("", "")]
	public void TheVariableParts_AreReplacedByAWord(string message, string expected) =>
		Assert.Equal(expected, MessageNormalizer.Normalize(message));

	private static LogEntry Entry(string message, string logger = "Acme.Shop", LogEntryLevel level = LogEntryLevel.Info, int minute = 0) =>
		new(level, logger, message) { TimeStamp = new DateTime(2026, 5, 15, 10, minute, 0) };

	[Fact]
	public void MessagesThatDifferOnlyInTheirVariableParts_AreOneGroup_TheMostFrequentFirst()
	{
		var groups = RepeatFinder.Group(new[]
		{
			Entry("Timeout after 1523487 ms for order 4f9c2a10-1b2c-4d3e-8f90-a1b2c3d4e5f6", minute: 5),
			Entry("Started"),
			Entry("Timeout after 9999999 ms for order 11111111-2222-3333-4444-555555555555", minute: 1),
			Entry("Timeout after 2222222 ms for order 99999999-2222-3333-4444-555555555555", minute: 9)
		});

		Assert.Equal(2, groups.Count);
		Assert.Equal(3, groups[0].Count);
		Assert.Equal(1, groups[1].Count);

		// First and last are by time, not by the order of the entries; the sample is the first entry as it was written.
		Assert.Equal(1, groups[0].First.TimeStamp.Minute);
		Assert.Equal(9, groups[0].Last.TimeStamp.Minute);
		Assert.StartsWith("Timeout after 1523487 ms", groups[0].Sample);
	}

	[Fact]
	public void AnotherLevelOrLogger_IsAnotherGroup_AndOnlyTheFirstLineIsTheSample()
	{
		var groups = RepeatFinder.Group(new[]
		{
			Entry("same"),
			Entry("same", level: LogEntryLevel.Error),
			Entry("same", logger: "Acme.Db"),
			Entry("first line\r\nsecond line")
		});

		Assert.Equal(4, groups.Count);
		Assert.Contains(groups, group => group.Sample == "first line");
	}

	[Fact]
	public void WithoutEntries_ThereAreNoGroups() => Assert.Empty(RepeatFinder.Group(Array.Empty<LogEntry>()));

	[Fact]
	public void ACancelledGrouping_ReturnsNothing_AndAnUncancelledOneReportsItsProgress()
	{
		var entries = Enumerable.Range(0, 3000).Select(i => Entry("message " + i % 10)).ToList();
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		Assert.Empty(RepeatFinder.Group(entries, cancelled.Token));

		var reported = 0;
		var groups = RepeatFinder.Group(entries, progress: done => reported = done);
		Assert.Equal(10, groups.Count);
		Assert.Equal(2048, reported);
	}
}
