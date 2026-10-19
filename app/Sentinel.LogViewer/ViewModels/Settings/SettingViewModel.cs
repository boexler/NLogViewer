using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// One setting of the settings window, shown as a card with title, description and a control
/// </summary>
public abstract class SettingViewModel : INotifyPropertyChanged
{
	protected SettingViewModel(string categoryId, string icon, string title, string description, bool needsRestart)
	{
		CategoryId = categoryId;
		Icon = icon;
		Title = title;
		Description = description;
		NeedsRestart = needsRestart;
	}

	public string CategoryId { get; }

	/// <summary>
	/// Glyph of the Segoe MDL2 Assets font
	/// </summary>
	public string Icon { get; }

	public string Title { get; }

	public string Description { get; }

	/// <summary>
	/// Heading above the card when a category groups its settings; settings of the same section follow each other
	/// </summary>
	public string? Section { get; init; }

	/// <summary>
	/// The group of settings that this one is shown with in one card; settings of the same group follow each other
	/// </summary>
	public SettingGroup? Group { get; init; }

	/// <summary>
	/// Whether a change only takes full effect after restarting the application
	/// </summary>
	public bool NeedsRestart { get; }

	/// <summary>
	/// Returns whether the title or the description contains the text
	/// </summary>
	public bool Matches(string text) =>
		Title.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
		Description.Contains(text, StringComparison.CurrentCultureIgnoreCase);

	/// <summary>
	/// Sets the setting back to the value of a fresh installation and saves it
	/// </summary>
	public abstract void ResetToDefault();

	/// <summary>
	/// Reads the value from the configuration again, e.g. after an import
	/// </summary>
	public abstract void Reload();

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

/// <summary>
/// A setting with a single value that is saved as soon as it changes
/// </summary>
public abstract class ValueSettingViewModel<T> : SettingViewModel
{
	private readonly Func<T> _get;
	private readonly Action<T> _set;
	private readonly Func<T> _getDefault;
	private readonly Action? _reset;

	/// <param name="set">Stores and saves the value</param>
	/// <param name="reset">Sets the setting back to its default when that is more than assigning the default value</param>
	protected ValueSettingViewModel(string categoryId, string icon, string title, string description, bool needsRestart,
		Func<T> get, Action<T> set, Func<T> getDefault, Action? reset)
		: base(categoryId, icon, title, description, needsRestart)
	{
		_get = get;
		_set = set;
		_getDefault = getDefault;
		_reset = reset;
	}

	public T Value
	{
		get => _get();
		set
		{
			if (EqualityComparer<T>.Default.Equals(value, _get()))
				return;

			_set(value);
			OnPropertyChanged();
		}
	}

	public override void ResetToDefault()
	{
		if (_reset != null)
			_reset();
		else
			_set(_getDefault());

		OnPropertyChanged(nameof(Value));
	}

	public override void Reload() => OnPropertyChanged(nameof(Value));
}

/// <summary>
/// An on / off setting
/// </summary>
public sealed class BoolSettingViewModel(string categoryId, string icon, string title, string description,
	Func<bool> get, Action<bool> set, Func<bool> getDefault, Action? reset = null)
	: ValueSettingViewModel<bool>(categoryId, icon, title, description, false, get, set, getDefault, reset);

/// <summary>
/// One entry of a <see cref="ChoiceSettingViewModel"/>
/// </summary>
public sealed record ChoiceOption(string Value, string Display);

/// <summary>
/// A setting with a fixed list of values
/// </summary>
public sealed class ChoiceSettingViewModel(string categoryId, string icon, string title, string description, bool needsRestart,
	IReadOnlyList<ChoiceOption> options, Func<string> get, Action<string> set, Func<string> getDefault, Action? reset = null)
	: ValueSettingViewModel<string>(categoryId, icon, title, description, needsRestart, get, set, getDefault, reset)
{
	public IReadOnlyList<ChoiceOption> Options { get; } = options;
}
