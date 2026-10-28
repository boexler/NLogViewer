using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Sentinel.LogViewer.Helpers;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels.Settings;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that the windows of the application take a new language at once, without a restart
/// </summary>
[Collection(WpfCollection.Name)]
public class LanguageSwitchTests
{
	private readonly WpfStaContextFixture _context;

	public LanguageSwitchTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	/// <summary>
	/// Runs a test with its own settings and with the texts of the XAML taken from them; the culture of the process is put back
	/// </summary>
	private void WithEnvironment(Action<SettingsTestEnvironment> test) => _context.RunOnSta(() =>
	{
		using var environment = new SettingsTestEnvironment();
		var source = ResourceExtension.Localization;
		var culture = CultureInfo.DefaultThreadCurrentCulture;
		var uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
		var threadCulture = Thread.CurrentThread.CurrentCulture;
		var threadUiCulture = Thread.CurrentThread.CurrentUICulture;
		try
		{
			ResourceExtension.Localization = () => environment.Localization;
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("en");
			test(environment);
		}
		finally
		{
			ResourceExtension.Localization = source;
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
			Thread.CurrentThread.CurrentCulture = threadCulture;
			Thread.CurrentThread.CurrentUICulture = threadUiCulture;
		}
	});

	[Fact]
	public void ATextInTheXaml_FollowsTheLanguageAtOnce_AndKeepsItsDefaultWithoutAKey() =>
		WithEnvironment(environment =>
		{
			var button = (Button)XamlReader.Parse(
				"<Button xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:h=\"clr-namespace:Sentinel.LogViewer.Helpers;assembly=Sentinel.LogViewer\" " +
				"Content=\"{h:Resource Key=Tab_Close, DefaultValue=Close tab}\" ToolTip=\"{h:Resource Key=No_Such_Key, DefaultValue=Fallback}\"/>");
			Assert.Equal(("Close tab", "Fallback"), (button.Content, button.ToolTip));

			environment.Localization.SetLanguage("de");

			Assert.Equal(("Tab schließen", "Fallback"), (button.Content, button.ToolTip));
		});

	[Fact]
	public void TheSettingsWindow_TakesTheNewLanguageWhenTheLanguageSettingIsChanged_AndStaysOnItsPage() =>
		WithEnvironment(environment =>
		{
			var viewModel = environment.CreateViewModel();
			viewModel.SelectedCategory = viewModel.Category("Appearance");
			var window = PrepareForTest(new SettingsWindow(viewModel));
			window.Show();
			Settle();
			try
			{
				Assert.Contains(Descendants<TextBlock>(window), text => text.Text == "Appearance");
				Assert.False(viewModel.Setting<ChoiceSettingViewModel>("Language").NeedsRestart);

				viewModel.Setting<ChoiceSettingViewModel>("Language").Value = "de";
				Settle();

				Assert.Equal("Appearance", viewModel.SelectedCategory!.Id);
				Assert.Equal("Darstellung", viewModel.SelectedCategory.Title);
				Assert.Contains(viewModel.Categories, category => category.Title == "Allgemein");
				Assert.Contains(Descendants<TextBlock>(window), text => text.Text == "Darstellung");
				Assert.DoesNotContain(Descendants<TextBlock>(window), text => text.Text == "Appearance");
				Assert.Equal("de", environment.Saved.Language);
			}
			finally
			{
				window.Close();
			}
		});
}
