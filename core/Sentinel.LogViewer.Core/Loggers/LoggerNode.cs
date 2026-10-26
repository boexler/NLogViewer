using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// One logger in the logger tree, e.g. <c>Service</c> in <c>Company.Product.Service</c>, with the counters of its entries and of everything below it
/// </summary>
public sealed class LoggerNode : INotifyPropertyChanged
{
	private bool _isExpanded = true;
	private bool _isListed = true;
	private bool _isSelected;

	internal LoggerNode(LoggerTree tree, LoggerNode? parent, string name, string fullName)
	{
		Tree = tree;
		Parent = parent;
		Name = name;
		FullName = fullName;
	}

	/// <summary>
	/// The tree that this logger belongs to; the commands of the context menu are found there
	/// </summary>
	public LoggerTree Tree { get; }

	public LoggerNode? Parent { get; }

	/// <summary>
	/// The last part of the logger name
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The whole logger name, which is also the path of the rules
	/// </summary>
	public string FullName { get; }

	/// <summary>
	/// The loggers directly below, sorted by name
	/// </summary>
	public ObservableCollection<LoggerNode> Children { get; } = new();

	/// <summary>
	/// Number of entries of exactly this logger
	/// </summary>
	public int OwnCount { get; internal set; }

	/// <summary>
	/// Number of entries of this logger and of everything below it
	/// </summary>
	public int TotalCount { get; internal set; }

	/// <summary>
	/// Number of warnings of this logger and of everything below it
	/// </summary>
	public int WarnCount { get; internal set; }

	/// <summary>
	/// Number of errors and fatal errors of this logger and of everything below it
	/// </summary>
	public int ErrorCount { get; internal set; }

	/// <summary>
	/// Whether this logger has had entries of its own, in contrast to a part of a name that only leads to loggers below
	/// </summary>
	internal bool HasHadEntries { get; set; }

	/// <summary>
	/// Whether the entries of this logger and everything below it are shown: true for all, false for none, null for some.
	/// Setting it shows or hides all of them.
	/// </summary>
	public bool? IsChecked
	{
		get => Tree.GetCheckState(this);
		set => Tree.Rules.SetVisible(FullName, value == true);
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			if (_isExpanded == value)
				return;

			_isExpanded = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Whether the logger is the selected one in the tree
	/// </summary>
	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (_isSelected == value)
				return;

			_isSelected = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Whether the logger is listed while the tree is searched: when it matches, or something below it does
	/// </summary>
	public bool IsListed
	{
		get => _isListed;
		internal set
		{
			if (_isListed == value)
				return;

			_isListed = value;
			OnPropertyChanged();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	internal void NotifyCountsChanged()
	{
		OnPropertyChanged(nameof(TotalCount));
		OnPropertyChanged(nameof(WarnCount));
		OnPropertyChanged(nameof(ErrorCount));
	}

	internal void NotifyCheckStateChanged() => OnPropertyChanged(nameof(IsChecked));

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
