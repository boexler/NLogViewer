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

	private sealed class LineNumberResolver : Wpf.Resolver.ILogEntryResolver
	{
		public string Resolve(LogEntry logEntry) => logEntry.Message.Split(' ')[1];
	}

	[Fact]
	public void TheIdColumn_FollowsAResolverThatIsSetWhileTheListIsSorted() =>
		WithViewer((viewer, entries) =>
		{
			entries.Clear();
			foreach (var message in new[] { "a 2", "a 1", "b 2", "b 1" })
				entries.Add(new LogEntry(LogEntryLevel.Info, "A", message));
			viewer.CycleSort(LogColumns.Id);
			Assert.Equal(new[] { "a 2", "a 1", "b 2", "b 1" }, Shown(viewer));

			viewer.IdResolver = new LineNumberResolver();

			Assert.Equal(new[] { "a 1", "b 1", "a 2", "b 2" }, Shown(viewer));
		});

	[Fact]
	public void TheSort_StaysWhenTheListOfEntriesIsReplaced() =>
		WithViewer((viewer, _) =>
		{
			viewer.CycleSort(LogColumns.Logger);

			viewer.ItemsSource = new ObservableCollection<LogEntry>
			{
				new LogEntry(LogEntryLevel.Info, "Zulu", "z"),
				new LogEntry(LogEntryLevel.Info, "Alpha", "a"),
				new LogEntry(LogEntryLevel.Info, "Mike", "m")
			};
			WpfTestHelper.Settle();

			Assert.Equal(new[] { "a", "m", "z" }, Shown(viewer));
		});

	[Fact]
	public void TheSortByTime_FollowsTheZoneOfTheFile_AndTheChipLeavesTheZoneOut() =>
		WithViewer((viewer, entries) =>
		{
			entries.Clear();
			// 14:00 written in Tokyo is 05:00 UTC, so it is before 10:00 UTC; written in UTC it is after.
			entries.Add(new LogEntry(LogEntryLevel.Info, "A", "written") { TimeStamp = new DateTime(2025, 12, 18, 14, 0, 0) });
			entries.Add(new LogEntry(LogEntryLevel.Info, "A", "utc") { TimeStamp = new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc) });
			viewer.FileTimeZone = TimeZoneInfo.Utc;
			viewer.CycleSort(LogColumns.TimeStamp);
			Assert.Equal(new[] { "utc", "written" }, Shown(viewer));

			viewer.FileTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

			Assert.Equal(new[] { "written", "utc" }, Shown(viewer));
			Assert.Equal("Sorted by TimeStamp ▲", viewer.SortText);
		});

	[Fact]
	public void WhenTheTopEntryIsHidden_TheListStaysAtItsNeighbourInTheShownOrder() =>
		_context.RunOnSta(() =>
		{
			// The loggers go down while the entries arrive, so the sort by logger is the order of arrival the other way round.
			var entries = new ObservableCollection<LogEntry>(Enumerable.Range(0, 200).Select(i =>
				new LogEntry(i == 99 ? LogEntryLevel.Debug : LogEntryLevel.Info, $"L{199 - i:000}", $"entry {i}")));
			var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
			try
			{
				window.Height = 800;
				viewer.CycleSort(LogColumns.Logger);
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				var scrollViewer = (ScrollViewer)Wpf.ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer))!;
				scrollViewer.ScrollToVerticalOffset(100);
				WpfTestHelper.Settle();
				Assert.Equal("L100", ((LogEntry)listView.Items[(int)scrollViewer.VerticalOffset]!).LoggerName);

				viewer.DebugFilter = true;
				WpfTestHelper.Settle();

				// L100 is hidden; the row before it in the shown order is L099. (In the order of arrival the neighbour would be L101.)
				Assert.Equal("L099", ((LogEntry)listView.Items[(int)scrollViewer.VerticalOffset]!).LoggerName);
			}
			finally
			{
				window.Close();
			}
		});}
