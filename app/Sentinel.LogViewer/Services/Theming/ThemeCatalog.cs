using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
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
/// A set of colours with a name. The colours are the brushes of a XAML dictionary (Styles/Themes), which <see cref="ThemeService"/>
/// merges into the application resources; <see cref="Colors"/> reads them from the same file.
/// </summary>
public sealed class ThemeDefinition
{
	private readonly Lazy<IReadOnlyDictionary<string, string>> _colors;

	/// <param name="dictionary">Name of the XAML dictionary in Styles/Themes, without the extension</param>
	/// <param name="isDark">Whether the theme is for dark mode; the title bars of the windows follow it</param>
	public ThemeDefinition(string id, string name, string dictionary, bool isDark = false)
	{
		Id = id;
		Name = name;
		IsDark = isDark;
		Dictionary = dictionary;
		_colors = new Lazy<IReadOnlyDictionary<string, string>>(() => ThemeCatalog.ReadColors(dictionary));
	}

	public string Id { get; }

	public string Name { get; }

	public bool IsDark { get; }

	/// <summary>
	/// Name of the XAML dictionary of the theme in Styles/Themes, without the extension
	/// </summary>
	public string Dictionary { get; }

	/// <summary>
	/// The colour of every token as #RRGGBB or #AARRGGBB, by the key of the token
	/// </summary>
	public IReadOnlyDictionary<string, string> Colors => _colors.Value;
}

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
		new ThemeDefinition(NewschoolId, "Newschool", "NewschoolLight"),
		new ThemeDefinition(OldschoolId, "Oldschool", "OldschoolLight"),
		new ThemeDefinition(NewschoolDarkId, "Newschool dark", "NewschoolDark", isDark: true),
		new ThemeDefinition(OldschoolDarkId, "Oldschool dark", "OldschoolDark", isDark: true)
	};

	/// <summary>
	/// Reads the brushes of a theme dictionary as colour texts by the key of the brush. The XAML file is embedded in the assembly as it is,
	/// so it can be read without WPF and without an application, e.g. in a unit test; there is no need to load the dictionary.
	/// </summary>
	internal static IReadOnlyDictionary<string, string> ReadColors(string dictionary)
	{
		var assembly = typeof(ThemeCatalog).Assembly;
		using var stream = assembly.GetManifestResourceStream($"Themes/{dictionary}.xaml")
		                   ?? throw new InvalidOperationException($"The theme dictionary {dictionary} is not part of the assembly.");

		XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
		var key = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
		var colors = new Dictionary<string, string>();
		foreach (var brush in XDocument.Load(stream).Descendants(presentation + "SolidColorBrush"))
		{
			if (brush.Attribute(key)?.Value is { } name && TryNormalize(brush.Attribute("Color")?.Value, out var color))
				colors[name] = color;
		}

		return colors;
	}
}
