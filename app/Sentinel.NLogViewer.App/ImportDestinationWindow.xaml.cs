using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.NLogViewer.App.Models;
using Sentinel.NLogViewer.App.Services;

namespace Sentinel.NLogViewer.App;

/// <summary>
/// Dialog for choosing how imported log files are routed to tabs.
/// </summary>
public partial class ImportDestinationWindow : Window
{
	private readonly IList<LogTabViewModel> _tabs;
	private readonly bool _showExistingTabOption;
	private readonly bool _showPerFileNewTabOption;
	private readonly LogTabViewModel? _preferredExistingTab;

	/// <summary>
	/// The user's choice when <see cref="Window.DialogResult"/> is true.
	/// </summary>
	public LogFileImportBatchContext.DestinationMode SelectedMode { get; private set; }

	/// <summary>
	/// When <see cref="SelectedMode"/> is <see cref="LogFileImportBatchContext.DestinationMode.ExistingTab"/>, the tab to append to.
	/// </summary>
	public LogTabViewModel? SelectedExistingTab { get; private set; }

	/// <summary>
	/// Creates the import destination picker.
	/// </summary>
	/// <param name="tabs">Open tabs used for "existing tab" mode.</param>
	/// <param name="preferredExistingTab">Tab to pre-select when enabling existing-tab mode.</param>
	/// <param name="showExistingTabOption">When false (no open tabs yet), hides existing-tab UI.</param>
	/// <param name="showPerFileNewTabOption">When false (single file only), hides the "new tab per file" choice.</param>
	public ImportDestinationWindow(IList<LogTabViewModel> tabs, LogTabViewModel? preferredExistingTab,
		bool showExistingTabOption, bool showPerFileNewTabOption = true)
	{
		if (tabs == null)
			throw new ArgumentNullException(nameof(tabs));

		_tabs = tabs;
		_showExistingTabOption = showExistingTabOption;
		_showPerFileNewTabOption = showPerFileNewTabOption;
		_preferredExistingTab = preferredExistingTab;

		InitializeComponent();

		TabsListBox.ItemsSource = _tabs;
		UpdateExistingTabAvailability();
		UpdatePerFileNewTabOptionVisibility();
		ApplyInitialSelection();
		UpdateOkEnabled();

		KeyDown += (_, e) =>
		{
			if (e.Key == System.Windows.Input.Key.Escape)
				DialogResult = false;
		};
	}

	private void ApplyInitialSelection()
	{
		if (_showExistingTabOption && _tabs.Count > 0)
		{
			var fallback = _tabs.First();
			TabsListBox.SelectedItem =
				_preferredExistingTab != null && _tabs.Contains(_preferredExistingTab)
					? _preferredExistingTab
					: fallback;
			ExistingTabRadio.IsChecked = true;
			TabsListBox.IsEnabled = true;
			SingleNewTabRadio.IsChecked = false;
			PerFileNewTabRadio.IsChecked = false;
		}
		else
		{
			SingleNewTabRadio.IsChecked = true;
			ExistingTabRadio.IsChecked = false;
			PerFileNewTabRadio.IsChecked = false;
			TabsListBox.IsEnabled = false;
			if (_tabs.Count > 0 && TabsListBox.SelectedItem == null)
				TabsListBox.SelectedItem = _tabs.First();
		}
	}

	private void UpdateExistingTabAvailability()
	{
		if (!_showExistingTabOption)
		{
			ExistingTabRadio.Visibility = Visibility.Collapsed;
			TabsListBox.Visibility = Visibility.Collapsed;
			return;
		}

		ExistingTabRadio.Visibility = Visibility.Visible;
		TabsListBox.Visibility = Visibility.Visible;
		TabsListBox.IsEnabled = ExistingTabRadio.IsChecked == true;
	}

	/// <summary>
	/// Hides "new tab per file" when only one file is being imported (option is redundant).
	/// </summary>
	private void UpdatePerFileNewTabOptionVisibility()
	{
		PerFileNewTabRadio.Visibility = _showPerFileNewTabOption ? Visibility.Visible : Visibility.Collapsed;
	}

	private void ModeRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (TabsListBox == null || ExistingTabRadio == null || OkButton == null)
			return;

		TabsListBox.IsEnabled = ExistingTabRadio.IsChecked == true;
		if (!TabsListBox.IsEnabled && TabsListBox.Items.Count > 0 && TabsListBox.SelectedItem == null)
			TabsListBox.SelectedIndex = 0;

		UpdateOkEnabled();
	}

	private void TabsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateOkEnabled();
	}

	private void UpdateOkEnabled()
	{
		var existingMode = ExistingTabRadio.IsChecked == true;
		OkButton.IsEnabled = !existingMode || TabsListBox.SelectedItem is LogTabViewModel;
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		if (ExistingTabRadio.IsChecked == true)
		{
			if (TabsListBox.SelectedItem is not LogTabViewModel tab)
			{
				MessageBox.Show(
					this,
					GetMessage("ImportDestination_SelectTab", "Please select a tab."));
				return;
			}

			SelectedMode = LogFileImportBatchContext.DestinationMode.ExistingTab;
			SelectedExistingTab = tab;
		}
		else if (_showPerFileNewTabOption && PerFileNewTabRadio.IsChecked == true)
		{
			SelectedMode = LogFileImportBatchContext.DestinationMode.PerFileNewTab;
			SelectedExistingTab = null;
		}
		else
		{
			SelectedMode = LogFileImportBatchContext.DestinationMode.SingleNewTab;
			SelectedExistingTab = null;
		}

		DialogResult = true;
	}

	private static string GetMessage(string key, string fallback)
	{
		try
		{
			var loc = App.ServiceProvider?.GetService<LocalizationService>();
			return loc != null ? loc.GetString(key, fallback) : fallback;
		}
		catch
		{
			return fallback;
		}
	}
}
