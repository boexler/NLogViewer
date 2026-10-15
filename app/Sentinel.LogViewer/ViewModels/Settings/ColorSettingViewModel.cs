using System;
using System.Windows.Input;
using System.Windows.Media;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// A colour of the current theme, edited as text like #1B1B1B. An invalid input is not saved and shows an error instead.
/// </summary>
public sealed class ColorSettingViewModel : SettingViewModel
{
	private readonly Func<string> _getColor;
	private readonly Func<bool> _isCustom;
	private readonly Action<string?> _setCustom;
	private readonly string _invalidMessage;
	private string? _error;

	/// <param name="getColor">The colour that is in effect: the custom colour, otherwise the one of the theme</param>
	/// <param name="isCustom">Whether the user changed the colour of the theme</param>
	/// <param name="setCustom">Stores and saves a custom colour; null goes back to the colour of the theme</param>
	/// <param name="invalidMessage">Shown when the input is not a colour</param>
	public ColorSettingViewModel(string categoryId, string icon, string title, string description,
		Func<string> getColor, Func<bool> isCustom, Action<string?> setCustom, string invalidMessage)
		: base(categoryId, icon, title, description, false)
	{
		_getColor = getColor;
		_isCustom = isCustom;
		_setCustom = setCustom;
		_invalidMessage = invalidMessage;
		ResetCommand = new RelayCommand(ResetToDefault);
	}

	public string Text
	{
		get => _getColor();
		set
		{
			if (!ThemeCatalog.TryNormalize(value, out var color))
			{
				// The text box keeps what was typed, so nothing is raised for the text.
				Error = _invalidMessage;
				return;
			}

			Error = null;
			if (color != _getColor())
				_setCustom(color);

			Reload();
		}
	}

	/// <summary>
	/// The colour as a sample next to the text
	/// </summary>
	public Brush Swatch
	{
		get
		{
			var brush = new SolidColorBrush(ThemeCatalog.ToColor(_getColor()));
			brush.Freeze();
			return brush;
		}
	}

	public bool IsCustom => _isCustom();

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

	/// <summary>
	/// Goes back to the colour of the theme
	/// </summary>
	public ICommand ResetCommand { get; }

	public override void ResetToDefault()
	{
		_setCustom(null);
		Reload();
	}

	public override void Reload()
	{
		Error = null;
		OnPropertyChanged(nameof(Text));
		OnPropertyChanged(nameof(Swatch));
		OnPropertyChanged(nameof(IsCustom));
	}
}
