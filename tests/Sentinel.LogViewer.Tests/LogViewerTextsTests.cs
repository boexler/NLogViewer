using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sentinel.LogViewer.Wpf;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// The texts of the control: every text exists in all seven languages, the XAML and the code of the control use only texts that exist, and
/// the language of the texts can be changed. The class is in the collection of the WPF tests, because the language is one for the whole process
/// and the tests of the controls read the English texts.
/// </summary>
[Collection(WpfCollection.Name)]
public class LogViewerTextsTests
{
	private static readonly string WpfFolder = Path.Combine(RepositoryRoot(), "ui", "Sentinel.LogViewer.Wpf");

	private static readonly string[] Languages = { "de", "es", "fr", "hu", "pl", "ro" };

	private static string RepositoryRoot()
	{
		var folder = new DirectoryInfo(AppContext.BaseDirectory);
		while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Sentinel.LogViewer.CI.slnf")))
			folder = folder.Parent;

		return folder?.FullName ?? throw new InvalidOperationException("The folder of the repository was not found.");
	}

	private static System.Collections.Generic.Dictionary<string, string> Table(string? language) =>
		XDocument.Load(Path.Combine(WpfFolder, "Texts", language == null ? "Texts.resx" : $"Texts.{language}.resx")).Descendants("data")
			.ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!);

	private static string Placeholders(string text) =>
		string.Join(",", Regex.Matches(text, @"\{(\d+)(:[^}]*)?\}").Select(match => match.Groups[1].Value).OrderBy(index => index));

	[Fact]
	public void EveryText_ExistsInEveryLanguage_IsNotEmpty_AndHasTheSameArguments()
	{
		var english = Table(null);
		var problems = new System.Collections.Generic.List<string>();
		foreach (var language in Languages)
		{
			var table = Table(language);
			problems.AddRange(english.Keys.Except(table.Keys).Select(key => $"{language}: {key} is missing"));
			problems.AddRange(table.Keys.Except(english.Keys).Select(key => $"{language}: {key} is not in English"));
			problems.AddRange(table.Where(pair => english.ContainsKey(pair.Key) && string.IsNullOrWhiteSpace(pair.Value)).Select(pair => $"{language}: {pair.Key} is empty"));
			problems.AddRange(table.Where(pair => english.ContainsKey(pair.Key) && Placeholders(pair.Value) != Placeholders(english[pair.Key]))
				.Select(pair => $"{language}: {pair.Key} has other arguments than the English text"));
		}

		Assert.Empty(problems);
	}

	[Fact]
	public void TheCodeAndTheXaml_UseOnlyTextsThatExist_AndEveryTextIsUsed()
	{
		var files = Directory.GetFiles(WpfFolder, "*.*", SearchOption.AllDirectories)
			.Where(file => (file.EndsWith(".cs") || file.EndsWith(".xaml")) && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
				&& !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !file.Contains($"{Path.DirectorySeparatorChar}Texts{Path.DirectorySeparatorChar}"))
			.Select(File.ReadAllText).ToList();
		var source = string.Join("\n", files);
		var used = Regex.Matches(source, @"(?:Key|Arg0Key)=(?<key>[A-Za-z0-9_]+)|(?:Get|Format)\(""(?<key>[A-Za-z0-9_]+)""|""(?<key>[A-Z][A-Za-z0-9]*_[A-Za-z0-9]+)""")
			.Select(match => match.Groups["key"].Value).ToHashSet();
		var keys = Table(null).Keys.ToHashSet();

		// The levels are looked up by their name ("Level_" + the level).
		var unused = keys.Where(key => !used.Contains(key) && !(key.StartsWith("Level_") && source.Contains("\"Level_\""))).ToList();
		var unknown = used.Where(key => !keys.Contains(key) && !key.EndsWith("_") && Regex.IsMatch(key, "^(Level|Ctl|Filter|Search|Opt|Fld|Tpl|Tr|Hits|Find|Result|Chip|Sort|Jump|Row|Tree|Col|Cell|Dlg|Mp|Msg|Default)_") ).ToList();

		Assert.Empty(unknown);
		Assert.Empty(unused);
	}

	[Fact]
	public void TheXamlOfTheControl_HasNoFixedTexts()
	{
		// Letters on the buttons, examples of formats and the symbols are not texts to translate.
		var allowed = new[] { "Aa", "ab", "T", "D", "I", "W", "E", "F", "ISO  2025-12-18 15:11:48.385", "German  18.12.2025 15:11:48.385", "English  12/18/2025 03:11:48.385 PM" };
		var fixedText = new Regex(@"(?<![\w:.])(Header|ToolTip|Content|Text|Hint|AutomationProperties\.Name)=""(?<value>[A-Za-z][^""{]*)""");

		var found = new[] { Path.Combine(WpfFolder, "Themes", "Generic.xaml"), Path.Combine(WpfFolder, "Helper", "SearchOptions.xaml") }
			.SelectMany(file => fixedText.Matches(File.ReadAllText(file)).Select(match => $"{Path.GetFileName(file)}: {match.Groups["value"].Value}"))
			.Where(text => !allowed.Any(value => text.EndsWith(": " + value)))
			.ToList();

		Assert.Empty(found);
	}

	[Theory]
	[InlineData("de", LogViewerLanguage.German)]
	[InlineData("ES", LogViewerLanguage.Spanish)]
	[InlineData("fr", LogViewerLanguage.French)]
	[InlineData("hu", LogViewerLanguage.Hungarian)]
	[InlineData("pl", LogViewerLanguage.Polish)]
	[InlineData("ro", LogViewerLanguage.Romanian)]
	[InlineData("en", LogViewerLanguage.English)]
	[InlineData("ja", LogViewerLanguage.English)]
	[InlineData(null, LogViewerLanguage.English)]
	public void TheCodeOfALanguage_IsMappedToTheLanguageOfTheControl(string? code, LogViewerLanguage expected) =>
		Assert.Equal(expected, LogViewerTexts.FromCode(code));

	[Fact]
	public void TheTexts_FollowTheLanguage_WithItsNumbers_AndAKeyThatIsNotThereIsReturnedAsItIs()
	{
		try
		{
			Assert.Equal("Entries: 6 of 10,992", LogViewerTexts.Format("Result_Of", 6, 10992));

			LogViewerTexts.Language = LogViewerLanguage.German;
			Assert.Equal("Warnung", LogViewerTexts.Get("Level_Warn"));
			Assert.Equal("Einträge: 6 von 10.992", LogViewerTexts.Format("Result_Of", 6, 10992));
			Assert.Equal("No_Such_Key", LogViewerTexts.Get("No_Such_Key"));
		}
		finally
		{
			LogViewerTexts.Language = LogViewerLanguage.English;
		}
	}
}