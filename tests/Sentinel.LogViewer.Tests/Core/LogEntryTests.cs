using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests for <see cref="LogEntry"/>
/// </summary>
public class LogEntryTests
{
	[Fact]
	public void ANewEntry_HasNoNullTexts_AndNoProperties()
	{
		var entry = new LogEntry(LogEntryLevel.Info, null, null);

		Assert.Equal(string.Empty, entry.LoggerName);
		Assert.Equal(string.Empty, entry.Message);
		Assert.Null(entry.Exception);
		Assert.Empty(entry.Properties);
	}

	[Fact]
	public void EntriesGetAGrowingNumber_UnlessTheSourceGivesItsOwn()
	{
		var first = new LogEntry(LogEntryLevel.Info, "a", "one");
		var second = new LogEntry(LogEntryLevel.Info, "a", "two");
		var fromFile = new LogEntry(LogEntryLevel.Info, "a", "three") { Id = 42 };

		Assert.True(second.Id > first.Id);
		Assert.Equal(42, fromFile.Id);
	}

	[Fact]
	public void TheLevels_AreOrdered_FromTraceToFatal()
	{
		Assert.True(LogEntryLevel.Trace < LogEntryLevel.Debug);
		Assert.True(LogEntryLevel.Warn < LogEntryLevel.Error);
		Assert.True(LogEntryLevel.Error < LogEntryLevel.Fatal);
		Assert.Equal("Warn", LogEntryLevel.Warn.ToString());
	}
}
