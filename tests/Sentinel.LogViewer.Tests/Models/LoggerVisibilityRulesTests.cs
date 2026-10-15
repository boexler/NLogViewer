using System.Linq;
using Sentinel.LogViewer.Wpf.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Models;

/// <summary>
/// Tests for <see cref="LoggerVisibilityRules"/>: the most specific rule wins and the rules can be saved as text
/// </summary>
public class LoggerVisibilityRulesTests
{
	[Fact]
	public void WithoutRules_EveryLoggerIsVisible()
	{
		var rules = new LoggerVisibilityRules();

		Assert.True(rules.IsVisible("Company.Product"));
		Assert.True(rules.IsVisible(string.Empty));
		Assert.False(rules.HasRules);
	}

	[Fact]
	public void Hiding_AppliesToTheLoggerAndEverythingBelowIt_ButNotToSimilarNames()
	{
		var rules = new LoggerVisibilityRules();

		rules.SetVisible("Company.Product", false);

		Assert.False(rules.IsVisible("Company.Product"));
		Assert.False(rules.IsVisible("Company.Product.Service"));
		Assert.False(rules.IsVisible("Company.Product.Service.Deep"));
		Assert.True(rules.IsVisible("Company.ProductExtra"));
		Assert.True(rules.IsVisible("Company"));
		Assert.True(rules.IsVisible("Other"));
	}

	[Fact]
	public void ShowingAChildOfAHiddenLogger_IsAnExceptionToTheHidingRule()
	{
		var rules = new LoggerVisibilityRules();
		rules.SetVisible("Company", false);

		rules.SetVisible("Company.Product.Service", true);

		Assert.True(rules.IsVisible("Company.Product.Service"));
		Assert.True(rules.IsVisible("Company.Product.Service.Deep"));
		Assert.False(rules.IsVisible("Company.Product"));
		Assert.False(rules.IsVisible("Company.Other"));
		Assert.Equal(new[] { "-Company", "+Company.Product.Service" }, rules.ToList());
	}

	[Fact]
	public void Showing_AHiddenLogger_DropsTheRulesBelowIt()
	{
		var rules = new LoggerVisibilityRules();
		rules.SetVisible("Company", false);
		rules.SetVisible("Company.Product.Service", true);

		rules.SetVisible("Company", true);

		Assert.False(rules.HasRules);
		Assert.True(rules.IsVisible("Company.Other"));
	}

	[Fact]
	public void SettingTheStateAThatLoggerAlreadyHas_AddsNoRule()
	{
		var rules = new LoggerVisibilityRules();
		var changes = 0;
		rules.Changed += (_, _) => changes++;

		rules.SetVisible("Company", true);

		Assert.False(rules.HasRules);
		Assert.Equal(0, changes);
	}

	[Fact]
	public void ShowOnly_HidesEverythingElse_EvenLoggersThatAppearLater()
	{
		var rules = new LoggerVisibilityRules();
		rules.SetVisible("Company.Other", false);

		rules.ShowOnly("Company.Product");

		Assert.True(rules.IsVisible("Company.Product"));
		Assert.True(rules.IsVisible("Company.Product.Service"));
		Assert.False(rules.IsVisible("Company.Other"));
		Assert.False(rules.IsVisible("Brand.New.Logger"));
		Assert.False(rules.IsVisible(string.Empty));
		Assert.Equal(new[] { "-", "+Company.Product" }, rules.ToList());
	}

	[Fact]
	public void Clear_ShowsEverythingAndRaisesChangedOnlyWhenThereWasARule()
	{
		var rules = new LoggerVisibilityRules();
		var changes = 0;
		rules.Changed += (_, _) => changes++;
		rules.Clear();
		Assert.Equal(0, changes);

		rules.SetVisible("A", false);
		rules.Clear();

		Assert.Equal(2, changes);
		Assert.True(rules.IsVisible("A"));
	}

	[Fact]
	public void ToList_AndLoad_RoundTrip_AlsoForLoggersThatDidNotLogYet()
	{
		var rules = new LoggerVisibilityRules();
		rules.SetVisible("Company", false);
		rules.SetVisible("Company.Product", true);
		var saved = rules.ToList();

		var restored = new LoggerVisibilityRules();
		restored.Load(saved);

		Assert.Equal(saved, restored.ToList());
		Assert.True(restored.IsVisible("Company.Product.NeverSeen"));
		Assert.False(restored.IsVisible("Company.Elsewhere"));
	}

	[Fact]
	public void Load_IgnoresWhatIsNotARule_AndRaisesChangedOnlyForADifferentSet()
	{
		var rules = new LoggerVisibilityRules();
		var changes = 0;
		rules.Changed += (_, _) => changes++;

		rules.Load(new[] { "-A", "nonsense", string.Empty, "*B", "+C.D" });
		rules.Load(new[] { "+C.D", "-A" });

		Assert.Equal(new[] { "+C.D", "-A" }.OrderBy(rule => rule.TrimStart('+', '-'), System.StringComparer.Ordinal), rules.ToList());
		Assert.Equal(1, changes);
	}
}
