using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using Sentinel.LogViewer.Core;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the options and fields that the filter and the search have in common
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerMatchingTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerMatchingTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Orders", "the cat sat"),
		new LogEntry(LogEntryLevel.Error, "Acme.Orders", "the category failed") { Exception = "System.TimeoutException: gave up" },
		new LogEntry(LogEntryLevel.Error, "Acme.Billing", "charge refused") { Properties = new Dictionary<string, object?> { ["Customer"] = "Contoso" } },
		new LogEntry(LogEntryLevel.Info, "Acme.Billing", "Cat in capitals")
	};

	private static List<string> Visible(Wpf.LogViewer viewer) =>
		((CollectionView)viewer.LogEvents.View).Cast<LogEntry>().Select(entry => entry.Message).ToList();

	private void WithViewer(Action<Wpf.LogViewer> test) => _context.RunOnSta(() => test(WpfTestHelper.CreateViewerWithTestData(Entries())));

	[Fact]
	public void TheVisibleEntryCount_FollowsTheFilters()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				Assert.Equal(4, viewer.VisibleEntryCount);

				viewer.AddRegexSearchTerm("cat");
				WpfTestHelper.Settle();
				Assert.Equal(3, viewer.VisibleEntryCount);

				viewer.ClearAllSearchTerms();
				viewer.ErrorFilter = true;
				WpfTestHelper.Settle();
				Assert.Equal(2, viewer.VisibleEntryCount);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheFilterInput_AppliesItsOptionsToTheNewTerm()
	{
		WithViewer(viewer =>
		{
			viewer.CurrentSearchText = "cat";
			viewer.FilterWholeWord = true;
			viewer.FilterMatchCase = true;
			viewer.AddSearchTerm();

			Assert.Equal(new[] { "the cat sat" }, Visible(viewer));
			var term = Assert.Single(viewer.ActiveSearchTerms);
			Assert.True(term.WholeWord);
			Assert.True(term.MatchCase);
		});
	}

	[Fact]
	public void ATermWithALevel_OnlyMatchesEntriesOfThatLevel()
	{
		WithViewer(viewer =>
		{
			viewer.CurrentSearchText = "cat";
			viewer.FilterLevel = LogEntryLevel.Error;
			viewer.AddSearchTerm();

			Assert.Equal(new[] { "the category failed" }, Visible(viewer));
		});
	}

	[Fact]
	public void ByDefault_OnlyTheMessageAndTheLoggerAreSearched()
	{
		WithViewer(viewer =>
		{
			viewer.AddRegexSearchTerm("timeout");
			Assert.Empty(Visible(viewer));

			viewer.ClearAllSearchTerms();
			viewer.AddRegexSearchTerm("billing");
			Assert.Equal(new[] { "charge refused", "Cat in capitals" }, Visible(viewer));
		});
	}

	[Fact]
	public void TheSearchFields_ChooseWhereTheTermsLook()
	{
		WithViewer(viewer =>
		{
			viewer.AddRegexSearchTerm("timeout");

			viewer.SearchFields = SearchFields.Default | SearchFields.Exception;
			Assert.Equal(new[] { "the category failed" }, Visible(viewer));

			viewer.ClearAllSearchTerms();
			viewer.AddRegexSearchTerm("contoso");
			viewer.SearchFields = SearchFields.Properties;
			Assert.Equal(new[] { "charge refused" }, Visible(viewer));
		});
	}

	[Fact]
	public void AnInvalidRegex_IsReportedAndNotAdded()
	{
		WithViewer(viewer =>
		{
			viewer.UseRegexSearch = true;
			viewer.CurrentSearchText = "(unclosed";

			Assert.NotNull(viewer.FilterError);

			viewer.AddSearchTerm();
			Assert.Empty(viewer.ActiveSearchTerms);
			Assert.Equal("(unclosed", viewer.CurrentSearchText);

			viewer.CurrentSearchText = "unclosed";
			Assert.Null(viewer.FilterError);
		});
	}

	[Fact]
	public void EditingATerm_BringsBackItsOptions()
	{
		WithViewer(viewer =>
		{
			viewer.CurrentSearchText = "cat";
			viewer.FilterIsExclude = true;
			viewer.FilterWholeWord = true;
			viewer.FilterLevel = LogEntryLevel.Info;
			viewer.AddSearchTerm();
			viewer.FilterIsExclude = false;
			viewer.FilterWholeWord = false;
			viewer.FilterLevel = null;

			viewer.EditSearchTerm(viewer.ActiveSearchTerms[0]);

			Assert.Equal("cat", viewer.CurrentSearchText);
			Assert.True(viewer.FilterIsExclude);
			Assert.True(viewer.FilterWholeWord);
			Assert.Equal(LogEntryLevel.Info, viewer.FilterLevel);
			Assert.Empty(viewer.ActiveSearchTerms);
		});
	}

	[Fact]
	public void TheSearch_UsesItsOptionsAndTheSearchFields()
	{
		WithViewer(viewer =>
		{
			viewer.FindText = "cat";
			Assert.Equal("– / 3", viewer.FindResultText);

			viewer.FindWholeWord = true;
			Assert.Equal("– / 2", viewer.FindResultText);

			viewer.FindWholeWord = false;
			viewer.FindMatchCase = true;
			Assert.Equal("– / 2", viewer.FindResultText);

			viewer.FindMatchCase = false;
			viewer.FindLevel = LogEntryLevel.Error;
			Assert.Equal("– / 1", viewer.FindResultText);

			viewer.FindLevel = null;
			viewer.FindText = "timeout";
			Assert.Equal("No results", viewer.FindResultText);
			viewer.SearchFields = SearchFields.All;
			Assert.Equal("– / 1", viewer.FindResultText);
		});
	}

	[Fact]
	public void AnInvalidSearchRegex_FindsNothingAndSaysSo()
	{
		WithViewer(viewer =>
		{
			viewer.FindUseRegex = true;
			viewer.FindText = "(unclosed";

			Assert.NotNull(viewer.FindError);
			Assert.Equal("Invalid pattern", viewer.FindResultText);

			viewer.FindText = "c.t";
			Assert.Null(viewer.FindError);
			Assert.Equal("– / 3", viewer.FindResultText);
		});
	}

	[Fact]
	public void TheSearchFields_TravelWithTheFilterState()
	{
		WithViewer(viewer =>
		{
			Assert.True(viewer.GetFilterState().IsEmpty);

			viewer.SearchFields = SearchFields.All;
			var state = viewer.GetFilterState();
			Assert.False(state.IsEmpty);
			Assert.Equal(SearchFields.All, state.SearchFields);

			viewer.SearchFields = SearchFields.Default;
			viewer.ApplyFilterState(state);
			Assert.Equal(SearchFields.All, viewer.SearchFields);
		});
	}
}
