using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the time range of the viewer: the entries of a period, the commands of a row, the filters as one value, and the zones
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerTimeRangeTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

	private readonly WpfStaContextFixture _context;

	public LogViewerTimeRangeTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static DateTime Utc(int hour, int minute = 0) => new(2025, 12, 18, hour, minute, 0, DateTimeKind.Utc);

	private static ObservableCollection<LogEntry> Entries() => new()
	{
		new LogEntry(LogEntryLevel.Info, "A", "ten") { TimeStamp = Utc(10) },
		new LogEntry(LogEntryLevel.Info, "A", "eleven") { TimeStamp = Utc(11) },
		new LogEntry(LogEntryLevel.Info, "A", "twelve") { TimeStamp = Utc(12) },
		// 14:00 in a file without a zone: 05:00 UTC when the file was written in Tokyo.
		new LogEntry(LogEntryLevel.Info, "A", "written") { TimeStamp = new DateTime(2025, 12, 18, 14, 0, 0) }
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

	private static string[] Visible(Wpf.LogViewer viewer) =>
		viewer.LogEvents.View.Cast<LogEntry>().Select(entry => entry.Message).ToArray();

	[Fact]
	public void OnlyTheEntriesOfThePeriodAreShown_AndBothEndsCount() =>
		WithViewer((viewer, entries) =>
		{
			viewer.FileTimeZone = Tokyo;

			viewer.TimeFrom = Utc(11);
			Assert.Equal(new[] { "eleven", "twelve" }, Visible(viewer));
			Assert.True(viewer.HasTimeRange);

			viewer.TimeTo = Utc(11);
			Assert.Equal(new[] { "eleven" }, Visible(viewer));

			viewer.TimeFrom = null;
			viewer.TimeTo = Utc(5);
			Assert.Equal(new[] { "written" }, Visible(viewer));

			viewer.TimeTo = null;
			Assert.Equal(4, Visible(viewer).Length);
			Assert.False(viewer.HasTimeRange);
		});

	[Fact]
	public void ANewEntryOutsideThePeriod_IsNotShown() =>
		WithViewer((viewer, entries) =>
		{
			viewer.TimeFrom = Utc(11);
			entries.Add(new LogEntry(LogEntryLevel.Info, "A", "late") { TimeStamp = Utc(13) });
			entries.Add(new LogEntry(LogEntryLevel.Info, "A", "early") { TimeStamp = Utc(9) });
			WpfTestHelper.Settle();

			Assert.Contains("late", Visible(viewer));
			Assert.DoesNotContain("early", Visible(viewer));
		});

	[Fact]
	public void TheZoneOfTheFile_DecidesWhereATimeWithoutAZoneIs() =>
		WithViewer((viewer, entries) =>
		{
			viewer.TimeFrom = Utc(5);
			viewer.TimeTo = Utc(5, 30);

			viewer.FileTimeZone = Tokyo;
			Assert.Equal(new[] { "written" }, Visible(viewer));

			viewer.FileTimeZone = TimeZoneInfo.Utc;
			Assert.Empty(Visible(viewer));
		});

	[Fact]
	public void TheRowCommands_SetTheEnds_AndAnEndThatWouldBeWrongIsCleared() =>
		WithViewer((viewer, entries) =>
		{
			viewer.TimeFromHereCommand.Execute(entries[1]);
			Assert.Equal((Utc(11), (DateTime?)null), (viewer.TimeFrom!.Value, viewer.TimeTo));

			viewer.TimeUntilHereCommand.Execute(entries[2]);
			Assert.Equal((Utc(11), Utc(12)), (viewer.TimeFrom!.Value, viewer.TimeTo!.Value));
			Assert.Equal(new[] { "eleven", "twelve" }, Visible(viewer));

			// A start behind the end: the end goes.
			viewer.TimeFromHereCommand.Execute(new LogEntry(LogEntryLevel.Info, "A", "x") { TimeStamp = Utc(13) });
			Assert.Equal((Utc(13), (DateTime?)null), (viewer.TimeFrom!.Value, viewer.TimeTo));

			// An end before the start: the start goes.
			viewer.TimeUntilHereCommand.Execute(entries[0]);
			Assert.Equal(((DateTime?)null, Utc(10)), (viewer.TimeFrom, viewer.TimeTo!.Value));

			Assert.True(viewer.ClearTimeRangeCommand.CanExecute(null));
			viewer.ClearTimeRangeCommand.Execute(null);
			Assert.False(viewer.ClearTimeRangeCommand.CanExecute(null));
			Assert.Equal(4, Visible(viewer).Length);
		});

	[Fact]
	public void TheTimeRangeIsPartOfTheFilterState_BothWays() =>
		WithViewer((viewer, entries) =>
		{
			viewer.TimeFrom = Utc(11);
			viewer.TimeTo = Utc(12);

			Assert.Equal((Utc(11), Utc(12)), (viewer.FilterState!.TimeFrom!.Value, viewer.FilterState.TimeTo!.Value));

			var state = viewer.GetFilterState();
			viewer.ClearTimeRangeCommand.Execute(null);
			Assert.Null(viewer.FilterState!.TimeFrom);

			viewer.ApplyFilterState(state);
			Assert.Equal((Utc(11), Utc(12)), (viewer.TimeFrom!.Value, viewer.TimeTo!.Value));
			Assert.Equal(new[] { "eleven", "twelve" }, Visible(viewer));
		});

	[Fact]
	public void TheRangeIsShownInTheZoneOfTheDisplay() =>
		WithViewer((viewer, _) =>
		{
			Assert.Equal(string.Empty, viewer.TimeRangeText);

			viewer.CustomTimeStampFormat = TimeStampFormat.Iso;
			viewer.DisplayTimeZone = TimeZoneInfo.Utc;
			viewer.TimeFrom = Utc(11);
			viewer.TimeTo = Utc(12, 30);
			Assert.Equal("2025-12-18 11:00:00.000 – 2025-12-18 12:30:00.000 (UTC)", viewer.TimeRangeText);

			viewer.DisplayTimeZone = Tokyo;
			Assert.Equal("2025-12-18 20:00:00.000 – 2025-12-18 21:30:00.000 (UTC+9)", viewer.TimeRangeText);
			Assert.Equal((Utc(11), Utc(12, 30)), (viewer.TimeFrom!.Value, viewer.TimeTo!.Value));

			// It is written like the list is: in the format of the time stamp column.
			viewer.CustomTimeStampFormat = TimeStampFormat.German;
			Assert.Equal("18.12.2025 20:00:00.000 – 18.12.2025 21:30:00.000 (UTC+9)", viewer.TimeRangeText);
		});

	[Fact]
	public void ControlShiftT_OpensTheDialogOfTheTimeRange() =>
		WithViewer((viewer, _) =>
		{
			var binding = viewer.InputBindings.OfType<KeyBinding>().Single(candidate => candidate.Key == Key.T && candidate.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift));

			Assert.Same(viewer.ChooseTimeRangeCommand, binding.Command);
		});

	[Fact]
	public void TheFirstAndTheLastEntry_AreOfferedToTheDialog_AsMoments() =>
		WithViewer((viewer, entries) =>
		{
			viewer.FileTimeZone = Tokyo;

			// 05:00 UTC for the entry without a zone, then 10:00 to 12:00 UTC.
			Assert.Equal((Utc(5), Utc(12)), (viewer.EntryTimeSpan().First!.Value, viewer.EntryTimeSpan().Last!.Value));

			entries.Clear();
			Assert.Equal(((DateTime?)null, (DateTime?)null), viewer.EntryTimeSpan());
		});

	[Fact]
	public void TheQuickChoices_CountBackFromTheLastEntry_AndLeaveTheEndOpen() =>
		WithViewer((viewer, entries) =>
		{
			viewer.FileTimeZone = Tokyo;

			// The last entry is at 12:00 UTC.
			viewer.TimeRangeLastCommand.Execute("1:00:00");
			Assert.Equal((Utc(11), (DateTime?)null), (viewer.TimeFrom!.Value, viewer.TimeTo));
			Assert.Equal(new[] { "eleven", "twelve" }, Visible(viewer));

			viewer.TimeRangeLastCommand.Execute("7.00:00:00");
			Assert.Equal(Utc(12).AddDays(-7), viewer.TimeFrom!.Value);
			Assert.Equal(4, Visible(viewer).Length);

			// Nothing that is not a length changes anything, and no entry means no range.
			viewer.TimeRangeLastCommand.Execute("soon");
			Assert.Equal(Utc(12).AddDays(-7), viewer.TimeFrom!.Value);
			entries.Clear();
			viewer.ClearTimeRangeCommand.Execute(null);
			viewer.TimeRangeLastCommand.Execute("1:00:00");
			Assert.False(viewer.HasTimeRange);
		});
}
