using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.LogViewer.Helpers;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer;

/// <summary>
/// The changelog: the versions on the left, the release notes of the selected one on the right
/// </summary>
public partial class ChangelogWindow : Window
{
	private readonly ChangelogViewModel _viewModel;
	private readonly bool _selectSince;

	/// <param name="selectSince">Whether the entry for everything since the previous version is selected first</param>
	public ChangelogWindow(ChangelogViewModel viewModel, bool selectSince = false)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		_selectSince = selectSince;
		InitializeComponent();
		DataContext = _viewModel;
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		ShowNotes();

		Loaded += async (_, _) => await _viewModel.LoadAsync(_selectSince);
	}

	/// <summary>
	/// Opens the changelog on top of the owner
	/// </summary>
	/// <param name="previous">The version that was installed before; the versions after it are marked and gathered in one entry</param>
	public static void Show(Window? owner, ReleaseVersion? previous = null)
	{
		var provider = App.ServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
		var viewModel = new ChangelogViewModel(
			provider.GetRequiredService<ChangelogService>(),
			provider.GetRequiredService<ConfigurationService>(),
			provider.GetRequiredService<LocalizationService>(),
			UpdateService.CurrentVersion,
			previous);
		var window = new ChangelogWindow(viewModel, selectSince: previous != null);
		if (owner is { IsVisible: true })
			window.Owner = owner;

		window.ShowDialog();
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ChangelogViewModel.NotesMarkdown))
			ShowNotes();
	}

	private void ShowNotes()
	{
		NotesViewer.Document = MarkdownDocumentBuilder.Build(_viewModel.NotesMarkdown);
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);

		if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
		{
			SearchBox.Focus();
			SearchBox.SelectAll();
			e.Handled = true;
		}
		else if (e.Key == Key.Escape && !e.Handled)
		{
			Close();
		}
	}
}
