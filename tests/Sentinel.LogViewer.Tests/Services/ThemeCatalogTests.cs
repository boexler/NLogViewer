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
			Theme = ThemeCatalog.OldschoolDarkId,
			ThemeColors = new()
			{
				["OldSchool"] = new() { ["ThemeAccent"] = "#abc", ["ThemeBorder"] = "nonsense", ["NoSuchToken"] = "#000000" },
				["gone"] = new() { ["ThemeAccent"] = "#000000" },
				["newschool"] = new() { ["ThemeBorder"] = "nonsense" },
				["oldschool-dark"] = new() { ["ThemeBorder"] = "#445566" }
			}
		};

		ThemeCatalog.Sanitize(configuration);

		Assert.Equal(ThemeCatalog.ModeSystem, configuration.ThemeMode);
		Assert.Equal(ThemeCatalog.DefaultThemeId, configuration.Theme);
		Assert.Equal(new[] { ThemeCatalog.OldschoolId, ThemeCatalog.OldschoolDarkId }, configuration.ThemeColors.Keys.OrderBy(key => key));
		Assert.Equal(new Dictionary<string, string> { ["ThemeAccent"] = "#AABBCC" }, configuration.ThemeColors[ThemeCatalog.OldschoolId]);
	}

	[Fact]
	public void Tokens_AreUnique_AndGroupedBySection()
	{
		Assert.Equal(ThemeCatalog.Tokens.Count, ThemeCatalog.Tokens.Select(token => token.Key).Distinct().Count());

		var sections = ThemeCatalog.Tokens.Select(token => token.Section).ToList();
		Assert.Equal(sections.OrderBy(section => section), sections);
	}

	[Theory]
	[InlineData("light", "newschool", true, "newschool")]
	[InlineData("light", "oldschool", false, "oldschool")]
	[InlineData("dark", "newschool", true, "newschool-dark")]
	[InlineData("dark", "oldschool", false, "oldschool-dark")]
	[InlineData("system", "newschool", true, "newschool")]
	[InlineData("system", "newschool", false, "newschool-dark")]
	[InlineData("system", "oldschool", true, "oldschool")]
	[InlineData("system", "oldschool", false, "oldschool-dark")]
	[InlineData("light", "warm", false, "warm")]
	[InlineData("dark", "warm", true, "warm-dark")]
	[InlineData("nonsense", "nonsense", false, "newschool-dark")]
	[InlineData("light", "newschool-dark", true, "newschool")]
	public void ResolveThemeId_CombinesTheDesignWithTheModeOrTheSystem(string mode, string design, bool systemUsesLightTheme, string expected)
	{
		var configuration = new AppConfiguration { ThemeMode = mode, Theme = design };

		Assert.Equal(expected, ThemeCatalog.ResolveThemeId(configuration, systemUsesLightTheme));
	}

	[Fact]
	public void EveryDesignHasALightAndADarkTheme()
	{
		foreach (var design in ThemeCatalog.Designs)
		{
			Assert.False(design.IsDark);
			Assert.True(ThemeCatalog.Find(design.Id + "-dark").IsDark);
			Assert.Equal(design.Id + "-dark", ThemeCatalog.Find(design.Id + "-dark").Id);
		}

		Assert.Equal(6, ThemeCatalog.Themes.Count);
	}

	[Fact]
	public void Oldschool_HasTheColoursThatTheLogViewerControlHasAsDefaults()
	{
		var oldschool = ThemeCatalog.Find(ThemeCatalog.OldschoolId);

		foreach (var token in ThemeCatalog.Tokens.Where(token => token.Key.StartsWith("Log")))
		{
			var property = (System.Windows.DependencyProperty)typeof(Sentinel.LogViewer.Wpf.LogViewer)
				.GetField(token.Key["Log".Length..] + "Property")!.GetValue(null)!;
			var color = ((System.Windows.Media.SolidColorBrush)property.DefaultMetadata.DefaultValue).Color;
			var text = color.A == 0xFF ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

			Assert.True(oldschool.Colors[token.Key] == text, $"{token.Key}: the dictionary has {oldschool.Colors[token.Key]}, the control {text}");
		}
	}

	[Fact]
	public void OldschoolDark_HasDarkSurfacesAndTheClassicColoursOfTheLogList()
	{
		var light = ThemeCatalog.Find(ThemeCatalog.OldschoolId).Colors;
		var dark = ThemeCatalog.Find(ThemeCatalog.OldschoolDarkId).Colors;

		Assert.NotEqual(light["ThemePageBackground"], dark["ThemePageBackground"]);
		foreach (var token in ThemeCatalog.Tokens.Where(token => token.Section != ThemeSection.Interface))
			Assert.Equal(light[token.Key], dark[token.Key]);
	}

	[Theory]
	[InlineData(ThemeCatalog.WarmId)]
	[InlineData(ThemeCatalog.WarmDarkId)]
	public void Warm_KeepsTheTextReadable_OnEverySurfaceAndInEveryLevelOfTheLogList(string themeId)
	{
		var colors = ThemeCatalog.Find(themeId).Colors;
		var pairs = new List<(string Text, string Background)>
		{
			("ThemeTextPrimary", "ThemeSurface"), ("ThemeTextPrimary", "ThemePageBackground"), ("ThemeTextSecondary", "ThemePageBackground"),
			("ThemeTextMuted", "ThemePageBackground"), ("ThemeTextMuted", "ThemeSurface"), ("ThemeAccent", "ThemeSurface"), ("ThemeOnAccent", "ThemeAccent"),
			("ThemeWarningText", "ThemeWarningBackground")
		};
		pairs.AddRange(new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" }.Select(level => ($"Log{level}Foreground", $"Log{level}Background")));

		var weak = pairs.Where(pair => Contrast(colors[pair.Text], colors[pair.Background]) < 4.5).Select(pair => $"{pair.Text} on {pair.Background}");

		Assert.Empty(weak);
	}

	private static double Contrast(string first, string second)
	{
		static double Luminance(string color)
		{
			var c = ThemeCatalog.ToColor(color);
			static double Channel(byte value) => value / 255.0 is var s && s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
			return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
		}

		var (light, dark) = (Math.Max(Luminance(first), Luminance(second)), Math.Min(Luminance(first), Luminance(second)));
		return (light + 0.05) / (dark + 0.05);
	}}
