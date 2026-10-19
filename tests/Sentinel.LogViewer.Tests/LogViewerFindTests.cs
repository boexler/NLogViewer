using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the search box: hits are counted and selected without hiding entries
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerFindTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerFindTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	/// <summary>
	/// "Entry 1" is found in Entry 1, Entry 10-19 and Entry 100-199: 111 hits; even entries are Info, odd ones Debug
	/// </summary>
	private static (Window Window, Wpf.LogViewer Viewer, ObservableCollection<LogEntry> Entries, ListView List) ShowViewer()
	{
		var entries = new ObservableCollection<LogEntry>(Enumerable.Range(0, 1000).Select(i =>
			new LogEntry(i % 2 == 0 ? LogEntryLevel.Info : LogEntryLevel.Debug, "Logger", $"Entry {i}")));
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		return (window, viewer, entries, (ListView)viewer.Template.FindName("PART_ListView", viewer));
	}

	private static string SelectedMessage(ListView listView) => ((LogEntry)listView.SelectedItem).Message;

	[Fact]
	public void FindText_CountsHitsWithoutHidingEntries()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _, listView) = ShowViewer();
			try
			{
				viewer.FindText = "entry 1";

				Assert.Equal("– / 111", viewer.FindResultText);
				Assert.Equal(1000, listView.Items.Count);

				viewer.FindText = "nothing like this";
				Assert.Equal("No results", viewer.FindResultText);

				viewer.FindText = string.Empty;
				Assert.Equal(string.Empty, viewer.FindResultText);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void FindNextAndPrevious_SelectHitsAndWrapAround()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _, listView) = ShowViewer();
			try
			{
				viewer.AutoScroll = true;
				viewer.FindText = "Entry 1";

				viewer.FindNextCommand.Execute(null);
				Assert.Equal("Entry 1", SelectedMessage(listView));
				Assert.Equal("1 / 111", viewer.FindResultText);
				Assert.False(viewer.AutoScroll);

				viewer.FindNextCommand.Execute(null);
				Assert.Equal("Entry 10", SelectedMessage(listView));

				viewer.FindPreviousCommand.Execute(null);
				viewer.FindPreviousCommand.Execute(null);
				Assert.Equal("Entry 199", SelectedMessage(listView));
				Assert.Equal("111 / 111", viewer.FindResultText);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void FindNext_WithoutCurrentHit_StartsAfterTheSelectedEntry()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries, listView) = ShowViewer();
			try
			{
				listView.SelectedItem = entries[150];
				viewer.FindText = "Entry 1";

				viewer.FindNextCommand.Execute(null);

				Assert.Equal("Entry 151", SelectedMessage(listView));
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void HitCount_FollowsFilterChangesAndNewEntries()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, entries, _) = ShowViewer();
			try
			{
				viewer.FindText = "Entry 1";

				// Debug hides the odd entries: Entry 10-18 and 100-198, even only.
				viewer.DebugFilter = true;
				WpfTestHelper.Settle();
				Assert.Equal("– / 55", viewer.FindResultText);

				entries.Add(new LogEntry(LogEntryLevel.Info, "Logger", "Entry 1000"));
				WpfTestHelper.Settle();
				Assert.Equal("– / 56", viewer.FindResultText);
			}
			finally
			{
				window.Close();
			}
		});
	}

	[Fact]
	public void FindText_HighlightsHitsInVisibleRows()
	{
		_context.RunOnSta(() =>
		{
			var (window, viewer, _, listView) = ShowViewer();
			try
			{
				viewer.FindText = "Entry 1";
				WpfTestHelper.Settle();

				var highlightedRuns = Descendants(listView).OfType<TextBlock>()
					.SelectMany(textBlock => textBlock.Inlines.OfType<Run>())
					.Where(run => ReferenceEquals(run.Background, viewer.FindHighlightBackground))
					.Select(run => run.Text)
					.ToList();

				Assert.Contains("Entry 1", highlightedRuns);
			}
			finally
			{
				window.Close();
			}
		});
	}

	private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			yield return child;
			foreach (var descendant in Descendants(child))
				yield return descendant;
		}
	}
}
