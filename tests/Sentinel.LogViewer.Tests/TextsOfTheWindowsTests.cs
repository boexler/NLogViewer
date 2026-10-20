using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
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
	/// The windows of the application; the one for testing the logging is only reachable from the debug menu
	/// </summary>
	private static IEnumerable<string> WindowFiles() =>
		Directory.GetFiles(AppFolder, "*Window.xaml").Where(file => Path.GetFileName(file) != "TestLoggingWindow.xaml");

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
