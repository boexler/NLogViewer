using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using NLog;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the logger tree of the log viewer: it follows the entries and hides loggers together with the other filters
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerLoggerTreeTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerLoggerTreeTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static LogEventInfo Entry(string logger, LogLevel level) => new(level, logger, "message");

	private static ObservableCollection<LogEventInfo> Entries() => new()
	{
		Entry("Acme.Shop.Orders", LogLevel.Info),
		Entry("Acme.Shop.Orders", LogLevel.Error),
		Entry("Acme.Shop.Payments", LogLevel.Warn),
		Entry("Acme.Core.Db", LogLevel.Info),
		Entry("Acme.Core.Db", LogLevel.Debug),
		Entry("System.Net", LogLevel.Info)
	};

	private static int VisibleCount(LogViewerBase viewer) => viewer.LogEvents.View.Cast<LogEventInfo>().Count();

	[Fact]
	public void Tree_ListsTheLoggersOfTheEntries_AndFollowsAddedAndRemovedEntries()
	{
		_context.RunOnSta(() =>
		{
			var entries = Entries();
			var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
			try
			{
				var acme = viewer.LoggerTree.Roots.Single(root => root.Name == "Acme");
				Assert.Equal(new[] { "Acme", "System" }, viewer.LoggerTree.Roots.Select(root => root.Name));
				Assert.Equal((5, 1, 1), (acme.TotalCount, acme.WarnCount, acme.ErrorCount));

				entries.Add(Entry("Acme.Core.Cache", LogLevel.Fatal));
				Assert.Equal((6, 2), (acme.TotalCount, acme.ErrorCount));
				Assert.Contains(acme.Children.Single(child => child.Name == "Core").Children, child => child.Name == "Cache");

				entries.RemoveAt(0);
				Assert.Equal(5, acme.TotalCount);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HidingALogger_HidesItsEntriesAndEverythingBelow_AndShowingBringsThemBack()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				Assert.Equal(6, VisibleCount(viewer));
				var shop = viewer.LoggerTree.Roots.Single(root => root.Name == "Acme").Children.Single(child => child.Name == "Shop");

				shop.IsChecked = false;
				Assert.Equal(3, VisibleCount(viewer));

				shop.IsChecked = true;
				Assert.Equal(6, VisibleCount(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void ShowOnlyThisLogger_HidesAllOthers_AlsoLoggersThatAppearLater()
	{
		_context.RunOnSta(() =>
		{
			var entries = Entries();
			var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
			try
			{
				var orders = viewer.LoggerTree.Roots.Single(root => root.Name == "Acme").Children
					.Single(child => child.Name == "Shop").Children.Single(child => child.Name == "Orders");

				viewer.LoggerTree.ShowOnlyCommand.Execute(orders);
				entries.Add(Entry("Brand.New", LogLevel.Info));

				Assert.Equal(2, VisibleCount(viewer));

				viewer.LoggerTree.ShowAllCommand.Execute(null);
				Assert.Equal(7, VisibleCount(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HiddenLoggers_WorkTogetherWithTheLevelToggles()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.LoggerTree.Rules.SetVisible("System", false);
				Assert.Equal(5, VisibleCount(viewer));

				viewer.DebugFilter = true;
				Assert.Equal(4, VisibleCount(viewer));

				viewer.LoggerTree.Rules.SetVisible("System", true);
				Assert.Equal(5, VisibleCount(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HiddenLoggers_WorkTogetherWithFilterTerms()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.CurrentSearchText = "Core";
				viewer.AddSearchTerm();
				Assert.Equal(2, VisibleCount(viewer));

				viewer.LoggerTree.Rules.SetVisible("Acme.Core.Db", false);

				Assert.Equal(0, VisibleCount(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void LoggerFilterRules_FollowTheTree_AndRestoreThemWhenSet()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				viewer.LoggerTree.Rules.SetVisible("Acme", false);
				viewer.LoggerTree.Rules.SetVisible("Acme.Core", true);
				Assert.Equal(new[] { "+Acme.Core", "-Acme" }.OrderBy(rule => rule.TrimStart('+', '-'), System.StringComparer.Ordinal), viewer.LoggerFilterRules);
				Assert.Equal(3, VisibleCount(viewer));

				viewer.LoggerFilterRules = new[] { "-System" };

				Assert.Equal(5, VisibleCount(viewer));
				Assert.False(viewer.LoggerTree.IsVisible("System.Net"));
				Assert.True(viewer.LoggerTree.IsVisible("Acme.Shop.Orders"));

				viewer.LoggerFilterRules = System.Array.Empty<string>();
				Assert.Equal(6, VisibleCount(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void ClearingTheEntries_EmptiesTheTree_ButKeepsTheRules()
	{
		_context.RunOnSta(() =>
		{
			var entries = Entries();
			var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
			try
			{
				viewer.LoggerTree.Rules.SetVisible("System", false);

				entries.Clear();

				Assert.Empty(viewer.LoggerTree.Roots);
				Assert.Equal(new[] { "-System" }, viewer.LoggerFilterRules);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void Panel_IsClosedByDefault_OpensWithTheProperty_AndIsGoneWithoutTheTree()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer) = WpfTestHelper.ShowInWindow(Entries());
			try
			{
				var panel = (FrameworkElement)viewer.Template.FindName("PART_LoggerTreePanel", viewer);
				var handle = (FrameworkElement)viewer.Template.FindName("PART_LoggerTreeHandle", viewer);
				Assert.Equal(Visibility.Collapsed, panel.Visibility);
				Assert.Equal(Visibility.Visible, handle.Visibility);

				viewer.IsLoggerTreeExpanded = true;
				Assert.Equal(Visibility.Visible, panel.Visibility);
				Assert.Equal(viewer.LoggerTreeWidth, panel.Width);

				viewer.ShowLoggerTree = false;
				Assert.Equal(Visibility.Collapsed, panel.Visibility);
				Assert.Equal(Visibility.Collapsed, handle.Visibility);
			}
			finally
			{
				window.Close();
			}
		});
	}
}
