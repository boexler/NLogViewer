using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for switching a filter term on and off: it stays in the list but does not filter, and the result row counts only the terms that are on
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerTermSwitchTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerTermSwitchTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "A", "alpha one"),
		new LogEntry(LogEntryLevel.Info, "A", "beta two"),
		new LogEntry(LogEntryLevel.Info, "A", "gamma three")
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

	private static string[] Shown(Wpf.LogViewer viewer) => viewer.LogEvents.View.Cast<LogEntry>().Select(entry => entry.Message).ToArray();

	[Fact]
	public void ATermThatIsOff_DoesNotFilter_AndFiltersAgainWhenItIsOn() =>
		WithViewer(viewer =>
		{
			viewer.AddRegexSearchTerm("beta");
			viewer.AddRegexSearchTermExclude("two");
			Assert.Empty(Shown(viewer));

			var include = viewer.ActiveSearchTerms[0];
			viewer.ToggleSearchTermCommand.Execute(viewer.ActiveSearchTerms[1]);
			Assert.Equal(new[] { "beta two" }, Shown(viewer));

			viewer.ToggleSearchTermCommand.Execute(include);
			Assert.Equal(3, Shown(viewer).Length);
			Assert.Equal(2, viewer.ActiveSearchTerms.Count);

			viewer.ToggleSearchTermCommand.Execute(include);
			viewer.ToggleSearchTermCommand.Execute(viewer.ActiveSearchTerms[1]);
			Assert.Empty(Shown(viewer));
		});

	[Fact]
	public void TheResultRow_CountsOnlyTheTermsThatAreOn() =>
		WithViewer(viewer =>
		{
			viewer.AddRegexSearchTerm("beta");
			WpfTestHelper.Settle();
			Assert.Equal(("Entries: 1 of 3", true), (viewer.FilterResultText, viewer.HasActiveFilter));

			viewer.ToggleSearchTermCommand.Execute(viewer.ActiveSearchTerms[0]);
			WpfTestHelper.Settle();

			Assert.Equal(("Entries: 3", false), (viewer.FilterResultText, viewer.HasActiveFilter));
			Assert.Single(viewer.ActiveSearchTerms);
		});

	[Fact]
	public void TheChipOfATerm_HasASwitch_ThatShowsAndChangesItsState() =>
		WithViewer(viewer =>
		{
			viewer.AddRegexSearchTerm("beta");
			WpfTestHelper.Settle();

			var box = WpfTestHelper.Descendants<CheckBox>(viewer).Single(candidate => System.Windows.Automation.AutomationProperties.GetName(candidate) == "Term on");
			Assert.True(box.IsChecked);

			box.Command.Execute(box.CommandParameter);
			WpfTestHelper.Settle();

			Assert.False(viewer.ActiveSearchTerms[0].IsEnabled);
			Assert.False(box.IsChecked);
			Assert.Equal(3, Shown(viewer).Length);
		});

	[Fact]
	public void ATermThatIsOff_MarksNothingInTheRows() =>
		WithViewer(viewer =>
		{
			string[] Marked() => WpfTestHelper.Descendants<TextBlock>(viewer)
				.SelectMany(textBlock => textBlock.Inlines.OfType<System.Windows.Documents.Run>())
				.Where(run => ReferenceEquals(run.Background, viewer.SearchHighlightBackground)).Select(run => run.Text).ToArray();

			viewer.AddRegexSearchTerm("beta");
			WpfTestHelper.Settle();
			Assert.Contains("beta", Marked());

			viewer.ToggleSearchTermCommand.Execute(viewer.ActiveSearchTerms[0]);
			WpfTestHelper.Settle();

			Assert.Empty(Marked());
		});}
