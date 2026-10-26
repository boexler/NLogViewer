using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the menu of a row: the logger, the time range and jumping, besides what the menu of a cell has
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerRowMenuTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerRowMenuTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "Acme.Shop.Orders", "created") { TimeStamp = new DateTime(2025, 12, 18, 10, 0, 0, DateTimeKind.Utc) },
		new LogEntry(LogEntryLevel.Error, "Acme.Billing", "failed") { TimeStamp = new DateTime(2025, 12, 18, 11, 0, 0, DateTimeKind.Utc) }
	};

	private void WithViewer(Action<Wpf.LogViewer, ObservableCollection<LogEntry>> test) => _context.RunOnSta(() =>
	{
		var entries = Entries();
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			test(viewer, entries);
		}
		finally
		{
			window.Close();
		}
	});

	private static string[] Headers(ContextMenu menu) => menu.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray();

	[Fact]
	public void TheMenuOfARowWithoutAMenuOfItsOwn_OffersTheLoggerTheTimeRangeAndJumping() =>
		WithViewer((viewer, entries) =>
		{
			var menu = new ContextMenu();

			viewer.AddRowActions(menu, entries[1]);

			Assert.Equal(new[]
			{
				"Show only this logger", "Hide this logger", "Show in logger tree", "Time range: from here", "Time range: until here",
				"Go to time...", "Next warning", "Previous warning", "Next error", "Previous error"
			}, Headers(menu));
			var items = menu.Items.OfType<MenuItem>().ToDictionary(item => (string)item.Header);
			Assert.Same(viewer.ShowOnlyLoggerCommand, items["Show only this logger"].Command);
			Assert.Equal("Acme.Billing", items["Hide this logger"].CommandParameter);
			Assert.Same(viewer.TimeFromHereCommand, items["Time range: from here"].Command);
			Assert.Same(entries[1], items["Time range: until here"].CommandParameter);
			Assert.Same(viewer.NextLevelCommand, items["Next error"].Command);
			Assert.Equal(LogEntryLevel.Error, items["Next error"].CommandParameter);
			Assert.Equal("F7", items["Next warning"].InputGestureText);
			Assert.Equal("Shift+F8", items["Previous error"].InputGestureText);
		});

	[Fact]
	public void TheMenuOfTheLoggerCell_HasNoSecondOfferToShowOnlyOrHideTheLogger() =>
		WithViewer((viewer, entries) =>
		{
			var menu = new ContextMenu();
			menu.Items.Add(new MenuItem { Header = "Show only this logger" });
			menu.Items.Add(new MenuItem { Header = "Hide this logger" });

			viewer.AddRowActions(menu, entries[0]);

			Assert.Single(Headers(menu), "Show only this logger");
			Assert.Single(Headers(menu), "Hide this logger");
			Assert.Contains("Time range: from here", Headers(menu));
		});

	[Fact]
	public void AskingAgain_ReplacesTheActions_AndTheEntryIsTheOneOfTheRow() =>
		WithViewer((viewer, entries) =>
		{
			var menu = new ContextMenu();
			menu.Items.Add(new MenuItem { Header = "Add to Filter" });

			viewer.AddRowActions(menu, entries[0]);
			var count = menu.Items.Count;
			viewer.AddRowActions(menu, entries[1]);

			Assert.Equal(count, menu.Items.Count);
			Assert.Equal("Add to Filter", Headers(menu)[0]);
			Assert.IsType<Separator>(menu.Items[1]);
			var from = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Time range: from here");
			Assert.Same(entries[1], from.CommandParameter);
		});

	[Fact]
	public void ClearingTheTimeRange_IsOnlyOfferedWhileThereIsOne() =>
		WithViewer((viewer, entries) =>
		{
			var menu = new ContextMenu();
			viewer.AddRowActions(menu, entries[0]);
			Assert.DoesNotContain("Clear the time range", Headers(menu));

			viewer.TimeFromHereCommand.Execute(entries[0]);
			viewer.AddRowActions(menu, entries[0]);

			Assert.Contains("Clear the time range", Headers(menu));
		});

	[Fact]
	public void ShowingALoggerInTheTree_OpensTheTree_OpensTheLoggersAboveIt_AndSelectsIt() =>
		WithViewer((viewer, entries) =>
		{
			var node = viewer.LoggerTree.Find("Acme.Shop.Orders")!;
			foreach (var parent in new[] { node.Parent!, node.Parent!.Parent! })
				parent.IsExpanded = false;
			viewer.LoggerTree.SearchText = "nothing like it";

			viewer.ShowInLoggerTreeCommand.Execute("Acme.Shop.Orders");

			Assert.True(viewer.IsLoggerTreeExpanded);
			Assert.True(node.IsSelected);
			Assert.True(node.Parent!.IsExpanded);
			Assert.True(node.Parent.Parent!.IsExpanded);
			Assert.Equal(string.Empty, viewer.LoggerTree.SearchText);
			Assert.False(viewer.ShowInLoggerTreeCommand.CanExecute(""));
			Assert.Null(viewer.LoggerTree.Find("No.Such.Logger"));
		});
}
