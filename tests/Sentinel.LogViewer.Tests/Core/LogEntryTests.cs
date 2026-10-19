using System;
using System.Collections.Generic;
using NLog;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Targets;
using Xunit;

namespace Sentinel.LogViewer.Tests.Core;

/// <summary>
/// Tests for <see cref="LogEntry"/> and for turning the events of NLog into log entries
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

	[Theory]
	[InlineData("Trace", LogEntryLevel.Trace)]
	[InlineData("Debug", LogEntryLevel.Debug)]
	[InlineData("Info", LogEntryLevel.Info)]
	[InlineData("Warn", LogEntryLevel.Warn)]
	[InlineData("Error", LogEntryLevel.Error)]
	[InlineData("Fatal", LogEntryLevel.Fatal)]
	[InlineData("Off", LogEntryLevel.Fatal)]
	public void TheLevelsOfNLog_BecomeTheSameLevel(string nlogLevel, LogEntryLevel expected)
	{
		Assert.Equal(expected, NLogEntryConverter.ToLevel(LogLevel.FromString(nlogLevel)));
	}

	[Fact]
	public void AnEventOfNLog_BecomesAnEntry_WithTheFormattedMessage_TheTimeAndTheNumber()
	{
		var time = new DateTime(2026, 10, 19, 12, 30, 15, 250);
		var logEvent = new LogEventInfo(LogLevel.Warn, "Acme.Shop", null, "{0} orders in {1} ms", new object[] { 3, 12 }) { TimeStamp = time };

		var entry = logEvent.ToLogEntry();

		Assert.Equal(LogEntryLevel.Warn, entry.Level);
		Assert.Equal("Acme.Shop", entry.LoggerName);
		Assert.Equal("3 orders in 12 ms", entry.Message);
		Assert.Equal(time, entry.TimeStamp);
		Assert.Equal(logEvent.SequenceID, entry.Id);
		Assert.Null(entry.Exception);
		Assert.Empty(entry.Properties);
	}

	[Fact]
	public void TheExceptionOfAnEvent_BecomesItsText()
	{
		var logEvent = new LogEventInfo(LogLevel.Error, "a", "failed") { Exception = new InvalidOperationException("boom") };

		var entry = logEvent.ToLogEntry();

		Assert.Contains("InvalidOperationException", entry.Exception);
		Assert.Contains("boom", entry.Exception);
	}

	[Fact]
	public void ThePropertiesOfAnEvent_AreKeptWithTextKeys()
	{
		var logEvent = new LogEventInfo(LogLevel.Info, "a", "m");
		logEvent.Properties["Thread"] = "worker-1";
		logEvent.Properties[7] = 99;

		var entry = logEvent.ToLogEntry();

		Assert.Equal(new Dictionary<string, object?> { ["Thread"] = "worker-1", ["7"] = 99 }, entry.Properties);
	}
}
