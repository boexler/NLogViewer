using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// The texts of the windows come from the language files, in every language
/// </summary>
public class TextsOfTheWindowsTests
{
	private static readonly string AppFolder = Path.Combine(RepositoryRoot(), "app", "Sentinel.LogViewer");

	private static string RepositoryRoot()
	{
		var folder = new DirectoryInfo(AppContext.BaseDirectory);
		while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Sentinel.LogViewer.CI.slnf")))
			folder = folder.Parent;

		return folder?.FullName ?? throw new InvalidOperationException("The folder of the repository was not found.");
	}

	/// <summary>
	/// The windows of the application
	/// </summary>
	private static IEnumerable<string> WindowFiles() =>
		Directory.GetFiles(AppFolder, "*Window.xaml");

	[Fact]
	public void TheWindows_HaveNoFixedTexts()
	{
		// Names and gestures are not texts to translate.
		var allowed = new[] { "Sentinel LogViewer" };
		var fixedText = new Regex(@"(?<![\w:.])(Text|Content|Header|ToolTip|Title)=""(?<value>[A-Za-z][^""{]*)""");

		var found = WindowFiles()
			.SelectMany(file => fixedText.Matches(File.ReadAllText(file)).Select(match => (File: Path.GetFileName(file), Value: match.Groups["value"].Value)))
			.Where(text => !allowed.Contains(text.Value))
			.Select(text => $"{text.File}: {text.Value}")
			.ToList();

		Assert.Empty(found);
	}

	[Fact]
	public void EveryTextThatTheWindowsUse_ExistsInEveryLanguageFile()
	{
		var keys = WindowFiles()
			.SelectMany(file => Regex.Matches(File.ReadAllText(file), @"Resource Key=(?<key>\w+)").Select(match => match.Groups["key"].Value))
			.Distinct()
			.ToList();
		Assert.NotEmpty(keys);

		var missing = new List<string>();
		foreach (var file in Directory.GetFiles(Path.Combine(AppFolder, "Resources"), "Resources*.resx"))
		{
			var names = XDocument.Load(file).Descendants("data").Select(data => (string?)data.Attribute("name")).ToHashSet();
			missing.AddRange(keys.Where(key => !names.Contains(key)).Select(key => $"{Path.GetFileName(file)}: {key}"));
		}

		Assert.Empty(missing);
	}

	[Fact]
	public void EveryTextOfTheShortcutsWindow_ExistsInEveryLanguageFile()
	{
		var keys = new[] { "Menu_Shortcuts", "Shortcuts_Title" }
			.Concat(ShortcutCatalog.Groups.SelectMany(group => new[] { group.TitleKey }.Concat(group.Shortcuts.Select(shortcut => shortcut.TextKey))))
			.Concat(ShortcutCatalog.LocalizedKeys.Select(key => "Key_" + key))
			.ToList();

		var missing = new List<string>();
		foreach (var file in Directory.GetFiles(Path.Combine(AppFolder, "Resources"), "Resources*.resx"))
		{
			var names = XDocument.Load(file).Descendants("data").Select(data => (string?)data.Attribute("name")).ToHashSet();
			missing.AddRange(keys.Where(key => !names.Contains(key)).Select(key => $"{Path.GetFileName(file)}: {key}"));
		}

		Assert.Empty(missing);
	}

	[Fact]
	public void TheKeysOfTheShortcuts_AreNamesToTranslate_OrKeysThatAreTheSameInEveryLanguage()
	{
		var same = new Regex(@"^(F\d{1,2}|[A-Z0-9,]|1…8|F\d{1,2}…F\d{1,2})$");

		var parts = ShortcutCatalog.Groups.SelectMany(group => group.Shortcuts).SelectMany(shortcut => shortcut.Keys.Split(" / ")).SelectMany(combination => combination.Split('+')).Distinct();

		Assert.Empty(parts.Where(part => !ShortcutCatalog.LocalizedKeys.Contains(part) && !same.IsMatch(part)));
	}

	[Fact]
	public void TheShortcutsWindow_ShowsTheKeysInTheLanguageOfTheUser()
	{
		using var environment = new ViewModels.SettingsTestEnvironment();
		var culture = CultureInfo.DefaultThreadCurrentCulture;
		var uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
		var threadCulture = Thread.CurrentThread.CurrentCulture;
		var threadUiCulture = Thread.CurrentThread.CurrentUICulture;
		try
		{
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("de");

			var tabs = new ShortcutsViewModel(environment.Localization).Groups.Single(group => group.Title == "Tabs");

			var close = tabs.Items.Single(item => item.Text == "Den aktuellen Tab schließen");
			Assert.Equal(new[] { "Strg + W", "Strg + F4" }, close.Keys);
			Assert.Equal(new[] { "Strg + Bild ↑", "Strg + Umschalt + Tab" }, tabs.Items.Single(item => item.Text == "Zum vorherigen Tab wechseln").Keys);
		}
		finally
		{
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
			Thread.CurrentThread.CurrentCulture = threadCulture;
			Thread.CurrentThread.CurrentUICulture = threadUiCulture;
		}
	}

	private static ColumnMappingViewModel Dialog(Sentinel.LogViewer.Services.LocalizationService? localization) =>
		new(new TextFileFormat { Separator = ";", ColumnCount = 3 }, new List<string> { "2025-12-18;INFO;hello" }, "sample.log", localization);

	[Fact]
	public void TheColumnMappingDialog_IsEnglish_WithoutALanguage()
	{
		var viewModel = Dialog(null);

		Assert.Equal(new[] { "None", "Timestamp", "Level", "Logger", "Message" }, viewModel.FieldTypes.Select(field => field.Display));
		Assert.Contains("Comma (,)", viewModel.Separators.Keys);
		Assert.Contains("Day first (18/12/2025)", viewModel.DateOrders.Keys);
		Assert.Contains("Timestamp: N/A", viewModel.PreviewResults[0].SampleData);
	}

	[Fact]
	public void TheColumnMappingDialog_ShowsTheNamesInTheLanguageThatIsSet_AndKeepsTheValues()
	{
		using var environment = new ViewModels.SettingsTestEnvironment();
		var culture = CultureInfo.DefaultThreadCurrentCulture;
		var uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
		var threadCulture = Thread.CurrentThread.CurrentCulture;
		var threadUiCulture = Thread.CurrentThread.CurrentUICulture;
		try
		{
			environment.Localization.Initialize();
			environment.Localization.SetLanguage("de");

			AssertGerman(Dialog(environment.Localization));
		}
		finally
		{
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
			Thread.CurrentThread.CurrentCulture = threadCulture;
			Thread.CurrentThread.CurrentUICulture = threadUiCulture;
		}
	}

	private static void AssertGerman(ColumnMappingViewModel viewModel)
	{

		Assert.Equal(new[] { "Keine", "Zeitstempel", "Level", "Logger", "Meldung" }, viewModel.FieldTypes.Select(field => field.Display));
		Assert.Equal(new[] { "None", "Timestamp", "Level", "Logger", "Message" }, viewModel.FieldTypes.Select(field => field.Value));
		Assert.Equal(";", viewModel.Separators["Semikolon (;)"]);
		Assert.Contains("Tag zuerst (18/12/2025)", viewModel.DateOrders.Keys);
		Assert.Contains(viewModel.DateOrders.Keys, key => key.StartsWith("Automatisch ("));
		Assert.Contains("Zeitstempel: k. A.", viewModel.PreviewResults[0].SampleData);
	}
}
