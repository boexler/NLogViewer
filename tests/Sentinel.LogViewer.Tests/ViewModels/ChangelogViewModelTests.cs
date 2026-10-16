using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ChangelogViewModel"/>: the list of versions with its marks, the entry for everything since the previous version,
/// the search and the notes of the selected line
/// </summary>
[Collection(CultureCollection.Name)]
public class ChangelogViewModelTests : IDisposable
{
	private readonly SettingsTestEnvironment _environment = new();
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
	private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

	public ChangelogViewModelTests()
	{
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
		_environment.Localization.Initialize();
		_environment.Localization.SetLanguage("en");
		SetPrereleases(true);
	}

	public void Dispose()
	{
		CultureInfo.CurrentCulture = _culture;
		CultureInfo.CurrentUICulture = _uiCulture;
		_environment.Dispose();
	}

	private void SetPrereleases(bool include)
	{
		var configuration = _environment.Saved;
		configuration.IncludePrereleaseUpdates = include;
		_environment.Configuration.SaveConfiguration(configuration);
	}

	private static ReleaseVersion Version(string text)
	{
		Assert.True(ReleaseVersion.TryParse(text, out var version));
		return version;
	}

	private static ChangelogEntry Entry(string version, string markdown, string date = "2026-10-14") =>
		new(Version(version), DateTimeOffset.Parse(date + "T10:00:00Z"), Version(version).IsPrerelease, markdown, "https://github.com/boexler/NLogViewer/releases/tag/v" + version);

	private static ChangelogList List(params ChangelogEntry[] entries) =>
		new(entries.OrderByDescending(entry => entry.Version).ToList(), false, DateTimeOffset.Now);

	private ChangelogList Sample() => List(
		Entry("5.0.0-beta.3", "## Features\n- logger tree", "2026-10-16"),
		Entry("5.0.0-beta.2", "## Features\n- updater\n\n## Fixes\n- a fix", "2026-10-14"),
		Entry("5.0.0-beta.1", "- first beta", "2026-10-13"),
		Entry("4.0.0", "- stable four", "2026-09-15"));

	private ChangelogViewModel Create(string current, string? previous = null) =>
		new(new ChangelogService(new UpdateService(), System.IO.Path.Combine(_environment.Directory, "changelog.json")), _environment.Configuration, _environment.Localization, Version(current), previous == null ? null : Version(previous));

	[Fact]
	public void Items_ListTheVersionsNewestFirst_WithTheDate()
	{
		var viewModel = Create("5.0.0-beta.3");

		viewModel.Apply(Sample(), selectSince: false);

		Assert.Equal(new[] { "5.0.0-beta.3", "5.0.0-beta.2", "5.0.0-beta.1", "4.0.0" }, viewModel.Items.Select(item => item.Title));
		Assert.Equal("10/16/2026", viewModel.Items[0].Subtitle);
	}

	[Fact]
	public void TheInstalledVersion_IsMarked_AndSelectedFirst()
	{
		var viewModel = Create("5.0.0-beta.2");

		viewModel.Apply(Sample(), selectSince: false);

		Assert.Equal("5.0.0-beta.2", viewModel.SelectedItem?.Title);
		Assert.Equal(new[] { false, true, false, false }, viewModel.Items.Select(item => item.IsInstalled));
	}

	[Fact]
	public void VersionsAfterThePreviousOne_AreMarkedAsNew_AndGatheredInTheEntryAtTheTop()
	{
		var viewModel = Create("5.0.0-beta.3", previous: "5.0.0-beta.1");

		viewModel.Apply(Sample(), selectSince: true);

		var since = viewModel.Items[0];
		Assert.True(since.IsSince);
		Assert.Equal("Since 5.0.0-beta.1", since.Title);
		Assert.Equal("2 versions", since.Subtitle);
		Assert.Equal(new[] { "5.0.0-beta.3", "5.0.0-beta.2" }, since.Entries.Select(entry => entry.Version.ToString()));
		Assert.Equal(new[] { false, true, true, false, false }, viewModel.Items.Select(item => item.IsNew));
		Assert.Same(since, viewModel.SelectedItem);
	}

	[Fact]
	public void TheEntryForOneVersion_IsCalledAVersion_NotVersions()
	{
		var viewModel = Create("5.0.0-beta.2", previous: "5.0.0-beta.1");

		viewModel.Apply(Sample(), selectSince: true);

		Assert.Equal("1 version", viewModel.Items[0].Subtitle);
	}

	[Fact]
	public void WithoutAPreviousVersion_ThereIsNoEntryForEverythingSince()
	{
		var viewModel = Create("5.0.0-beta.3");

		viewModel.Apply(Sample(), selectSince: true);

		Assert.DoesNotContain(viewModel.Items, item => item.IsSince);
		Assert.DoesNotContain(viewModel.Items, item => item.IsNew);
		Assert.Equal("5.0.0-beta.3", viewModel.SelectedItem?.Title);
	}

	[Theory]
	[InlineData("5.0.0-beta.3")]
	[InlineData("5.0.0-beta.4")]
	public void APreviousVersionThatIsNotOlder_IsIgnored(string previous)
	{
		var viewModel = Create("5.0.0-beta.3", previous);

		viewModel.Apply(Sample(), selectSince: true);

		Assert.DoesNotContain(viewModel.Items, item => item.IsSince);
	}

	[Fact]
	public void SelectingAVersion_ShowsItsNotes_ItsTitleAndItsPage()
	{
		var viewModel = Create("5.0.0-beta.3");
		viewModel.Apply(Sample(), selectSince: false);

		viewModel.SelectedItem = viewModel.Items.Single(item => item.Title == "5.0.0-beta.2");

		Assert.Equal("5.0.0-beta.2", viewModel.NotesTitle);
		Assert.Equal("## Features\n- updater\n\n## Fixes\n- a fix", viewModel.NotesMarkdown);
		Assert.Equal("https://github.com/boexler/NLogViewer/releases/tag/v5.0.0-beta.2", viewModel.OpenUrl);
		Assert.True(viewModel.OpenOnGitHubCommand.CanExecute(null));
	}

	[Fact]
	public void TheNotesSinceThePreviousVersion_HaveAHeadingPerVersion_AndTheirOwnHeadingsMoveDown()
	{
		var viewModel = Create("5.0.0-beta.3", previous: "5.0.0-beta.1");
		viewModel.Apply(Sample(), selectSince: true);

		var notes = viewModel.NotesMarkdown;

		Assert.StartsWith("## 5.0.0-beta.3  ·  10/16/2026\n\n### Features\n- logger tree", notes);
		Assert.Contains("## 5.0.0-beta.2  ·  10/14/2026\n\n### Features\n- updater\n\n### Fixes\n- a fix", notes);
		Assert.DoesNotContain("- first beta", notes);
	}

	[Fact]
	public void AVersionWithoutNotes_SaysSo()
	{
		var viewModel = Create("4.0.0");

		viewModel.Apply(List(Entry("4.0.0", string.Empty)), selectSince: false);

		Assert.Equal("This version has no release notes.", viewModel.NotesMarkdown);
	}

	[Fact]
	public void Search_FindsVersionsByTheirNumberAndByWordsInTheirNotes_AndHidesTheEntryForEverythingSince()
	{
		var viewModel = Create("5.0.0-beta.3", previous: "5.0.0-beta.1");
		viewModel.Apply(Sample(), selectSince: true);

		viewModel.SearchText = "UPDATER";
		Assert.Equal(new[] { "5.0.0-beta.2" }, viewModel.Items.Select(item => item.Title));
		Assert.Equal("5.0.0-beta.2", viewModel.SelectedItem?.Title);

		viewModel.SearchText = "4.0";
		Assert.Equal(new[] { "4.0.0" }, viewModel.Items.Select(item => item.Title));

		viewModel.SearchText = string.Empty;
		Assert.Equal(5, viewModel.Items.Count);
		Assert.Contains(viewModel.Items, item => item.IsSince);
	}

	[Fact]
	public void Search_WithoutAMatch_ShowsNoVersions_AndSaysSo()
	{
		var viewModel = Create("5.0.0-beta.3");
		viewModel.Apply(Sample(), selectSince: false);

		viewModel.SearchText = "nothing like this";

		Assert.Empty(viewModel.Items);
		Assert.Null(viewModel.SelectedItem);
		Assert.Equal("No version found for “nothing like this”.", viewModel.EmptyText);
		Assert.False(viewModel.OpenOnGitHubCommand.CanExecute(null));
	}

	[Fact]
	public void AnEmptyList_SaysThatTheChangelogIsNotAvailable()
	{
		var viewModel = Create("5.0.0-beta.3");

		viewModel.Apply(ChangelogList.Empty, selectSince: false);

		Assert.Contains("not available yet", viewModel.EmptyText);
		Assert.Empty(viewModel.Items);
	}

	[Fact]
	public void AListThatWasSaved_IsShownWithTheDateItWasSaved()
	{
		var viewModel = Create("5.0.0-beta.3");

		viewModel.Apply(new ChangelogList(Sample().Entries, true, new DateTimeOffset(2026, 10, 12, 14, 30, 0, TimeSpan.Zero)), selectSince: false);

		Assert.StartsWith("GitHub could not be reached. This is the list saved on 10/12/2026", viewModel.StatusText);

		viewModel.Apply(Sample(), selectSince: false);
		Assert.Equal(string.Empty, viewModel.StatusText);
	}

	[Fact]
	public void Prereleases_AreListedWhenTheSettingIsOn_AndOtherwiseOnlyTheInstalledOne()
	{
		SetPrereleases(false);
		var viewModel = Create("5.0.0-beta.2");

		viewModel.Apply(Sample(), selectSince: false);

		Assert.Equal(new[] { "5.0.0-beta.2", "4.0.0" }, viewModel.Items.Select(item => item.Title));
	}

	[Fact]
	public void ApplyingTheLoadedList_KeepsTheLineThatWasSelected()
	{
		var viewModel = Create("5.0.0-beta.3");
		viewModel.Apply(Sample(), selectSince: false);
		viewModel.SelectedItem = viewModel.Items.Single(item => item.Title == "4.0.0");

		viewModel.Apply(Sample(), selectSince: false, keepSelection: true);

		Assert.Equal("4.0.0", viewModel.SelectedItem?.Title);
	}

	[Fact]
	public void TheMarks_AreWordsOfTheLanguage()
	{
		_environment.Localization.SetLanguage("de");
		var viewModel = Create("5.0.0-beta.3", previous: "5.0.0-beta.1");

		viewModel.Apply(Sample(), selectSince: true);

		Assert.Equal("Seit 5.0.0-beta.1", viewModel.Items[0].Title);
		Assert.Equal("Installiert", viewModel.Items[1].InstalledText);
		Assert.Equal("Neu", viewModel.Items[1].NewText);
	}
}
