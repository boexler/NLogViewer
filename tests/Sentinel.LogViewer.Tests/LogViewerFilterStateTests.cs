using System.Collections.ObjectModel;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the filter state of the log viewer: reading the filters, applying them to another viewer, and following the changes
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerFilterStateTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerFilterStateTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Shop.Orders", "order created"),
		new LogEntry(LogEntryLevel.Debug, "Acme.Shop.Orders", "debug detail"),
		new LogEntry(LogEntryLevel.Error, "Acme.Billing", "timeout while charging"),
		new LogEntry(LogEntryLevel.Warn, "Acme.Billing", "retrying")
	};

	private static LogViewerFilterState SampleState() => new()
	{
		HiddenLevels = { "Trace", "Debug" },
		Terms = { new LogViewerFilterTerm { Text = "timeout", IsRegex = false }, new LogViewerFilterTerm { Text = "retry", IsExclude = true, MatchCase = true, WholeWord = true, Level = LogEntryLevel.Error } },
		LoggerRules = { "-Acme.Shop" },
		TimeStampFormat = TimeStampFormat.Iso
	};

	[Fact]
	public void AFreshViewer_HasNoFilters()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				Assert.True(viewer.GetFilterState().IsEmpty);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheFiltersOfOneViewer_CanBeApplied_ToAnother()
	{
		_context.RunOnSta(() =>
		{
			var (windowA, viewerA) = WpfTestHelper.ShowInWindow(Entries());
			var (windowB, viewerB) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewerA.ApplyFilterState(SampleState());

				viewerB.ApplyFilterState(viewerA.GetFilterState());

				var state = viewerB.GetFilterState();
				Assert.True(state.IsSameAs(SampleState()));
				Assert.True(viewerB.DebugFilter);
				Assert.True(viewerB.TraceFilter);
				Assert.False(viewerB.InfoFilter);
				Assert.Equal(TimeStampFormat.Iso, viewerB.CustomTimeStampFormat);
				Assert.Equal(new[] { "-Acme.Shop" }, viewerB.LoggerTree.Rules.ToList());
				Assert.Equal(new[] { "timeout", "retry" }, viewerB.ActiveSearchTerms.Select(term => term.Text));
				Assert.True(viewerB.ActiveSearchTerms[1].IsExclude);
			}
			finally
			{
				windowA.Close();
				windowB.Close();
			}
		});
	}

	[Fact]
	public void TheFilterState_FollowsTheChangesOfTheViewer()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.TraceFilter = true;
				Assert.Equal(new[] { "Trace" }, viewer.FilterState?.HiddenLevels);

				viewer.AddRegexSearchTermCommand.Execute("order");
				Assert.Equal("order", Assert.Single(viewer.FilterState!.Terms).Text);

				viewer.CustomTimeStampFormat = TimeStampFormat.German;
				Assert.Equal(TimeStampFormat.German, viewer.FilterState!.TimeStampFormat);

				viewer.LoggerTree.Rules.Load(new[] { "-Acme.Billing" });
				Assert.Equal(new[] { "-Acme.Billing" }, viewer.FilterState!.LoggerRules);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void SettingTheFilterState_ChangesWhatTheViewerShows()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.FilterState = new LogViewerFilterState { HiddenLevels = { "Debug" }, LoggerRules = { "-Acme.Billing" } };
				WpfTestHelper.Settle();

				var shown = viewer.LogEvents.View.Cast<LogEntry>().Select(entry => entry.Message).ToList();
				Assert.Equal(new[] { "order created" }, shown);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void ATermThatIsNotAValidRegex_IsLeftOut_AndTheRestIsApplied()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.ApplyFilterState(new LogViewerFilterState
				{
					Terms = { new LogViewerFilterTerm { Text = "(unclosed", IsRegex = true }, new LogViewerFilterTerm { Text = "order" } }
				});

				Assert.Equal("order", Assert.Single(viewer.ActiveSearchTerms).Text);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TheState_DoesNotChange_WhenTheFiltersStayTheSame()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.TraceFilter = true;
				var first = viewer.FilterState;

				viewer.TraceFilter = true;
				viewer.ApplyFilterState(viewer.GetFilterState());

				Assert.Same(first, viewer.FilterState);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void ATermThatIsOff_StaysOffInTheState_InACopy_AndWhenItIsApplied_AndAStateFromBeforeTheSwitchHasAllTermsOn()
	{
		var state = new LogViewerFilterState { Terms = { new LogViewerFilterTerm { Text = "a", IsEnabled = false }, new LogViewerFilterTerm { Text = "b" } } };

		Assert.Equal(new[] { false, true }, state.Clone().Terms.Select(term => term.IsEnabled));
		Assert.False(state.IsSameAs(new LogViewerFilterState { Terms = { new LogViewerFilterTerm { Text = "a" }, new LogViewerFilterTerm { Text = "b" } } }));

		var old = System.Text.Json.JsonSerializer.Deserialize<LogViewerFilterTerm>("""{ "Text": "a", "IsExclude": true }""")!;
		Assert.True(old.IsEnabled);

		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.ApplyFilterState(state);
				Assert.Equal(new[] { false, true }, viewer.ActiveSearchTerms.Select(term => term.IsEnabled));
				Assert.Equal(new[] { false, true }, viewer.GetFilterState().Terms.Select(term => term.IsEnabled));
			}
			finally
			{
				window.Close();
			}
		});
	}}
