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
/// The saved filter presets, to rename and to remove. They are made from a tab, so there is no way to add one here. Every change is saved right away.
/// </summary>
public sealed class PresetListSettingViewModel : SettingViewModel
{
	private readonly Func<List<FilterPreset>> _get;
	private readonly Action _save;

	/// <param name="get">The presets of the settings; they are changed in place</param>
	/// <param name="save">Saves the settings</param>
	public PresetListSettingViewModel(string categoryId, string icon, string title, string description,
		Func<List<FilterPreset>> get, Action save, string emptyNameMessage, string nameInUseMessage)
		: base(categoryId, icon, title, description, false)
	{
		_get = get;
		_save = save;
		EmptyNameMessage = emptyNameMessage;
		NameInUseMessage = nameInUseMessage;
		Load();
	}

	public ObservableCollection<PresetItemViewModel> Items { get; } = new();

	/// <summary>
	/// Whether there is no preset, which the page says
	/// </summary>
	public bool IsEmpty => Items.Count == 0;

	internal string EmptyNameMessage { get; }

	internal string NameInUseMessage { get; }

	/// <summary>
	/// The presets are the user's own work and not a setting with a default; the page has no reset, and this keeps them
	/// </summary>
	public override void ResetToDefault()
	{
	}

	public override void Reload() => Load();

	internal bool Rename(FilterPreset preset, string name) => FilterPresetService.TryRename(_get(), preset, name) && Saved();

	internal void Remove(PresetItemViewModel item)
	{
		_get().Remove(item.Preset);
		Items.Remove(item);
		_save();
		OnPropertyChanged(nameof(IsEmpty));
	}

	private bool Saved()
	{
		_save();
		return true;
	}

	private void Load()
	{
		Items.Clear();
		foreach (var preset in _get())
			Items.Add(new PresetItemViewModel(this, preset));

		OnPropertyChanged(nameof(IsEmpty));
	}
}

/// <summary>
/// One preset in the list
/// </summary>
public sealed class PresetItemViewModel : INotifyPropertyChanged
{
	private readonly PresetListSettingViewModel _owner;
	private string? _error;

	internal PresetItemViewModel(PresetListSettingViewModel owner, FilterPreset preset)
	{
		_owner = owner;
		Preset = preset;
		RemoveCommand = new RelayCommand(() => _owner.Remove(this));
	}

	internal FilterPreset Preset { get; }

	/// <summary>
	/// The name of the preset. An empty name, or the name of another preset, is not saved and shows an error instead.
	/// </summary>
	public string Name
	{
		get => Preset.Name;
		set
		{
			var name = FilterPresetService.CleanName(value);
			if (name == Preset.Name)
			{
				Error = null;
				OnPropertyChanged();
				return;
			}

			if (_owner.Rename(Preset, name))
			{
				Error = null;
				OnPropertyChanged();
				return;
			}

			// The text box keeps what was typed, so nothing is raised for the name.
			Error = name.Length == 0 ? _owner.EmptyNameMessage : _owner.NameInUseMessage;
		}
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

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
