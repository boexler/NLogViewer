using System.IO;
using System.Linq;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for the zones of the times of a file: which zone a tab of a file starts with, what happens when a file is added to a tab that
/// exists, the zone in the mapping of a file, and the dialog for the zones
/// </summary>
public class TimeZoneChoicesTests
{
	private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
	private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

	private static LogEntry Written(int hour) => new(LogEntryLevel.Info, "L", "m") { TimeStamp = new DateTime(2025, 12, 18, hour, 0, 0) };

	private static LogEntry Moment(int hour) => new(LogEntryLevel.Info, "L", "m") { TimeStamp = new DateTime(2025, 12, 18, hour, 0, 0, DateTimeKind.Utc) };

	[Fact]
	public void ANewFileTab_TakesTheZoneOfTheMapping_ThenTheOneChosenLast_ButOnlyForTimesWithoutAZone()
	{
		var written = new[] { Written(10) };

		Assert.Equal(Tokyo, TimeZoneChoices.ForNewFile(written, Tokyo.Id, Berlin));
		Assert.Equal(Berlin, TimeZoneChoices.ForNewFile(written, "", Berlin));
		Assert.Null(TimeZoneChoices.ForNewFile(written, "", null));

		// Times that have a zone (a file of Log4J events, or logs from the network) never take a zone of a file.
		Assert.Null(TimeZoneChoices.ForNewFile(new[] { Moment(10) }, Tokyo.Id, Berlin));
		Assert.Null(TimeZoneChoices.ForNewFile(new[] { Moment(10) }, "", Berlin));
	}

	[Fact]
	public void AMappingThatSaysLocalTime_IsNotOverruledByTheZoneChosenLast()
	{
		var zone = TimeZoneChoices.ForNewFile(new[] { Written(10) }, EntryTime.LocalId, Tokyo);

		Assert.Null(zone);
	}

	[Fact]
	public void TheChoices_StartWithLocalTimeAndUtc_AndFindTheZoneOfAnId()
	{
		var choices = TimeZoneChoices.Create();

		Assert.Equal(new[] { EntryTime.LocalId, "UTC" }, choices.Take(2).Select(choice => choice.Id));
		Assert.Equal(choices.Count, choices.Select(choice => choice.Id).Distinct().Count());
		Assert.Equal(EntryTime.LocalId, TimeZoneChoices.Find(choices, null).Id);
		Assert.Equal(EntryTime.LocalId, TimeZoneChoices.Find(choices, TimeZoneInfo.Local).Id);
		Assert.Equal(Tokyo.Id, TimeZoneChoices.Find(choices, Tokyo).Id);
		Assert.Null(choices[0].Zone);
		Assert.Equal(Tokyo, TimeZoneChoices.Find(choices, Tokyo).Zone);
	}

	[Fact]
	public void TheChoicesOfAMapping_CanLeaveTheZoneOpen()
	{
		var choices = TimeZoneChoices.Create(withNotSet: true);

		Assert.Equal(new[] { "", EntryTime.LocalId, "UTC" }, choices.Take(3).Select(choice => choice.Id));
	}

	[Fact]
	public void AFileAddedToATabThatReadsLocalTime_NeedsNoQuestion_AndIsNotChanged()
	{
		var entries = new List<LogEntry> { Written(10) };
		var asked = 0;

		var fitted = TimeZoneChoices.FitToTab(entries, "", null, () => ++asked > 0);

		Assert.Same(entries, fitted);
		Assert.Equal(0, asked);
	}

	[Fact]
	public void AFileWithoutAZoneAddedToATabOfAnotherZone_AsksOnce_AndReadsItLikeTheTabOrAsLocalTime()
	{
		var entries = new List<LogEntry> { Written(10), Moment(11) };
		var asked = 0;

		var like = TimeZoneChoices.FitToTab(entries, "", Tokyo, () => { asked++; return true; });
		var local = TimeZoneChoices.FitToTab(entries, "", Tokyo, () => { asked++; return false; });

		Assert.Equal(2, asked);
		Assert.Same(entries, like);
		Assert.Equal(DateTimeKind.Utc, local[0].TimeStamp.Kind);
		Assert.Equal(EntryTime.ToMoment(new DateTime(2025, 12, 18, 10, 0, 0), null), local[0].TimeStamp);
		Assert.Same(entries[1], local[1]);
		Assert.Equal(new DateTime(2025, 12, 18, 10, 0, 0), entries[0].TimeStamp);
	}

	[Fact]
	public void AFileWithAZoneOfItsOwn_KeepsIt_WithoutAQuestion()
	{
		var entries = new List<LogEntry> { Written(10) };
		var asked = 0;

		var fitted = TimeZoneChoices.FitToTab(entries, Berlin.Id, Tokyo, () => ++asked > 0);

		Assert.Equal(0, asked);
		Assert.Equal(new DateTime(2025, 12, 18, 9, 0, 0, DateTimeKind.Utc), fitted[0].TimeStamp);
		Assert.Equal(entries[0].Id, fitted[0].Id);
		Assert.Equal(DateTimeKind.Unspecified, entries[0].TimeStamp.Kind);
	}

	[Fact]
	public void AFileOfTheZoneOfTheTab_AndAFileOfMoments_AreNotChanged()
	{
		var written = new List<LogEntry> { Written(10) };
		var moments = new List<LogEntry> { Moment(10) };

		Assert.Same(written, TimeZoneChoices.FitToTab(written, Tokyo.Id, Tokyo, () => false));
		Assert.Same(moments, TimeZoneChoices.FitToTab(moments, Berlin.Id, Tokyo, () => false));
	}

	[Fact]
	public void TheZoneOfAFile_IsSavedWithItsMapping_AndComesBack()
	{
		using var environment = new SettingsTestEnvironment();
		var service = new TextFileFormatConfigService(Path.Combine(environment.Directory, "formats.json"));
		var viewModel = new ColumnMappingViewModel(new TextFileFormat { Separator = ";", ColumnCount = 2 }, new List<string> { "a;b" }, "tokyo.log")
		{
			TimeZoneId = Tokyo.Id
		};

		service.SaveFormatForPattern("tokyo.log", viewModel.FinalFormat);

		Assert.Equal(Tokyo.Id, service.GetFormatForFile("tokyo.log")!.TimeZoneId);
	}

	[Fact]
	public void TheDialog_ShowsTheTimeBeforeAndAfter_AndHasNoZoneOfTheFileForLiveLogs()
	{
		using var environment = new SettingsTestEnvironment();
		var sample = Written(14);

		var file = new TimeZoneViewModel(false, Tokyo, TimeZoneInfo.Utc, sample, environment.Localization);
		var live = new TimeZoneViewModel(true, Tokyo, TimeZoneInfo.Utc, Moment(14), environment.Localization);

		Assert.True(file.ShowFileZone);
		Assert.Equal("2025-12-18 14:00:00 (UTC+9) → 2025-12-18 05:00:00 (UTC)", file.Preview);
		Assert.False(live.ShowFileZone);
		Assert.Equal("2025-12-18 14:00:00 (UTC) → 2025-12-18 14:00:00 (UTC)", live.Preview);
		Assert.Equal(Tokyo, file.FileZone);
		Assert.Equal(TimeZoneInfo.Utc, file.DisplayZone);
	}

	[Fact]
	public void TheDialogCanBeResetToLocalTime()
	{
		using var environment = new SettingsTestEnvironment();
		var viewModel = new TimeZoneViewModel(false, Tokyo, Berlin, Written(14), environment.Localization);
		var changes = new List<string?>();
		viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		viewModel.ResetCommand.Execute(null);

		Assert.Null(viewModel.FileZone);
		Assert.Null(viewModel.DisplayZone);
		Assert.Contains(nameof(TimeZoneViewModel.Preview), changes);
	}
}
