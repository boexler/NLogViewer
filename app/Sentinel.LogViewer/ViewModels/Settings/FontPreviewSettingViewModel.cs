using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>One row of the preview: the kind decides its colours (<c>Header</c>, <c>Info</c>, <c>Warn</c> or <c>Error</c>)</summary>
public sealed record FontPreviewRow(string Kind, string Id, string Level, string TimeStamp, string Logger, string Message);

/// <summary>
/// A card that shows a few rows of the log list in the font family and size of the settings next to it, so the choice can be judged before
/// the list shows it
/// </summary>
public sealed class FontPreviewSettingViewModel(string categoryId, string icon, string title, string description, Func<string> getFamily, Func<int> getSize)
	: SettingViewModel(categoryId, icon, title, description, false)
{
	public FontFamily FontFamily => string.IsNullOrEmpty(getFamily()) ? SystemFonts.MessageFontFamily : new FontFamily(getFamily());

	public double FontSize => getSize();

	public IReadOnlyList<FontPreviewRow> Rows { get; } = new FontPreviewRow[]
	{
		new("Header", "ID", "Level", "TimeStamp", "LoggerName", "Message"),
		new("Info", "1", "Info", "2025-12-18 15:11:48", "Acme.Orders", "Order 4711 created"),
		new("Warn", "2", "Warn", "2025-12-18 15:11:49", "Acme.Billing", "Retry 2 of 3"),
		new("Error", "3", "Error", "2025-12-18 15:11:50", "Acme.Billing", "Payment failed: timeout")
	};

	public override void ResetToDefault() => Reload();

	/// <summary>Shows the font and size as they are now</summary>
	public override void Reload() => OnPropertyChanged(string.Empty);
}
