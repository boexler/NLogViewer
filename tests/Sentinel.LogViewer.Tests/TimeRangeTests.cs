using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.Wpf.Helper;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the time range without a window: which entries are in it, what the user types, and where it is kept and where it is not
/// </summary>
public class TimeRangeTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

	private static DateTime Utc(int hour, int minute = 0, int second = 0) => new(2025, 12, 18, hour, minute, second, DateTimeKind.Utc);

	[Fact]
	public void BothEndsAreInclusive_AnOpenEndIsNull_AndNothingGivenMeansEverything()
	{
		var moment = Utc(10);

		Assert.True(EntryTime.IsInRange(moment, null, Utc(10), Utc(10)));
		Assert.False(EntryTime.IsInRange(moment, null, Utc(10, 0, 1), null));
		Assert.False(EntryTime.IsInRange(moment, null, null, Utc(9, 59, 59)));
		Assert.True(EntryTime.IsInRange(moment, null, null, null));
	}

	[Fact]
	public void ATimeWithoutAZone_IsComparedInTheZoneOfTheFile()
	{
		var written = new DateTime(2025, 12, 18, 14, 0, 0);

		// 14:00 in Tokyo is 05:00 UTC.
		Assert.True(EntryTime.IsInRange(written, Tokyo, Utc(5), Utc(5, 30)));
		Assert.False(EntryTime.IsInRange(written, Tokyo, Utc(6), null));
	}

	[Fact]
	public void TheTimeRangeIsPartOfTheFilters_ButNotOfAPresetOrOfWhatANewTabTakesOver()
	{
		var state = new LogViewerFilterState { HiddenLevels = { "Debug" }, TimeFrom = Utc(10), TimeTo = Utc(11) };

		Assert.False(state.IsEmpty);
		Assert.False(new LogViewerFilterState { TimeFrom = Utc(10) }.IsEmpty);
		Assert.True(new LogViewerFilterState().IsEmpty);
		Assert.False(state.IsSameAs(new LogViewerFilterState { HiddenLevels = { "Debug" }, TimeFrom = Utc(10) }));
		Assert.True(state.IsSameAs(state.Clone()));
		Assert.Equal((Utc(10), Utc(11)), (state.Clone().TimeFrom!.Value, state.Clone().TimeTo!.Value));

		var without = state.WithoutTimeRange();
		Assert.Null(without.TimeFrom);
		Assert.Null(without.TimeTo);
		Assert.Equal(new[] { "Debug" }, without.HiddenLevels);
		Assert.Equal(Utc(10), state.TimeFrom);

		var preset = FilterPresetService.ForPreset(state);
		Assert.Null(preset.TimeFrom);
		Assert.Null(preset.TimeTo);
	}

	[Fact]
	public void ANewTabOfAnApplication_TakesOverTheFilters_WithoutTheTimeRange()
	{
		using var environment = new SettingsTestEnvironment();
		var service = new ApplicationTabService(environment.Configuration);
		var key = ApplicationTabService.KeyOf(new AppInfo { AppName = new AppName("MyApp", "(100)"), Sender = "10.0.0.5" });

		service.RememberFilters(key, new LogViewerFilterState { TimeFrom = Utc(10) });
		Assert.Null(service.FiltersForNewTab(key));

		service.RememberFilters(key, new LogViewerFilterState { HiddenLevels = { "Trace" }, TimeFrom = Utc(10), TimeTo = Utc(11) });
		var restored = service.FiltersForNewTab(key);

		Assert.NotNull(restored);
		Assert.Equal(new[] { "Trace" }, restored!.HiddenLevels);
		Assert.Null(restored.TimeFrom);
		Assert.Null(restored.TimeTo);
	}

	[Fact]
	public void APickedTime_IsAMomentInTheZoneOfTheList_AndTheEndIncludesTheWholeSecond()
	{
		var clock = new DateTime(2025, 12, 18, 14, 30, 15);

		Assert.Equal(Utc(5, 30).AddSeconds(15), TimeRangeInput.StartOf(clock, Tokyo, null));
		Assert.Equal(Utc(5, 30).AddSeconds(16).AddTicks(-1), TimeRangeInput.EndOf(clock, Tokyo, null));
		Assert.Equal(clock, TimeRangeInput.ToClock(TimeRangeInput.StartOf(clock, Tokyo, null), Tokyo));
	}

	[Fact]
	public void ADialogThatIsClosedWithoutAChange_KeepsTheMomentsToTheMillisecond()
	{
		var start = Utc(5, 30).AddSeconds(15).AddMilliseconds(385);
		var end = Utc(6, 0).AddSeconds(20).AddMilliseconds(120);

		// The pickers show the seconds only; what lies in the second that they show is not changed.
		Assert.Equal(start, TimeRangeInput.StartOf(TimeRangeInput.ToClock(start, Tokyo).AddMilliseconds(-385), Tokyo, start));
		Assert.Equal(end, TimeRangeInput.EndOf(TimeRangeInput.ToClock(end, Tokyo).AddMilliseconds(-120), Tokyo, end));

		// Another second is another moment.
		Assert.NotEqual(start, TimeRangeInput.StartOf(TimeRangeInput.ToClock(start, Tokyo).AddSeconds(1), Tokyo, start));
	}

	[Fact]
	public void TheRangeIsShownInTheZoneOfTheList()
	{
		Assert.Equal("2025-12-18 14:30:00", TimeRangeInput.Format(Utc(5, 30), Tokyo));
		Assert.Equal("2025-12-18 05:30:00.250", TimeRangeInput.Format(Utc(5, 30).AddMilliseconds(250), TimeZoneInfo.Utc));
		Assert.Equal(string.Empty, TimeRangeInput.Format(null, Tokyo));

		Assert.Equal("2025-12-18 05:00:00 \u2013 2025-12-18 06:00:00 (UTC)", TimeRangeInput.Describe(Utc(5), Utc(6), TimeZoneInfo.Utc));
		Assert.Equal("from 2025-12-18 14:00:00 (UTC+9)", TimeRangeInput.Describe(Utc(5), null, Tokyo));
		Assert.Equal("until 2025-12-18 14:00:00 (UTC+9)", TimeRangeInput.Describe(null, Utc(5), Tokyo));
		Assert.Equal(string.Empty, TimeRangeInput.Describe(null, null, Tokyo));
	}
}
