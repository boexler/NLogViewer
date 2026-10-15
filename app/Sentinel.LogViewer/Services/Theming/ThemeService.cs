using System.Windows;
using System.Windows.Media;

namespace Sentinel.LogViewer.Services.Theming;

/// <summary>
/// Applies the colours of the selected theme, with the custom colours of that theme, to the application resources.
/// Everything that uses them through DynamicResource changes at once.
/// </summary>
public class ThemeService
{
	/// <summary>
	/// Sets the brush of every token. Does nothing while there is no application, e.g. in unit tests of view models.
	/// </summary>
	public void Apply(AppConfiguration configuration)
	{
		var application = Application.Current;
		if (application == null)
			return;

		if (!application.Dispatcher.CheckAccess())
		{
			application.Dispatcher.Invoke(() => Apply(configuration));
			return;
		}

		configuration.ThemeColors.TryGetValue(ThemeCatalog.Find(configuration.Theme).Id, out var customColors);
		foreach (var token in ThemeCatalog.Tokens)
		{
			var brush = new SolidColorBrush(ThemeCatalog.ToColor(ThemeCatalog.Resolve(configuration.Theme, customColors, token.Key)));
			brush.Freeze();
			application.Resources[token.Key] = brush;
		}
	}
}
