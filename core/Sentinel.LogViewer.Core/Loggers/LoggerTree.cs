using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// The loggers of a log viewer as a tree, split at the dots of their names, with the number of entries of each, and the rules
/// that show or hide loggers
/// </summary>
public sealed class LoggerTree : INotifyPropertyChanged
{
	private readonly Dictionary<string, LoggerNode> _nodes = new(StringComparer.Ordinal);
	private readonly HashSet<LoggerNode> _changed = new();
	private readonly Action<Action>? _schedule;
	private bool _flushScheduled;
	private string _searchText = string.Empty;

	/// <param name="schedule">Runs the update of the counters later, so that a burst of entries does not update the list for each of them;
	/// when not given the counters are updated right away</param>
	public LoggerTree(Action<Action>? schedule = null)
	{
		_schedule = schedule;
		Rules.Changed += (_, _) =>
		{
			foreach (var node in _nodes.Values)
				node.NotifyCheckStateChanged();

			RulesChanged?.Invoke(this, EventArgs.Empty);
		};

		ShowOnlyCommand = new DelegateCommand<LoggerNode>(node =>
		{
			if (node != null)
				Rules.ShowOnly(node.FullName);
		});
		ShowAllCommand = new DelegateCommand(Rules.Clear);
		ExpandAllCommand = new DelegateCommand(() => SetExpanded(true));
		CollapseAllCommand = new DelegateCommand(() => SetExpanded(false));
	}

	/// <summary>
	/// The loggers that have no dot above them, sorted by name
	/// </summary>
	public ObservableCollection<LoggerNode> Roots { get; } = new();

	/// <summary>
	/// Which loggers are shown
	/// </summary>
	public LoggerVisibilityRules Rules { get; } = new();

	/// <summary>
	/// Raised when loggers were shown or hidden
	/// </summary>
	public event EventHandler? RulesChanged;

	/// <summary>
	/// Hides everything except one logger and everything below it; the parameter is a <see cref="LoggerNode"/>
	/// </summary>
	public ICommand ShowOnlyCommand { get; }

	public ICommand ShowAllCommand { get; }

	public ICommand ExpandAllCommand { get; }

	public ICommand CollapseAllCommand { get; }

	/// <summary>
	/// Text that the loggers are searched for (case-insensitive); the tree lists the loggers that contain it, and the ones above them
	/// </summary>
	public string SearchText
	{
		get => _searchText;
		set
		{
			value ??= string.Empty;
			if (_searchText == value)
				return;

			_searchText = value;
			OnPropertyChanged();
			ApplySearch();
		}
	}

	/// <summary>
	/// Returns whether the entries of the logger are shown. Entries without a logger name are not part of the tree and are always shown,
	/// unless everything is hidden.
	/// </summary>
	public bool IsVisible(string? loggerName) => Rules.IsVisible(loggerName ?? string.Empty);

	/// <summary>
	/// Counts an entry
	/// </summary>
	public void Add(LogEntry entry)
	{
		Count(entry, 1);
		Flush();
	}

	/// <summary>
	/// Takes an entry out of the counters again, e.g. when the oldest entries are removed
	/// </summary>
	public void Remove(LogEntry entry)
	{
		Count(entry, -1);
		Flush();
	}

	/// <summary>
	/// Counts all entries anew
	/// </summary>
	public void Rebuild(IEnumerable<LogEntry> entries)
	{
		Roots.Clear();
		_nodes.Clear();
		_changed.Clear();
		foreach (var entry in entries)
			Count(entry, 1);

		Flush();
	}

	/// <summary>
	/// Returns whether the entries of the logger and everything below it are shown: true for all, false for none, null for some
	/// </summary>
	internal bool? GetCheckState(LoggerNode node)
	{
		if (!Rules.HasRules)
			return true;

		var visible = 0;
		var hidden = 0;
		CountVisibility(node, ref visible, ref hidden);
		if (visible + hidden == 0)
			return Rules.IsVisible(node.FullName);

		return hidden == 0 ? true : visible == 0 ? false : null;
	}

	private void CountVisibility(LoggerNode node, ref int visible, ref int hidden)
	{
		if (node.HasHadEntries)
		{
			if (Rules.IsVisible(node.FullName))
				visible++;
			else
				hidden++;
		}

		foreach (var child in node.Children)
			CountVisibility(child, ref visible, ref hidden);
	}

	private void Count(LogEntry entry, int change)
	{
		var name = entry.LoggerName;
		if (string.IsNullOrEmpty(name))
			return;

		LoggerNode? node;
		if (change > 0)
			node = GetOrCreate(name);
		else if (!_nodes.TryGetValue(name, out node))
			return;

		if (change > 0)
			node.HasHadEntries = true;

		node.OwnCount += change;
		var warn = entry.Level == LogEntryLevel.Warn ? change : 0;
		var error = entry.Level >= LogEntryLevel.Error ? change : 0;
		for (var current = node; current != null; current = current.Parent)
		{
			current.TotalCount += change;
			current.WarnCount += warn;
			current.ErrorCount += error;
			_changed.Add(current);
		}
	}

	private LoggerNode GetOrCreate(string fullName)
	{
		if (_nodes.TryGetValue(fullName, out var existing))
			return existing;

		var dot = fullName.LastIndexOf('.');
		var parent = dot < 0 ? null : GetOrCreate(fullName[..dot]);
		var node = new LoggerNode(this, parent, dot < 0 ? fullName : fullName[(dot + 1)..], fullName);
		node.IsListed = Matches(node);
		Insert(parent?.Children ?? Roots, node);
		_nodes[fullName] = node;
		if (parent != null && node.IsListed)
			SetListed(parent);

		return node;
	}

	private static void Insert(ObservableCollection<LoggerNode> siblings, LoggerNode node)
	{
		var index = 0;
		while (index < siblings.Count && Compare(siblings[index], node) < 0)
			index++;

		siblings.Insert(index, node);
	}

	private static int Compare(LoggerNode left, LoggerNode right)
	{
		var result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
		return result != 0 ? result : string.CompareOrdinal(left.Name, right.Name);
	}

	/// <summary>
	/// Tells the list about the changed counters, now or later
	/// </summary>
	private void Flush()
	{
		if (_schedule == null)
		{
			RaiseChangedCounters();
			return;
		}

		if (_flushScheduled)
			return;

		_flushScheduled = true;
		_schedule(() =>
		{
			_flushScheduled = false;
			RaiseChangedCounters();
		});
	}

	private void RaiseChangedCounters()
	{
		foreach (var node in _changed)
			node.NotifyCountsChanged();

		_changed.Clear();
	}

	private void SetExpanded(bool expanded)
	{
		foreach (var node in _nodes.Values)
			node.IsExpanded = expanded;
	}

	private bool Matches(LoggerNode node) =>
		_searchText.Length == 0 || node.FullName.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

	private void ApplySearch()
	{
		foreach (var root in Roots)
			ApplySearch(root);
	}

	/// <returns>Whether the logger is listed</returns>
	private bool ApplySearch(LoggerNode node)
	{
		var listed = Matches(node);
		foreach (var child in node.Children)
			listed |= ApplySearch(child);

		node.IsListed = listed;
		if (_searchText.Length > 0 && node.Children.Any(child => child.IsListed))
			node.IsExpanded = true;

		return listed;
	}

	/// <summary>
	/// Lists a logger and the ones above it
	/// </summary>
	private static void SetListed(LoggerNode node)
	{
		for (var current = node; current != null; current = current.Parent)
			current.IsListed = true;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
