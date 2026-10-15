using System.Collections.Generic;

namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// A category in the navigation of the settings window
/// </summary>
public sealed class SettingsCategoryViewModel(string id, string icon, string title, string subtitle, IReadOnlyList<SettingViewModel> settings)
{
	public string Id { get; } = id;

	/// <summary>
	/// Glyph of the Segoe MDL2 Assets font
	/// </summary>
	public string Icon { get; } = icon;

	public string Title { get; } = title;

	public string Subtitle { get; } = subtitle;

	public IReadOnlyList<SettingViewModel> Settings { get; } = settings;
}

/// <summary>
/// Title of the page at the top of the settings list
/// </summary>
public sealed record SettingsPageHeader(string Title, string Subtitle);

/// <summary>
/// Name of a category above its settings in the search results
/// </summary>
public sealed record SettingsSectionLabel(string Title);
