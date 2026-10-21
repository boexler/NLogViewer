using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// The senders whose logs are dropped. Every valid change is saved right away; a rule that is not valid yet stays in the list with its
/// error and is not saved.
/// </summary>
public sealed class IgnoreRuleListSettingViewModel : SettingViewModel
{
	private readonly Func<List<IgnoreRule>> _get;
	private readonly Action _save;

	/// <param name="get">The rules of the settings; they are changed in place</param>
	/// <param name="save">Saves the settings</param>
	public IgnoreRuleListSettingViewModel(string categoryId, string icon, string title, string description,
		Func<List<IgnoreRule>> get, Action save, string emptyMessage, string invalidIpMessage)
		: base(categoryId, icon, title, description, false)
	{
		_get = get;
		_save = save;
		EmptyMessage = emptyMessage;
		InvalidIpMessage = invalidIpMessage;
		AddCommand = new RelayCommand(() => Items.Add(new IgnoreRuleItemViewModel(this, new IgnoreRule())));
		Load();
	}

	public ObservableCollection<IgnoreRuleItemViewModel> Items { get; } = new();

	public ICommand AddCommand { get; }

	internal string EmptyMessage { get; }

	internal string InvalidIpMessage { get; }

	public override void ResetToDefault()
	{
		_get().Clear();
		_save();
		Load();
	}

	public override void Reload() => Load();

	internal void Remove(IgnoreRuleItemViewModel item)
	{
		Items.Remove(item);
		Save();
	}

	internal void Save()
	{
		var rules = _get();
		rules.Clear();
		rules.AddRange(Items.Select(item => item.Rule).Where(rule => rule.IsValid));
		_save();
	}

	private void Load()
	{
		Items.Clear();
		foreach (var rule in _get())
			Items.Add(new IgnoreRuleItemViewModel(this, rule));
	}
}

/// <summary>
/// One rule in the list
/// </summary>
public sealed class IgnoreRuleItemViewModel : INotifyPropertyChanged
{
	private readonly IgnoreRuleListSettingViewModel _owner;
	private string? _error;

	internal IgnoreRuleItemViewModel(IgnoreRuleListSettingViewModel owner, IgnoreRule rule)
	{
		_owner = owner;
		Rule = rule;
		RemoveCommand = new RelayCommand(() => _owner.Remove(this));
	}

	internal IgnoreRule Rule { get; }

	public string Ip
	{
		get => Rule.Ip;
		set => Change(() => Rule.Ip = value?.Trim() ?? string.Empty);
	}

	public string Application
	{
		get => Rule.Application;
		set => Change(() => Rule.Application = value?.Trim() ?? string.Empty);
	}

	public string? Error
	{
		get => _error;
		private set
		{
			if (_error == value)
				return;

			_error = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasError));
		}
	}

	public bool HasError => _error != null;

	public ICommand RemoveCommand { get; }

	private void Change(Action apply, [CallerMemberName] string? property = null)
	{
		apply();
		Error = Rule.IsEmpty ? _owner.EmptyMessage : Rule.IsValid ? null : _owner.InvalidIpMessage;
		OnPropertyChanged(property);
		_owner.Save();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
