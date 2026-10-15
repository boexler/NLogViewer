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
				var brush = Assert.IsType<SolidColorBrush>(Application.Current.Resources[token.Key]);
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

			Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ((SolidColorBrush)Application.Current.Resources["ThemeAccent"]).Color);
			Assert.Equal(Color.FromArgb(0x80, 0xFF, 0, 0), ((SolidColorBrush)Application.Current.Resources["LogTraceBackground"]).Color);
			Assert.Equal(Color.FromRgb(0xD0, 0xD0, 0xD0), ((SolidColorBrush)Application.Current.Resources["ThemeBorder"]).Color);
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
