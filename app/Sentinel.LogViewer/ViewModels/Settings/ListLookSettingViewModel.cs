using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>One row of the preview; how it looks follows the settings (symbol and text of the level, symbol size, padding)</summary>
public sealed record FontPreviewRow(string Kind, string Id, string Level, string TimeStamp, string Logger, string Message, string? Icon,
	bool ShowSymbol, bool ShowText, double IconHeight, Thickness Padding);

/// <summary>
/// How the log list looks: how the level is shown, how high the rows are, the font and its size, in one card with a preview of a few rows
/// that follows every change
/// </summary>
public sealed class ListLookSettingViewModel : SettingViewModel
{
	private const string IconFolder = "pack://application:,,,/Sentinel.LogViewer.Wpf;component/Images/Small/";

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
	private string? _error;

	/// <param name="reset">Sets all of the settings of the card back to their defaults and saves them</param>
	public ListLookSettingViewModel(string categoryId, string icon, string title, string description,
		IReadOnlyList<ChoiceOption> levelOptions, IReadOnlyList<ChoiceOption> rowHeightOptions, IReadOnlyList<ChoiceOption> fontOptions,
		Func<string> getLevel, Action<string> setLevel, Func<string> getRowHeight, Action<string> setRowHeight,
		Func<string> getFamily, Action<string> setFamily, Func<int> getSize, Action<int> setSize, string invalidSizeMessage, Action reset)
		: base(categoryId, icon, title, description, false)
	{
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
	}

	public IReadOnlyList<ChoiceOption> LevelOptions { get; }

	public IReadOnlyList<ChoiceOption> RowHeightOptions { get; }

	public IReadOnlyList<ChoiceOption> FontOptions { get; }

	/// <summary>The sizes that the drop-down offers; any whole number in the limits can be typed as well</summary>
	public static IReadOnlyList<string> SizeOptions { get; } = new[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32" };

	public string Level
	{
		get => _getLevel();
		set
		{
			if (value == null || value == _getLevel())
				return;

			_setLevel(value);
			Reload();
		}
	}

	public string RowHeight
	{
		get => _getRowHeight();
		set
		{
			if (value == null || value == _getRowHeight())
				return;

			_setRowHeight(value);
			Reload();
		}
	}

	public string Family
	{
		get => _getFamily();
		set
		{
			if (value == null || value == _getFamily())
				return;

			_setFamily(value);
			Reload();
		}
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
			{
				_setSize(size);
				Reload();
			}
			else
			{
				OnPropertyChanged();
			}
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
		}
	}

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

			FontPreviewRow Row(string kind, string id, string level, string time, string logger, string message, string? icon) =>
				new(kind, id, level, time, logger, message, icon, showSymbol, showText, iconHeight, padding);

			return new[]
			{
				new FontPreviewRow("Header", "ID", "Level", "TimeStamp", "LoggerName", "Message", null, false, true, iconHeight, padding),
				Row("Info", "1", "Info", "2025-12-18 15:11:48", "Acme.Orders", "Order 4711 created", IconFolder + "Info.png"),
				Row("Warn", "2", "Warn", "2025-12-18 15:11:49", "Acme.Billing", "Retry 2 of 3", IconFolder + "Warning.png"),
				Row("Error", "3", "Error", "2025-12-18 15:11:50", "Acme.Billing", "Payment failed: timeout", IconFolder + "Error.png")
			};
		}
	}

	public override void ResetToDefault()
	{
		_reset();
		Error = null;
		Reload();
	}

	/// <summary>Shows the settings and the preview as they are now</summary>
	public override void Reload() => OnPropertyChanged(string.Empty);
}
