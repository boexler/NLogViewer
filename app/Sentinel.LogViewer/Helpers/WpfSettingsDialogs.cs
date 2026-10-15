using System.Windows;
using Microsoft.Win32;
using Sentinel.LogViewer.ViewModels.Settings;

namespace Sentinel.LogViewer.Helpers;

/// <summary>
/// Shows the dialogs of the settings window on top of that window
/// </summary>
public sealed class WpfSettingsDialogs(Window owner) : ISettingsDialogs
{
	public bool Confirm(string caption, string message) =>
		MessageDialogWindow.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

	public void ShowError(string caption, string message) =>
		MessageDialogWindow.Show(owner, message, caption, MessageBoxButton.OK, MessageBoxImage.Error);

	public string? PickFileToExport(string title, string filter, string defaultFileName)
	{
		var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName, DefaultExt = "json" };
		return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
	}

	public string? PickFileToImport(string title, string filter)
	{
		var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
		return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
	}
}
