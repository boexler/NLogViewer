using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Sentinel.LogViewer.Models;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The order of the tabs of the log lists
/// </summary>
public enum TabOrder
{
	/// <summary>In the order the tabs were opened</summary>
	ByTime,

	/// <summary>By the address of the sender, then by the name of the application, then by time</summary>
	ByIpAddress,

	/// <summary>Where the user put them; a new tab goes to the end</summary>
	ByHand
}

/// <summary>
/// How the colour of a tab of an application is shown
/// </summary>
public enum TabLook
{
	/// <summary>The light shade as the background of the tab and a round dot in the dark shade; the default, so it is the first</summary>
	Tinted,

	/// <summary>A stripe of 4 px over the whole tab, in the dark shade</summary>
	Stripe
}

/// <summary>
/// Works out in which order the tabs stand
/// </summary>
public static class TabOrdering
{
	public static TabOrder Parse(string? text, TabOrder fallback = TabOrder.ByTime) =>
		Enum.TryParse<TabOrder>(text, ignoreCase: true, out var order) && Enum.IsDefined(order) ? order : fallback;

	public static TabLook ParseLook(string? text, TabLook fallback = TabLook.Tinted) =>
		Enum.TryParse<TabLook>(text, ignoreCase: true, out var look) && Enum.IsDefined(look) ? look : fallback;

	/// <summary>
	/// The tabs in the order they should stand: the pinned tabs first in the order their applications were pinned, then the others in the given
	/// order. Pinned tabs of one application, and the tabs by hand, keep the order they have, so what the user moved stays and a new tab
	/// (which is at the end) stays behind them.
	/// </summary>
	public static IReadOnlyList<LogTabViewModel> Arrange(IReadOnlyList<LogTabViewModel> tabs, TabOrder order)
	{
		var pinned = tabs.Where(tab => tab.IsPinned).OrderBy(tab => tab.PinOrder ?? int.MaxValue);
		var others = tabs.Where(tab => !tab.IsPinned);
		others = order switch
		{
			TabOrder.ByIpAddress => others
				.OrderBy(tab => AddressGroup(tab.Sender))
				.ThenBy(tab => AddressNumber(tab.Sender))
				.ThenBy(tab => AddressGroup(tab.Sender) == 1 ? tab.Sender : string.Empty, StringComparer.OrdinalIgnoreCase)
				.ThenBy(tab => tab.ApplicationName, StringComparer.OrdinalIgnoreCase)
				.ThenBy(tab => tab.Sequence),
			TabOrder.ByTime => others.OrderBy(tab => tab.Sequence),
			_ => others
		};

		return pinned.Concat(others).ToList();
	}

	/// <summary>0 for an IPv4 address, 1 for an IPv6 address, 2 for no address; so the addresses of the first kind come first and a tab without one last</summary>
	private static int AddressGroup(string sender) =>
		IPAddress.TryParse(sender, out var address) ? address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1 : 2;

	/// <summary>The IPv4 address as a number, so that 192.168.0.9 comes before 192.168.0.10</summary>
	private static uint AddressNumber(string sender) =>
		IPAddress.TryParse(sender, out var address) && address.AddressFamily == AddressFamily.InterNetwork
			? (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(address.GetAddressBytes(), 0))
			: 0;
}
