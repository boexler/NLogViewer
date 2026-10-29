using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using System.Windows.Shapes;
using Sentinel.LogViewer.Wpf.XamlMultiValueConverter;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests of the bookmarks (the mark in the narrow column, comments, F2 and Shift+F2) and of copying the selected rows with Ctrl+C
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerBookmarkTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerBookmarkTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private void WithViewer(Action<Wpf.LogViewer, ObservableCollection<LogEntry>, ListView> test) => _context.RunOnSta(() =>
	{
		var entries = new ObservableCollection<LogEntry>
		{
			new(LogEntryLevel.Info, "Acme.A", "zero"),
			new(LogEntryLevel.Debug, "Acme.B", "one"),
			new(LogEntryLevel.Info, "Acme.C", "two"),
			new(LogEntryLevel.Error, "Acme.D", "three"),
			new(LogEntryLevel.Info, "Acme.E", "four"),
			new(LogEntryLevel.Warn, "Acme.F", "five")
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

	private static void Select(ListView list, ObservableCollection<LogEntry> entries, params int[] indices)
	{
		list.SelectedItems.Clear();
		foreach (var index in indices)
			list.SelectedItems.Add(entries[index]);
	}

	[Fact]
	public void TheBookmarkOfTheSelectedEntries_IsSet_AndTakenOffWhenAllOfThemHaveOne()
	{
		WithViewer((viewer, entries, list) =>
		{
			Select(list, entries, 1, 3);
			viewer.ToggleBookmarkCommand.Execute(null);
			Assert.Equal(new[] { true, true }, new[] { viewer.IsBookmarked(entries[1]), viewer.IsBookmarked(entries[3]) });
			Assert.Equal(2, viewer.BookmarkCount);

			// One of them has none: the others get one, nothing is taken off.
			Select(list, entries, 3, 4);
			viewer.ToggleBookmarkCommand.Execute(null);
			Assert.Equal(3, viewer.BookmarkCount);

			Select(list, entries, 1, 3);
			viewer.ToggleBookmarkCommand.Execute(null);
			Assert.Equal(1, viewer.BookmarkCount);
			Assert.True(viewer.IsBookmarked(entries[4]));
		});
	}

	[Fact]
	public void ABookmarkCanHaveACommentThatTheMarkShows_AndAnEmptyCommentTakesItOff()
	{
		WithViewer((viewer, entries, _) =>
		{
			var converter = new BookmarkConverter();
			object? Tip() => converter.Convert(new object[] { entries[2], viewer, viewer.BookmarkVersion }, typeof(object), "Tip", System.Globalization.CultureInfo.InvariantCulture);

			Assert.Null(Tip());

			viewer.SetBookmarkComment(entries[2], "  look at the second try  ");
			Assert.True(viewer.IsBookmarked(entries[2]));
			Assert.Equal("look at the second try", viewer.BookmarkCommentOf(entries[2]));
			Assert.Equal("look at the second try", Tip());

			viewer.SetBookmarkComment(entries[2], " ");
			Assert.True(viewer.IsBookmarked(entries[2]));
			Assert.Equal(string.Empty, viewer.BookmarkCommentOf(entries[2]));
			Assert.Equal("Bookmark", Tip());
		});
	}

	[Fact]
	public void TheMarkShowsInTheRowOfABookmark_AndOnlyThere()
	{
		WithViewer((viewer, entries, list) =>
		{
			// A dense font (a line of text is about 1.17 times the font size) is where a ribbon that is too high makes a row higher.
			viewer.ListFontFamily = new System.Windows.Media.FontFamily("Consolas");
			// Without a comment: a row with a comment has a second line and is higher on purpose.
			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[4], null);
			WpfTestHelper.Settle();

			var marks = WpfTestHelper.Descendants<Path>(list).Where(path => ReferenceEquals(path.Data, BookmarkIcon.Ribbon)).ToList();
			Assert.Equal(6, marks.Count);
			Assert.Equal(2, marks.Count(mark => mark.IsVisible));

			// The ribbon is as high as the line of text (it has a height), and a row with a bookmark is not higher than one without.
			var rows = WpfTestHelper.Descendants<ListViewItem>(list).ToList();
			Assert.True(rows.Select(row => row.ActualHeight).Distinct().Count() == 1, string.Join(", ", rows.Select(row => row.ActualHeight)));
			var visible = marks.First(mark => mark.IsVisible);
			Assert.InRange(visible.ActualHeight, 8, rows[0].ActualHeight);

			Select(list, entries, 1);
			viewer.ToggleBookmarkCommand.Execute(null);
			WpfTestHelper.Settle();
			Assert.Equal(1, WpfTestHelper.Descendants<Path>(list).Count(path => ReferenceEquals(path.Data, BookmarkIcon.Ribbon) && path.IsVisible));
		});
	}

	[Fact]
	public void ASelectedRow_IsTintedUnderItsText_AndARowThatIsNotSelectedIsNot()
	{
		WithViewer((viewer, entries, list) =>
		{
			WpfTestHelper.Settle();
			double Tint(int index)
			{
				var row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(entries[index]);
				return ((Border)row.Template.FindName("Selection", row)).Opacity;
			}

			Select(list, entries, 2);
			WpfTestHelper.Settle();

			Assert.True(Tint(2) >= 0.4);
			Assert.Equal(0, Tint(4));
		});
	}

	[Fact]
	public void F2AndShiftF2_GoThroughTheBookmarksInTheShownOrder_SayWhenTheEndIsReached_AndGoOnAtTheOtherEnd()
	{
		WithViewer((viewer, entries, list) =>
		{
			foreach (var index in new[] { 1, 3, 5 })
				viewer.SetBookmarkComment(entries[index], null);

			Select(list, entries, 0);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[1], list.SelectedItem);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[5], list.SelectedItem);

			// At the end the first press says so and stays; the next one starts at the first bookmark.
			Assert.False(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[5], list.SelectedItem);
			Assert.Equal("No further bookmark. Press the key again to start at the first one", viewer.JumpText);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[1], list.SelectedItem);

			// Backwards the same at the other end.
			Assert.False(viewer.JumpToBookmark(forward: false));
			Assert.Equal("No earlier bookmark. Press the key again to start at the last one", viewer.JumpText);
			Assert.True(viewer.JumpToBookmark(forward: false));
			Assert.Same(entries[5], list.SelectedItem);
		});
	}

	[Fact]
	public void TheJumpFollowsTheSortOfTheList()
	{
		WithViewer((viewer, entries, list) =>
		{
			foreach (var index in new[] { 1, 3, 5 })
				viewer.SetBookmarkComment(entries[index], null);

			// By logger, descending: the list shows five, four, three, two, one, zero.
			viewer.CycleSort(LogColumns.Logger);
			viewer.CycleSort(LogColumns.Logger);
			WpfTestHelper.Settle();
			list.SelectedItem = null;

			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[5], list.SelectedItem);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[3], list.SelectedItem);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[1], list.SelectedItem);
		});
	}

	[Fact]
	public void ThePressAtTheEnd_ThatIsNotRepeated_DoesNotWrapLater()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[3], null);
			Select(list, entries, 3);

			Assert.False(viewer.JumpToBookmark(forward: true));
			// Something else was selected in between: the hint of the end counts again.
			Select(list, entries, 4);
			Assert.False(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[4], list.SelectedItem);
		});
	}

	[Fact]
	public void WithoutBookmarks_TheJumpSaysSo()
	{
		WithViewer((viewer, _, list) =>
		{
			Assert.False(viewer.JumpToBookmark(forward: true));

			Assert.Equal("There are no bookmarks in the list", viewer.JumpText);
		});
	}

	[Fact]
	public void TheBookmarksSurviveAFilter_AreSkippedWhileTheirEntryIsHidden_AndLeaveWithTheirEntry()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[3], null);

			// The Debug entry is hidden: its bookmark is still there, but a jump does not stop at an entry that is not shown.
			viewer.DebugFilter = true;
			WpfTestHelper.Settle();
			Assert.True(viewer.IsBookmarked(entries[1]));
			Select(list, entries, 0);
			Assert.True(viewer.JumpToBookmark(forward: true));
			Assert.Same(entries[3], list.SelectedItem);

			viewer.DebugFilter = false;
			WpfTestHelper.Settle();
			Assert.Equal(2, viewer.BookmarkCount);

			entries.RemoveAt(1);
			Assert.Equal(1, viewer.BookmarkCount);
			Assert.False(viewer.IsBookmarked(entries[0]));

			entries.Clear();
			Assert.Equal(0, viewer.BookmarkCount);
		});
	}

	[Fact]
	public void ANewListOfEntries_HasNoBookmarksOfTheOldOne()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.SetBookmarkComment(entries[1], "a");

			viewer.ItemsSource = new ObservableCollection<LogEntry>(entries.Take(3));

			Assert.Equal(0, viewer.BookmarkCount);
			Assert.False(viewer.IsBookmarked(entries[1]));
		});
	}

	// The tests do not open the window of the list (ShowBookmarksCommand): it would show in front of the user. They use the list as a control.
	[Fact]
	public void TheListOfTheBookmarks_HasEveryBookmarkInTheOrderOfTheEntries_WithItsCommentAndTheFirstLineOfTheMessage()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.RefreshBookmarkItems();
			Assert.Empty(viewer.BookmarkItems);

			viewer.SetBookmarkComment(entries[4], "check this");
			viewer.SetBookmarkComment(entries[1], null);
			viewer.RefreshBookmarkItems();

			var items = viewer.BookmarkItems;
			Assert.Equal(new[] { entries[1], entries[4] }, items.Select(item => item.Entry));
			Assert.Equal(new[] { "one", "four" }, items.Select(item => item.Message));
			Assert.Equal(new[] { "Acme.B", "Acme.E" }, items.Select(item => item.Logger));
			Assert.Equal(new[] { false, true }, items.Select(item => item.HasComment));
			// The level in the language of the control, as the filter chips have it.
			Assert.Equal(new[] { "Debug", "Info" }, items.Select(item => item.Level));
			Assert.Equal("check this", items[1].Comment);
			Assert.False(string.IsNullOrEmpty(items[0].Time));
		});
	}

	[Fact]
	public void AnEntryOfTheList_IsSelectedInTheMainList_AndAnEntryThatAFilterHidesSaysSo()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[3], null);
			viewer.RefreshBookmarkItems();
			var shown = viewer.BookmarkItems.Single(item => item.Entry == entries[3]);
			var hidden = viewer.BookmarkItems.Single(item => item.Entry == entries[1]);

			viewer.GoToBookmarkCommand.Execute(shown);
			Assert.Same(entries[3], list.SelectedItem);

			// The Debug entry is hidden by a filter: it stays a bookmark, and the selection stays where it was.
			viewer.DebugFilter = true;
			WpfTestHelper.Settle();
			viewer.GoToBookmarkCommand.Execute(hidden);
			Assert.Same(entries[3], list.SelectedItem);
			Assert.Equal("The entry is hidden by a filter", viewer.JumpText);
			Assert.True(viewer.IsBookmarked(entries[1]));
		});
	}

	[Fact]
	public void TheListControl_ShowsTheRowsWithATipAndOffersTheButtonsOnlyForASelectedRow()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.SetBookmarkComment(entries[1], "look here");
			viewer.SetBookmarkComment(entries[3], null);
			viewer.RefreshBookmarkItems();

			var control = new Wpf.Helper.BookmarkList { DataContext = viewer, Width = 1000, Height = 300 };
			control.Measure(new System.Windows.Size(1000, 300));
			control.Arrange(new System.Windows.Rect(0, 0, 1000, 300));
			control.UpdateLayout();
			var rows = (ListView)control.FindName("BookmarkRows");
			var buttons = WpfTestHelper.Descendants<Button>(control).ToList();

			Assert.Equal(2, rows.Items.Count);
			Assert.All(buttons.Where(button => button.Command != viewer.ClearBookmarksCommand), button => Assert.False(button.IsEnabled));

			// The tip shows the comment and the whole message; it reads them from the row it belongs to.
			var row = (ListViewItem)rows.ItemContainerGenerator.ContainerFromIndex(0);
			var tip = (System.Windows.Controls.ToolTip)row.ToolTip;
			tip.PlacementTarget = row;
			Assert.Equal("look here" + Environment.NewLine + Environment.NewLine + "one", tip.Content);

			rows.SelectedIndex = 1;
			control.UpdateLayout();
			Assert.All(buttons, button => Assert.True(button.IsEnabled));

			// A change makes a new list; the row of the same entry is selected again, so the buttons stay on.
			viewer.SetBookmarkComment(entries[3], "now with a comment");
			viewer.RefreshBookmarkItems();
			control.UpdateLayout();
			Assert.Equal(entries[3], ((Wpf.Models.BookmarkItem)rows.SelectedItem).Entry);
			Assert.All(buttons, button => Assert.True(button.IsEnabled));
		});
	}

	[Fact]
	public void ARowOfTheList_TakesItsBookmarkOff()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.SetBookmarkComment(entries[1], "a");
			viewer.SetBookmarkComment(entries[3], null);
			viewer.RefreshBookmarkItems();

			viewer.RemoveBookmarkItemCommand.Execute(viewer.BookmarkItems.Single(item => item.Entry == entries[1]));

			Assert.False(viewer.IsBookmarked(entries[1]));
			Assert.Equal(string.Empty, viewer.BookmarkCommentOf(entries[1]));
			Assert.Equal(1, viewer.BookmarkCount);
		});
	}

	[Fact]
	public void TheButtonOfTheList_IsNextToTheSearchButton_AndCountsTheBookmarks()
	{
		WithViewer((viewer, entries, _) =>
		{
			var button = (Button)viewer.Template.FindName("PART_BookmarkButton", viewer);
			var counts = () => WpfTestHelper.Descendants<TextBlock>(button).Where(text => text.Visibility == System.Windows.Visibility.Visible).Select(text => text.Text).ToList();
			Assert.Empty(counts());

			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[2], null);
			WpfTestHelper.Settle();

			Assert.Contains("2", counts());
			Assert.Equal(viewer.ShowBookmarksCommand, button.Command);
		});
	}

	[Fact]
	public void TheButton_ShowsThePlaceOfTheSelectedBookmark_AndOtherwiseOnlyTheNumber()
	{
		WithViewer((viewer, entries, list) =>
		{
			Assert.Equal(string.Empty, viewer.BookmarkLabel);

			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[3], null);
			viewer.SetBookmarkComment(entries[4], null);
			Assert.Equal("3", viewer.BookmarkLabel);

			Select(list, entries, 3);
			Assert.Equal("2/3", viewer.BookmarkLabel);

			// An entry without a bookmark, or several selected entries: only the number.
			Select(list, entries, 2);
			Assert.Equal("3", viewer.BookmarkLabel);
			Select(list, entries, 1, 4);
			Assert.Equal("3", viewer.BookmarkLabel);

			// The place is in the order that the list shows, as F2 jumps: sorted by logger, downwards, the entry "four" is the first of the three.
			viewer.SortKey = LogColumns.Logger;
			viewer.SortDescending = true;
			WpfTestHelper.Settle();
			Select(list, entries, 4);
			Assert.Equal("1/3", viewer.BookmarkLabel);
			Select(list, entries, 1);
			Assert.Equal("3/3", viewer.BookmarkLabel);

			// A bookmark that a filter hides is not in the list; it counts only in the number.
			viewer.SortKey = null;
			viewer.DebugFilter = true;
			WpfTestHelper.Settle();
			Select(list, entries, 3);
			Assert.Equal("1/3", viewer.BookmarkLabel);
			viewer.DebugFilter = false;
			WpfTestHelper.Settle();

			// The last of the three; with its bookmark taken off, only the number of the other two is left.
			Select(list, entries, 4);
			Assert.Equal("3/3", viewer.BookmarkLabel);
			viewer.ToggleBookmarkCommand.Execute(null);
			Assert.Equal("2", viewer.BookmarkLabel);
		});
	}

	[Fact]
	public void TheButton_FollowsAFilterThatHidesABookmarkBeforeTheSelectedOne_WithoutANewSelection()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.SetBookmarkComment(entries[1], null);
			viewer.SetBookmarkComment(entries[3], null);
			viewer.SetBookmarkComment(entries[4], null);
			Select(list, entries, 3);
			Assert.Equal("2/3", viewer.BookmarkLabel);

			// The selected entry stays selected, so no selection changes; the hidden bookmark before it still leaves the count of the places.
			viewer.DebugFilter = true;
			WpfTestHelper.Settle();
			Assert.Same(entries[3], list.SelectedItem);
			Assert.Equal("1/3", viewer.BookmarkLabel);

			viewer.DebugFilter = false;
			WpfTestHelper.Settle();
			Assert.Equal("2/3", viewer.BookmarkLabel);

			// The same for the sort: the entry "four" is the last of the three, and the first when sorted by logger downwards.
			Select(list, entries, 4);
			Assert.Equal("3/3", viewer.BookmarkLabel);
			viewer.SortKey = LogColumns.Logger;
			viewer.SortDescending = true;
			WpfTestHelper.Settle();
			Assert.Same(entries[4], list.SelectedItem);
			Assert.Equal("1/3", viewer.BookmarkLabel);
		});
	}

	[Fact]
	public void TheComment_IsWrittenUnderTheMessage_AndTheMarkHasLinesInIt()
	{
		WithViewer((viewer, entries, list) =>
		{
			viewer.SetBookmarkComment(entries[1], "look here");
			viewer.SetBookmarkComment(entries[3], null);
			viewer.SetBookmarkComment(entries[4], "first line\r\nsecond line");
			WpfTestHelper.Settle();

			var withComment = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(entries[1]);
			var without = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(entries[3]);
			var visible = (ListViewItem row) => WpfTestHelper.Descendants<TextBlock>(row).Where(text => text.IsVisible).Select(text => text.Text).ToList();

			Assert.Contains("look here", visible(withComment));
			Assert.DoesNotContain("look here", visible(without));

			// Only the first line of a comment of several lines, so the row does not grow with the comment.
			var several = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(entries[4]);
			Assert.Contains("first line …", visible(several));
			Assert.DoesNotContain(visible(several), text => text.Contains("second line"));
			Assert.Same(Wpf.BookmarkIcon.RibbonWithNote, WpfTestHelper.Descendants<Path>(withComment).Single(path => path.IsVisible && ReferenceEquals(path.Fill, Wpf.BookmarkIcon.Gold)).Data);
			Assert.Same(Wpf.BookmarkIcon.Ribbon, WpfTestHelper.Descendants<Path>(without).Single(path => path.IsVisible && ReferenceEquals(path.Fill, Wpf.BookmarkIcon.Gold)).Data);
		});
	}

	[Fact]
	public void ADoubleClickOnARowWithoutABookmark_DoesNothing()
	{
		WithViewer((viewer, entries, list) =>
		{
			var row = (ListViewItem)list.ItemContainerGenerator.ContainerFromItem(entries[2]);
			var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = row };

			// A row with a bookmark opens the dialog of the comment, which waits for an answer and cannot be shown in a test.
			row.RaiseEvent(click);

			Assert.False(click.Handled);
			Assert.Equal(0, viewer.BookmarkCount);
		});
	}

	[Fact]
	public void ClearingTheBookmarks_TakesThemAllOff()
	{
		// The command asks first when a comment would be lost; a question box cannot be shown in a test, so it clears bookmarks without comments here.
		WithViewer((viewer, entries, _) =>
		{
			viewer.SetBookmarkComment(entries[0], null);
			viewer.SetBookmarkComment(entries[1], null);
			Assert.True(viewer.ClearBookmarksCommand.CanExecute(null));

			viewer.ClearBookmarksCommand.Execute(null);

			Assert.Equal(0, viewer.BookmarkCount);
			Assert.False(viewer.ClearBookmarksCommand.CanExecute(null));

			viewer.SetBookmarkComment(entries[0], "a");
			viewer.ClearBookmarks();
			Assert.Equal(string.Empty, viewer.BookmarkCommentOf(entries[0]));
		});
	}

	[Fact]
	public void ARowCommandWithoutARow_DoesNothing()
	{
		WithViewer((viewer, entries, _) =>
		{
			viewer.SetBookmarkComment(entries[1], null);

			viewer.RemoveBookmarkItemCommand.Execute(null);
			viewer.GoToBookmarkCommand.Execute(null);

			Assert.Equal(1, viewer.BookmarkCount);
		});
	}

	[Fact]
	public void TheMenuOfARow_BookmarksTheEntryUnderTheMouse_NotTheSelectedOne()
	{
		WithViewer((viewer, entries, list) =>
		{
			Select(list, entries, 0);
			var menu = new ContextMenu();
			viewer.AddRowActions(menu, entries[2]);
			var item = menu.Items.OfType<MenuItem>().Single(entry => entry.Header as string == "Add bookmark");

			item.Command.Execute(item.CommandParameter);

			Assert.True(viewer.IsBookmarked(entries[2]));
			Assert.False(viewer.IsBookmarked(entries[0]));
			viewer.AddRowActions(menu, entries[2]);
			Assert.Contains(menu.Items.OfType<MenuItem>(), entry => entry.Header as string == "Remove bookmark");

			// The entry of the bookmark has the picture of a bookmark, the others have none.
			var items = menu.Items.OfType<MenuItem>().ToList();
			Assert.Same(BookmarkIcon.Ribbon, ((System.Windows.Shapes.Path)items.Single(entry => entry.Header as string == "Remove bookmark").Icon).Data);
			Assert.Null(items.Single(entry => entry.Header as string == "Next bookmark").Icon);
		});
	}

	[Fact]
	public void CtrlC_CopiesTheSelectedRows_OneLinePerEntry_InTheOrderOfTheList()
	{
		WithViewer((viewer, entries, list) =>
		{
			Assert.False(ApplicationCommands.Copy.CanExecute(null, list));

			// The rows were selected in another order than the list shows them.
			Select(list, entries, 4, 0, 2);
			Assert.True(ApplicationCommands.Copy.CanExecute(null, list));

			var text = viewer.CopySelectedRows();

			var lines = text.Split(Environment.NewLine);
			Assert.Equal(3, lines.Length);
			Assert.Contains("zero", lines[0]);
			Assert.Contains("two", lines[1]);
			Assert.Contains("four", lines[2]);
			Assert.Equal(string.Empty, new Wpf.LogViewer().CopySelectedRows());
		});
	}

	[Fact]
	public void TheNarrowColumnOfTheBookmarks_StaysAtTheLeft_AndIsNotOneOfTheColumnsThatCanBeChosen()
	{
		WithViewer((viewer, _, list) =>
		{
			var columns = ((GridView)list.View).Columns;
			var marker = columns[0];
			Assert.Equal(6, columns.Count);
			// The column is found by its mark, not by its place.
			Assert.True(Wpf.LogViewer.GetIsBookmarkColumn(marker));
			Assert.DoesNotContain(columns.Skip(1), Wpf.LogViewer.GetIsBookmarkColumn);

			// A header that is dragged in front of it: it goes back to the left.
			columns.Move(2, 0);

			Assert.Same(marker, columns[0]);
			Assert.Equal(new[] { LogColumns.Level, LogColumns.Id, LogColumns.TimeStamp, LogColumns.Logger, LogColumns.Message }, viewer.ColumnKeys);

			viewer.SetColumnVisible(LogColumns.Id, false);
			viewer.SetColumnVisible(LogColumns.Level, false);
			viewer.SetColumnVisible(LogColumns.TimeStamp, false);
			viewer.SetColumnVisible(LogColumns.Logger, false);

			Assert.Same(marker, ((GridView)list.View).Columns[0]);
			Assert.Equal(2, ((GridView)list.View).Columns.Count);
		});
	}
}
