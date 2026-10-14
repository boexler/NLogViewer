using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NLog;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that changing filters keeps the entry the user is looking at in view
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerScrollPositionTests
{
	private const int EntryCount = 1000;
	private readonly WpfStaContextFixture _context;

	public LogViewerScrollPositionTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	/// <summary>
	/// Shows a viewer with alternating Info and Debug entries in an off-screen window
	/// </summary>
	private static (Window Window, LogViewerBase Viewer, ObservableCollection<LogEventInfo> Entries) ShowViewer()
	{
		var entries = new ObservableCollection<LogEventInfo>(Enumerable.Range(0, EntryCount).Select(i =>
			new LogEventInfo(i % 2 == 0 ? LogLevel.Info : LogLevel.Debug, "Logger", $"Entry {i}")));
		var viewer = new LogViewerBase { ItemsSource = entries, AutoScroll = false };
		var window = new Window
		{
			Width = 900,
			Height = 500,
			Left = -10000,
			Top = -10000,
			ShowActivated = false,
			ShowInTaskbar = false,
			Content = viewer
		};
		window.Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml")
		});
		window.Show();
		Settle();
		return (window, viewer, entries);
	}

	private static void Settle()
	{
		for (var i = 0; i < 3; i++)
			Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
	}

	private static ScrollViewer ScrollViewerOf(LogViewerBase viewer)
	{
		var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
		return (ScrollViewer)ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer))!;
	}

	private static string TopMessage(LogViewerBase viewer)
	{
		var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
		return ((LogEventInfo)listView.Items[(int)ScrollViewerOf(viewer).VerticalOffset]!).FormattedMessage;
	}

	private static void ScrollToEntry(LogViewerBase viewer, int viewIndex)
	{
		ScrollViewerOf(viewer).ScrollToVerticalOffset(viewIndex);
		Settle();
	}

	[Fact]
	public void ShowingALevel_KeepsTheTopEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _) = ShowViewer();
			try
			{
				viewer.DebugFilter = true;
				Settle();
				ScrollToEntry(viewer, 200);
				Assert.Equal("Entry 400", TopMessage(viewer));

				viewer.DebugFilter = false;
				Settle();

				Assert.Equal("Entry 400", TopMessage(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HidingTheLevelOfTheTopEntry_KeepsTheNearestEarlierEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _) = ShowViewer();
			try
			{
				ScrollToEntry(viewer, 401);
				Assert.Equal("Entry 401", TopMessage(viewer));

				viewer.DebugFilter = true;
				Settle();

				Assert.Equal("Entry 400", TopMessage(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HidingAndShowingALevel_WhenTheRestFitsTheWindow_ReturnsToTheTopEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries) = ShowViewer();
			try
			{
				// Leaves only a handful of Info entries, which fit into the window without scrolling.
				for (var i = 0; i < EntryCount; i += 2)
				{
					if (i % 200 != 0)
						entries[i].Level = LogLevel.Trace;
				}
				ScrollToEntry(viewer, 401);
				Assert.Equal("Entry 401", TopMessage(viewer));

				viewer.DebugFilter = true;
				viewer.TraceFilter = true;
				Settle();
				viewer.TraceFilter = false;
				viewer.DebugFilter = false;
				Settle();

				Assert.Equal("Entry 401", TopMessage(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TogglingALevel_KeepsTheSelectedEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries) = ShowViewer();
			try
			{
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				listView.SelectedItem = entries[400];
				Settle();

				viewer.DebugFilter = true;
				Settle();

				Assert.Same(entries[400], listView.SelectedItem);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void TogglingALevel_KeepsTheSelectedEntryInItsRow()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries) = ShowViewer();
			try
			{
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				ScrollToEntry(viewer, 400);
				listView.SelectedItem = entries[410];
				Settle();

				viewer.DebugFilter = true;
				Settle();

				// Entry 410 stays ten rows below the top; only even entries are left above it.
				Assert.Equal("Entry 390", TopMessage(viewer));
				Assert.Same(entries[410], listView.SelectedItem);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HidingAndShowingTheSelectedEntry_ReturnsToItAndSelectsItAgain()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries) = ShowViewer();
			try
			{
				var listView = (ListView)viewer.Template.FindName("PART_ListView", viewer);
				ScrollToEntry(viewer, 400);
				listView.SelectedItem = entries[411];
				Settle();

				viewer.DebugFilter = true;
				Settle();
				// Entry 410 takes the row of the hidden Entry 411.
				Assert.Equal("Entry 388", TopMessage(viewer));

				viewer.DebugFilter = false;
				Settle();

				Assert.Equal("Entry 400", TopMessage(viewer));
				Assert.Same(entries[411], listView.SelectedItem);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void AddingAFilterTerm_KeepsTheTopEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _) = ShowViewer();
			try
			{
				ScrollToEntry(viewer, 600);
				Assert.Equal("Entry 600", TopMessage(viewer));

				// Keeps Entry 6, 60-69 and 600-699.
				viewer.AddRegexSearchTerm("Entry 6");
				Settle();

				Assert.Equal("Entry 600", TopMessage(viewer));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void AtTheEnd_TogglingALevel_StaysAtTheEnd()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _) = ShowViewer();
			try
			{
				ScrollViewerOf(viewer).ScrollToEnd();
				Settle();

				viewer.DebugFilter = true;
				Settle();

				var scrollViewer = ScrollViewerOf(viewer);
				Assert.Equal(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset);
			}
			finally
			{
				window.Close();
			}
		});
	}
}
