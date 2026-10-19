using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.Services.Theming;

/// <summary>
/// Where a colour is used; the settings window shows one section per value
/// </summary>
public enum ThemeSection
{
	Interface,
	LogLevels,
	Highlighting
}

/// <summary>
/// A colour that a theme defines. The key is the name of the brush in the application resources.
/// </summary>
/// <param name="Name">Name that the texts of the settings window are looked up with</param>
/// <param name="Level">The log level of a colour of the log list, e.g. "Trace"</param>
/// <param name="IsBackground">Whether the colour of a log level is its background (otherwise its text)</param>
public sealed record ThemeToken(string Key, ThemeSection Section, string Name, string? Level = null, bool IsBackground = false);

/// <summary>
/// A set of colours with a name
/// </summary>
/// <param name="IsDark">Whether the theme is for dark mode; the title bars of the windows follow it</param>
public sealed record ThemeDefinition(string Id, string Name, IReadOnlyDictionary<string, string> Colors, bool IsDark = false);

/// <summary>
/// The colours of the application: the tokens that exist, the built-in themes and the rules for custom colours.
/// The colours of the log list are dependency properties of the <see cref="Wpf.LogViewer"/> control, so a theme sets them
/// through the resource "Log" + the name of the property.
/// </summary>
public static class ThemeCatalog
{
	public const string NewschoolId = "newschool";
	public const string OldschoolId = "oldschool";
	public const string NewschoolDarkId = NewschoolId + DarkSuffix;
	public const string OldschoolDarkId = OldschoolId + DarkSuffix;
	public const string DefaultThemeId = NewschoolId;

	/// <summary>
	/// The values of <see cref="AppConfiguration.ThemeMode"/>
	/// </summary>
	public const string ModeSystem = "system";
	public const string ModeLight = "light";
	public const string ModeDark = "dark";

	private const string LogPrefix = "Log";
	private const string DarkSuffix = "-dark";

	private static readonly string[] Levels = { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

	private static readonly string[] HighlightProperties =
	{
		"TextSearchTermBackground", "TextSearchTermBorderBrush", "TextSearchTermForeground",
		"RegexSearchTermBackground", "RegexSearchTermBorderBrush", "RegexSearchTermForeground", "RegexPrefixForeground",
		"SearchHighlightBackground", "FindHighlightBackground"
	};

	private static readonly string[] InterfaceKeys =
	{
		"ThemePageBackground", "ThemeSurface", "ThemeSurfaceAlt", "ThemeBorder", "ThemeBorderStrong",
		"ThemeTextPrimary", "ThemeTextSecondary", "ThemeTextMuted", "ThemeAccent", "ThemeAccentSoft", "ThemeOnAccent",
		"ThemeHover", "ThemeSuccess", "ThemeError", "ThemeWarningBackground", "ThemeWarningText"
	};

	public static IReadOnlyList<ThemeToken> Tokens { get; } = CreateTokens();

	public static IReadOnlyList<ThemeDefinition> Themes { get; } = CreateThemes();

	/// <summary>
	/// The designs the user can choose from; each of them exists in a light and in a dark version
	/// </summary>
	public static IEnumerable<ThemeDefinition> Designs => Themes.Where(theme => !theme.IsDark);

	/// <summary>
	/// Returns the id of the theme that is in effect: the chosen design in its light or dark version, depending on the mode;
	/// the mode that follows Windows takes the one that fits the system setting
	/// </summary>
	public static string ResolveThemeId(AppConfiguration configuration, bool systemUsesLightTheme)
	{
		var dark = configuration.ThemeMode switch
		{
			ModeDark => true,
			ModeLight => false,
			_ => !systemUsesLightTheme
		};

		var design = Designs.FirstOrDefault(theme => theme.Id.Equals(configuration.Theme, StringComparison.OrdinalIgnoreCase))
		             ?? Find(DefaultThemeId);
		return dark ? design.Id + DarkSuffix : design.Id;
	}

	/// <summary>
	/// Returns the theme with the id, or the default theme when there is none
	/// </summary>
	public static ThemeDefinition Find(string? id) =>
		Themes.FirstOrDefault(theme => theme.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Themes.First(theme => theme.Id == DefaultThemeId);

	/// <summary>
	/// Returns the colour a token has in a theme, with the custom colours of that theme applied
	/// </summary>
	public static string Resolve(string? themeId, IReadOnlyDictionary<string, string>? customColors, string key)
	{
		if (customColors != null && customColors.TryGetValue(key, out var custom) && TryNormalize(custom, out var normalized))
			return normalized;

		return Find(themeId).Colors[key];
	}

	/// <summary>
	/// Parses #RGB, #RRGGBB or #AARRGGBB (the # is optional) and returns it as #RRGGBB, or #AARRGGBB when it is transparent
	/// </summary>
	public static bool TryNormalize(string? text, out string normalized)
	{
		normalized = string.Empty;
		var digits = (text ?? string.Empty).Trim().TrimStart('#');
		if (digits.Length == 3)
			digits = string.Concat(digits.Select(digit => new string(digit, 2)));

		if (digits.Length is not (6 or 8) || !digits.All(Uri.IsHexDigit))
			return false;

		if (digits.Length == 8 && digits.StartsWith("FF", StringComparison.OrdinalIgnoreCase))
			digits = digits[2..];

		normalized = "#" + digits.ToUpperInvariant();
		return true;
	}

	/// <summary>
	/// Creates the colour of a normalized colour text
	/// </summary>
	public static Color ToColor(string normalized)
	{
		var digits = normalized.TrimStart('#');
		var alpha = digits.Length == 8 ? byte.Parse(digits[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)0xFF;
		var rgb = digits[^6..];
		return Color.FromArgb(alpha,
			byte.Parse(rgb[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
			byte.Parse(rgb[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
			byte.Parse(rgb[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Removes what a hand-edited or imported configuration could contain that does not exist: unknown themes, unknown tokens and invalid colours
	/// </summary>
	public static void Sanitize(AppConfiguration configuration)
	{
		configuration.ThemeMode = configuration.ThemeMode is ModeLight or ModeDark ? configuration.ThemeMode : ModeSystem;
		configuration.Theme = Designs.FirstOrDefault(theme => theme.Id.Equals(configuration.Theme, StringComparison.OrdinalIgnoreCase))?.Id
		                      ?? DefaultThemeId;

		var knownKeys = Tokens.Select(token => token.Key).ToHashSet();
		var cleaned = new Dictionary<string, Dictionary<string, string>>();
		foreach (var (themeId, colors) in configuration.ThemeColors ?? new Dictionary<string, Dictionary<string, string>>())
		{
			var theme = Themes.FirstOrDefault(candidate => candidate.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase));
			if (theme == null || colors == null)
				continue;

			var valid = new Dictionary<string, string>();
			foreach (var (key, value) in colors)
			{
				if (knownKeys.Contains(key) && TryNormalize(value, out var normalized))
					valid[key] = normalized;
			}

			if (valid.Count > 0)
				cleaned[theme.Id] = valid;
		}

		configuration.ThemeColors = cleaned;
	}

	private static List<ThemeToken> CreateTokens()
	{
		var tokens = InterfaceKeys
			.Select(key => new ThemeToken(key, ThemeSection.Interface, key))
			.ToList();

		foreach (var level in Levels)
		{
			tokens.Add(new ThemeToken(LogPrefix + level + "Background", ThemeSection.LogLevels, level + "Background", level, true));
			tokens.Add(new ThemeToken(LogPrefix + level + "Foreground", ThemeSection.LogLevels, level + "Foreground", level));
		}

		tokens.AddRange(HighlightProperties.Select(property => new ThemeToken(LogPrefix + property, ThemeSection.Highlighting, LogPrefix + property)));
		return tokens;
	}

	private static IReadOnlyList<ThemeDefinition> CreateThemes() => new[]
	{
		new ThemeDefinition(NewschoolId, "Newschool", new Dictionary<string, string>
		{
			["ThemePageBackground"] = "#F1F3F5",
			["ThemeSurface"] = "#FFFFFF",
			["ThemeSurfaceAlt"] = "#E6E9ED",
			["ThemeBorder"] = "#C9CED6",
			["ThemeBorderStrong"] = "#8A929D",
			["ThemeTextPrimary"] = "#101418",
			["ThemeTextSecondary"] = "#3F4854",
			["ThemeTextMuted"] = "#69727E",
			["ThemeAccent"] = "#0A5CB8",
			["ThemeAccentSoft"] = "#D2E3F7",
			["ThemeOnAccent"] = "#FFFFFF",
			["ThemeHover"] = "#DDE1E6",
			["ThemeSuccess"] = "#0B6A0B",
			["ThemeError"] = "#B5271A",
			["ThemeWarningBackground"] = "#FFE69C",
			["ThemeWarningText"] = "#5A4300",

			["LogTraceBackground"] = "#E4E7EB",
			["LogTraceForeground"] = "#4A5360",
			["LogDebugBackground"] = "#CDEBD3",
			["LogDebugForeground"] = "#12501F",
			["LogInfoBackground"] = "#CFE3F8",
			["LogInfoForeground"] = "#083E78",
			["LogWarnBackground"] = "#FFE69C",
			["LogWarnForeground"] = "#5A4300",
			["LogErrorBackground"] = "#F9C6CA",
			["LogErrorForeground"] = "#8A1C22",
			["LogFatalBackground"] = "#8A1C22",
			["LogFatalForeground"] = "#FFFFFF",

			["LogTextSearchTermBackground"] = "#FFE69C",
			["LogTextSearchTermBorderBrush"] = "#D9A400",
			["LogTextSearchTermForeground"] = "#101418",
			["LogRegexSearchTermBackground"] = "#D2E3F7",
			["LogRegexSearchTermBorderBrush"] = "#4F8FD6",
			["LogRegexSearchTermForeground"] = "#101418",
			["LogRegexPrefixForeground"] = "#0A5CB8",
			["LogSearchHighlightBackground"] = "#FFD54F",
			["LogFindHighlightBackground"] = "#F58CC0"
		}),
		new ThemeDefinition(OldschoolId, "Oldschool", CreateOldschoolColors(dark: false)),
		new ThemeDefinition(NewschoolDarkId, "Newschool dark", new Dictionary<string, string>
		{
			["ThemePageBackground"] = "#1E2024",
			["ThemeSurface"] = "#2A2D33",
			["ThemeSurfaceAlt"] = "#17191C",
			["ThemeBorder"] = "#3A3F47",
			["ThemeBorderStrong"] = "#6A727E",
			["ThemeTextPrimary"] = "#ECEFF3",
			["ThemeTextSecondary"] = "#B6BDC8",
			["ThemeTextMuted"] = "#8A929E",
			["ThemeAccent"] = "#5AA2F0",
			["ThemeAccentSoft"] = "#1F3A5C",
			["ThemeOnAccent"] = "#0B1220",
			["ThemeHover"] = "#353A42",
			["ThemeSuccess"] = "#5FD068",
			["ThemeError"] = "#FF6B5E",
			["ThemeWarningBackground"] = "#4A3B10",
			["ThemeWarningText"] = "#FFD97A",

			["LogTraceBackground"] = "#2B2E34",
			["LogTraceForeground"] = "#A0A8B4",
			["LogDebugBackground"] = "#1F3B28",
			["LogDebugForeground"] = "#8FE0A0",
			["LogInfoBackground"] = "#1C3552",
			["LogInfoForeground"] = "#9CC9F8",
			["LogWarnBackground"] = "#4A3B10",
			["LogWarnForeground"] = "#FFD97A",
			["LogErrorBackground"] = "#4F2226",
			["LogErrorForeground"] = "#FF9EA3",
			["LogFatalBackground"] = "#B3262E",
			["LogFatalForeground"] = "#FFFFFF",

			["LogTextSearchTermBackground"] = "#4A3B10",
			["LogTextSearchTermBorderBrush"] = "#B38A00",
			["LogTextSearchTermForeground"] = "#ECEFF3",
			["LogRegexSearchTermBackground"] = "#1F3A5C",
			["LogRegexSearchTermBorderBrush"] = "#3F78B8",
			["LogRegexSearchTermForeground"] = "#ECEFF3",
			["LogRegexPrefixForeground"] = "#5AA2F0",
			["LogSearchHighlightBackground"] = "#8A6A00",
			["LogFindHighlightBackground"] = "#A63D7B"
		}, IsDark: true),
		new ThemeDefinition(OldschoolDarkId, "Oldschool dark", CreateOldschoolColors(dark: true), IsDark: true)
	};

	/// <summary>
	/// The classic look: grey surfaces and the colours that the log list control brings along, also in the dark version
	/// </summary>
	private static Dictionary<string, string> CreateOldschoolColors(bool dark)
	{
		var colors = dark ? new Dictionary<string, string>
		{
			["ThemePageBackground"] = "#1F1F1F",
			["ThemeSurface"] = "#2D2D2D",
			["ThemeSurfaceAlt"] = "#383838",
			["ThemeBorder"] = "#4A4A4A",
			["ThemeBorderStrong"] = "#7A7A7A",
			["ThemeTextPrimary"] = "#F0F0F0",
			["ThemeTextSecondary"] = "#C8C8C8",
			["ThemeTextMuted"] = "#999999",
			["ThemeAccent"] = "#3A96DD",
			["ThemeAccentSoft"] = "#26425E",
			["ThemeOnAccent"] = "#FFFFFF",
			["ThemeHover"] = "#444444",
			["ThemeSuccess"] = "#5FD068",
			["ThemeError"] = "#FF6B5E",
			["ThemeWarningBackground"] = "#4A3B10",
			["ThemeWarningText"] = "#FFD97A"
		} : new Dictionary<string, string>
		{
			["ThemePageBackground"] = "#FFFFFF",
			["ThemeSurface"] = "#FFFFFF",
			["ThemeSurfaceAlt"] = "#F0F0F0",
			["ThemeBorder"] = "#D0D0D0",
			["ThemeBorderStrong"] = "#ABABAB",
			["ThemeTextPrimary"] = "#000000",
			["ThemeTextSecondary"] = "#444444",
			["ThemeTextMuted"] = "#777777",
			["ThemeAccent"] = "#0078D7",
			["ThemeAccentSoft"] = "#CCE4F7",
			["ThemeOnAccent"] = "#FFFFFF",
			["ThemeHover"] = "#E5E5E5",
			["ThemeSuccess"] = "#107C10",
			["ThemeError"] = "#C42B1C",
			["ThemeWarningBackground"] = "#FFF4CE",
			["ThemeWarningText"] = "#6D5200"
		};

		// The control keeps its colours as defaults of its dependency properties; reading them from there keeps the two in step.
		foreach (var token in Tokens.Where(token => token.Key.StartsWith(LogPrefix, StringComparison.Ordinal)))
		{
			var property = (DependencyProperty)typeof(Wpf.LogViewer)
				.GetField(token.Key[LogPrefix.Length..] + "Property")!.GetValue(null)!;
			var color = ((SolidColorBrush)property.DefaultMetadata.DefaultValue).Color;
			colors[token.Key] = color.A == 0xFF
				? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
				: $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
		}

		return colors;
	}
}
