using System.Linq;
using System.Windows;
using System.Windows.Media;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests that <see cref="ThemeService"/> puts the colours into the application resources, where the windows pick them up
/// </summary>
[Collection(WpfCollection.Name)]
public class ThemeServiceTests : IDisposable
{
	private readonly WpfStaContextFixture _context;

	public ThemeServiceTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	// The application is shared by all WPF tests, so it goes back to the default theme.
	public void Dispose() => _context.RunOnSta(() => new ThemeService(() => true).Apply(new AppConfiguration()));

	[Fact]
	public void Apply_SetsABrushForEveryToken()
	{
		_context.RunOnSta(() =>
		{
			new ThemeService(() => true).Apply(new AppConfiguration { ThemeMode = ThemeCatalog.ModeLight,
				Theme = ThemeCatalog.OldschoolId });

			foreach (var token in ThemeCatalog.Tokens)
			{
				var brush = Assert.IsType<SolidColorBrush>(Application.Current.FindResource(token.Key));
				Assert.True(brush.IsFrozen);
				Assert.Equal(ThemeCatalog.ToColor(ThemeCatalog.Find(ThemeCatalog.OldschoolId).Colors[token.Key]), brush.Color);
			}
		});
	}

	[Fact]
	public void Apply_UsesTheCustomColoursOfTheSelectedThemeOnly()
	{
		_context.RunOnSta(() =>
		{
			var configuration = new AppConfiguration
			{
				ThemeMode = ThemeCatalog.ModeLight,
				Theme = ThemeCatalog.OldschoolId,
				ThemeColors = new()
				{
					[ThemeCatalog.OldschoolId] = new() { ["ThemeAccent"] = "#112233", ["LogTraceBackground"] = "#80FF0000" },
					[ThemeCatalog.NewschoolId] = new() { ["ThemeBorder"] = "#445566" }
				}
			};

			new ThemeService(() => true).Apply(configuration);

			Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ((SolidColorBrush)Application.Current.FindResource("ThemeAccent")).Color);
			Assert.Equal(Color.FromArgb(0x80, 0xFF, 0, 0), ((SolidColorBrush)Application.Current.FindResource("LogTraceBackground")).Color);
			Assert.Equal(Color.FromRgb(0xD0, 0xD0, 0xD0), ((SolidColorBrush)Application.Current.FindResource("ThemeBorder")).Color);
		});
	}

	private static int ThemeDictionaries() => Application.Current.Resources.MergedDictionaries.Count(
		dictionary => dictionary.Source?.OriginalString.Contains("/Styles/Themes/") == true);

	[Fact]
	public void Apply_MergesTheDictionaryOfTheTheme_AndSwapsItWhenTheThemeChanges()
	{
		_context.RunOnSta(() =>
		{
			var service = new ThemeService(() => true);
			service.Apply(new AppConfiguration { ThemeMode = ThemeCatalog.ModeLight, Theme = ThemeCatalog.OldschoolId });
			Assert.Equal(1, ThemeDictionaries());
			Assert.Equal(Color.FromRgb(0xF0, 0xF0, 0xF0), ((SolidColorBrush)Application.Current.FindResource("ThemeSurfaceAlt")).Color);

			service.Apply(new AppConfiguration { ThemeMode = ThemeCatalog.ModeDark, Theme = ThemeCatalog.NewschoolId });

			Assert.Equal(1, ThemeDictionaries());
			Assert.Equal(Color.FromRgb(0x17, 0x19, 0x1C), ((SolidColorBrush)Application.Current.FindResource("ThemeSurfaceAlt")).Color);
		});
	}

	[Fact]
	public void TheCustomColours_AreAnOverrideBehindTheTheme_AndGoWhenTheyAreRemoved()
	{
		_context.RunOnSta(() =>
		{
			var service = new ThemeService(() => true);
			var theme = ThemeCatalog.Find(ThemeCatalog.NewschoolId);
			var configuration = new AppConfiguration { ThemeColors = new() { [ThemeCatalog.NewschoolId] = new() { ["ThemeAccent"] = "#112233" } } };

			service.Apply(configuration);
			Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ((SolidColorBrush)Application.Current.FindResource("ThemeAccent")).Color);
			Assert.Equal(1, ThemeDictionaries());

			configuration.ThemeColors.Clear();
			service.Apply(configuration);
			Assert.Equal(ThemeCatalog.ToColor(theme.Colors["ThemeAccent"]), ((SolidColorBrush)Application.Current.FindResource("ThemeAccent")).Color);
		});
	}

	[Fact]
	public void Apply_UpdatesWhatUsesTheBrushesThroughDynamicResource()
	{
		_context.RunOnSta(() =>
		{
			var border = new System.Windows.Controls.Border();
			border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ThemeSurface");
			var window = new Window { Content = border };

			new ThemeService(() => true).Apply(new AppConfiguration { ThemeColors = new() { [ThemeCatalog.NewschoolId] = new() { ["ThemeSurface"] = "#102030" } } });

			Assert.Equal(Color.FromRgb(0x10, 0x20, 0x30), ((SolidColorBrush)border.Background).Color);
			window.Close();
		});
	}
}
