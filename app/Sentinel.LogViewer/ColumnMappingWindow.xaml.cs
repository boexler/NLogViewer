using System;
using System.Windows;
using Sentinel.LogViewer.ViewModels;

namespace Sentinel.LogViewer;

/// <summary>
/// Interaction logic for ColumnMappingWindow.xaml
/// </summary>
public partial class ColumnMappingWindow : Window
{
	private readonly ColumnMappingViewModel _viewModel;

	public ColumnMappingWindow(ColumnMappingViewModel viewModel)
	{
		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		InitializeComponent();
		DataContext = _viewModel;

		_viewModel.CancelCommand = new Sentinel.LogViewer.Wpf.RelayCommand(() => DialogResult = false);
		_viewModel.SaveCommand = new Sentinel.LogViewer.Wpf.RelayCommand(() =>
		{
			DialogResult = true;
			Close();
		});
	}

	/// <summary>
	/// Gets the final format configuration
	/// </summary>
	public Models.TextFileFormat FinalFormat => _viewModel.FinalFormat;

	/// <summary>
	/// Gets whether to save the format for the pattern
	/// </summary>
	public bool SaveForPattern => _viewModel.SaveForPattern;

	/// <summary>
	/// Gets the file pattern to save
	/// </summary>
	public string FilePattern => _viewModel.FilePattern;

	public bool? ShowDialog(Window owner)
	{
		Owner = owner;
		return ShowDialog();
	}
}

