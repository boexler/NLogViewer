using System;
using System.Collections.Generic;
using System.Linq;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// Which loggers are shown and which are hidden. A rule names a logger path like <c>Company.Product</c> and applies to that logger
/// and everything below it; the most specific rule wins and a logger without a rule is shown. The empty path stands for all loggers.
/// </summary>
/// <remarks>
/// Hiding a logger and then showing one of its children gives two rules, one that hides and one that shows. The rules are kept as text
/// (<c>-Company.Product</c>, <c>+Company.Product.Service</c>) so that they can be saved with a filter preset or a session, also
/// for loggers that did not log anything yet.
/// </remarks>
public sealed class LoggerVisibilityRules
{
	private readonly Dictionary<string, bool> _rules = new(StringComparer.Ordinal);
	private readonly Dictionary<string, bool> _cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Raised when the rules changed
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>
	/// Whether any logger is hidden or shown by a rule
	/// </summary>
	public bool HasRules => _rules.Count > 0;

	/// <summary>
	/// How many rules there are
	/// </summary>
	public int Count => _rules.Count;

	/// <summary>
	/// Returns whether the entries of the logger are shown
	/// </summary>
	public bool IsVisible(string loggerName)
	{
		if (_rules.Count == 0)
			return true;

		if (_cache.TryGetValue(loggerName, out var cached))
			return cached;

		var visible = Resolve(loggerName);
		_cache[loggerName] = visible;
		return visible;
	}

	/// <summary>
	/// Shows or hides a logger and everything below it; rules that were set for loggers below it are dropped
	/// </summary>
	public void SetVisible(string path, bool visible)
	{
		var changed = RemoveRulesBelow(path);
		if (Resolve(path) != visible)
		{
			_rules[path] = visible;
			changed = true;
		}

		if (changed)
			OnChanged();
	}

	/// <summary>
	/// Hides everything except the logger and everything below it
	/// </summary>
	public void ShowOnly(string path)
	{
		_rules.Clear();
		_rules[string.Empty] = false;
		_rules[path] = true;
		OnChanged();
	}

	/// <summary>
	/// Shows all loggers again
	/// </summary>
	public void Clear()
	{
		if (_rules.Count == 0)
			return;

		_rules.Clear();
		OnChanged();
	}

	/// <summary>
	/// The rules as text: <c>-path</c> hides, <c>+path</c> shows, in an order that does not change
	/// </summary>
	public IReadOnlyList<string> ToList() =>
		_rules.OrderBy(rule => rule.Key, StringComparer.Ordinal).Select(rule => (rule.Value ? "+" : "-") + rule.Key).ToList();

	/// <summary>
	/// Replaces the rules with saved ones; entries that are not a rule are ignored
	/// </summary>
	public void Load(IEnumerable<string>? rules)
	{
		var loaded = new Dictionary<string, bool>(StringComparer.Ordinal);
		foreach (var rule in rules ?? Enumerable.Empty<string>())
		{
			if (rule is { Length: > 0 } && rule[0] is '+' or '-')
				loaded[rule[1..]] = rule[0] == '+';
		}

		if (loaded.Count == _rules.Count && loaded.All(rule => _rules.TryGetValue(rule.Key, out var visible) && visible == rule.Value))
			return;

		_rules.Clear();
		foreach (var rule in loaded)
			_rules[rule.Key] = rule.Value;

		OnChanged();
	}

	private bool Resolve(string loggerName)
	{
		var path = loggerName;
		while (true)
		{
			if (_rules.TryGetValue(path, out var visible))
				return visible;

			if (path.Length == 0)
				return true;

			var dot = path.LastIndexOf('.');
			path = dot < 0 ? string.Empty : path[..dot];
		}
	}

	private bool RemoveRulesBelow(string path)
	{
		var prefix = path + ".";
		var below = _rules.Keys.Where(key => key == path || path.Length == 0 || key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
		foreach (var key in below)
			_rules.Remove(key);

		return below.Count > 0;
	}

	private void OnChanged()
	{
		_cache.Clear();
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
