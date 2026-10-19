using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.ViewModels.Settings;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

/// <summary>
/// Tests for the settings of the settings window: they are saved as they change and can be searched, reset, exported and imported
/// </summary>
[Collection(CultureCollection.Name)]
public class SettingsViewModelTests : IDisposable
{
	private const string AutoStartTitle = "Start listening on launch";
	private const string MaxEntriesTitle = "Maximum entries per tab";
	private const string PortsTitle = "Ports";
	private const string CheckForUpdatesTitle = "Automatically check for updates";
	private const string PrereleaseTitle = "Also offer prerelease versions";
	private const string ChangelogTitle = "Show what’s new after an update";

	private readonly SettingsTestEnvironment _environment = new();
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
	private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
	private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
	private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

	public SettingsViewModelTests()
	{
		// Numbers in the texts are formatted with the current culture.
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
	}

	public void Dispose()
	{
		CultureInfo.CurrentCulture = _culture;
		CultureInfo.CurrentUICulture = _uiCulture;
		CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
		CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
		_environment.Dispose();
	}

	[Fact]
	public void Categories_ListOnlyCategoriesThatHaveSettings()
	{
		var viewModel = _environment.CreateViewModel();

		Assert.Equal(new[] { "General", "Receiving", "Appearance", "Updates" }, viewModel.Categories.Select(category => category.Id));
		Assert.Equal("General", viewModel.SelectedCategory?.Id);
		Assert.Equal(1 + viewModel.SelectedCategory!.Settings.Count, viewModel.PageItems.Count);
		Assert.IsType<SettingsPageHeader>(viewModel.PageItems[0]);
	}

	[Fact]
	public void Toggle_IsSavedImmediately()
	{
		var viewModel = _environment.CreateViewModel();

		viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;

		Assert.True(_environment.Saved.AutoStartListening);
		Assert.True(viewModel.HasChanges);
	}

	[Fact]
	public void SettingsAreReadFromTheConfiguration()
	{
		var configuration = _environment.Saved;
		configuration.AutoStartListening = true;
		configuration.CheckForUpdates = false;
		_environment.Configuration.SaveConfiguration(configuration);

		var viewModel = _environment.CreateViewModel();

		Assert.True(viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value);
		Assert.False(viewModel.Setting<BoolSettingViewModel>(CheckForUpdatesTitle).Value);
		Assert.False(viewModel.HasChanges);
	}

	[Theory]
	[InlineData("abc")]
	[InlineData("")]
	[InlineData("99")]
	[InlineData("1000001")]
	public void MaxEntries_InvalidInput_IsNotSavedAndShowsAnError(string input)
	{
		var setting = _environment.CreateViewModel().Setting<IntSettingViewModel>(MaxEntriesTitle);

		setting.Text = input;

		Assert.True(setting.HasError);
		Assert.Contains("between 100 and 1,000,000", setting.Error, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(10000, _environment.Saved.MaxLogEntriesPerTab);
	}

	[Fact]
	public void MaxEntries_ValidInput_IsSavedAndClearsTheError()
	{
		var setting = _environment.CreateViewModel().Setting<IntSettingViewModel>(MaxEntriesTitle);
		setting.Text = "abc";

		setting.Text = "5000";

		Assert.False(setting.HasError);
		Assert.Equal(5000, _environment.Saved.MaxLogEntriesPerTab);
	}

	[Fact]
	public void Ports_AddEditAndRemove_AreSaved()
	{
		var ports = _environment.CreateViewModel().Setting<PortListSettingViewModel>(PortsTitle);
		Assert.Equal(new[] { "udp://0.0.0.0:4000" }, ports.Items.Select(item => item.Address));

		ports.AddCommand.Execute(null);
		Assert.Equal(new[] { "udp://0.0.0.0:4000", "udp://0.0.0.0:4001" }, _environment.Saved.Ports);

		ports.Items[1].Address = " udp://127.0.0.1:5000 ";
		Assert.Equal(new[] { "udp://0.0.0.0:4000", "udp://127.0.0.1:5000" }, _environment.Saved.Ports);

		ports.Items[0].RemoveCommand.Execute(null);
		Assert.Equal(new[] { "udp://127.0.0.1:5000" }, _environment.Saved.Ports);
	}

	[Theory]
	[InlineData("http://0.0.0.0:4000")]
	[InlineData("udp://0.0.0.0")]
	[InlineData("udp://0.0.0.0:70000")]
	[InlineData("localhost")]
	public void Ports_InvalidAddress_IsNotSavedAndShowsAnError(string address)
	{
		var ports = _environment.CreateViewModel().Setting<PortListSettingViewModel>(PortsTitle);

		ports.Items[0].Address = address;

		Assert.True(ports.Items[0].HasError);
		Assert.Equal("udp://0.0.0.0:4000", ports.Items[0].Address);
		Assert.Equal(new[] { "udp://0.0.0.0:4000" }, _environment.Saved.Ports);
	}

	[Fact]
	public void Ports_SamePortTwice_IsRejected()
	{
		var ports = _environment.CreateViewModel().Setting<PortListSettingViewModel>(PortsTitle);
		ports.AddCommand.Execute(null);

		ports.Items[1].Address = "udp://192.168.0.5:4000";

		Assert.True(ports.Items[1].HasError);
		Assert.Equal(new[] { "udp://0.0.0.0:4000", "udp://0.0.0.0:4001" }, _environment.Saved.Ports);
	}

	[Fact]
	public void Language_Change_IsSavedAndAppliedAndNeedsARestart()
	{
		var language = _environment.CreateViewModel().Setting<ChoiceSettingViewModel>("Language");

		language.Value = "fr";

		Assert.True(language.NeedsRestart);
		Assert.Equal("fr", _environment.Saved.Language);
		Assert.Equal("fr", _environment.Localization.CurrentLanguageCode);
		Assert.Contains(language.Options, option => option.Value == "de" && option.Display == "Deutsch");
	}

	[Fact]
	public void Texts_AreTranslatedIntoEveryLanguage()
	{
		_environment.Localization.Initialize();
		_environment.Localization.SetLanguage("en");
		var english = _environment.CreateViewModel();
		var englishTexts = english.AllSettings().Select(setting => setting.Description)
			.Concat(english.Categories.Select(category => category.Subtitle)).ToList();
		Assert.Equal("Language", english.AllSettings().First().Title);

		foreach (var language in LocalizationService.AvailableLanguages.Keys.Where(code => code != "en"))
		{
			_environment.Localization.SetLanguage(language);
			var translated = _environment.CreateViewModel();
			var translatedTexts = translated.AllSettings().Select(setting => setting.Description)
				.Concat(translated.Categories.Select(category => category.Subtitle)).ToList();

			Assert.Equal(englishTexts.Count, translatedTexts.Count);
			for (var i = 0; i < englishTexts.Count; i++)
				Assert.True(englishTexts[i] != translatedTexts[i], $"{language}: not translated: {englishTexts[i]}");

			Assert.NotEqual("Language", translated.AllSettings().First().Title);
		}
	}

	[Fact]
	public void Search_FindsSettingsByTitleAndDescriptionAcrossCategories()
	{
		var viewModel = _environment.CreateViewModel();

		// Only the description of the update check names GitHub.
		viewModel.SearchText = "github";
		Assert.Equal(new[] { CheckForUpdatesTitle }, SearchResultTitles(viewModel));
		Assert.Null(viewModel.SelectedCategory);
		Assert.True(viewModel.IsSearching);

		// Case is ignored; the title of the ports and the description of the auto start contain "port".
		viewModel.SearchText = "PORT";
		Assert.Equal(new[] { AutoStartTitle, PortsTitle }, SearchResultTitles(viewModel));
		Assert.Single(viewModel.PageItems.OfType<SettingsSectionLabel>());
	}

	[Fact]
	public void Search_WithoutMatch_ShowsNoResults()
	{
		var viewModel = _environment.CreateViewModel();

		viewModel.SearchText = "zzz";

		Assert.True(viewModel.HasNoResults);
		Assert.Equal("No settings found for “zzz”.", viewModel.NoResultsText);
		Assert.Empty(SearchResultTitles(viewModel));
	}

	[Fact]
	public void ClearingTheSearch_ReturnsToTheLastCategory()
	{
		var viewModel = _environment.CreateViewModel();
		viewModel.SelectedCategory = viewModel.Category("Receiving");
		viewModel.SearchText = "github";

		viewModel.ClearSearchCommand.Execute(null);

		Assert.False(viewModel.IsSearching);
		Assert.Equal("Receiving", viewModel.SelectedCategory?.Id);
		Assert.Equal(1 + 5, viewModel.PageItems.Count);
	}

	[Fact]
	public void SelectingACategory_ClearsTheSearch()
	{
		var viewModel = _environment.CreateViewModel();
		viewModel.SearchText = "github";

		viewModel.SelectedCategory = viewModel.Category("Updates");

		Assert.Equal(string.Empty, viewModel.SearchText);
		Assert.False(viewModel.HasNoResults);
	}

	[Fact]
	public void ResetCategory_RestoresDefaultsOfThatCategoryOnly()
	{
		var viewModel = _environment.CreateViewModel();
		viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;
		viewModel.Setting<IntSettingViewModel>(MaxEntriesTitle).Text = "5000";
		viewModel.Setting<PortListSettingViewModel>(PortsTitle).AddCommand.Execute(null);
		viewModel.Setting<BoolSettingViewModel>(CheckForUpdatesTitle).Value = false;
		viewModel.SelectedCategory = viewModel.Category("Receiving");

		viewModel.ResetCategoryCommand.Execute(null);

		var saved = _environment.Saved;
		Assert.False(saved.AutoStartListening);
		Assert.Equal(10000, saved.MaxLogEntriesPerTab);
		Assert.Equal(new[] { "udp://0.0.0.0:4000" }, saved.Ports);
		Assert.Equal(new[] { "udp://0.0.0.0:4000" }, viewModel.Setting<PortListSettingViewModel>(PortsTitle).Items.Select(item => item.Address));
		Assert.False(saved.CheckForUpdates);
		Assert.Single(_environment.Dialogs.Confirmations);
		Assert.Equal("Receiving was reset to its defaults.", viewModel.FooterText);
	}

	[Fact]
	public void ResetCategory_WhenDeclined_ChangesNothing()
	{
		var viewModel = _environment.CreateViewModel();
		viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;
		viewModel.SelectedCategory = viewModel.Category("Receiving");
		_environment.Dialogs.ConfirmAnswer = false;

		viewModel.ResetCategoryCommand.Execute(null);

		Assert.True(_environment.Saved.AutoStartListening);
	}

	[Fact]
	public void ResetUpdates_LetsThePrereleaseSettingFollowTheRunningVersionAgain()
	{
		var viewModel = _environment.CreateViewModel();
		var prerelease = viewModel.Setting<BoolSettingViewModel>(PrereleaseTitle);
		prerelease.Value = !prerelease.Value;
		Assert.NotNull(_environment.Saved.IncludePrereleaseUpdates);
		viewModel.SelectedCategory = viewModel.Category("Updates");

		viewModel.ResetCategoryCommand.Execute(null);

		Assert.Null(_environment.Saved.IncludePrereleaseUpdates);
	}

	[Fact]
	public void TheChangelogAfterAnUpdate_IsOnByDefault_CanBeSwitchedOff_AndIsResetWithTheUpdates()
	{
		var viewModel = _environment.CreateViewModel();
		var setting = viewModel.Setting<BoolSettingViewModel>(ChangelogTitle);
		Assert.True(setting.Value);

		setting.Value = false;
		Assert.False(_environment.Saved.ShowChangelogAfterUpdate);

		viewModel.SelectedCategory = viewModel.Category("Updates");
		viewModel.ResetCategoryCommand.Execute(null);
		Assert.True(_environment.Saved.ShowChangelogAfterUpdate);
	}

	[Fact]
	public void TheReceivingSettingsForRestartedApplications_HaveDefaults_AreSaved_AndAreReset()
	{
		var viewModel = _environment.CreateViewModel();
		var restart = viewModel.Setting<ChoiceSettingViewModel>("After a restart of an application");
		var takeOver = viewModel.Setting<BoolSettingViewModel>("Take over the filters into new tabs of the same application");
		Assert.Equal("NewTab", restart.Value);
		Assert.True(takeOver.Value);
		Assert.Equal(new[] { "NewTab", "ExtendTab", "ReplaceTab" }, restart.Options.Select(option => option.Value));

		restart.Value = "ReplaceTab";
		takeOver.Value = false;
		Assert.Equal("ReplaceTab", _environment.Saved.RestartBehavior);
		Assert.False(_environment.Saved.TakeOverFilters);

		viewModel.SelectedCategory = viewModel.Category("Receiving");
		viewModel.ResetCategoryCommand.Execute(null);
		Assert.Equal("NewTab", _environment.Saved.RestartBehavior);
		Assert.True(_environment.Saved.TakeOverFilters);
	}

	[Fact]
	public void TheLoggerTreeSettings_HaveDefaults_AreSaved_RejectAnInvalidWidth_AndAreReset()
	{
		var viewModel = _environment.CreateViewModel();
		var side = viewModel.Setting<ChoiceSettingViewModel>("Side");
		var open = viewModel.Setting<BoolSettingViewModel>("Open");
		var width = viewModel.Setting<IntSettingViewModel>("Width in pixels");
		Assert.Equal("right", side.Value);
		Assert.False(open.Value);
		Assert.Equal("260", width.Text);

		side.Value = "left";
		open.Value = true;
		width.Text = "400";
		var saved = _environment.Saved;
		Assert.True(saved.LoggerTreeOnLeft);
		Assert.True(saved.LoggerTreeOpen);
		Assert.Equal(400, saved.LoggerTreeWidth);

		width.Text = "20";
		Assert.True(width.HasError);
		Assert.Equal(400, _environment.Saved.LoggerTreeWidth);

		viewModel.SelectedCategory = viewModel.Category("Appearance");
		viewModel.ResetCategoryCommand.Execute(null);
		saved = _environment.Saved;
		Assert.False(saved.LoggerTreeOnLeft);
		Assert.False(saved.LoggerTreeOpen);
		Assert.Equal(260, saved.LoggerTreeWidth);
	}

	[Fact]
	public void TheLoggerTreeSettings_ShareOneCard_WithTheHeadingOfTheGroup()
	{
		var viewModel = _environment.CreateViewModel();
		viewModel.SelectedCategory = viewModel.Category("Appearance");

		var group = Assert.Single(viewModel.PageItems.OfType<SettingsGroupViewModel>());

		Assert.Equal("Logger tree", group.Title);
		Assert.Equal("Default view for new tabs", group.Subtitle);
		Assert.Equal(new[] { "Side", "Open", "Width in pixels" }, group.Settings.Select(setting => setting.Title));
		Assert.DoesNotContain(viewModel.PageItems.OfType<SettingViewModel>(), setting => setting.Group != null);
	}

	[Fact]
	public void SearchingForTheLoggerTree_ShowsTheMatchingRowsInTheirCard()
	{
		var viewModel = _environment.CreateViewModel();

		viewModel.SearchText = "splitter";

		var group = Assert.Single(viewModel.PageItems.OfType<SettingsGroupViewModel>());
		Assert.Equal("Logger tree", group.Title);
		Assert.Equal(new[] { "Width in pixels" }, group.Settings.Select(setting => setting.Title));
		Assert.False(viewModel.HasNoResults);
	}

	[Fact]
	public void ExportAndImport_MoveSettingsAndColumnMappingsToAnotherComputer()
	{
		var exportFile = _environment.PathOf("export.json");
		var source = _environment.CreateViewModel();
		source.Setting<IntSettingViewModel>(MaxEntriesTitle).Text = "5000";
		source.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;
		_environment.Formats.SaveFormatForPattern("*.log", new TextFileFormat { Separator = "\t", ColumnCount = 4 });
		_environment.Dialogs.ExportPath = exportFile;

		source.ExportCommand.Execute(null);

		Assert.True(File.Exists(exportFile));
		Assert.Equal("Settings exported to export.json", source.FooterText);

		using var other = new SettingsTestEnvironment();
		var target = other.CreateViewModel();
		other.Dialogs.ImportPath = exportFile;

		target.ImportCommand.Execute(null);

		Assert.Equal(5000, other.Saved.MaxLogEntriesPerTab);
		Assert.True(other.Saved.AutoStartListening);
		Assert.Equal("*.log", Assert.Single(other.Formats.GetAll().Formats).FilePattern);
		Assert.True(target.Setting<BoolSettingViewModel>(AutoStartTitle).Value);
		Assert.Equal("5,000", target.Setting<IntSettingViewModel>(MaxEntriesTitle).Text);
		Assert.True(target.HasChanges);
		Assert.Single(other.Dialogs.Confirmations);
	}

	[Fact]
	public void Import_OfAFileThatIsNoSettingsExport_ShowsAnErrorAndKeepsTheSettings()
	{
		var file = _environment.PathOf("other.json");
		File.WriteAllText(file, "{ \"Name\": \"something else\" }");
		var viewModel = _environment.CreateViewModel();
		viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;
		_environment.Dialogs.ImportPath = file;

		viewModel.ImportCommand.Execute(null);

		Assert.Equal("This is not a settings file exported from Sentinel LogViewer.", Assert.Single(_environment.Dialogs.Errors));
		Assert.True(_environment.Saved.AutoStartListening);
	}

	[Fact]
	public void Import_WhenDeclined_ChangesNothing()
	{
		var exportFile = _environment.PathOf("export.json");
		var viewModel = _environment.CreateViewModel();
		_environment.Dialogs.ExportPath = exportFile;
		viewModel.ExportCommand.Execute(null);
		viewModel.Setting<BoolSettingViewModel>(AutoStartTitle).Value = true;
		_environment.Dialogs.ImportPath = exportFile;
		_environment.Dialogs.ConfirmAnswer = false;

		viewModel.ImportCommand.Execute(null);

		Assert.True(_environment.Saved.AutoStartListening);
	}

	private static string[] SearchResultTitles(Sentinel.LogViewer.ViewModels.SettingsViewModel viewModel) =>
		viewModel.PageItems.OfType<SettingViewModel>().Select(setting => setting.Title).ToArray();
}
