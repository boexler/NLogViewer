using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

public class TabColorsTests
{
	[Theory]
	[InlineData("myapp;10.0.0.5", "#3FB0A0")]
	[InlineData("MyApp;10.0.0.5", "#3FB0A0")]
	[InlineData("orders;10.0.0.7", "#B07CD8")]
	public void TheColour_IsFixed_ForAKey_AndDoesNotDependOnTheCase(string key, string expected) =>
		Assert.Equal(expected, TabColors.HexFor(key));

	[Fact]
	public void TheColours_ComeFromThePalette_AndAreSpreadOverIt()
	{
		var colours = Enumerable.Range(0, 200).Select(i => TabColors.HexFor($"app{i};10.0.0.{i % 250}")).ToList();

		Assert.All(colours, colour => Assert.Contains(colour, TabColors.Palette));
		Assert.True(colours.Distinct().Count() >= 9);
	}

	[Fact]
	public void OnlyTheTabOfAnApplicationHasAColour()
	{
		Assert.Null(new LogTabViewModel().AccentBrush);

		var live = new LogTabViewModel { SenderKey = "myapp;10.0.0.5" };

		Assert.Equal("#FF3FB0A0", live.AccentBrush!.ToString());
	}
}
