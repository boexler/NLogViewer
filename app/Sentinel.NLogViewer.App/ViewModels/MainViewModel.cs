using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sentinel.NLogViewer.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NLog;
using Sentinel.NLogViewer.App.Models;
using Sentinel.NLogViewer.App.Services;
using Sentinel.NLogViewer.App;

namespace Sentinel.NLogViewer.App.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
	private readonly UdpLogReceiverService _udpReceiverService;
	private readonly LogFileParserService _fileParserService;
	private readonly ConfigurationService _configService;
	private readonly LocalizationService _localizationService;
	private bool _isListening;
	private string _listeningStatus;
	private string _statusMessage = "Ready";
	private string _lastLogTimestamp = string.Empty;
	private LogTabViewModel? _selectedTab;
	private string _currentLanguageFlag = "🇬🇧";
	private bool _isLoading;
	private string _loadingProgress = string.Empty;

	private readonly CompositeDisposable _subscriptions = new();


	public MainViewModel(
		UdpLogReceiverService udpReceiverService,
		LogFileParserService fileParserService,
		ConfigurationService configService,
		LocalizationService localizationService)
	{
		_udpReceiverService = udpReceiverService ?? throw new ArgumentNullException(nameof(udpReceiverService));
		_fileParserService = fileParserService ?? throw new ArgumentNullException(nameof(fileParserService));
		_configService = configService ?? throw new ArgumentNullException(nameof(configService));
		_localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

		LogTabs = new ObservableCollection<LogTabViewModel>();

		// Initialize commands
		StartListeningCommand = new AsyncRelayCommand(StartListeningAsync, () => !_isListening);
		StopListeningCommand = new RelayCommand(StopListening, () => _isListening);
		OpenFileCommand = new RelayCommand(OpenFile);
		OpenSettingsCommand = new RelayCommand(OpenSettings);
		ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
		AboutCommand = new RelayCommand(ShowAbout);
		ChangeLanguageCommand = new RelayCommand<string>(ChangeLanguage);
		ExportLogsCommand = new RelayCommand(ExportLogs, () => SelectedTab != null);

		// Initialize current language flag
		UpdateLanguageFlag();

		// Initialize listening status with translated value
		ListeningStatus = _localizationService.GetString("Status_Stopped", "Stopped");

		// Subscribe to log events
		// Buffer events for 250ms, filter empty batches, group by AppInfo.Id, and process each group as a batch
		_udpReceiverService.Log4JEventObservable
			.Buffer(TimeSpan.FromMilliseconds(250))
			.Where(list => list.Count > 0)
			.SelectMany(list => list.GroupBy(le => le.AppInfo.Id).Select(g => g.ToList()))
			.ObserveOn(new DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher))
			.Subscribe(OnLogEvent);
		//_fileParserService.LogParsed += OnLogReceived;

		// Load configuration
		LoadConfiguration();
	}

	/// <summary>
	/// Processes a batch of log events grouped by AppInfo.Id
	/// </summary>
	/// <param name="logEvents">List of log events for a specific AppInfo.Id</param>
	private void OnLogEvent(IList<LogEvent> logEvents)
	{
		// We're already on the Dispatcher thread, no need to invoke
		if (logEvents == null || logEvents.Count == 0)
			return;

		// All events in this batch have the same AppInfo.Id (they were grouped)
		var firstEvent = logEvents[0];
		var appInfoId = firstEvent.AppInfo.Id;
		
		// Find or create tab for this AppInfo
		var tab = LogTabs.FirstOrDefault(t => t.TargetName == appInfoId);
		if (tab == null)
		{
			tab = new LogTabViewModel
			{
				Header = firstEvent.AppInfo.ToString(),
				TargetName = appInfoId,
				MaxCount = _configService.LoadConfiguration().MaxLogEntriesPerTab
			};
			LogTabs.Add(tab);
			SelectedTab = tab;
		}
		
		// Add all log events from the batch to the tab (Cache replays to NLogViewer when it subscribes)
		foreach (var logEvent in logEvents)
		{
			tab.AddLogEvent(logEvent.LogEventInfo);
		}
		LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
		StatusMessage = $"Received {logEvents.Count} log(s) from {firstEvent.AppInfo.AppName.Name}";
	}

	public ObservableCollection<LogTabViewModel> LogTabs { get; }

	public LogTabViewModel? SelectedTab
	{
		get => _selectedTab;
		set
		{
			if (_selectedTab != value)
			{
				_selectedTab = value;
				OnPropertyChanged();
				// Update ExportLogsCommand CanExecute
				((RelayCommand)ExportLogsCommand).RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsListening
	{
		get => _isListening;
		private set
		{
			if (_isListening != value)
			{
				_isListening = value;
				OnPropertyChanged();
				((AsyncRelayCommand)StartListeningCommand).RaiseCanExecuteChanged();
				((RelayCommand)StopListeningCommand).RaiseCanExecuteChanged();
			}
		}
	}

	public string ListeningStatus
	{
		get => _listeningStatus;
		set
		{
			if (_listeningStatus != value)
			{
				_listeningStatus = value;
				OnPropertyChanged();
			}
		}
	}

	public string StatusMessage
	{
		get => _statusMessage;
		set
		{
			if (_statusMessage != value)
			{
				_statusMessage = value;
				OnPropertyChanged();
			}
		}
	}

	public string LastLogTimestamp
	{
		get => _lastLogTimestamp;
		set
		{
			if (_lastLogTimestamp != value)
			{
				_lastLogTimestamp = value;
				OnPropertyChanged();
			}
		}
	}

	public ICommand StartListeningCommand { get; }
	public ICommand StopListeningCommand { get; }
	public ICommand OpenFileCommand { get; }
	public ICommand OpenSettingsCommand { get; }
	public ICommand ExitCommand { get; }
	public ICommand AboutCommand { get; }
	public ICommand ChangeLanguageCommand { get; }
	public ICommand ExportLogsCommand { get; }

	/// <summary>
	/// Gets the flag emoji for the current language
	/// </summary>
	public string CurrentLanguageFlag
	{
		get => _currentLanguageFlag;
		private set
		{
			if (_currentLanguageFlag != value)
			{
				_currentLanguageFlag = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets the list of available languages with flags
	/// </summary>
	public Dictionary<string, string> AvailableLanguages => Services.LocalizationService.AvailableLanguages;

	/// <summary>
	/// Gets or sets whether the application is currently loading a file
	/// </summary>
	public bool IsLoading
	{
		get => _isLoading;
		private set
		{
			if (_isLoading != value)
			{
				_isLoading = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets the loading progress message
	/// </summary>
	public string LoadingProgress
	{
		get => _loadingProgress;
		private set
		{
			if (_loadingProgress != value)
			{
				_loadingProgress = value;
				OnPropertyChanged();
			}
		}
	}

	private void LoadConfiguration()
	{
		var config = _configService.LoadConfiguration();
            
		if (config.AutoStartListening)
		{
			Task.Run(async () =>
			{
				await Task.Delay(500); // Small delay to ensure UI is ready
				System.Windows.Application.Current.Dispatcher.Invoke(() => _ = StartListeningAsync());
			});
		}
	}

	/// <summary>
	/// Starts the UDP listener asynchronously. On failure (e.g. port in use), shows a MessageBox and keeps the listener inactive.
	/// </summary>
	private async Task StartListeningAsync()
	{
		try
		{
			var config = _configService.LoadConfiguration();
			var result = await _udpReceiverService.StartListeningAsync(config.Ports);

			if (result.AnyStarted)
			{
				IsListening = true;
				ListeningStatus = _localizationService.GetString("Status_Listening", "Listening");
				StatusMessage = _localizationService.GetString("Status_ListeningOnPorts", $"Listening on {config.Ports.Count} port(s)");
				if (!string.IsNullOrEmpty(result.ErrorMessage))
				{
					// Partial failure: some ports failed
					StatusMessage += " " + _localizationService.GetString("Status_SomePortsFailed", "(Some ports could not be opened.)");
				}
			}
			else
			{
				IsListening = false;
				ListeningStatus = _localizationService.GetString("Status_Error", "Error");
				StatusMessage = result.ErrorMessage;
				var caption = _localizationService.GetString("Error_StartingListenerCaption", "Error starting listener");
				MessageBox.Show(result.ErrorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		catch (Exception ex)
		{
			IsListening = false;
			ListeningStatus = _localizationService.GetString("Status_Error", "Error");
			StatusMessage = _localizationService.GetString("Error_StartingListener", $"Error starting listener: {ex.Message}");
			var caption = _localizationService.GetString("Error_StartingListenerCaption", "Error starting listener");
			MessageBox.Show(ex.Message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void StopListening()
	{
		try
		{
			_udpReceiverService.StopListening();
			IsListening = false;
			ListeningStatus = _localizationService.GetString("Status_Stopped", "Stopped");
			StatusMessage = _localizationService.GetString("Status_StoppedListening", "Stopped listening");
		}
		catch (Exception ex)
		{
			StatusMessage = _localizationService.GetString("Error_StoppingListener", $"Error stopping listener: {ex.Message}");
		}
	}

	private void OpenFile()
	{
		var dialog = new OpenFileDialog
		{
			Filter = "Log Files (*.xml;*.log;*.txt;*.json)|*.xml;*.log;*.txt;*.json|XML Files (*.xml)|*.xml|Text Files (*.txt;*.log)|*.txt;*.log|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
			Title = _localizationService.GetString("Import_OpenTitle", "Open Log File"),
			Multiselect = true
		};

		if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
			BeginImportPathsFromUi(dialog.FileNames);
	}

	/// <summary>
	/// Starts unified import for paths given by the open-file dialog or drag-drop (filters and asks destination).
	/// </summary>
	public void BeginImportPathsFromUi(IEnumerable<string> rawPaths)
	{
		var filtered = (rawPaths ?? Enumerable.Empty<string>())
			.Where(IsSupportedImportPath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (filtered.Count == 0)
		{
			StatusMessage = _localizationService.GetString("Import_NoSupportedFiles", "No supported log files were found.");
			return;
		}

		if (IsLoading)
			return;

		if (!TryResolveImportBatchContext(filtered, out var ctx) || ctx == null)
			return;

		_ = RunImportBatchAsync(filtered, ctx);
	}

	/// <summary>
	/// Returns whether the path is a supported regular file for import (aligned with the open-file filter).
	/// </summary>
	private static bool IsSupportedImportPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return false;

		try
		{
			var attributes = File.GetAttributes(path);
			if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.Device) != 0)
				return false;
		}
		catch
		{
			return false;
		}

		var extension = Path.GetExtension(path);
		return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
		       || extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
		       || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
		       || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
		       || extension.Length == 0;
	}

	/// <summary>
	/// Shows the destination dialog when needed and builds the batch context.
	/// </summary>
	private bool TryResolveImportBatchContext(IReadOnlyList<string> paths, out LogFileImportBatchContext? ctx)
	{
		ctx = null;
		if (paths.Count == 0)
			return false;

		if (LogTabs.Count == 0 && paths.Count == 1)
		{
			ctx = new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.PerFileNewTab, null, null);
			return true;
		}

		var owner = System.Windows.Application.Current.MainWindow;
		var sharedHeader = paths.Count > 1
			? _localizationService.GetString("Import_TabMerged", "Merged import")
			: Path.GetFileName(paths[0]);

		if (LogTabs.Count == 0)
		{
			var dlgMinimal = new ImportDestinationWindow(LogTabs.ToList(), null, showExistingTabOption: false,
					showPerFileNewTabOption: paths.Count > 1)
			{
				Owner = owner
			};
			if (dlgMinimal.ShowDialog() != true)
				return false;

			ctx = dlgMinimal.SelectedMode == LogFileImportBatchContext.DestinationMode.SingleNewTab
				? new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.SingleNewTab, null, sharedHeader)
				: new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.PerFileNewTab, null, null);
			return true;
		}

		var dlgFull = new ImportDestinationWindow(LogTabs.ToList(), SelectedTab, showExistingTabOption: true,
				showPerFileNewTabOption: paths.Count > 1)
		{
			Owner = owner
		};
		if (dlgFull.ShowDialog() != true)
			return false;

		switch (dlgFull.SelectedMode)
		{
			case LogFileImportBatchContext.DestinationMode.ExistingTab:
				var tab = dlgFull.SelectedExistingTab;
				if (tab == null)
					return false;
				ctx = new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.ExistingTab, tab, null);
				return true;
			case LogFileImportBatchContext.DestinationMode.SingleNewTab:
				ctx = new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.SingleNewTab, null, sharedHeader);
				return true;
			default:
				ctx = new LogFileImportBatchContext(LogFileImportBatchContext.DestinationMode.PerFileNewTab, null, null);
				return true;
		}
	}

	/// <summary>
	/// Parses each file in order and applies logs according to <paramref name="ctx"/>.
	/// </summary>
	private async Task RunImportBatchAsync(IReadOnlyList<string> paths, LogFileImportBatchContext ctx)
	{
		IsLoading = true;
		LoadingProgress = _localizationService.GetString("Import_Starting", "Starting...");
		StatusMessage = _localizationService.GetString("Import_InProgress", "Importing...");

		try
		{
			for (var i = 0; i < paths.Count; i++)
			{
				var path = paths[i];
				try
				{
					await ImportSinglePathAsync(path, ctx, i + 1, paths.Count).ConfigureAwait(true);
				}
			catch (Exception ex)
			{
				var pathName = Path.GetFileName(path);
				var tpl = _localizationService.GetString(
					"Import_FileError_Format",
					"Error importing {0}: {1}");
				StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, tpl, pathName, ex.Message);
			}
			}

			StatusMessage = _localizationService.GetString("Import_BatchComplete", "Import finished.");
		}
		finally
		{
			IsLoading = false;
			LoadingProgress = string.Empty;
		}
	}

	/// <summary>
	/// Runs format detection (if needed) and parsing for a single path in a batch.
	/// </summary>
	private async Task ImportSinglePathAsync(string filePath, LogFileImportBatchContext ctx, int index, int total)
	{
		var extension = Path.GetExtension(filePath).ToLowerInvariant();
		if (extension == ".txt" || extension == ".log" || extension == "")
			await ImportTextFormatFlowAsync(filePath, ctx, index, total).ConfigureAwait(true);
		else
			await ImportDirectParseFlowAsync(filePath, ctx, index, total).ConfigureAwait(true);
	}

	/// <summary>
	/// Text / extensionless log paths: detect format, optionally show column-mapping, then parse.
	/// </summary>
	private async Task ImportTextFormatFlowAsync(string filePath, LogFileImportBatchContext ctx, int index, int total)
	{
		var dispatcher = System.Windows.Application.Current.Dispatcher;
		TextFileFormat? format;
		try
		{
			format = await Task.Run(() => _fileParserService.GetTextFileFormat(filePath)).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await dispatcher.InvokeAsync(() =>
			{
				StatusMessage = _localizationService.GetString("Import_FormatDetectError", "Error detecting file format") +
				                $": {ex.Message}";
			}).Task.ConfigureAwait(false);
			await ImportDirectParseFlowAsync(filePath, ctx, index, total).ConfigureAwait(true);
			return;
		}

		if (format != null && format.ColumnMapping.IsValid())
		{
			await ImportDirectParseFlowAsync(filePath, ctx, index, total).ConfigureAwait(true);
			return;
		}

		var sampleLines =
			await Task.Run(() => File.ReadLines(filePath).Take(20).ToList()).ConfigureAwait(false);

		if (format == null)
		{
			var detector = App.ServiceProvider?.GetRequiredService<TextFileFormatDetector>();
			format = detector?.DetectFormat(filePath);
		}

		TextFileFormat? mappedFormat = await dispatcher.InvokeAsync(() =>
		{
			if (format == null)
				return null;

			var viewModel = new ColumnMappingViewModel(format, sampleLines, filePath);
			var mappingWindow = new ColumnMappingWindow(viewModel);
			var mainWindow = System.Windows.Application.Current.MainWindow;

			if (mappingWindow.ShowDialog(mainWindow) != true)
				return null;

			var userFormat = mappingWindow.FinalFormat;

			if (mappingWindow.SaveForPattern && !string.IsNullOrEmpty(mappingWindow.FilePattern))
				_fileParserService.SaveTextFileFormat(mappingWindow.FilePattern, userFormat);

			return userFormat;
		}).Task.ConfigureAwait(false);

		if (mappedFormat != null)
			await ParseAndApplyWithFormatAsync(filePath, mappedFormat, ctx, index, total).ConfigureAwait(true);
		else
			await ImportDirectParseFlowAsync(filePath, ctx, index, total).ConfigureAwait(true);
	}

	/// <summary>
	/// Parses using an explicit text format and applies to the tab batch target.
	/// </summary>
	private async Task ParseAndApplyWithFormatAsync(string filePath, TextFileFormat format,
		LogFileImportBatchContext ctx, int index, int total)
	{
		var dispatcher = System.Windows.Application.Current.Dispatcher;

		var progress = new Progress<(int current, int total)>(p =>
		{
			var percentage = p.total > 0 ? p.current * 100 / p.total : 0;
			dispatcher.Invoke(() => { LoadingProgress = FormatParseProgress(filePath, index, total, p.current, p.total, percentage); });
		});

		await Task.Run(() => _fileParserService.SaveTextFileFormat(Path.GetFileName(filePath), format)).ConfigureAwait(false);

		List<LogEventInfo> logEvents;
		try
		{
			logEvents = await _fileParserService.ParseFileAsync(filePath, progress).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await dispatcher.InvokeAsync(() =>
			{
				StatusMessage = _localizationService.GetString("Import_ParseError", "Error parsing file") + $": {ex.Message}";
			}).Task.ConfigureAwait(false);
			return;
		}

		await dispatcher.InvokeAsync(() =>
		{
			ApplyParsedLogs(logEvents, filePath, ctx);
			StatusMessage = FormatLoadedStatus(filePath, logEvents.Count);
			LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
		}).Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Parses without injecting a new text format and applies to the batch target.
	/// </summary>
	private async Task ImportDirectParseFlowAsync(string filePath, LogFileImportBatchContext ctx, int index, int total)
	{
		var dispatcher = System.Windows.Application.Current.Dispatcher;

		var progress = new Progress<(int current, int total)>(p =>
		{
			var percentage = p.total > 0 ? p.current * 100 / p.total : 0;
			dispatcher.Invoke(() => { LoadingProgress = FormatParseProgress(filePath, index, total, p.current, p.total, percentage); });
		});

		List<LogEventInfo> logEvents;
		try
		{
			logEvents = await _fileParserService.ParseFileAsync(filePath, progress).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await dispatcher.InvokeAsync(() =>
			{
				StatusMessage = _localizationService.GetString("Import_ParseError", "Error parsing file") + $": {ex.Message}";
			}).Task.ConfigureAwait(false);
			return;
		}

		await dispatcher.InvokeAsync(() =>
		{
			ApplyParsedLogs(logEvents, filePath, ctx);
			StatusMessage = FormatLoadedStatus(filePath, logEvents.Count);
			LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
		}).Task.ConfigureAwait(false);
	}

	private string FormatParseProgress(string filePath, int fileIndex, int fileTotal, int current, int parsedTotal,
		int percentage)
	{
		var name = Path.GetFileName(filePath);
		var tpl = _localizationService.GetString(
			"Import_ParseProgress_Format",
			"File {0}/{1} — {2}: {3} / {4} ({5}%)");
		return string.Format(System.Globalization.CultureInfo.CurrentCulture, tpl, fileIndex, fileTotal, name,
			current, parsedTotal, percentage);
	}

	private string FormatLoadedStatus(string filePath, int count)
	{
		var name = Path.GetFileName(filePath);
		var tpl = _localizationService.GetString(
			"Import_LoadedFromFile_Format",
			"Loaded {0} log entries from {1}");
		return string.Format(System.Globalization.CultureInfo.CurrentCulture, tpl, count, name);
	}

	/// <summary>
	/// Appends parsed rows to the tab chosen for this import batch.
	/// </summary>
	private void ApplyParsedLogs(List<LogEventInfo> logEvents, string filePath, LogFileImportBatchContext ctx)
	{
		if (logEvents == null || logEvents.Count == 0)
			return;

		LogTabViewModel tab;
		switch (ctx.Mode)
		{
			case LogFileImportBatchContext.DestinationMode.ExistingTab:
				tab = ctx.ExistingTargetTab
				      ?? throw new InvalidOperationException("Existing tab target is not set.");
				break;
			case LogFileImportBatchContext.DestinationMode.SingleNewTab:
				if (ctx.SharedNewTab == null)
				{
					ctx.SharedNewTab = new LogTabViewModel
					{
						Header = ctx.SharedTabHeaderWhenSingleNewTab ?? Path.GetFileName(filePath),
						TargetName = $"FileImport_{Guid.NewGuid()}",
						MaxCount = int.MaxValue
					};
					LogTabs.Add(ctx.SharedNewTab);
					SelectedTab = ctx.SharedNewTab;
				}

				tab = ctx.SharedNewTab;
				break;
			default:
				tab = new LogTabViewModel
				{
					Header = Path.GetFileName(filePath),
					TargetName = $"FileImport_{Guid.NewGuid()}",
					MaxCount = int.MaxValue
				};
				LogTabs.Add(tab);
				SelectedTab = tab;
				break;
		}

		const int batchSize = 500;
		for (var i = 0; i < logEvents.Count; i += batchSize)
		{
			var batch = logEvents.Skip(i).Take(batchSize).ToList();
			foreach (var logEvent in batch)
				tab.AddLogEvent(logEvent);

			if (i + batchSize < logEvents.Count)
			{
				var progress = (i + batchSize) * 100 / logEvents.Count;
				LoadingProgress = string.Format(System.Globalization.CultureInfo.CurrentCulture,
					_localizationService.GetString("Import_ApplyProgress_Format", "Adding to view: {0}%"), progress);
			}
		}

		LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
	}

	private void OpenSettings()
	{
		using var scope = App.ServiceProvider.CreateScope();
		var settingsWindow = scope.ServiceProvider.GetRequiredService<SettingsWindow>();
		var result = settingsWindow.ShowDialog(System.Windows.Application.Current.MainWindow);
		if (result == true)
		{
			StatusMessage = "Settings saved";
			// Reload configuration if needed
			LoadConfiguration();
		}
	}

	private void ShowAbout()
	{
		// TODO: Show about dialog
		StatusMessage = "About dialog not yet implemented";
	}

	private void ChangeLanguage(string? languageCode)
	{
		if (string.IsNullOrEmpty(languageCode))
			return;

		try
		{
			_localizationService.SetLanguage(languageCode);
			UpdateLanguageFlag();
                
			// Notify that language has changed - this will require app restart for full effect
			StatusMessage = "Language changed. Please restart the application for full effect.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error changing language: {ex.Message}";
		}
	}

	/// <summary>
	/// Exports filtered log entries from the currently selected tab to a file
	/// </summary>
	private void ExportLogs()
	{
		if (SelectedTab == null)
			return;

		// Generate default filename from tab header (remove whitespaces) + timestamp
		var headerWithoutSpaces = SelectedTab.Header?.Replace(" ", string.Empty) ?? "Logs";
		var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
		var defaultFileName = $"{headerWithoutSpaces}-{timestamp}.log";

		var dialog = new SaveFileDialog
		{
			Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*",
			Title = "Export Logs",
			DefaultExt = "log",
			FileName = defaultFileName
		};

		if (dialog.ShowDialog() == true)
		{
			var filePath = dialog.FileName;
			var nLogViewer = FindNLogViewerInTab();

			if (nLogViewer == null)
			{
				StatusMessage = "Could not find NLogViewer instance in selected tab.";
				return;
			}

			var exportParameter = new ExportParameter
			{
				FilePath = filePath,
				Format = ExportFormat.Log
			};

			// Execute the export command on the NLogViewer
			if (nLogViewer.ExportCommand?.CanExecute(exportParameter) == true)
			{
				nLogViewer.ExportCommand.Execute(exportParameter);
				StatusMessage = $"Exported logs to {Path.GetFileName(filePath)}";
			}
			else
			{
				StatusMessage = "Export command is not available.";
			}
		}
	}

	/// <summary>
	/// Finds the NLogViewer instance in the currently selected tab's visual tree
	/// </summary>
	/// <returns>The NLogViewer instance if found, null otherwise</returns>
	private Wpf.NLogViewerBase? FindNLogViewerInTab()
	{
		if (SelectedTab == null)
			return null;

		// Get the main window to access the TabControl
		var mainWindow = System.Windows.Application.Current.MainWindow;
		if (mainWindow == null)
			return null;

		// Find the TabControl in the main window
		var tabControl = FindVisualChild<TabControl>(mainWindow);
		if (tabControl == null)
			return null;

		// The TabControl uses TabContent attached property which caches content in a Border
		// Search for Border elements that might contain the tab content
		var borders = FindVisualChildren<Border>(tabControl);
		
		foreach (var border in borders)
		{
			// Check if this border contains a ContentControl (which is used by TabContent)
			var contentControl = FindVisualChild<ContentControl>(border);
			if (contentControl != null && contentControl.DataContext == SelectedTab)
			{
				// Found the content control for the selected tab, now find NLogViewer
				var nLogViewer = FindVisualChild<Wpf.NLogViewerBase>(contentControl);
				if (nLogViewer != null)
					return nLogViewer;
			}
		}

		// Fallback: Search directly in TabControl for NLogViewer
		return FindVisualChild<Wpf.NLogViewerBase>(tabControl);
	}

	/// <summary>
	/// Recursively searches the visual tree for all child elements of the specified type
	/// </summary>
	/// <typeparam name="T">The type of child elements to find</typeparam>
	/// <param name="parent">The parent element to search in</param>
	/// <returns>An enumerable collection of found child elements</returns>
	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null)
			yield break;

		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);

			if (child is T result)
				yield return result;

			foreach (var childOfType in FindVisualChildren<T>(child))
			{
				yield return childOfType;
			}
		}
	}

	/// <summary>
	/// Recursively searches the visual tree for a child element of the specified type
	/// </summary>
	/// <typeparam name="T">The type of child element to find</typeparam>
	/// <param name="parent">The parent element to search in</param>
	/// <returns>The found child element, or null if not found</returns>
	private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null)
			return null;

		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);

			if (child is T result)
				return result;

			var childOfType = FindVisualChild<T>(child);
			if (childOfType != null)
				return childOfType;
		}

		return null;
	}

	private void UpdateLanguageFlag()
	{
		CurrentLanguageFlag = _localizationService.CurrentLanguageFlag;
	}
	
	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void Dispose()
	{
		StopListening();
		//_udpReceiverService.LogReceived -= OnLogReceived;
		//_fileParserService.LogParsed -= OnLogReceived;
		_udpReceiverService?.Dispose();
		_fileParserService?.Dispose();
		_subscriptions.Dispose();
	}
}