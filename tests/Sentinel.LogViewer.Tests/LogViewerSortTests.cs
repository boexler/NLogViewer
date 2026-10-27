using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for sorting the list by a column: the steps of a click, the order, new entries, auto-scroll, and where the sort can be undone
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerSortTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerSortTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Charlie", "one"),
		new LogEntry(LogEntryLevel.Error, "Alpha", "two"),
		new LogEntry(LogEntryLevel.Debug, "Bravo", "three")
	};

	private void WithViewer(Action<Wpf.LogViewer, ObservableCollection<LogEntry>> test) => _context.RunOnSta(() =>
	{
		var entries = Entries();
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			WpfTestHelper.Settle();
			test(viewer, entries);
		}
		finally
		{
			window.Close();
		}
	});

	private static string[] Shown(Wpf.LogViewer viewer) => viewer.LogEvents.View.Cast<LogEntry>().Select(entry => entry.Message).ToArray();

	private static GridViewColumnHeader HeaderOf(Wpf.LogViewer viewer, string title) =>
		WpfTestHelper.Descendants<GridViewColumnHeader>(viewer).First(header => header.Column != null && ((string)header.Column.Header).StartsWith(title));

	private static void Click(GridViewColumnHeader header) => header.RaiseEvent(new System.Windows.RoutedEventArgs(GridViewColumnHeader.ClickEvent, header));

	[Fact]
	public void AClickOnAHeader_SortsAscending_ThenDescending_ThenShowsTheOrderOfArrival() =>
		WithViewer((viewer, _) =>
		{
			Click(HeaderOf(viewer, "LoggerName"));
			Assert.Equal(new[] { "two", "three", "one" }, Shown(viewer));
			Assert.Equal("LoggerName ▲", (string)HeaderOf(viewer, "LoggerName").Column.Header);
			Assert.Equal("Sorted by LoggerName ▲", viewer.SortText);

			Click(HeaderOf(viewer, "LoggerName"));
			Assert.Equal(new[] { "one", "three", "two" }, Shown(viewer));
			Assert.Equal("Sorted by LoggerName ▼", viewer.SortText);

			Click(HeaderOf(viewer, "LoggerName"));
			Assert.Equal(new[] { "one", "two", "three" }, Shown(viewer));
			Assert.Equal(("LoggerName", string.Empty, null), ((string)HeaderOf(viewer, "LoggerName").Column.Header, viewer.SortText, viewer.SortKey));
		});

	[Fact]
	public void AnotherColumn_StartsAscendingAgain_AndTheMarkMovesToItsHeader() =>
		WithViewer((viewer, _) =>
		{
			viewer.CycleSort(LogColumns.Logger);
			viewer.CycleSort(LogColumns.Logger);
			viewer.CycleSort(LogColumns.Level);

			Assert.Equal(new[] { "three", "one", "two" }, Shown(viewer));
			Assert.False(viewer.SortDescending);
			Assert.Equal("LoggerName", (string)HeaderOf(viewer, "LoggerName").Column.Header);
			Assert.Equal("Level ▲", (string)HeaderOf(viewer, "Level").Column.Header);
		});

	[Fact]
	public void ANewEntry_TakesItsPlaceInTheSortedList_AndIsInItsPlaceAgainWhenTheSortIsReset() =>
		WithViewer((viewer, entries) =>
		{
			viewer.CycleSort(LogColumns.Logger);

			entries.Add(new LogEntry(LogEntryLevel.Info, "Beta", "four"));
			viewer.AppendLogEntriesBatch(new[] { new LogEntry(LogEntryLevel.Info, "Aardvark", "five") });
			WpfTestHelper.Settle();

			Assert.Equal(new[] { "five", "two", "four", "three", "one" }, Shown(viewer));

			viewer.ResetSort();
			Assert.Equal(new[] { "one", "two", "three", "four", "five" }, Shown(viewer));
		});

	[Fact]
	public void AutoScroll_IsOffWhileTheListIsSorted_AndComesBackWhenTheSortIsReset() =>
		WithViewer((viewer, _) =>
		{
			viewer.AutoScroll = true;

			viewer.CycleSort(LogColumns.Logger);
			Assert.False(viewer.AutoScroll);

			viewer.ResetSort();
			Assert.True(viewer.AutoScroll);

			viewer.AutoScroll = false;
			viewer.CycleSort(LogColumns.Logger);
			viewer.ResetSort();
			Assert.False(viewer.AutoScroll);
		});

	[Fact]
	public void HidingTheColumnOfTheSort_ResetsTheSort() =>
		WithViewer((viewer, _) =>
		{
			viewer.CycleSort(LogColumns.Logger);

			viewer.SetColumnVisible(LogColumns.Logger, false);

			Assert.Null(viewer.SortKey);
			Assert.Equal(new[] { "one", "two", "three" }, Shown(viewer));
		});

	[Fact]
	public void TheSearch_FollowsTheShownOrder() =>
		WithViewer((viewer, _) =>
		{
			viewer.CycleSort(LogColumns.Logger);
			viewer.FindText = "t";
			WpfTestHelper.Settle();

			// From the top of the list the next hit is the second row of the shown order (two, three); in the order of arrival it would be two.
			viewer.FindNext();

			Assert.Equal("three", ((LogEntry)((ListView)viewer.Template.FindName("PART_ListView", viewer)).SelectedItem).Message);
		});

	[Fact]
	public void TheMenuOfAHeader_OffersTheSortAndTheDefaultView() =>
		WithViewer((viewer, _) =>
		{
			var column = HeaderOf(viewer, "LoggerName").Column;
			var headers = viewer.BuildColumnMenu(column, null).Items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
			Assert.Equal(new[] { "Sort ascending", "Sort descending" }, headers.Take(2));
			Assert.DoesNotContain("Default view", headers);

			var menu = viewer.BuildColumnMenu(column, null);
			var descending = menu.Items.OfType<MenuItem>().First(item => (string)item.Header == "Sort descending");
			descending.Command.Execute(null);
			Assert.Equal((LogColumns.Logger, true), (viewer.SortKey, viewer.SortDescending));

			var again = viewer.BuildColumnMenu(column, null).Items.OfType<MenuItem>().ToList();
			Assert.True(again.First(item => (string)item.Header == "Sort descending").IsChecked);
			again.First(item => (string)item.Header == "Default view").Command.Execute(null);
			Assert.Null(viewer.SortKey);
		});

	[Fact]
	public void TheChipOfTheSort_ResetsTheSort() =>
		WithViewer((viewer, _) =>
		{
			viewer.CycleSort(LogColumns.Logger);
			WpfTestHelper.Settle();

			var close = WpfTestHelper.Descendants<Button>(viewer).Single(button => System.Windows.Automation.AutomationProperties.GetName(button) == "Reset column sorting");
			close.Command.Execute(null);

			Assert.Null(viewer.SortKey);
		});
}