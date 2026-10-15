using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Sentinel.LogViewer.Helpers;

namespace Sentinel.LogViewer.Services.Theming;

/// <summary>
/// Applies the colours of the theme that is in effect, with the custom colours of that theme, to the application resources.
/// Everything that uses them through DynamicResource changes at once. In the mode that follows Windows, the theme
/// changes when the system changes between light and dark.
/// </summary>
public class ThemeService : IDisposable
{
	private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

	private static bool _darkTitleBars;
	private static bool _titleBarHandlerRegistered;

	private readonly Func<bool> _systemUsesLightTheme;
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
	/// Sets the brush of every token. Does nothing while there is no application, e.g. in unit tests of view models.
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
		foreach (var token in ThemeCatalog.Tokens)
			application.Resources[token.Key] = CreateBrush(ThemeCatalog.Resolve(theme.Id, customColors, token.Key));

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
		resources[SystemColors.WindowBrushKey] = resources["ThemeSurface"];
		resources[SystemColors.WindowTextBrushKey] = resources["ThemeTextPrimary"];
		resources[SystemColors.ControlBrushKey] = resources["ThemeSurfaceAlt"];
		resources[SystemColors.ControlTextBrushKey] = resources["ThemeTextPrimary"];
		resources[SystemColors.GrayTextBrushKey] = resources["ThemeTextMuted"];
		resources[SystemColors.ControlDarkBrushKey] = resources["ThemeBorder"];
		resources[SystemColors.HighlightBrushKey] = resources["ThemeAccent"];
		resources[SystemColors.HighlightTextBrushKey] = resources["ThemeOnAccent"];
		resources[SystemColors.InactiveSelectionHighlightBrushKey] = resources["ThemeAccentSoft"];
		resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = resources["ThemeTextPrimary"];
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
