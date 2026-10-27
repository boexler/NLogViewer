using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Sentinel.LogViewer.Helpers;

namespace Sentinel.LogViewer.Services.Theming;

/// <summary>
/// Applies the colours of the theme that is in effect, with the custom colours of that theme, to the application resources: the XAML
/// dictionary of the theme is merged in, and the custom colours go into a dictionary of their own behind it, so they win.
/// Everything that uses them through DynamicResource changes at once. In the mode that follows Windows, the theme
/// changes when the system changes between light and dark.
/// </summary>
public class ThemeService : IDisposable
{
	private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

	private static bool _darkTitleBars;
	private static bool _titleBarHandlerRegistered;

	private const string ThemeDictionaryFolder = "Styles/Themes/";

	private readonly Func<bool> _systemUsesLightTheme;
	private readonly Dictionary<string, ResourceDictionary> _themeDictionaries = new();
	private readonly ResourceDictionary _customColors = new();
	private AppConfiguration? _current;
	private bool _watching;

	/// <param name="systemUsesLightTheme">Tells whether Windows is in light mode; reads the system setting when not given</param>
	public ThemeService(Func<bool>? systemUsesLightTheme = null)
	{
		_systemUsesLightTheme = systemUsesLightTheme ?? ReadSystemUsesLightTheme;
	}

	/// <summary>
	/// Returns the id of the theme that is in effect for the configuration
	/// </summary>
	public string ResolveThemeId(AppConfiguration configuration) =>
		ThemeCatalog.ResolveThemeId(configuration, _systemUsesLightTheme());

	/// <summary>
	/// Merges the dictionary of the theme into the application resources and puts the custom colours behind it. Does nothing while
	/// there is no application, e.g. in unit tests of view models.
	/// </summary>
	public void Apply(AppConfiguration configuration)
	{
		_current = configuration;

		var application = Application.Current;
		if (application == null)
			return;

		if (!application.Dispatcher.CheckAccess())
		{
			application.Dispatcher.Invoke(() => Apply(configuration));
			return;
		}

		var theme = ThemeCatalog.Find(ResolveThemeId(configuration));
		configuration.ThemeColors.TryGetValue(theme.Id, out var customColors);

		if (!_themeDictionaries.TryGetValue(theme.Id, out var dictionary))
			_themeDictionaries[theme.Id] = dictionary = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Sentinel.LogViewer;component/{ThemeDictionaryFolder}{theme.Dictionary}.xaml") };

		// The dictionary that App.xaml merges for the designer goes as well as the one of the earlier theme.
		var merged = application.Resources.MergedDictionaries;
		for (var i = merged.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(merged[i], _customColors) || merged[i].Source?.OriginalString.Contains(ThemeDictionaryFolder) == true)
				merged.RemoveAt(i);
		}

		_customColors.Clear();
		foreach (var token in ThemeCatalog.Tokens)
		{
			if (customColors != null && customColors.ContainsKey(token.Key))
				_customColors[token.Key] = CreateBrush(ThemeCatalog.Resolve(theme.Id, customColors, token.Key));
		}

		merged.Add(dictionary);
		merged.Add(_customColors);

		SetSystemColors(application);
		ApplyTitleBars(application, theme.IsDark);
	}

	/// <summary>
	/// Starts following the Windows setting while the mode is "like Windows"
	/// </summary>
	public void StartWatching()
	{
		if (_watching)
			return;

		_watching = true;
		SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
	}

	public void Dispose()
	{
		if (!_watching)
			return;

		_watching = false;
		SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
	}

	private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (_current is { ThemeMode: ThemeCatalog.ModeSystem } configuration &&
		    e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
		{
			Apply(configuration);
		}
	}

	private static SolidColorBrush CreateBrush(string color)
	{
		var brush = new SolidColorBrush(ThemeCatalog.ToColor(color));
		brush.Freeze();
		return brush;
	}

	/// <summary>
	/// Controls that the application does not restyle read the system colours; they get the colours of the theme
	/// </summary>
	private static void SetSystemColors(Application application)
	{
		var resources = application.Resources;
		resources[SystemColors.WindowBrushKey] = application.FindResource("ThemeSurface");
		resources[SystemColors.WindowTextBrushKey] = application.FindResource("ThemeTextPrimary");
		resources[SystemColors.ControlBrushKey] = application.FindResource("ThemeSurfaceAlt");
		resources[SystemColors.ControlTextBrushKey] = application.FindResource("ThemeTextPrimary");
		resources[SystemColors.GrayTextBrushKey] = application.FindResource("ThemeTextMuted");
		resources[SystemColors.ControlDarkBrushKey] = application.FindResource("ThemeBorder");
		resources[SystemColors.ControlDarkDarkBrushKey] = application.FindResource("ThemeBorderStrong");
		resources[SystemColors.HighlightBrushKey] = application.FindResource("ThemeAccent");
		resources[SystemColors.HighlightTextBrushKey] = application.FindResource("ThemeOnAccent");
		resources[SystemColors.InactiveSelectionHighlightBrushKey] = application.FindResource("ThemeAccentSoft");
		resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = application.FindResource("ThemeTextPrimary");
	}

	private static void ApplyTitleBars(Application application, bool dark)
	{
		_darkTitleBars = dark;
		if (!_titleBarHandlerRegistered)
		{
			_titleBarHandlerRegistered = true;
			EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
				new RoutedEventHandler((sender, _) => DarkTitleBar.Apply((Window)sender, _darkTitleBars)));
		}

		foreach (Window window in application.Windows)
			DarkTitleBar.Apply(window, dark);
	}

	private static bool ReadSystemUsesLightTheme()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
			return key?.GetValue("AppsUseLightTheme") is not 0;
		}
		catch (Exception)
		{
			return true;
		}
	}
}
