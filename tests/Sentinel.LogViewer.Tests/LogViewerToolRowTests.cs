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
		});}
