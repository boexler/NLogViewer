using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Helper;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests of the panel below the list: opening and closing, the hits of the search, the repeated messages and the details of the selected entry
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerBottomPanelTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerBottomPanelTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static readonly DateTime Start = new(2026, 5, 15, 10, 0, 0);

	private void WithViewer(Action<Wpf.LogViewer, ObservableCollection<LogEntry>, ListView> test) => _context.RunOnSta(() =>
	{
		var entries = new ObservableCollection<LogEntry>
		{
			new(LogEntryLevel.Info, "Acme.A", "Order 4f9c2a10-1b2c-4d3e-8f90-a1b2c3d4e5f6 shipped") { TimeStamp = Start.AddMinutes(1) },
			new(LogEntryLevel.Error, "Acme.B", "Timeout")
			{
				TimeStamp = Start.AddMinutes(2),
				Exception = "System.TimeoutException: too slow\n   at Acme.B.Run()",
				Application = "Acme",
				Sender = "PC1 (10.0.0.5)",
				Properties = new Dictionary<string, object?> { ["Thread"] = "worker 4", ["FileName"] = "B.cs", ["LineNumber"] = 42 }
			},
			new(LogEntryLevel.Info, "Acme.A", "Order 11111111-2222-3333-4444-555555555555 shipped") { TimeStamp = Start.AddMinutes(3) },
			new(LogEntryLevel.Info, "Acme.C", "first line\r\nsecond line") { TimeStamp = Start.AddMinutes(4), Properties = new Dictionary<string, object?> { ["FileLine"] = 7 } },
			new(LogEntryLevel.Info, "Acme.A", "Order 99999999-2222-3333-4444-555555555555 shipped") { TimeStamp = Start.AddMinutes(5) }
		};
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			test(viewer, entries, (ListView)viewer.Template.FindName("PART_ListView", viewer));
		}
		finally
		{
			window.Close();
		}
	});

	private static RowDefinition PanelRow(Wpf.LogViewer viewer) => (RowDefinition)viewer.Template.FindName("PART_BottomPanelRow", viewer);

	private static BottomPanel Panel(Wpf.LogViewer viewer) => (BottomPanel)viewer.Template.FindName("PART_BottomPanel", viewer);

	[Fact]
	public void ThePanel_IsClosedAtFirst_OpensWithItsButton_AndTakesItsHeightFromTheViewer()
	{
		WithViewer((viewer, _, _) =>
		{
			Assert.False(viewer.IsBottomPanelOpen);
			Assert.Equal(0, PanelRow(viewer).ActualHeight);
			Assert.Equal(Visibility.Collapsed, Panel(viewer).Visibility);

			viewer.BottomPanelHeight = 150;
			viewer.ToggleBottomPanelCommand.Execute(null);
			WpfTestHelper.Settle();

			Assert.Equal(Visibility.Visible, Panel(viewer).Visibility);
			Assert.Equal(150, PanelRow(viewer).ActualHeight, 1);
			Assert.Equal(3, WpfTestHelper.Descendants<TabItem>(Panel(viewer)).Count());

			viewer.ToggleBottomPanelCommand.Execute(null);
			WpfTestHelper.Settle();
			Assert.Equal(0, PanelRow(viewer).ActualHeight);

			// A host that does not want the panel switches it off, and an open panel closes with it.
			viewer.IsBottomPanelOpen = true;
			viewer.ShowBottomPanel = false;
			WpfTestHelper.Settle();
			Assert.Equal(Visibility.Collapsed, Panel(viewer).Visibility);
			Assert.Equal(0, PanelRow(viewer).ActualHeight);
		});
	}

	[Fact]
	public void TheTab_FollowsTheViewer_AndTheViewerFollowsTheTab()
	{
		WithViewer((viewer, _, _) =>
		{
			viewer.OpenBottomPanel(BottomPanelTab.Details);
			WpfTestHelper.Settle();
			var tabs = WpfTestHelper.Descendants<TabControl>(Panel(viewer)).First(tab => tab.Name == "Tabs");
			Assert.Equal(2, tabs.SelectedIndex);

			tabs.SelectedIndex = 1;
			Assert.Equal(BottomPanelTab.Repeats, viewer.BottomPanelTab);
		});
	}

	[Fact]
	public void TheResults_ListAllHitsOfTheSearch_FollowTheText_AndAnEntryOfTheListIsSelected()
	{
		WithViewer((viewer, entries, list) =>
		{
			var changes = 0;
			((INotifyCollectionChanged)viewer.FindHits).CollectionChanged += (_, _) => changes++;

			viewer.FindText = "shipped";
			Assert.Equal(3, viewer.FindHits.Count);
			Assert.Same(entries[2], viewer.FindHits[1]);
			Assert.True(changes > 0);

			viewer.FindText = "timeout";
			Assert.Single(viewer.FindHits);
			viewer.FindText = string.Empty;
			Assert.Empty(viewer.FindHits);

			viewer.FindText = "shipped";
			viewer.GoToHitCommand.Execute(entries[4]);
			Assert.Same(entries[4], list.SelectedItem);
			Assert.Equal("Entry 3 of 3", viewer.FindResultText);
		});
	}

	[Fact]
	public void TheDetails_FollowTheSelection_AndShowTheFirstOfSeveralSelectedEntries()
	{
		WithViewer((viewer, entries, list) =>
		{
			Assert.Null(viewer.Details);

			list.SelectedItem = entries[1];
			var details = viewer.Details!;
			Assert.Same(entries[1], details.Entry);
			Assert.Equal("Error", details.Level);
			Assert.Equal("Acme.B", details.Logger);
			Assert.Equal("Acme · PC1 (10.0.0.5)", details.Origin);
			Assert.Equal("B.cs:42", details.Source);
			Assert.True(details.HasException);
			Assert.Contains("TimeoutException", details.Exception);
			Assert.Equal(new[] { "FileName", "LineNumber", "Thread" }, details.Properties.Select(property => property.Key));
			Assert.Contains("Thread = worker 4", details.FullText);

			// A text file knows only the line; an entry without any of it shows nothing.
			list.SelectedItem = entries[3];
			Assert.Equal("Line 7", viewer.Details!.Source);
			list.SelectedItem = entries[0];
			Assert.False(viewer.Details!.HasSource);
			Assert.False(viewer.Details!.HasOrigin);

			list.SelectedItems.Clear();
			list.SelectedItems.Add(entries[2]);
			list.SelectedItems.Add(entries[0]);
			Assert.Same(entries[2], viewer.Details!.Entry);

			list.SelectedItems.Clear();
			Assert.Null(viewer.Details);
		});
	}

	[Fact]
	public void TheDetailsTab_ShowsTheEntry_AndHidesTheExceptionOfAnEntryWithoutOne()
	{
		WithViewer((viewer, entries, list) =>
		{
			list.SelectedItem = entries[1];
			viewer.OpenBottomPanel(BottomPanelTab.Details);
			WpfTestHelper.Settle();

			var visible = WpfTestHelper.Descendants<TextBox>(Panel(viewer)).Where(box => box.IsVisible).Select(box => box.Text).ToList();
			Assert.Contains("Timeout", visible);
			Assert.Contains(visible, text => text.Contains("TimeoutException"));

			list.SelectedItem = entries[0];
			WpfTestHelper.Settle();
			visible = WpfTestHelper.Descendants<TextBox>(Panel(viewer)).Where(box => box.IsVisible).Select(box => box.Text).ToList();
			Assert.DoesNotContain(visible, text => text.Contains("TimeoutException"));
		});
	}

	[Fact]
	public void TheRepeats_GroupMessagesThatDifferOnlyInGuids_AndGoToTheirFirstAndLastEntry()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.OpenBottomPanel(BottomPanelTab.Repeats);
			WpfTestHelper.Settle();

			var group = Assert.Single(viewer.RepeatGroups);
			Assert.Equal(3, group.Count);
			Assert.Equal("Acme.A", group.Logger);
			Assert.Equal("1 message repeats, 3 entries in total", viewer.RepeatsText);

			viewer.GoToFirstRepeatCommand.Execute(group);
			Assert.Same(entries[0], list.SelectedItem);
			viewer.GoToLastRepeatCommand.Execute(group);
			Assert.Same(entries[4], list.SelectedItem);

			// The repeats follow the filter: with the Info entries hidden nothing repeats.
			viewer.InfoFilter = true;
			WpfTestHelper.Settle();
			Assert.Empty(viewer.RepeatGroups);
		});
	}

	// The text of the cells that are shown: a binding that does not find the viewer leaves a cell empty and says nothing.
	private static List<string> CellTexts(Wpf.LogViewer viewer, string list)
	{
		var rows = WpfTestHelper.Descendants<ListView>(Panel(viewer)).First(rows => rows.Name == list);
		return WpfTestHelper.Descendants<TextBlock>(rows).Where(text => text.IsVisible).Select(text => text.Text).ToList();
	}

	[Fact]
	public void TheCellsOfTheResults_ShowTimeLevelLoggerAndTheFirstLineOfTheMessage()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.FindText = "timeout";
			viewer.OpenBottomPanel(BottomPanelTab.Results);
			WpfTestHelper.Settle();

			var cells = CellTexts(viewer, "HitRows");
			Assert.Contains(viewer.TimeText(entries[1]), cells);
			Assert.Contains("Error", cells);
			Assert.Contains("Acme.B", cells);
			Assert.Contains("Timeout", cells);

			// The columns come in the order of the main list (ID, level, time, logger, message), and the level is centered.
			var rows = WpfTestHelper.Descendants<ListView>(Panel(viewer)).First(rows => rows.Name == "HitRows");
			Assert.Equal(new[] { "ID", "Level", "TimeStamp", "LoggerName", "Message" }, ((GridView)rows.View).Columns.Select(column => (string)column.Header));

			// The text is in the middle of its column, not at the left edge of it.
			var level = WpfTestHelper.Descendants<TextBlock>(rows).First(text => text.IsVisible && text.Text == "Error");
			var presenter = WpfTestHelper.Descendants<GridViewRowPresenter>(rows).First();
			var columns = ((GridView)rows.View).Columns;
			var middle = level.TranslatePoint(new Point(level.ActualWidth / 2, 0), presenter).X;
			Assert.True(level.ActualWidth < columns[1].ActualWidth);
			Assert.Equal(columns[0].ActualWidth + columns[1].ActualWidth / 2, middle, 1);

			// Only the first line of a message with several.
			viewer.FindText = "first line";
			WpfTestHelper.Settle();
			cells = CellTexts(viewer, "HitRows");
			Assert.Contains("first line", cells);
			Assert.DoesNotContain(cells, text => text.Contains("second line"));
		});
	}

	[Fact]
	public void TheCellsOfTheRepeats_ShowCountLevelLoggerSampleAndTheTimeOfTheFirstAndTheLastEntry()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.OpenBottomPanel(BottomPanelTab.Repeats);
			WpfTestHelper.Settle();

			var cells = CellTexts(viewer, "RepeatRows");
			Assert.Contains("3", cells);
			Assert.Contains("Info", cells);
			Assert.Contains("Acme.A", cells);
			Assert.Contains(entries[0].Message, cells);
			Assert.Contains(viewer.TimeText(entries[0]), cells);
			Assert.Contains(viewer.TimeText(entries[4]), cells);
		});
	}

	[Fact]
	public void TheSelectedRepeat_StaysSelected_WhenTheMessagesAreCountedAgain()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.OpenBottomPanel(BottomPanelTab.Repeats);
			WpfTestHelper.Settle();
			var rows = WpfTestHelper.Descendants<ListView>(Panel(viewer)).First(rows => rows.Name == "RepeatRows");
			rows.SelectedItem = Assert.Single(viewer.RepeatGroups);

			entries.Add(new LogEntry(LogEntryLevel.Info, "Acme.A", "Order aaaaaaaa-2222-3333-4444-555555555555 shipped") { TimeStamp = Start.AddMinutes(6) });
			WpfTestHelper.Settle();

			var group = Assert.Single(viewer.RepeatGroups);
			Assert.Equal(4, group.Count);
			Assert.Same(group, viewer.SelectedRepeat);
			Assert.Same(group, rows.SelectedItem);
		});
	}

	[Fact]
	public void TheRepeats_AreNotCountedWhileAnotherTabIsShown()
	{
		WithViewer((viewer, _, _) =>
		{
			viewer.OpenBottomPanel(BottomPanelTab.Results);
			WpfTestHelper.Settle();
			Assert.Empty(viewer.RepeatGroups);

			viewer.BottomPanelTab = BottomPanelTab.Repeats;
			WpfTestHelper.Settle();
			Assert.Single(viewer.RepeatGroups);
		});
	}
}
