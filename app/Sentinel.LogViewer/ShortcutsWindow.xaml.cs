using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer;

/// <summary>
/// The keyboard and mouse commands of the application (Help &gt; Keyboard shortcuts, F1)
/// </summary>
public partial class ShortcutsWindow : Window
{
	public ShortcutsWindow(ShortcutsViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
	}

	/// <summary>
	/// Opens the window on top of the owner
	/// </summary>
	public static void Show(Window? owner)
	{
		var provider = App.ServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
		var window = new ShortcutsWindow(new ShortcutsViewModel(provider.GetRequiredService<LocalizationService>()));
		if (owner is { IsVisible: true })
			window.Owner = owner;

		window.ShowDialog();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);

		if (e.Key is Key.Escape or Key.F1)
		{
			Close();
			e.Handled = true;
		}
	}
}
