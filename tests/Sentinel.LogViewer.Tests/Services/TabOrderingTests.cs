using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

public class TabOrderingTests
{
	private static LogTabViewModel Tab(string name, string sender, int? pinOrder = null) => new()
	{
		SenderKey = sender.Length == 0 ? null : $"{name};{sender}".ToLowerInvariant(),
		ApplicationName = name,
		Sender = sender,
		Header = name,
		PinOrder = pinOrder
	};

	private static string[] Names(IEnumerable<LogTabViewModel> tabs) => tabs.Select(tab => tab.Header).ToArray();

	[Fact]
	public void ByTime_IsTheOrderTheTabsWereMade()
	{
		var a = Tab("a", "10.0.0.9");
		var b = Tab("b", "10.0.0.1");
		var c = Tab("c", "10.0.0.5");

		Assert.Equal(new[] { "a", "b", "c" }, Names(TabOrdering.Arrange(new[] { c, a, b }, TabOrder.ByTime)));
	}

	[Fact]
	public void ByIpAddress_SortsTheNumbers_NotTheText_ThenTheApplication_ThenTheTime()
	{
		var tabs = new[]
		{
			Tab("late", "192.168.0.10"),
			Tab("nine", "192.168.0.9"),
			Tab("beta", "10.0.0.5"),
			Tab("alpha", "10.0.0.5"),
			Tab("same-first", "172.16.0.1"),
			Tab("same-second", "172.16.0.1")
		};

		// The names of the first two differ, the last two have the same address: the application and then the time decide.
		tabs[4].ApplicationName = tabs[5].ApplicationName = "shared";

		Assert.Equal(new[] { "alpha", "beta", "same-first", "same-second", "nine", "late" }, Names(TabOrdering.Arrange(tabs, TabOrder.ByIpAddress)));
	}

	[Fact]
	public void ByIpAddress_PutsIPv6AfterIPv4_AsText_AndTabsWithoutAnAddressLast()
	{
		var tabs = new[]
		{
			Tab("file", ""),
			Tab("v6b", "fe80::2"),
			Tab("v4", "10.0.0.1"),
			Tab("v6a", "2001:db8::1")
		};

		Assert.Equal(new[] { "v4", "v6a", "v6b", "file" }, Names(TabOrdering.Arrange(tabs, TabOrder.ByIpAddress)));
	}

	[Fact]
	public void ByHand_KeepsTheOrder_AndANewTabStaysAtTheEnd()
	{
		var tabs = new[] { Tab("z", "10.0.0.9"), Tab("a", "10.0.0.1"), Tab("new", "10.0.0.5") };

		Assert.Equal(new[] { "z", "a", "new" }, Names(TabOrdering.Arrange(tabs, TabOrder.ByHand)));
	}

	[Theory]
	[InlineData(TabOrder.ByTime)]
	[InlineData(TabOrder.ByIpAddress)]
	[InlineData(TabOrder.ByHand)]
	public void PinnedTabsAlwaysStandFirst_InTheOrderTheyWerePinned(TabOrder order)
	{
		var tabs = new[]
		{
			Tab("plain", "10.0.0.1"),
			Tab("second", "10.0.0.9", pinOrder: 1),
			Tab("first", "10.0.0.200", pinOrder: 0)
		};

		Assert.Equal(new[] { "first", "second", "plain" }, Names(TabOrdering.Arrange(tabs, order)));
	}

	[Fact]
	public void PinnedTabsOfOneApplication_KeepTheOrderTheyHave()
	{
		var second = Tab("beta", "10.0.0.9", pinOrder: 0);
		var first = Tab("beta", "10.0.0.9", pinOrder: 0);
		var other = Tab("alpha", "10.0.0.1", pinOrder: 1);

		// The user put the later tab first; sorting again does not undo that.
		Assert.Equal(new[] { second, first, other }, TabOrdering.Arrange(new[] { second, first, other }, TabOrder.ByTime));
	}

	[Fact]
	public void TheOrderAndTheLook_ComeFromTheText_AndAreNeverInvalid()
	{
		Assert.Equal(TabOrder.ByIpAddress, TabOrdering.Parse("byipaddress"));
		Assert.Equal(TabOrder.ByTime, TabOrdering.Parse("nonsense"));
		Assert.Equal(TabOrder.ByTime, TabOrdering.Parse("42"));
		Assert.Equal(TabLook.Tinted, TabOrdering.ParseLook("Tinted"));
		Assert.Equal(TabLook.Tinted, TabOrdering.ParseLook(null));
		Assert.Equal(TabLook.Stripe, TabOrdering.ParseLook("stripe"));
	}
}
