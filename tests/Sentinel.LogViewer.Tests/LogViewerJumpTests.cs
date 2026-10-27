using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for jumping through the list: to the next or previous entry of a level, and to a time
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerJumpTests
{
	private readonly WpfStaContextFixture _context;

	public LogViewerJumpTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static DateTime Utc(int hour) => new(2025, 12, 18, hour, 0, 0, DateTimeKind.Utc);

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "A", "info 1") { TimeStamp = Utc(10) },
		new LogEntry(LogEntryLevel.Error, "A", "error 1") { TimeStamp = Utc(11) },
		new LogEntry(LogEntryLevel.Info, "A", "info 2") { TimeStamp = Utc(12) },
		new LogEntry(LogEntryLevel.Warn, "B", "warn 1") { TimeStamp = Utc(13) },
		new LogEntry(LogEntryLevel.Error, "B", "error 2") { TimeStamp = Utc(14) },
		new LogEntry(LogEntryLevel.Info, "A", "info 3") { TimeStamp = Utc(15) }
	};

	private void WithViewer(Action<Wpf.LogViewer, ListView, ObservableCollection<LogEntry>> test) => _context.RunOnSta(() =>
	{
		var entries = Entries();
		var (window, viewer) = WpfTestHelper.ShowInWindow(entries);
		try
		{
			test(viewer, (ListView)viewer.Template.FindName("PART_ListView", viewer), entries);
		}
		finally
		{
			window.Close();
		}
	});

	private static string? Selected(ListView listView) => (listView.SelectedItem as LogEntry)?.Message;

	[Fact]
	public void TheNextEntryOfALevel_IsFoundFromTheSelection_AndTheSearchDoesNotWrapAround() =>
		WithViewer((viewer, listView, _) =>
		{
			Assert.True(viewer.JumpToLevel(LogEntryLevel.Error, forward: true));
			Assert.Equal("error 1", Selected(listView));

			Assert.True(viewer.JumpToLevel(LogEntryLevel.Error, forward: true));
			Assert.Equal("error 2", Selected(listView));

			Assert.False(viewer.JumpToLevel(LogEntryLevel.Error, forward: true));
			Assert.Equal("error 2", Selected(listView));
			Assert.Equal("No further Error entry", viewer.JumpText);

			Assert.True(viewer.JumpToLevel(LogEntryLevel.Error, forward: false));
			Assert.Equal("error 1", Selected(listView));
			Assert.Equal(string.Empty, viewer.JumpText);

			Assert.False(viewer.JumpToLevel(LogEntryLevel.Error, forward: false));
			Assert.Equal("No earlier Error entry", viewer.JumpText);
		});

	[Fact]
	public void WithoutASelection_ItStartsAtTheTopOrAtTheBottom() =>
		WithViewer((viewer, listView, _) =>
		{
			Assert.True(viewer.JumpToLevel(LogEntryLevel.Info, forward: true));
			Assert.Equal("info 1", Selected(listView));

			listView.SelectedItem = null;
			Assert.True(viewer.JumpToLevel(LogEntryLevel.Info, forward: false));
			Assert.Equal("info 3", Selected(listView));

			Assert.False(viewer.JumpToLevel(LogEntryLevel.Fatal, forward: true));
		});

	[Fact]
	public void OnlyTheEntriesThatAreShownCount_AndAJumpStopsFollowingNewEntries() =>
		WithViewer((viewer, listView, _) =>
		{
			viewer.ErrorFilter = true;
			WpfTestHelper.Settle();
			Assert.False(viewer.JumpToLevel(LogEntryLevel.Error, forward: true));

			viewer.ErrorFilter = false;
			viewer.AutoScroll = true;
			WpfTestHelper.Settle();
			Assert.True(viewer.JumpToLevel(LogEntryLevel.Warn, forward: true));
			Assert.Equal("warn 1", Selected(listView));
			Assert.False(viewer.AutoScroll);
		});

	[Fact]
	public void AJumpToATime_SelectsTheFirstEntryFromThenOn_OrTheLastOneAndSaysSo() =>
		WithViewer((viewer, listView, _) =>
		{
			Assert.True(viewer.GoToTime(Utc(12)));
			Assert.Equal("info 2", Selected(listView));

			Assert.True(viewer.GoToTime(Utc(12).AddMinutes(1)));
			Assert.Equal("warn 1", Selected(listView));

			Assert.False(viewer.GoToTime(Utc(23)));
			Assert.Equal("info 3", Selected(listView));
			Assert.NotEqual(string.Empty, viewer.JumpText);
		});

	[Fact]
	public void TheKeys_F4ToF9_JumpToTheLevelsInOrder_WithShiftBackwards_AndCtrlTGoesToATime() =>
		WithViewer((viewer, _, _) =>
		{
			var bindings = viewer.InputBindings.OfType<KeyBinding>().ToList();
			var levels = new[] { LogEntryLevel.Trace, LogEntryLevel.Debug, LogEntryLevel.Info, LogEntryLevel.Warn, LogEntryLevel.Error, LogEntryLevel.Fatal };
			var keys = new[] { Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9 };

			for (var i = 0; i < levels.Length; i++)
			{
				var next = bindings.Single(binding => binding.Key == keys[i] && binding.Modifiers == ModifierKeys.None);
				var previous = bindings.Single(binding => binding.Key == keys[i] && binding.Modifiers == ModifierKeys.Shift);
				Assert.Same(viewer.NextLevelCommand, next.Command);
				Assert.Equal(levels[i], next.CommandParameter);
				Assert.Same(viewer.PreviousLevelCommand, previous.Command);
				Assert.Equal(levels[i], previous.CommandParameter);
			}

			Assert.Contains(LogViewerCommands.GoToTime.InputGestures.OfType<KeyGesture>(), gesture => gesture.Key == Key.T && gesture.Modifiers == ModifierKeys.Control);
		});
}
