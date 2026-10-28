using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

public class TabColorsTests
{
	private static double Lightness(string hex)
	{
		var colour = (Color)ColorConverter.ConvertFromString(hex);
		return 0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B;
	}

	[Fact]
	public void ThereAre16Colours_EachWithADarkAndALightShade_AndNoTwoLookAlike()
	{
		var dark = Enumerable.Range(0, TabColors.Count).Select(TabColors.Dark).ToList();
		var light = Enumerable.Range(0, TabColors.Count).Select(TabColors.Light).ToList();

		Assert.Equal(16, TabColors.Count);
		Assert.Equal(TabColors.Count, dark.Distinct().Count());
		Assert.Equal(TabColors.Count, light.Distinct().Count());
		Assert.All(dark.Concat(light), hex => Assert.Matches("^#[0-9A-F]{6}$", hex));
		for (var i = 0; i < TabColors.Count; i++)
			Assert.True(Lightness(light[i]) > Lightness(dark[i]) + 60, $"colour {i}: the light shade is not clearly lighter");
	}

	[Fact]
	public void TheNumberOfAColour_WrapsAround()
	{
		Assert.Equal(TabColors.Dark(0), TabColors.Dark(TabColors.Count));
		Assert.Equal(TabColors.Light(15), TabColors.Light(-1));
	}

	[Fact]
	public void OnlyTheTabOfAnApplicationShowsAColour_InTheLookThatIsSet()
	{
		var file = new LogTabViewModel { ColourIndex = 3, Look = TabLook.Stripe };
		Assert.Null(file.StripeBrush);
		Assert.Null(file.DotBrush);

		var live = new LogTabViewModel { SenderKey = "myapp;10.0.0.5", ColourIndex = 3, Look = TabLook.Stripe };
		Assert.Equal(TabColors.Dark(3), ColourOf(live.StripeBrush!));
		Assert.Null(live.DotBrush);
		Assert.Null(live.TintBrush);

		live.Look = TabLook.Tinted;
		Assert.Null(live.StripeBrush);
		Assert.Equal(TabColors.Dark(3), ColourOf(live.DotBrush!));
		Assert.Equal(TabColors.Light(3), ColourOf(live.TintBrush!));
		Assert.NotEqual(ColourOf(live.TintBrush!), ColourOf(live.TintSelectedBrush!));
	}

	[Fact]
	public void ANewApplication_TakesTheFreeColourThatDiffersMostFromTheOpenTabs()
	{
		var open = new List<(string Key, int Colour)>();

		for (var i = 0; i < 5; i++)
			open.Add(($"app{i};x", TabColors.Choose($"app{i};x", open)));

		// The first one starts at 0; then always the colour that is farthest on the circle of hues from the ones that are shown.
		Assert.Equal(new[] { 0, 8, 4, 12, 2 }, open.Select(tab => tab.Colour));

		// The colour of the second tab is free again: it is the one that differs most from the others now.
		open.RemoveAt(1);
		Assert.Equal(8, TabColors.Choose("new;x", open));
	}

	[Fact]
	public void TheColoursOfTheFirst16Applications_AreAllDifferent()
	{
		var open = new List<(string Key, int Colour)>();

		for (var i = 0; i < TabColors.Count; i++)
			open.Add(($"app{i};x", TabColors.Choose($"app{i};x", open)));

		Assert.Equal(TabColors.Count, open.Select(tab => tab.Colour).Distinct().Count());
	}

	[Fact]
	public void AnApplicationThatHasATabOpen_KeepsItsColourInTheNextTab()
	{
		var open = new[] { ("a;x", 0), ("b;x", 1), ("c;x", 2) };

		Assert.Equal(1, TabColors.Choose("b;x", open));
	}

	[Fact]
	public void WhenAll16ColoursAreShown_ANewApplicationTakesOneThatIsShownLeast_AndFarthestFromTheCrowded()
	{
		var open = Enumerable.Range(0, TabColors.Count).Select(i => ($"app{i};x", i)).ToList();
		// Colour 5 is shown twice, so it is not taken; of the others the one opposite to it (13) differs most from the tabs that are shown.
		open.Add(("again;x", 5));

		Assert.Equal(13, TabColors.Choose("new;x", open));
	}

	private static string ColourOf(Brush brush)
	{
		var colour = ((SolidColorBrush)brush).Color;
		return $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
	}
}
