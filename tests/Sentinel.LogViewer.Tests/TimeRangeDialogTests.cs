using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests for the dialogs of the time range and of go to time: what the pickers do, what the buttons offer, and what is refused
/// </summary>
[Collection(WpfCollection.Name)]
public class TimeRangeDialogTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

	private readonly WpfStaContextFixture _context;

	public TimeRangeDialogTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	private static DateTime Utc(int hour, int second = 0) => new(2025, 12, 18, hour, 0, second, DateTimeKind.Utc);

	private void OnSta(Action test) => _context.RunOnSta(test);

	[Fact]
	public void WithoutARange_BothEndsAreOff_AndShowTheFirstAndTheLastEntry() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, null, null, Utc(10), Utc(12));

			Assert.False(dialog.FromPicker.IsActive);
			Assert.False(dialog.ToPicker.IsActive);
			Assert.Equal(TimeRangeInput.ToClock(Utc(10), Tokyo), dialog.FromPicker.Clock);
			Assert.Equal(TimeRangeInput.ToClock(Utc(12), Tokyo), dialog.ToPicker.Clock);

			Assert.True(dialog.TryAccept());
			Assert.Null(dialog.From);
			Assert.Null(dialog.To);
		});

	[Fact]
	public void ChangingABox_SwitchesItsEndOn_AndOnlyThatOne() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, null, null, Utc(10), Utc(12));

			dialog.ToPicker.HourBox.SelectedIndex = 3;

			Assert.True(dialog.ToPicker.IsActive);
			Assert.False(dialog.FromPicker.IsActive);
		});

	[Fact]
	public void TheButtonsOfTheEntries_TakeTheirTimes_AndSwitchTheEndsOn() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, null, null, Utc(10), Utc(12, second: 7));

			dialog.UseFirst();
			dialog.UseLast();

			Assert.True(dialog.FromPicker.IsActive && dialog.ToPicker.IsActive);
			Assert.True(dialog.TryAccept());
			Assert.Equal(Utc(10), dialog.From);
			// The end is the end of that second, so that the last entry is in the range.
			Assert.Equal(Utc(12, second: 8).AddTicks(-1), dialog.To);
		});

	[Fact]
	public void ADialogThatIsClosedWithoutAChange_KeepsTheMomentsToTheMillisecond() =>
		OnSta(() =>
		{
			var from = Utc(10).AddMilliseconds(385);
			var to = Utc(12).AddMilliseconds(120);
			var dialog = new TimeRangeDialog(Tokyo, from, to, Utc(9), Utc(13));

			Assert.True(dialog.FromPicker.IsActive && dialog.ToPicker.IsActive);
			Assert.True(dialog.TryAccept());

			Assert.Equal(from, dialog.From);
			Assert.Equal(to, dialog.To);
		});

	[Fact]
	public void AStartAfterTheEnd_IsRefused_AndNothingIsTaken() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, Utc(12), Utc(10), Utc(9), Utc(13));

			Assert.False(dialog.TryAccept());

			Assert.Equal("The start is after the end.", dialog.Error);
			Assert.Null(dialog.From);
			Assert.Null(dialog.To);
		});

	[Fact]
	public void AnEndThatIsOnWithoutADate_IsRefused_InsteadOfBecomingToday() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, null, null, Utc(10), Utc(12));
			dialog.UseFirst();

			dialog.FromPicker.DateBox.SelectedDate = null;

			Assert.False(dialog.TryAccept());
			Assert.Equal("Pick a date.", dialog.Error);

			// An end that is off needs no date.
			dialog.FromPicker.IsActive = false;
			Assert.True(dialog.TryAccept());
			Assert.Equal(string.Empty, dialog.Error);
		});

	[Fact]
	public void GoToTime_TakesTheTimeOfThePicker_AndRefusesAMissingDate() =>
		OnSta(() =>
		{
			var dialog = new GoToTimeDialog(Tokyo, Utc(10, second: 30));

			Assert.True(dialog.TryAccept());
			Assert.Equal(Utc(10, second: 30), dialog.Moment);

			dialog.Picker.DateBox.SelectedDate = null;
			Assert.False(dialog.TryAccept());
			Assert.Equal("Pick a date.", dialog.Error);
		});

	[Fact]
	public void TheListsOfThePickers_HaveNamesForAScreenReader() =>
		OnSta(() =>
		{
			var dialog = new TimeRangeDialog(Tokyo, null, null, Utc(10), Utc(12));

			Assert.Equal("From hour", System.Windows.Automation.AutomationProperties.GetName(dialog.FromPicker.HourBox));
			Assert.Equal("Until date", System.Windows.Automation.AutomationProperties.GetName(dialog.ToPicker.DateBox));
		});
}
