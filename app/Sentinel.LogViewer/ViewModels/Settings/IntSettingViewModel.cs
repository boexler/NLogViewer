using System;
using System.Globalization;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// A whole number within limits, edited as text. An invalid input is not saved and shows an error instead.
/// </summary>
public class IntSettingViewModel : SettingViewModel
{
	private readonly Func<int> _get;
	private readonly Action<int> _set;
	private readonly Func<int> _getDefault;
	private readonly Func<int, bool> _isValid;
	private readonly string _invalidMessage;
	private string? _error;

	/// <param name="set">Stores and saves the value</param>
	/// <param name="isValid">Whether a number is allowed</param>
	/// <param name="invalidMessage">Shown when the input is not a whole number or not allowed</param>
	public IntSettingViewModel(string categoryId, string icon, string title, string description,
		Func<int> get, Action<int> set, Func<int> getDefault, Func<int, bool> isValid, string invalidMessage)
		: base(categoryId, icon, title, description)
	{
		_get = get;
		_set = set;
		_getDefault = getDefault;
		_isValid = isValid;
		_invalidMessage = invalidMessage;
	}

	public string Text
	{
		get => _get().ToString("N0", CultureInfo.CurrentCulture);
		set
		{
			if (!int.TryParse(value?.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var number) ||
			    !_isValid(number))
			{
				// The text box keeps what was typed, so nothing is raised for the text.
				Error = _invalidMessage;
				return;
			}

			Error = null;
			if (number != _get())
				_set(number);

			OnPropertyChanged();
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

	public override void ResetToDefault()
	{
		_set(_getDefault());
		Reload();
	}

	public override void Reload()
	{
		Error = null;
		OnPropertyChanged(nameof(Text));
	}
}
