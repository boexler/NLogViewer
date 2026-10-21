using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// A sender whose logs are dropped when they arrive: an IP address, an application name, or both, in which case both have to match.
/// It does not matter whether the logs come over UDP or TCP.
/// </summary>
public class IgnoreRule
{
	/// <summary>The IP address of the sender; empty for any address</summary>
	public string Ip { get; set; } = string.Empty;

	/// <summary>The name of the application; empty for any application</summary>
	public string Application { get; set; } = string.Empty;

	public bool IsEmpty => Ip.Length == 0 && Application.Length == 0;

	/// <summary>
	/// Whether the logs of this application from this sender are dropped; a rule with nothing in it matches nothing
	/// </summary>
	public bool Matches(string? application, string? sender) =>
		!IsEmpty
		&& (Ip.Length == 0 || string.Equals(Ip, sender, StringComparison.OrdinalIgnoreCase))
		&& (Application.Length == 0 || string.Equals(Application, application, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Whether the rule can be saved: it is not empty and its address, if there is one, is an IP address
	/// </summary>
	public bool IsValid => !IsEmpty && (Ip.Length == 0 || IPAddress.TryParse(Ip, out _));

	/// <summary>
	/// Drops what a hand-edited file could contain: empty rules, addresses that are none, and the same rule twice
	/// </summary>
	public static void Sanitize(AppConfiguration configuration)
	{
		var clean = new List<IgnoreRule>();
		foreach (var rule in configuration.IgnoreRules ?? new List<IgnoreRule>())
		{
			var copy = new IgnoreRule { Ip = rule?.Ip?.Trim() ?? string.Empty, Application = rule?.Application?.Trim() ?? string.Empty };
			if (copy.IsValid && !clean.Any(other => string.Equals(other.Ip, copy.Ip, StringComparison.OrdinalIgnoreCase)
			                                       && string.Equals(other.Application, copy.Application, StringComparison.OrdinalIgnoreCase)))
				clean.Add(copy);
		}

		configuration.IgnoreRules = clean;
	}
}
