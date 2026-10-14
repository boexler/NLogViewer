using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer;

/// <summary>
/// About dialog with version information and the update check
/// </summary>
public partial class AboutWindow : Window
{
	private readonly AboutViewModel _viewModel;

	public AboutWindow(AboutViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		InitializeComponent();
		DataContext = _viewModel;

		// Like Firefox, opening the dialog always checks for updates.
		Loaded += async (_, _) => await _viewModel.CheckAsync();
	}

	public bool? ShowDialog(Window owner)
	{
		Owner = owner;
		return ShowDialog();
	}

	private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
		e.Handled = true;
	}
}
