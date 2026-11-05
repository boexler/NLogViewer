using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Core.Sessions;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// Sessions: all tabs with their entries, filters and bookmarks in one file, and the tabs that are kept when the application closes
/// </summary>
public partial class MainViewModel
{
	private const int MaxRecentSessions = 10;

	private ICommand? _saveSessionCommand;
	private ICommand? _openSessionCommand;
	private ICommand? _openRecentSessionCommand;
	private bool _restoringSession;
	private bool _tabsKept;

	/// <summary>The session files that were saved or opened last, the newest first</summary>
	public ObservableCollection<string> RecentSessions { get; } = new();

	public bool HasRecentSessions => RecentSessions.Count > 0;

	/// <summary>Saves all tabs in a file that the user chooses</summary>
	public ICommand SaveSessionCommand => _saveSessionCommand ??= new RelayCommand(() => _ = SaveSessionFromUiAsync(), () => HasContentToKeep() && !IsLoading);

	/// <summary>Opens a session file that the user chooses; its tabs are added to the tabs that are open</summary>
	public ICommand OpenSessionCommand => _openSessionCommand ??= new RelayCommand(OpenSessionFromUi, () => !IsLoading);

	/// <summary>Opens the session file that is the parameter (an entry of the recent sessions)</summary>
	public ICommand OpenRecentSessionCommand => _openRecentSessionCommand ??= new RelayCommand<string>(path => _ = OpenSessionAsync(path!), path => !IsLoading && !string.IsNullOrEmpty(path));

	/// <summary>The file in which the tabs wait for the next start when the setting "Keep tabs across restarts" is on</summary>
	private string KeptTabsPath => Path.Combine(_configService.DataDirectory, "tabs" + SessionSerializer.Extension);

	private void LoadRecentSessions()
	{
		RecentSessions.Clear();
		foreach (var path in _configService.LoadConfiguration().RecentSessions ?? new List<string>())
			RecentSessions.Add(path);

		OnPropertyChanged(nameof(HasRecentSessions));
	}

	private void RememberSession(string path)
	{
		var configuration = _configService.LoadConfiguration();
		configuration.RecentSessions = (configuration.RecentSessions ?? new List<string>())
			.Where(other => !string.Equals(other, path, StringComparison.OrdinalIgnoreCase))
			.Prepend(path)
			.Take(MaxRecentSessions)
			.ToList();
		_configService.SaveConfiguration(configuration);
		LoadRecentSessions();
	}

	private string Text(string key, string english) => _localizationService.GetString(key, english);

	private string Text(string key, string english, params object[] arguments) =>
		string.Format(CultureInfo.CurrentCulture, _localizationService.GetString(key, english), arguments);

	// A tab without entries and without bookmarks holds nothing that would be lost.
	private bool HasContentToKeep() => LogTabs.Any(tab => tab.LogEntries.Count > 0 || tab.Bookmarks.Count > 0);

	private async Task SaveSessionFromUiAsync()
	{
		var dialog = new SaveFileDialog
		{
			Filter = Text("Session_Filter", "Sentinel.LogViewer sessions (*.slvsession)|*.slvsession|All files (*.*)|*.*"),
			Title = Text("Session_SaveTitle", "Save session"),
			DefaultExt = SessionSerializer.Extension,
			FileName = "session" + SessionSerializer.Extension
		};

		if (dialog.ShowDialog() == true)
			await SaveSessionAsync(dialog.FileName);
	}

	/// <summary>
	/// Writes all tabs to a file in the background and remembers the file as a recent session
	/// </summary>
	internal async Task SaveSessionAsync(string path)
	{
		var data = SessionTabs.Capture(LogTabs.ToList(), SelectedTab);
		IsLoading = true;
		LoadingProgress = Text("Session_Saving", "Saving session...");
		try
		{
			await Task.Run(() => SessionSerializer.Save(path, data)).ConfigureAwait(true);
			RememberSession(path);
			Snackbar.Show(Text("Session_Saved", "Session saved to {0}", Path.GetFileName(path)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Snackbar.ShowError(Text("Session_SaveError", "The session could not be saved: {0}", ex.Message));
		}
		finally
		{
			IsLoading = false;
			LoadingProgress = string.Empty;
		}
	}

	private void OpenSessionFromUi()
	{
		var dialog = new OpenFileDialog
		{
			Filter = Text("Session_Filter", "Sentinel.LogViewer sessions (*.slvsession)|*.slvsession|All files (*.*)|*.*"),
			Title = Text("Session_OpenTitle", "Open session")
		};

		if (dialog.ShowDialog() == true)
			_ = OpenSessionAsync(dialog.FileName);
	}

	/// <summary>
	/// Reads a session file in the background and adds its tabs to the tabs that are open
	/// </summary>
	/// <returns>Whether the session was opened</returns>
	internal async Task<bool> OpenSessionAsync(string path, bool remember = true)
	{
		if (IsLoading)
			return false;

		IsLoading = true;
		LoadingProgress = Text("Session_Opening", "Opening session...");
		try
		{
			var (tabs, selected) = await Task.Run(() =>
			{
				var data = SessionSerializer.Load(path);
				return (data.Tabs.Select(SessionTabs.ToTab).ToList(), data.SelectedTab);
			}).ConfigureAwait(true);

			AddSessionTabs(tabs, selected);
			if (remember)
				RememberSession(path);

			Snackbar.Show(Text("Session_Opened", "Session {0} opened: {1} tabs", Path.GetFileName(path), tabs.Count));
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			Snackbar.ShowError(Text("Session_OpenError", "The session could not be opened: {0}", ex.Message));
			return false;
		}
		finally
		{
			IsLoading = false;
			LoadingProgress = string.Empty;
		}
	}

	/// <summary>
	/// Puts the tabs of a session among the open ones. A tab of an application takes its place in the application's tabs: it keeps the key, so
	/// the application that starts again is handled by the rules for a restart.
	/// </summary>
	private void AddSessionTabs(IReadOnlyList<LogTabViewModel> tabs, int selected)
	{
		_restoringSession = true;
		try
		{
			foreach (var tab in tabs)
			{
				var columns = tab.ColumnKeys;
				var searchFields = tab.FilterState?.SearchFields ?? tab.SearchFields;
				if (tab.SenderKey is { } key)
				{
					tab.Look = _tabLook;
					tab.PinOrder = _applicationTabs.PinOrderOf(key);
					if (tab.ColourIndex < 0)
						tab.ColourIndex = TabColors.Choose(key, LogTabs.Where(open => open.SenderKey != null).Select(open => (open.SenderKey!, open.ColourIndex)));

					tab.PropertyChanged += OnLiveTabPropertyChanged;
				}

				AddTab(tab);
				// A new tab starts with the view settings of the application; the tab of a session has its own.
				tab.ColumnKeys = columns;
				tab.SearchFields = searchFields;
			}
		}
		finally
		{
			_restoringSession = false;
		}

		if (tabs.Count > 0)
			SelectedTab = selected >= 0 && selected < tabs.Count ? tabs[selected] : tabs[^1];
	}

	/// <summary>
	/// Called when the window closes: with the setting "Keep tabs across restarts" on, the tabs with entries or bookmarks are saved for the next start
	/// </summary>
	public void SaveTabsOnExit()
	{
		if (_tabsKept)
			return;

		var keep = _configService.LoadConfiguration().KeepTabsAcrossRestarts;
		try
		{
			if (!keep || !HasContentToKeep())
			{
				File.Delete(KeptTabsPath);
				return;
			}

			SaveKeptTabs();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// shortcut: the tabs are lost when the file cannot be written at exit, as the window is already closing; upgrade to a message when it comes up
			System.Diagnostics.Debug.WriteLine(ex);
		}
	}

	private void SaveKeptTabs()
	{
		var withContent = LogTabs.Where(tab => tab.LogEntries.Count > 0 || tab.Bookmarks.Count > 0).ToList();
		SessionSerializer.Save(KeptTabsPath, SessionTabs.Capture(withContent, SelectedTab));
		_tabsKept = true;
	}

	/// <summary>
	/// Before the application restarts to update: with the setting on the tabs are kept without asking, otherwise the user may save a session
	/// </summary>
	/// <returns>False when the user took back the restart</returns>
	public bool SaveTabsBeforeUpdate()
	{
		if (!HasContentToKeep())
			return true;

		try
		{
			if (_configService.LoadConfiguration().KeepTabsAcrossRestarts)
			{
				SaveKeptTabs();
				return true;
			}

			var answer = Ask(Text("Session_UpdateAsk", "The application restarts to update. Save the open tabs as a session first?"),
				Text("Session_UpdateCaption", "Restart to update"), MessageBoxButton.YesNoCancel);
			if (answer == MessageBoxResult.Cancel)
				return false;

			if (answer == MessageBoxResult.Yes)
			{
				var dialog = new SaveFileDialog
				{
					Filter = Text("Session_Filter", "Sentinel.LogViewer sessions (*.slvsession)|*.slvsession|All files (*.*)|*.*"),
					Title = Text("Session_SaveTitle", "Save session"),
					DefaultExt = SessionSerializer.Extension,
					FileName = "session" + SessionSerializer.Extension
				};
				if (dialog.ShowDialog() != true)
					return false;

				SessionSerializer.Save(dialog.FileName, SessionTabs.Capture(LogTabs.ToList(), SelectedTab));
				RememberSession(dialog.FileName);
			}

			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Snackbar.ShowError(Text("Session_SaveError", "The session could not be saved: {0}", ex.Message));
			return false;
		}
	}

	/// <summary>
	/// Called when the window is up: when the tabs of the last run were kept, asks whether to restore them
	/// </summary>
	public async Task OfferToRestoreTabsAsync()
	{
		var path = KeptTabsPath;
		if (!_configService.LoadConfiguration().KeepTabsAcrossRestarts || !File.Exists(path))
			return;

		var answer = Ask(Text("Session_RestoreAsk", "The tabs of the last run were kept. Restore them?"),
			Text("Session_RestoreCaption", "Restore tabs"), MessageBoxButton.YesNo);
		if (answer == MessageBoxResult.Yes && !await OpenSessionAsync(path, remember: false).ConfigureAwait(true))
			return;

		// The tabs are in the window now, or the user does not want them back; the next close writes them again.
		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{
			// shortcut: a file that cannot be deleted is offered again at the next start; it is replaced when the application closes
		}
	}
}
