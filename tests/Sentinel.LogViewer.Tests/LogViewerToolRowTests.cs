using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the top of the viewer: the result row, the search bar, and the commands that a host sends to the viewer
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerToolRowTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerToolRowTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "A", "alpha") { TimeStamp = DateTime.UtcNow.AddMinutes(-90) },
		new LogEntry(LogEntryLevel.Error, "A", "beta") { TimeStamp = DateTime.UtcNow.AddMinutes(-30) },
		new LogEntry(LogEntryLevel.Error, "B", "beta again") { TimeStamp = DateTime.UtcNow.AddMinutes(-10) }
	};

	private void WithViewer(Action<Wpf.LogViewer> test) => _context.RunOnSta(() =>
	{
		var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
		try
		{
			WpfTestHelper.Settle();
			test(viewer);
		}
		finally
		{
			window.Close();
		}
	});

	[Fact]
	public void TheResultRow_CountsTheEntries_AndTheResetTakesTheFiltersOffButKeepsTheFields() =>
		WithViewer(viewer =>
		{
			Assert.Equal("Entries: 3", viewer.FilterResultText);
			Assert.False(viewer.HasActiveFilter);
			Assert.False(viewer.ResetFilterCommand.CanExecute(null));

			viewer.AddRegexSearchTerm("beta");
			viewer.InfoFilter = true;
			WpfTestHelper.Settle();

			Assert.Equal("Entries: 2 of 3", viewer.FilterResultText);
			Assert.True(viewer.HasActiveFilter);

			viewer.SearchFields = SearchFields.All;
			viewer.CustomTimeStampFormat = TimeStampFormat.German;
			viewer.ResetFilterCommand.Execute(null);
			WpfTestHelper.Settle();

			Assert.Equal("Entries: 3", viewer.FilterResultText);
			Assert.False(viewer.HasActiveFilter);
			Assert.Empty(viewer.ActiveSearchTerms);
			Assert.False(viewer.InfoFilter);
			Assert.Equal((SearchFields.All, TimeStampFormat.German), (viewer.SearchFields, viewer.CustomTimeStampFormat));
		});

	[Fact]
	public void TheResultRow_CountsTheTimeRangeAndTheRulesForLoggers() =>
		WithViewer(viewer =>
		{
			viewer.TimeFrom = DateTime.UtcNow.AddMinutes(-45);
			WpfTestHelper.Settle();
			Assert.Equal("Entries: 2 of 3", viewer.FilterResultText);
			Assert.True(viewer.HasActiveFilter);

			viewer.LoggerTree.Rules.ShowOnly("B");
			WpfTestHelper.Settle();
			Assert.True(viewer.LoggerRuleCount > 0);
			Assert.Equal("Entries: 1 of 3", viewer.FilterResultText);
		});

	[Fact]
	public void TheCommandsOfTheHost_RunOnTheViewerTheyAreSentTo() =>
		WithViewer(viewer =>
		{
			Assert.True(LogViewerCommands.GoToTime.CanExecute(null, viewer));
			Assert.True(LogViewerCommands.ChooseTimeRange.CanExecute(null, viewer));
			Assert.True(LogViewerCommands.NextLevel.CanExecute(LogEntryLevel.Error, viewer));
			Assert.False(LogViewerCommands.ResetFilter.CanExecute(null, viewer));

			LogViewerCommands.TimeRangeLast.Execute("0:20:00", viewer);
			WpfTestHelper.Settle();
			Assert.NotNull(viewer.TimeFrom);
			Assert.True(LogViewerCommands.ResetFilter.CanExecute(null, viewer));

			LogViewerCommands.ResetFilter.Execute(null, viewer);
			Assert.Null(viewer.TimeFrom);

			LogViewerCommands.NextLevel.Execute(LogEntryLevel.Error, viewer);
			Assert.Equal("beta", ((LogEntry)((ListView)viewer.Template.FindName("PART_ListView", viewer)).SelectedItem).Message);
		});

	[Fact]
	public void TheSearchBar_OpensWithTheFindCommand_AndClosingItClearsTheSearch() =>
		WithViewer(viewer =>
		{
			Assert.False(viewer.IsFindOpen);

			ApplicationCommands.Find.Execute(null, viewer);
			Assert.True(viewer.IsFindOpen);

			viewer.FindText = "beta";
			WpfTestHelper.Settle();
			Assert.Equal("2 hits", viewer.FindResultText);

			viewer.CloseFindCommand.Execute(null);
			Assert.False(viewer.IsFindOpen);
			Assert.Equal(string.Empty, viewer.FindText);
		});

	[Fact]
	public void TheSearchBar_StaysClosed_WhenTheHostSwitchesTheSearchOff() =>
		WithViewer(viewer =>
		{
			viewer.ShowFindControls = false;
			Assert.False(ApplicationCommands.Find.CanExecute(null, viewer));
		});

	[Fact]
	public void TheLevelButtons_ShowTheIconByDefault_AndTheLetterWhenAsked() =>
		WithViewer(viewer =>
		{
			Assert.Equal(LevelDisplayMode.Symbol, viewer.LevelButtonDisplay);
			var button = Assert.Single(WpfTestHelper.Descendants<ToggleButton>(viewer), candidate => Equals(candidate.ToolTip, "Hide the Warn entries (press again to show them)"));
			var letter = WpfTestHelper.Descendants<TextBlock>(button).Single();
			Assert.Equal(System.Windows.Visibility.Collapsed, letter.Visibility);

			viewer.LevelButtonDisplay = LevelDisplayMode.Text;
			Assert.Equal(System.Windows.Visibility.Visible, letter.Visibility);
			Assert.Equal("W", letter.Text);
			Assert.Equal(System.Windows.Visibility.Collapsed, WpfTestHelper.Descendants<Image>(button).Single().Visibility);
		});

	[Fact]
	public void TheBoxesOfTheFilterAndTheSearch_ShowAHintAndASymbol_WhileTheyAreEmpty() =>
		WithViewer(viewer =>
		{
			ApplicationCommands.Find.Execute(null, viewer);
			WpfTestHelper.Settle();

			foreach (var (name, hint, glyph) in new[] { ("Filter", "Filter the entries", "\uE71C"), ("Search", "Find in the entries", "\uE721") })
			{
				var box = WpfTestHelper.Descendants<TextBox>(viewer).Single(candidate => System.Windows.Automation.AutomationProperties.GetName(candidate) == name);
				var shown = (TextBlock)box.Template.FindName("Hint", box);
				Assert.Equal((hint, glyph), (((Wpf.Helper.WatermarkTextBox)box).Hint, ((Wpf.Helper.WatermarkTextBox)box).Glyph));
				Assert.Equal(System.Windows.Visibility.Visible, shown.Visibility);

				// The hint goes as soon as the box has the focus, not only when something is typed.
				box.Focus();
				Assert.Equal(System.Windows.Visibility.Collapsed, shown.Visibility);

				box.Text = "x";
				Assert.Equal(System.Windows.Visibility.Collapsed, shown.Visibility);
			}
		});

	[Fact]
	public void TheBoxOfTheLoggerTree_HasAHintAndASymbol_AndNarrowsTheTree() =>
		WithViewer(viewer =>
		{
			viewer.IsLoggerTreeExpanded = true;
			WpfTestHelper.Settle();
			var box = (Wpf.Helper.WatermarkTextBox)WpfTestHelper.Descendants<TextBox>(viewer).Single(candidate => System.Windows.Automation.AutomationProperties.GetName(candidate) == "Search loggers");
			Assert.Equal(("Search loggers", "\uE721"), (box.Hint, box.Glyph));
			Assert.Equal(System.Windows.Visibility.Visible, ((TextBlock)box.Template.FindName("Hint", box)).Visibility);

			box.Text = "B";
			Assert.Equal("B", viewer.LoggerTree.SearchText);
			Assert.False(viewer.LoggerTree.Find("A")!.IsListed);
		});

	[Fact]
	public void TheOptionsOfTheFilterAndTheSearch_AreOneControl_AndEachBindsItsOwnFlags() =>
		WithViewer(viewer =>
		{
			ApplicationCommands.Find.Execute(null, viewer);
			WpfTestHelper.Settle();
			var options = WpfTestHelper.Descendants<Wpf.Helper.SearchOptions>(viewer).ToList();
			Assert.Equal(2, options.Count);
			var (filter, find) = (options[0], options[1]);

			viewer.FilterMatchCase = true;
			Assert.Equal((true, false), (filter.MatchCase, find.MatchCase));
			find.WholeWord = true;
			Assert.Equal((false, true), (viewer.FilterWholeWord, viewer.FindWholeWord));

			var names = WpfTestHelper.Descendants<ToggleButton>(find).Select(System.Windows.Automation.AutomationProperties.GetName).ToArray();
			Assert.Equal(new[] { "Match case of the search", "Whole word of the search", "Regex of the search" }, names);

			// The fields are the ones of the viewer, from either row.
			var message = WpfTestHelper.Descendants<MenuItem>(find).Single(item => (string)item.Header == "Fields ▾").Items.OfType<MenuItem>().Single(item => (string)item.Header == "Message");
			Assert.True(message.IsChecked);
			message.Command.Execute(message.CommandParameter);
			Assert.False(viewer.SearchFields.HasFlag(SearchFields.Message));
		});

	[Fact]
	public void ThePatterns_OfTheFilterAndOfTheSearch_GoIntoTheirOwnBox() =>
		WithViewer(viewer =>
		{
			ApplicationCommands.Find.Execute(null, viewer);
			WpfTestHelper.Settle();
			var options = WpfTestHelper.Descendants<Wpf.Helper.SearchOptions>(viewer).ToList();
			var pattern = Wpf.LogViewer.RegexTemplates.First().Pattern;

			foreach (var (row, text) in new[] { (options[0], (Func<string>)(() => viewer.CurrentSearchText)), (options[1], () => viewer.FindText) })
			{
				var menu = WpfTestHelper.Descendants<MenuItem>(row).Single(item => (string)item.Header == "Patterns ▾");
				menu.IsSubmenuOpen = true;
				WpfTestHelper.Settle();

				var first = (MenuItem)menu.ItemContainerGenerator.ContainerFromIndex(0);
				Assert.NotNull(first.Command);
				first.Command.Execute(first.CommandParameter);
				menu.IsSubmenuOpen = false;

				Assert.Equal(pattern, text());
			}
		});}
