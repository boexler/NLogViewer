using System;
using System.Globalization;
using System.Linq;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.ViewModels;
using Sentinel.LogViewer.ViewModels.Settings;
using Xunit;

namespace Sentinel.LogViewer.Tests.ViewModels;

/// <summary>
/// Tests for the appearance settings: the theme and its colours are saved as they change, for each theme on its own
/// </summary>
[Collection(CultureCollection.Name)]
public class AppearanceSettingsTests : IDisposable
{
	private const string ModeTitle = "Colour mode";
	private const string LightThemeTitle = "Light theme";
	private const string AccentTitle = "Accent";
	private const string TraceBackgroundTitle = "Trace: background";

	private readonly SettingsTestEnvironment _environment = new();
	private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
	private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
	private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
	private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

	public AppearanceSettingsTests()
	{
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
		_environment.Localization.Initialize();
		_environment.Localization.SetLanguage("en");
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
	public void Category_HasThemeAndACardPerColour_InSections()
	{
		var viewModel = _environment.CreateViewModel();
		var appearance = viewModel.Category("Appearance");

		Assert.Equal(2 + ThemeCatalog.Tokens.Count, appearance.Settings.Count);
		Assert.Equal(ThemeCatalog.Tokens.Count, appearance.Settings.OfType<ColorSettingViewModel>().Count());

		viewModel.SelectedCategory = appearance;
		Assert.Equal(new[] { "Interface", "Log levels", "Search highlighting" },
			viewModel.PageItems.OfType<SettingsSectionLabel>().Select(label => label.Title));
	}

	[Fact]
	public void Theme_IsNewschoolByDefault_AndSavedWhenChanged()
	{
		var theme = _environment.CreateViewModel().Setting<ChoiceSettingViewModel>(LightThemeTitle);
		Assert.Equal(ThemeCatalog.NewschoolId, theme.Value);

		theme.Value = ThemeCatalog.OldschoolId;

		Assert.Equal(ThemeCatalog.OldschoolId, _environment.Saved.LightTheme);
	}

	[Fact]
	public void ChangingTheTheme_ShowsTheColoursOfTheNewTheme()
	{
		var viewModel = _environment.CreateViewModel();
		var traceBackground = viewModel.Setting<ColorSettingViewModel>(TraceBackgroundTitle);
		var changes = 0;
		traceBackground.PropertyChanged += (_, _) => changes++;
		Assert.Equal("#E4E7EB", traceBackground.Text);

		viewModel.Setting<ChoiceSettingViewModel>(LightThemeTitle).Value = ThemeCatalog.OldschoolId;

		Assert.Equal("#D3D3D3", traceBackground.Text);
		Assert.True(changes > 0);
	}

	[Fact]
	public void EditingAColour_SavesItForTheCurrentThemeOnly()
	{
		var viewModel = _environment.CreateViewModel();
		var accent = viewModel.Setting<ColorSettingViewModel>(AccentTitle);

		accent.Text = "#abc";

		Assert.Equal("#AABBCC", accent.Text);
		Assert.True(accent.IsCustom);
		Assert.False(accent.HasError);
		Assert.Equal("#AABBCC", _environment.Saved.ThemeColors[ThemeCatalog.NewschoolId]["ThemeAccent"]);
		Assert.True(viewModel.HasChanges);

		viewModel.Setting<ChoiceSettingViewModel>(LightThemeTitle).Value = ThemeCatalog.OldschoolId;
		Assert.Equal("#0078D7", accent.Text);
		Assert.False(accent.IsCustom);

		viewModel.Setting<ChoiceSettingViewModel>(LightThemeTitle).Value = ThemeCatalog.NewschoolId;
		Assert.Equal("#AABBCC", accent.Text);
	}

	[Fact]
	public void EditingAColour_ToWhatTheThemeHas_RemovesTheCustomColour()
	{
		var accent = _environment.CreateViewModel().Setting<ColorSettingViewModel>(AccentTitle);
		accent.Text = "#112233";

		accent.Text = "#0A5CB8";

		Assert.False(accent.IsCustom);
		Assert.Empty(_environment.Saved.ThemeColors);
	}

	[Theory]
	[InlineData("red")]
	[InlineData("#12")]
	[InlineData("")]
	public void EditingAColour_WithInvalidInput_IsNotSavedAndShowsAnError(string input)
	{
		var accent = _environment.CreateViewModel().Setting<ColorSettingViewModel>(AccentTitle);

		accent.Text = input;

		Assert.True(accent.HasError);
		Assert.Contains("#1B1B1B", accent.Error);
		Assert.Empty(_environment.Saved.ThemeColors);

		accent.Text = "#445566";
		Assert.False(accent.HasError);
	}

	[Fact]
	public void ResetCommand_GoesBackToTheColourOfTheTheme()
	{
		var accent = _environment.CreateViewModel().Setting<ColorSettingViewModel>(AccentTitle);
		accent.Text = "#112233";

		accent.ResetCommand.Execute(null);

		Assert.Equal("#0A5CB8", accent.Text);
		Assert.False(accent.IsCustom);
		Assert.Empty(_environment.Saved.ThemeColors);
	}

	[Fact]
	public void ResetCategory_TakesBackTheThemeAndEveryCustomColourOfEveryTheme()
	{
		var viewModel = _environment.CreateViewModel();
		var theme = viewModel.Setting<ChoiceSettingViewModel>(LightThemeTitle);
		viewModel.Setting<ColorSettingViewModel>(AccentTitle).Text = "#112233";
		theme.Value = ThemeCatalog.OldschoolId;
		viewModel.Setting<ColorSettingViewModel>(AccentTitle).Text = "#445566";
		viewModel.Setting<ChoiceSettingViewModel>(ModeTitle).Value = ThemeCatalog.ModeDark;
		viewModel.SelectedCategory = viewModel.Category("Appearance");

		viewModel.ResetCategoryCommand.Execute(null);

		var saved = _environment.Saved;
		Assert.Equal(ThemeCatalog.ModeSystem, saved.ThemeMode);
		Assert.Equal(ThemeCatalog.DefaultThemeId, saved.LightTheme);
		Assert.Empty(saved.ThemeColors);
		Assert.Equal(ThemeCatalog.DefaultThemeId, theme.Value);
		Assert.Equal("#0A5CB8", viewModel.Setting<ColorSettingViewModel>(AccentTitle).Text);
	}

	[Fact]
	public void Colours_AreFoundBySearch()
	{
		var viewModel = _environment.CreateViewModel();

		viewModel.SearchText = "trace";

		var titles = viewModel.PageItems.OfType<SettingViewModel>().Select(setting => setting.Title).ToList();
		Assert.Equal(new[] { TraceBackgroundTitle, "Trace: text" }, titles);
	}

	[Fact]
	public void Mode_IsLikeWindowsByDefault_AndSavedWhenChanged()
	{
		var mode = _environment.CreateViewModel().Setting<ChoiceSettingViewModel>(ModeTitle);
		Assert.Equal(ThemeCatalog.ModeSystem, mode.Value);
		Assert.Equal(new[] { "system", "light", "dark" }, mode.Options.Select(option => option.Value));

		mode.Value = ThemeCatalog.ModeDark;

		Assert.Equal(ThemeCatalog.ModeDark, _environment.Saved.ThemeMode);
	}

	[Fact]
	public void LightTheme_OffersOnlyLightThemes()
	{
		var theme = _environment.CreateViewModel().Setting<ChoiceSettingViewModel>(LightThemeTitle);

		Assert.Equal(new[] { ThemeCatalog.NewschoolId, ThemeCatalog.OldschoolId }, theme.Options.Select(option => option.Value));
	}

	[Fact]
	public void DarkMode_ShowsAndEditsTheColoursOfTheDarkTheme()
	{
		var viewModel = _environment.CreateViewModel();
		var accent = viewModel.Setting<ColorSettingViewModel>(AccentTitle);
		accent.Text = "#112233";

		viewModel.Setting<ChoiceSettingViewModel>(ModeTitle).Value = ThemeCatalog.ModeDark;

		Assert.Equal("#5AA2F0", accent.Text);
		Assert.False(accent.IsCustom);

		accent.Text = "#445566";

		var colors = _environment.Saved.ThemeColors;
		Assert.Equal("#445566", colors[ThemeCatalog.DarkId]["ThemeAccent"]);
		Assert.Equal("#112233", colors[ThemeCatalog.NewschoolId]["ThemeAccent"]);
	}

	[Fact]
	public void ModeLikeWindows_FollowsTheSystemSetting()
	{
		var systemIsLight = true;
		var viewModel = new SettingsViewModel(_environment.Configuration, _environment.Localization, _environment.Transfer,
			new ThemeService(() => systemIsLight));
		var accent = viewModel.Setting<ColorSettingViewModel>(AccentTitle);
		Assert.Equal("#0A5CB8", accent.Text);

		systemIsLight = false;
		viewModel.Setting<ChoiceSettingViewModel>(ModeTitle).Value = ThemeCatalog.ModeSystem;
		accent.Reload();

		Assert.Equal("#5AA2F0", accent.Text);
	}
}
