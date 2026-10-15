using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="ThemeCatalog"/>: the themes, and the rules for custom colours
/// </summary>
public class ThemeCatalogTests
{
	[Fact]
	public void EveryThemeDefinesAValidColourForEveryToken()
	{
		foreach (var theme in ThemeCatalog.Themes)
		{
			foreach (var token in ThemeCatalog.Tokens)
			{
				Assert.True(theme.Colors.TryGetValue(token.Key, out var color), $"{theme.Id} has no colour for {token.Key}");
				Assert.True(ThemeCatalog.TryNormalize(color, out var normalized) && normalized == color, $"{theme.Id}: {token.Key} = {color}");
			}

			Assert.Equal(ThemeCatalog.Tokens.Count, theme.Colors.Count);
		}
	}

	[Fact]
	public void Oldschool_KeepsTheColoursOfTheLogViewerControl()
	{
		var oldschool = ThemeCatalog.Find(ThemeCatalog.OldschoolId);

		Assert.Equal("#D3D3D3", oldschool.Colors["LogTraceBackground"]);
		Assert.Equal("#042271", oldschool.Colors["LogTraceForeground"]);
		Assert.Equal("#90EE90", oldschool.Colors["LogDebugBackground"]);
		Assert.Equal("#FF0000", oldschool.Colors["LogErrorBackground"]);
		Assert.Equal("#FFFBE6", oldschool.Colors["LogTextSearchTermBackground"]);
		Assert.Equal("#FFD54F", oldschool.Colors["LogFindHighlightBackground"]);
	}

	[Fact]
	public void Find_ForAnUnknownTheme_ReturnsTheDefault()
	{
		Assert.Equal(ThemeCatalog.DefaultThemeId, ThemeCatalog.Find("nonsense").Id);
		Assert.Equal(ThemeCatalog.DefaultThemeId, ThemeCatalog.Find(null).Id);
		Assert.Equal(ThemeCatalog.OldschoolId, ThemeCatalog.Find("OLDSCHOOL").Id);
	}

	[Theory]
	[InlineData("#1b1b1b", "#1B1B1B")]
	[InlineData("1B1B1B", "#1B1B1B")]
	[InlineData("  #abc ", "#AABBCC")]
	[InlineData("#FF123456", "#123456")]
	[InlineData("#80123456", "#80123456")]
	public void TryNormalize_AcceptsColoursInCommonNotations(string input, string expected)
	{
		Assert.True(ThemeCatalog.TryNormalize(input, out var normalized));
		Assert.Equal(expected, normalized);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("red")]
	[InlineData("#12345")]
	[InlineData("#1234567")]
	[InlineData("#GG0000")]
	public void TryNormalize_RejectsEverythingElse(string? input)
	{
		Assert.False(ThemeCatalog.TryNormalize(input, out _));
	}

	[Fact]
	public void Resolve_PrefersACustomColourAndIgnoresAnInvalidOne()
	{
		var custom = new Dictionary<string, string> { ["ThemeAccent"] = "#112233", ["ThemeBorder"] = "nonsense" };

		Assert.Equal("#112233", ThemeCatalog.Resolve(ThemeCatalog.NewschoolId, custom, "ThemeAccent"));
		Assert.Equal("#C9CED6", ThemeCatalog.Resolve(ThemeCatalog.NewschoolId, custom, "ThemeBorder"));
		Assert.Equal("#0A5CB8", ThemeCatalog.Resolve(ThemeCatalog.NewschoolId, null, "ThemeAccent"));
	}

	[Fact]
	public void Sanitize_RemovesWhatDoesNotExist()
	{
		var configuration = new AppConfiguration
		{
			ThemeMode = "nonsense",
			LightTheme = ThemeCatalog.DarkId,
			ThemeColors = new()
			{
				["OldSchool"] = new() { ["ThemeAccent"] = "#abc", ["ThemeBorder"] = "nonsense", ["NoSuchToken"] = "#000000" },
				["gone"] = new() { ["ThemeAccent"] = "#000000" },
				["newschool"] = new() { ["ThemeBorder"] = "nonsense" }
			}
		};

		ThemeCatalog.Sanitize(configuration);

		Assert.Equal(ThemeCatalog.ModeSystem, configuration.ThemeMode);
		Assert.Equal(ThemeCatalog.DefaultThemeId, configuration.LightTheme);
		var colors = Assert.Single(configuration.ThemeColors);
		Assert.Equal(ThemeCatalog.OldschoolId, colors.Key);
		Assert.Equal(new Dictionary<string, string> { ["ThemeAccent"] = "#AABBCC" }, colors.Value);
	}

	[Fact]
	public void Tokens_AreUnique_AndGroupedBySection()
	{
		Assert.Equal(ThemeCatalog.Tokens.Count, ThemeCatalog.Tokens.Select(token => token.Key).Distinct().Count());

		var sections = ThemeCatalog.Tokens.Select(token => token.Section).ToList();
		Assert.Equal(sections.OrderBy(section => section), sections);
	}

	[Theory]
	[InlineData("light", true, "newschool")]
	[InlineData("light", false, "newschool")]
	[InlineData("dark", true, "dark")]
	[InlineData("dark", false, "dark")]
	[InlineData("system", true, "newschool")]
	[InlineData("system", false, "dark")]
	[InlineData("nonsense", false, "dark")]
	public void ResolveThemeId_FollowsTheModeAndTheSystem(string mode, bool systemUsesLightTheme, string expected)
	{
		var configuration = new AppConfiguration { ThemeMode = mode };

		Assert.Equal(expected, ThemeCatalog.ResolveThemeId(configuration, systemUsesLightTheme));
	}

	[Fact]
	public void ResolveThemeId_InLightMode_UsesTheChosenLightTheme_NeverTheDarkOne()
	{
		var oldschool = new AppConfiguration { ThemeMode = ThemeCatalog.ModeLight, LightTheme = ThemeCatalog.OldschoolId };
		var broken = new AppConfiguration { ThemeMode = ThemeCatalog.ModeLight, LightTheme = ThemeCatalog.DarkId };

		Assert.Equal(ThemeCatalog.OldschoolId, ThemeCatalog.ResolveThemeId(oldschool, true));
		Assert.Equal(ThemeCatalog.DefaultThemeId, ThemeCatalog.ResolveThemeId(broken, true));
	}

	[Fact]
	public void OnlyTheDarkThemeIsDark()
	{
		Assert.Equal(new[] { ThemeCatalog.DarkId }, ThemeCatalog.Themes.Where(theme => theme.IsDark).Select(theme => theme.Id));
		Assert.DoesNotContain(ThemeCatalog.LightThemes, theme => theme.IsDark);
	}
}
