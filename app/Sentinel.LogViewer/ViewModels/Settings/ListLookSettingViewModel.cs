using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>One row of the preview; how it looks follows the settings (symbol and text of the level, symbol size, padding)</summary>
public sealed record FontPreviewRow(string Kind, string Id, string Level, string TimeStamp, string Logger, string Message, ImageSource? Icon,
	bool ShowSymbol, bool ShowText, double IconHeight, Thickness Padding);

/// <summary>
/// How the log list looks: the colour mode, the theme, the format of the timestamp, how the level is shown, how high the rows are, the font and its
/// size, in one card with a preview of a few rows that follows every change. The last changes can be taken back.
/// </summary>
public sealed class ListLookSettingViewModel : SettingViewModel
{
	private const string IconFolder = "pack://application:,,,/Sentinel.LogViewer.Wpf;component/Images/Small/";

	private static readonly Dictionary<string, ImageSource> Icons = new();

	/// <summary>Everything that the card sets, as it was before a change</summary>
	private sealed record Look(string Level, string RowHeight, string Family, int Size, string Mode, string Theme, string TimeStamp);

	private readonly ChoiceSettingViewModel _mode;
	private readonly ChoiceSettingViewModel _theme;
	private readonly ChoiceSettingViewModel _timeStamp;
	private readonly Func<string> _getLevel;
	private readonly Action<string> _setLevel;
	private readonly Func<string> _getRowHeight;
	private readonly Action<string> _setRowHeight;
	private readonly Func<string> _getFamily;
	private readonly Action<string> _setFamily;
	private readonly Func<int> _getSize;
	private readonly Action<int> _setSize;
	private readonly string _invalidSizeMessage;
	private readonly Action _reset;
	private readonly Stack<Look> _undo = new();
	private string? _error;

	/// <param name="colourMode">The settings of the colour mode, the theme and the format of the timestamp are shown in this card instead of their own; they stay in
	/// the list of the settings, so that they are reset and reloaded with the others</param>
	/// <param name="reset">Sets the settings of the card that are not the three above back to their defaults and saves them</param>
	public ListLookSettingViewModel(string categoryId, string icon, string title, string description,
		ChoiceSettingViewModel colourMode, ChoiceSettingViewModel theme, ChoiceSettingViewModel timeStamp,
		IReadOnlyList<ChoiceOption> levelOptions, IReadOnlyList<ChoiceOption> rowHeightOptions, IReadOnlyList<ChoiceOption> fontOptions,
		Func<string> getLevel, Action<string> setLevel, Func<string> getRowHeight, Action<string> setRowHeight,
		Func<string> getFamily, Action<string> setFamily, Func<int> getSize, Action<int> setSize, string invalidSizeMessage, Action reset)
		: base(categoryId, icon, title, description, false)
	{
		_mode = colourMode;
		_theme = theme;
		_timeStamp = timeStamp;
		LevelOptions = levelOptions;
		RowHeightOptions = rowHeightOptions;
		FontOptions = fontOptions;
		_getLevel = getLevel;
		_setLevel = setLevel;
		_getRowHeight = getRowHeight;
		_setRowHeight = setRowHeight;
		_getFamily = getFamily;
		_setFamily = setFamily;
		_getSize = getSize;
		_setSize = setSize;
		_invalidSizeMessage = invalidSizeMessage;
		_reset = reset;
		UndoCommand = new Wpf.RelayCommand(Undo, () => CanUndo);

		// A change that is made elsewhere, e.g. a reset of the category, shows in the preview as well.
		foreach (var setting in new[] { _mode, _theme, _timeStamp })
			setting.PropertyChanged += (_, _) => Reload();
	}

	public IReadOnlyList<ChoiceOption> ColourModeOptions => _mode.Options;

	public IReadOnlyList<ChoiceOption> ThemeOptions => _theme.Options;

	public IReadOnlyList<ChoiceOption> TimeStampOptions => _timeStamp.Options;

	public IReadOnlyList<ChoiceOption> LevelOptions { get; }

	public IReadOnlyList<ChoiceOption> RowHeightOptions { get; }

	public IReadOnlyList<ChoiceOption> FontOptions { get; }

	/// <summary>The sizes that the drop-down offers; any whole number in the limits can be typed as well</summary>
	public static IReadOnlyList<string> SizeOptions { get; } = new[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32" };

	public string ColourMode
	{
		get => _mode.Value;
		set => Change(value, _mode.Value, () => _mode.Value = value);
	}

	public string Theme
	{
		get => _theme.Value;
		set => Change(value, _theme.Value, () => _theme.Value = value);
	}

	public string TimeStampFormat
	{
		get => _timeStamp.Value;
		set => Change(value, _timeStamp.Value, () => _timeStamp.Value = value);
	}

	public string Level
	{
		get => _getLevel();
		set => Change(value, _getLevel(), () => _setLevel(value));
	}

	public string RowHeight
	{
		get => _getRowHeight();
		set => Change(value, _getRowHeight(), () => _setRowHeight(value));
	}

	public string Family
	{
		get => _getFamily();
		set => Change(value, _getFamily(), () => _setFamily(value));
	}

	/// <summary>The size as text, which can be typed; an input that is no size within the limits is not saved and shows an error</summary>
	public string SizeText
	{
		get => _getSize().ToString(CultureInfo.CurrentCulture);
		set
		{
			if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var size) || !SettingsRules.IsValidListFontSize(size))
			{
				// The box keeps what was typed, so nothing is raised for the text.
				Error = _invalidSizeMessage;
				return;
			}

			Error = null;
			if (size != _getSize())
				Change(size, _getSize(), () => _setSize(size));
			else
				OnPropertyChanged();
		}
	}

	/// <summary>The entry of <see cref="SizeOptions"/> that is chosen, null when the size is not one of them</summary>
	public string? SelectedSize
	{
		get => SizeOptions.Contains(SizeText) ? SizeText : null;
		set
		{
			// The box picks the entry of the size again whenever it shows a size, which must not clear the error of an input that was refused.
			if (value != null && value != SizeText)
				SizeText = value;
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

	public FontFamily FontFamily => string.IsNullOrEmpty(Family) ? SystemFonts.MessageFontFamily : new FontFamily(Family);

	public double FontSize => _getSize();

	/// <summary>The rows of the preview, built from the settings as they are now</summary>
	public IReadOnlyList<FontPreviewRow> Rows
	{
		get
		{
			var mode = SettingsRules.ParseLevelDisplay(Level);
			var compact = SettingsRules.ParseRowHeight(RowHeight) == Wpf.RowHeightMode.Compact;
			var showSymbol = mode != Wpf.LevelDisplayMode.Text;
			var showText = mode != Wpf.LevelDisplayMode.Symbol;
			var iconHeight = compact ? 14 : 18;
			var padding = compact ? new Thickness(8, 0, 8, 0) : new Thickness(8, 3, 8, 3);
			var resolver = new TimeStampResolver(TimeStampFormats.Parse(TimeStampFormat));

			FontPreviewRow Row(string kind, string id, string level, int milliseconds, string logger, string message, string icon) =>
				new(kind, id, level, resolver.Resolve(new LogEntry(LogEntryLevel.Info, null, null) { TimeStamp = new DateTime(2025, 12, 18, 15, 11, 48 + int.Parse(id) - 1, milliseconds) }),
					logger, message, LoadIcon(icon), showSymbol, showText, iconHeight, padding);

			return new[]
			{
				new FontPreviewRow("Header", "ID", "Level", "TimeStamp", "LoggerName", "Message", null, false, true, iconHeight, padding),
				Row("Info", "1", "Info", 385, "Acme.Orders", "Order 4711 created", "Info.png"),
				Row("Warn", "2", "Warn", 412, "Acme.Billing", "Retry 2 of 3", "Warning.png"),
				Row("Error", "3", "Error", 507, "Acme.Billing", "Payment failed: timeout", "Error.png")
			};
		}
	}

	/// <summary>Takes back the last change of the card; again and again, back to the state when the window opened</summary>
	public ICommand UndoCommand { get; }

	public bool CanUndo => _undo.Count > 0;

	private static ImageSource LoadIcon(string file)
	{
		if (!Icons.TryGetValue(file, out var image))
		{
			var bitmap = new BitmapImage(new Uri(IconFolder + file));
			bitmap.Freeze();
			Icons[file] = image = bitmap;
		}

		return image;
	}

	private Look Snapshot() => new(_getLevel(), _getRowHeight(), _getFamily(), _getSize(), _mode.Value, _theme.Value, _timeStamp.Value);

	private void Remember()
	{
		_undo.Push(Snapshot());
		((Wpf.RelayCommand)UndoCommand).RaiseCanExecuteChanged();
	}

	/// <summary>Remembers the state, makes the change and shows it; nothing happens when the value is the same</summary>
	private void Change<T>(T value, T current, Action apply) where T : notnull
	{
		if (EqualityComparer<T>.Default.Equals(value, current))
			return;

		Remember();
		apply();
		Reload();
	}

	private void Undo()
	{
		if (_undo.Count == 0)
			return;

		var look = _undo.Pop();
		if (look.Level != _getLevel())
			_setLevel(look.Level);

		if (look.RowHeight != _getRowHeight())
			_setRowHeight(look.RowHeight);

		if (look.Family != _getFamily())
			_setFamily(look.Family);

		if (look.Size != _getSize())
			_setSize(look.Size);

		_mode.Value = look.Mode;
		_theme.Value = look.Theme;
		_timeStamp.Value = look.TimeStamp;
		Error = null;
		((Wpf.RelayCommand)UndoCommand).RaiseCanExecuteChanged();
		Reload();
	}

	public override void ResetToDefault()
	{
		Remember();
		_reset();
		Error = null;
		Reload();
	}

	/// <summary>Shows the settings and the preview as they are now</summary>
	public override void Reload() => OnPropertyChanged(string.Empty);
}
