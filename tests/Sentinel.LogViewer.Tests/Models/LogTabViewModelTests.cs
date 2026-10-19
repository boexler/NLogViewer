using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Models;

/// <summary>
/// Tests for <see cref="LogTabViewModel"/>: the number in the header of the tab is the number of entries in it
/// </summary>
public class LogTabViewModelTests
{
	private static LogEntry Entry(int number) => new(LogEntryLevel.Info, "Logger", "message " + number);

	[Fact]
	public void LogCount_FollowsTheAddedEntries()
	{
		var tab = new LogTabViewModel();

		foreach (var number in Enumerable.Range(0, 3))
			tab.AddLogEntry(Entry(number));

		Assert.Equal(3, tab.LogCount);
	}

	[Fact]
	public void LogCount_IsZeroAfterTheEntriesWereCleared_AsTheViewerDoesWithClear()
	{
		var tab = new LogTabViewModel();
		foreach (var number in Enumerable.Range(0, 24))
			tab.AddLogEntry(Entry(number));

		tab.LogEntries.Clear();

		Assert.Equal(0, tab.LogCount);
	}

	[Fact]
	public void LogCount_DropsWhenEntriesAreRemoved()
	{
		var tab = new LogTabViewModel();
		foreach (var number in Enumerable.Range(0, 5))
			tab.AddLogEntry(Entry(number));

		tab.LogEntries.RemoveAt(0);

		Assert.Equal(4, tab.LogCount);
	}

	[Fact]
	public void LogCount_IsTheNumberOfEntriesThatAreKept_WhenTheOldestAreRemovedAtTheMaximum()
	{
		var tab = new LogTabViewModel { MaxCount = 10 };

		foreach (var number in Enumerable.Range(0, 25))
			tab.AddLogEntry(Entry(number));

		Assert.Equal(10, tab.LogCount);
		Assert.Equal("message 15", tab.LogEntries[0].Message);
	}

	[Fact]
	public void LogCount_IsAnnouncedWhenTheEntriesChange()
	{
		var tab = new LogTabViewModel();
		var announced = new List<string?>();
		tab.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

		tab.AddLogEntry(Entry(1));
		tab.LogEntries.Clear();

		Assert.Equal(2, announced.Count(name => name == nameof(LogTabViewModel.LogCount)));
	}

	[Fact]
	public void LogCount_FollowsTheEntriesThatTheViewerAddsToTheCollectionItself()
	{
		var tab = new LogTabViewModel();

		tab.LogEntries.Add(Entry(1));
		tab.LogEntries.Add(Entry(2));

		Assert.Equal(2, tab.LogCount);
	}
}
