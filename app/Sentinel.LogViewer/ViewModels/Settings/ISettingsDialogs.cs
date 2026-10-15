namespace Sentinel.LogViewer.ViewModels.Settings;

/// <summary>
/// Dialogs the settings window needs, so the view model does not depend on WPF windows
/// </summary>
public interface ISettingsDialogs
{
	/// <summary>
	/// Asks a yes / no question
	/// </summary>
	bool Confirm(string caption, string message);

	void ShowError(string caption, string message);

	/// <returns>The chosen file, or null when cancelled</returns>
	string? PickFileToExport(string title, string filter, string defaultFileName);

	/// <returns>The chosen file, or null when cancelled</returns>
	string? PickFileToImport(string title, string filter);
}
