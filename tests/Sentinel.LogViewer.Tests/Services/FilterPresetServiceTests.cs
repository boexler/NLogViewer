using Sentinel.LogViewer.Core;
using System;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for the saved filter presets
/// </summary>
public class FilterPresetServiceTests : IDisposable
{
	private readonly SettingsTestEnvironment _environment = new();

	public void Dispose() => _environment.Dispose();

	private FilterPresetService Service => new(_environment.Configuration);

	private static LogViewerFilterState Filters(string text = "timeout") => new()
	{
		HiddenLevels = { "Trace", "Debug" },
		Terms = { new LogViewerFilterTerm { Text = text, MatchCase = true, WholeWord = true, Level = LogEntryLevel.Error } },
		LoggerRules = { "-Acme.Shop" },
		SearchFields = SearchFields.All,
		TimeStampFormat = TimeStampFormat.Iso
	};

	[Fact]
	public void APreset_IsSaved_ReadAgain_AndKeepsTheFilters_ButNotTheTimeStampFormat()
	{
		Service.Save("Timeouts", Filters());

		var preset = Assert.Single(new FilterPresetService(_environment.Configuration).GetAll());
		Assert.Equal("Timeouts", preset.Name);
		Assert.Equal(new[] { "Trace", "Debug" }, preset.Filters.HiddenLevels);
		var term = Assert.Single(preset.Filters.Terms);
		Assert.Equal(("timeout", true, true, LogEntryLevel.Error), (term.Text, term.MatchCase, term.WholeWord, term.Level));
		Assert.Equal(new[] { "-Acme.Shop" }, preset.Filters.LoggerRules);
		Assert.Equal(SearchFields.All, preset.Filters.SearchFields);
		Assert.Null(preset.Filters.TimeStampFormat);
	}

	[Fact]
	public void SavingUnderTheNameOfAPreset_ReplacesIt_InItsPlace_WithoutRegardToCase()
	{
		Service.Save("First", Filters("a"));
		Service.Save("Second", Filters("b"));

		var replaced = Service.Save("  FIRST ", Filters("c"));

		Assert.True(replaced);
		var presets = Service.GetAll();
		Assert.Equal(new[] { "FIRST", "Second" }, presets.Select(preset => preset.Name));
		Assert.Equal("c", presets[0].Filters.Terms[0].Text);
		Assert.False(Service.Save("Third", Filters()));
		Assert.True(Service.Exists("third"));
	}

	[Fact]
	public void APresetWithoutAName_IsNotSaved() =>
		Assert.Throws<ArgumentException>(() => Service.Save("   ", Filters()));

	[Fact]
	public void Rename_NeedsANameThatIsNotEmptyAndNotUsed()
	{
		Service.Save("One", Filters());
		Service.Save("Two", Filters());
		var presets = _environment.Saved.FilterPresets;

		Assert.False(FilterPresetService.TryRename(presets, presets[0], "two"));
		Assert.False(FilterPresetService.TryRename(presets, presets[0], " "));
		Assert.True(FilterPresetService.TryRename(presets, presets[0], "ONE"));
		Assert.True(FilterPresetService.TryRename(presets, presets[0], "Uno"));
		Assert.Equal(new[] { "Uno", "Two" }, presets.Select(preset => preset.Name));
	}

	[Fact]
	public void WhatAHandEditedFileMakesWrong_IsLeftOut_WhenTheSettingsAreRead()
	{
		var file = _environment.PathOf("edited.json");
		File.WriteAllText(file, """
			{ "Version": 1, "Settings": { "FilterPresets": [
				{ "Name": "Good", "Filters": { "Terms": [ { "Text": "x" } ] } },
				{ "Name": "  ", "Filters": { } },
				{ "Name": "good", "Filters": { } },
				{ "Name": "NoFilters", "Filters": null },
				null
			] } }
			""");

		_environment.Transfer.Import(file);

		var preset = Assert.Single(_environment.Saved.FilterPresets);
		Assert.Equal("Good", preset.Name);
		Assert.Equal("x", Assert.Single(preset.Filters.Terms).Text);
	}

	[Fact]
	public void ThePresets_AreExportedAndImportedWithTheSettings()
	{
		var file = _environment.PathOf("export.json");
		Service.Save("Timeouts", Filters());
		_environment.Transfer.Export(file);

		_environment.Configuration.SaveConfiguration(ConfigurationService.CreateDefaultConfiguration());
		Assert.Empty(Service.GetAll());
		_environment.Transfer.Import(file);

		var preset = Assert.Single(Service.GetAll());
		Assert.Equal("Timeouts", preset.Name);
		Assert.True(preset.Filters.IsSameAs(FilterPresetService.ForPreset(Filters())));
	}
}
