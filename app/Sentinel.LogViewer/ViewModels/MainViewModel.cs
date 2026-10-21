using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
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
using Sentinel.LogViewer.Wpf;
using Sentinel.LogViewer.Wpf.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
	private readonly LogReceiverService _udpReceiverService;
	private readonly LogFileParserService _fileParserService;
	private readonly ConfigurationService _configService;
	private readonly LocalizationService _localizationService;
	private readonly UpdateService _updateService;
	private string? _updateNotice;
	private bool _isListening;
	private bool _hasStarted;
	private LogTabViewModel? _summaryTab;
	private string _tabSummary = string.Empty;
	private bool _hasActiveFilter;
	private bool _sourcesPanelOpen;
	private string _receiveStatusText = string.Empty;
	private ReceiveStatus _receiveStatus;
	private readonly System.Windows.Threading.DispatcherTimer _sourcesTimer = new() { Interval = TimeSpan.FromSeconds(1) };
	private string _lastLogTimestamp = string.Empty;
	private LogTabViewModel? _selectedTab;
	private string _currentLanguageFlag = "🇬🇧";
	private bool _isLoading;
	private string _loadingProgress = string.Empty;
	private TimeStampFormat _defaultTimeStampFormat;
	private string _listFontFamilyName = string.Empty;
	private int _listFontSize = SettingsRules.DefaultListFontSize;
	private int _fontZoom;

	private readonly CompositeDisposable _subscriptions = new();
	private readonly ApplicationTabService _applicationTabs;
	private readonly FilterPresetService _filterPresets;
	private IgnoreRule[] _ignoreRules = Array.Empty<IgnoreRule>();
	private bool _applyingFilterPreset;


	public MainViewModel(
		LogReceiverService udpReceiverService,
		LogFileParserService fileParserService,
		ConfigurationService configService,
		LocalizationService localizationService,
		UpdateService updateService,
		ApplicationTabService applicationTabService)
	{
		_udpReceiverService = udpReceiverService ?? throw new ArgumentNullException(nameof(udpReceiverService));
		_fileParserService = fileParserService ?? throw new ArgumentNullException(nameof(fileParserService));
		_configService = configService ?? throw new ArgumentNullException(nameof(configService));
		_localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
		_updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
		_applicationTabs = applicationTabService ?? throw new ArgumentNullException(nameof(applicationTabService));
		_filterPresets = new FilterPresetService(_configService);
		RefreshFilterPresets();

		LogTabs = new ObservableCollection<LogTabViewModel>();
		LogTabs.CollectionChanged += OnLogTabsCollectionChanged;

		// Initialize commands
		StartListeningCommand = new AsyncRelayCommand(StartListeningAsync, () => !_isListening);
		ToggleReceivingCommand = new RelayCommand(() =>
		{
			if (_isListening)
				StopListening();
			else
				_ = StartListeningAsync();
		});
		ToggleSourcesPanelCommand = new RelayCommand(() => IsSourcesPanelOpen = !IsSourcesPanelOpen);
		ResetFilterCommand = new RelayCommand(ResetFilter, () => HasActiveFilter);
		IgnoreApplicationCommand = new RelayCommand<LogTabViewModel>(tab => ConfirmAndIgnore(tab, IgnoreScope.Application), tab => tab?.SenderKey != null);
		IgnoreApplicationFromSenderCommand = new RelayCommand<LogTabViewModel>(tab => ConfirmAndIgnore(tab, IgnoreScope.ApplicationFromSender), HasIpAddress);
		IgnoreSenderCommand = new RelayCommand<LogTabViewModel>(tab => ConfirmAndIgnore(tab, IgnoreScope.Sender), HasIpAddress);
		ManageSourcesCommand = new RelayCommand(() =>
		{
			IsSourcesPanelOpen = false;
			OpenSettings(SettingsViewModel.ReceivingId);
		});
		StopListeningCommand = new RelayCommand(StopListening, () => _isListening);
		OpenFileCommand = new RelayCommand(() => OpenFile(askColumnMapping: false));
		OpenFileWithMappingCommand = new RelayCommand(() => OpenFile(askColumnMapping: true));
		OpenSettingsCommand = new RelayCommand(() => OpenSettings());
		ManageFilterPresetsCommand = new RelayCommand(() => OpenSettings(SettingsViewModel.FilterPresetsId));
		SaveFilterPresetCommand = new RelayCommand(SaveFilterPreset, () => SelectedTab != null);
		ApplyFilterPresetCommand = new RelayCommand<FilterPreset>(ApplyFilterPreset, _ => SelectedTab != null);
		ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
		AboutCommand = new RelayCommand(ShowAbout);
		ChangeLanguageCommand = new RelayCommand<string>(ChangeLanguage);
		ExportLogsCommand = new RelayCommand(() => ExportLogs(SelectedTab), () => SelectedTab != null);
		CloseTabCommand = new RelayCommand<LogTabViewModel>(CloseTab);
		CloseOtherTabsCommand = new RelayCommand<LogTabViewModel>(CloseOtherTabs, _ => LogTabs.Count > 1);
		SaveFilteredListCommand = new RelayCommand<LogTabViewModel>(SaveFilteredList);
		CloseCurrentTabCommand = new RelayCommand(CloseCurrentTab, () => SelectedTab != null && LogTabs.Count > 0);
		CloseAllTabsCommand = new RelayCommand(CloseAllTabs, () => LogTabs.Count > 0);
		CloseTabsToTheRightCommand = new RelayCommand<LogTabViewModel>(CloseTabsToTheRight, tab => tab != null && LogTabs.IndexOf(tab) < LogTabs.Count - 1);
		SelectNextTabCommand = new RelayCommand(() => SelectTabRelative(1), () => LogTabs.Count > 1);
		SelectPreviousTabCommand = new RelayCommand(() => SelectTabRelative(-1), () => LogTabs.Count > 1);
		ZoomInCommand = new RelayCommand(() => Zoom(1));
		ZoomOutCommand = new RelayCommand(() => Zoom(-1));
		ResetFontZoomCommand = new RelayCommand(() => Zoom(0));
		SaveFontZoomCommand = new RelayCommand(SaveFontZoom);
		SelectTabCommand = new RelayCommand<int>(SelectTab, number => LogTabs.Count > 0 && number is >= 1 and <= 9);
		ToggleTakeOverFiltersCommand = new RelayCommand<LogTabViewModel>(ToggleTakeOverFilters, tab => tab?.SenderKey != null);
		RemoveTakenOverFiltersCommand = new RelayCommand<LogTabViewModel>(RemoveTakenOverFilters);

		// Initialize current language flag
		UpdateLanguageFlag();

		// The sources of the receiver follow its changes; their numbers are read again while the panel is open
		_udpReceiverService.SourcesChanged += OnSourcesChanged;
		_sourcesTimer.Tick += (_, _) =>
		{
			foreach (var source in Sources)
				source.Refresh();
		};

		// Subscribe to log events
		// Buffer events for 250ms, filter empty batches, group by AppInfo.Id, and process each group as a batch
		_udpReceiverService.Log4JEventObservable
			.Where(logEvent => !IsIgnored(logEvent))
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
		
		// A restarted application is another process, so the tab is chosen by what the settings say about restarts
		var route = _applicationTabs.Route(LogTabs, firstEvent.AppInfo, DateTime.UtcNow);
		foreach (var replaced in route.TabsToClose)
			CloseTab(replaced);

		var tab = route.Tab;
		if (tab == null)
		{
			tab = CreateLiveTab(firstEvent.AppInfo);
			LogTabs.Add(tab);
			SelectedTab = tab;
		}
		else if (tab.TargetName != appInfoId)
		{
			tab.TargetName = appInfoId;
			tab.Header = firstEvent.AppInfo.ToString();
		}
		
		// Add all log events from the batch to the tab (Cache replays to LogViewer when it subscribes)
		foreach (var logEvent in logEvents)
		{
			tab.AddLogEntry(logEvent.Entry);
		}
		LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
	}

	/// <summary>The font family of the log list; the font of the application when none is set</summary>
	public System.Windows.Media.FontFamily ListFontFamily =>
		string.IsNullOrEmpty(_listFontFamilyName) ? System.Windows.SystemFonts.MessageFontFamily : new System.Windows.Media.FontFamily(_listFontFamilyName);

	private int EffectiveFontSize => Math.Clamp(_listFontSize + _fontZoom, SettingsRules.MinListFontSize, SettingsRules.MaxListFontSize);

	/// <summary>The font size of the log list: the saved one and what the user zoomed in this session</summary>
	public double ListFontSize => EffectiveFontSize;

	/// <summary>Whether the size of this session differs from the saved one, which the status bar offers to save</summary>
	public bool IsFontZoomed => EffectiveFontSize != _listFontSize;

	public string FontZoomText => string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("Zoom_FontSize", "Font size {0}"), EffectiveFontSize);

	/// <summary>Zooms the log list for this session</summary>
	public ICommand ZoomInCommand { get; }
	public ICommand ZoomOutCommand { get; }

	/// <summary>Goes back to the saved size</summary>
	public ICommand ResetFontZoomCommand { get; }

	/// <summary>Saves the size of this session as the font size of the settings</summary>
	public ICommand SaveFontZoomCommand { get; }

	/// <param name="step">1 or -1 to zoom, 0 to go back to the saved size</param>
	private void Zoom(int step)
	{
		_fontZoom = step == 0 ? 0 : Math.Clamp(_listFontSize + _fontZoom + step, SettingsRules.MinListFontSize, SettingsRules.MaxListFontSize) - _listFontSize;
		NotifyFont();
	}

	private void SaveFontZoom()
	{
		var config = _configService.LoadConfiguration();
		config.ListFontSize = EffectiveFontSize;
		_configService.SaveConfiguration(config);
		_listFontSize = config.ListFontSize;
		_fontZoom = 0;
		NotifyFont();
	}

	private void NotifyFont()
	{
		OnPropertyChanged(nameof(ListFontFamily));
		OnPropertyChanged(nameof(ListFontSize));
		OnPropertyChanged(nameof(IsFontZoomed));
		OnPropertyChanged(nameof(FontZoomText));
	}

	/// <summary>
	/// The format of the timestamp column that tabs show until the user picks another one for a tab
	/// </summary>
	public TimeStampFormat DefaultTimeStampFormat
	{
		get => _defaultTimeStampFormat;
		private set
		{
			if (_defaultTimeStampFormat == value)
				return;

			_defaultTimeStampFormat = value;
			OnPropertyChanged();
		}
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
				((RelayCommand)CloseCurrentTabCommand).RaiseCanExecuteChanged();
				((RelayCommand)CloseAllTabsCommand).RaiseCanExecuteChanged();
				((RelayCommand)SaveFilterPresetCommand).RaiseCanExecuteChanged();
				((RelayCommand<FilterPreset>)ApplyFilterPresetCommand).RaiseCanExecuteChanged();
				FollowSummaryOfSelectedTab();
			}
		}
	}

	private void OnLogTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null)
		{
			// A new tab starts with the logger tree and the search fields the way the settings say, before its viewer exists. What the user changes in a tab
			// is saved as the setting, so that the next tab and the next start use it; tabs that are open keep what they have.
			var configuration = _configService.LoadConfiguration();
			foreach (var tab in e.NewItems.OfType<LogTabViewModel>())
			{
				tab.LoggerTreeOnLeft = configuration.LoggerTreeOnLeft;
				tab.LoggerTreeOpen = configuration.LoggerTreeOpen;
				tab.LoggerTreeWidth = configuration.LoggerTreeWidth;
				tab.SearchFields = SettingsRules.ParseSearchFields(configuration.SearchFields);
				tab.ColumnKeys = Wpf.LogColumns.Normalize(configuration.Columns);
				tab.PropertyChanged += OnTabViewSettingsChanged;
			}
		}

		if (e.OldItems != null)
		{
			foreach (var tab in e.OldItems.OfType<LogTabViewModel>())
				tab.PropertyChanged -= OnTabViewSettingsChanged;
		}

		((RelayCommand)CloseCurrentTabCommand).RaiseCanExecuteChanged();
		((RelayCommand)CloseAllTabsCommand).RaiseCanExecuteChanged();
		((RelayCommand<LogTabViewModel>)CloseOtherTabsCommand).RaiseCanExecuteChanged();
		((RelayCommand<LogTabViewModel>)CloseTabsToTheRightCommand).RaiseCanExecuteChanged();
		((RelayCommand)SelectNextTabCommand).RaiseCanExecuteChanged();
		((RelayCommand)SelectPreviousTabCommand).RaiseCanExecuteChanged();
	}

	/// <summary>
	/// Removes the selected tab; like the close button of a tab, it does not ask.
	/// </summary>
	private void CloseCurrentTab() => CloseTab(SelectedTab);

	/// <summary>
	/// Removes a tab without asking (tab header close button and context menu).
	/// </summary>
	private void OnTabViewSettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not LogTabViewModel tab)
			return;

		var configuration = _configService.LoadConfiguration();
		switch (e.PropertyName)
		{
			case nameof(LogTabViewModel.LoggerTreeOnLeft):
				configuration.LoggerTreeOnLeft = tab.LoggerTreeOnLeft;
				break;
			case nameof(LogTabViewModel.LoggerTreeOpen):
				configuration.LoggerTreeOpen = tab.LoggerTreeOpen;
				break;
			case nameof(LogTabViewModel.LoggerTreeWidth):
				configuration.LoggerTreeWidth = Math.Clamp((int)Math.Round(tab.LoggerTreeWidth), SettingsRules.MinLoggerTreeWidth, SettingsRules.MaxLoggerTreeWidth);
				break;
			case nameof(LogTabViewModel.ColumnKeys):
				configuration.Columns = Wpf.LogColumns.Normalize(tab.ColumnKeys);
				break;
			case nameof(LogTabViewModel.SearchFields):
				if (_applyingFilterPreset)
					return;

				configuration.SearchFields = tab.SearchFields.ToString();
				break;
			default:
				return;
		}

		_configService.SaveConfiguration(configuration);
	}

	private LogTabViewModel CreateLiveTab(AppInfo appInfo)
	{
		var key = ApplicationTabService.KeyOf(appInfo);
		var tab = new LogTabViewModel
		{
			Header = appInfo.ToString(),
			TargetName = appInfo.Id,
			MaxCount = _configService.LoadConfiguration().MaxLogEntriesPerTab,
			SenderKey = key,
			ApplicationName = appInfo.AppName?.Name ?? string.Empty,
			Sender = appInfo.Sender,
			TakesOverFilters = _applicationTabs.TakesOverFilters(key),
			RestartBehavior = _applicationTabs.RestartBehaviorOf(key)
		};

		var filters = _applicationTabs.FiltersForNewTab(key);
		if (filters != null)
		{
			tab.FilterState = filters;
			tab.FiltersTakenOver = true;
		}

		tab.PropertyChanged += OnLiveTabPropertyChanged;
		return tab;
	}

	/// <summary>
	/// Keeps the filters of a tab for the next tab of its application
	/// </summary>
	private void OnLiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(LogTabViewModel.FilterState) || sender is not LogTabViewModel { SenderKey: { } key } tab)
			return;

		_applicationTabs.RememberFilters(key, tab.FilterState);
		if (tab.FilterState is not { IsEmpty: false })
			tab.FiltersTakenOver = false;
	}

	private void ToggleTakeOverFilters(LogTabViewModel? tab)
	{
		if (tab?.SenderKey is not { } key)
			return;

		var value = !tab.TakesOverFilters;
		_applicationTabs.SetTakesOverFilters(key, value);
		foreach (var other in LogTabs.Where(t => t.SenderKey == key))
			other.TakesOverFilters = value;

		if (value)
			_applicationTabs.RememberFilters(key, tab.FilterState);
	}

	/// <summary>
	/// Sets what happens when the application of the tab starts again
	/// </summary>
	public void SetRestartBehavior(LogTabViewModel tab, RestartBehavior behavior)
	{
		if (tab.SenderKey is not { } key)
			return;

		_applicationTabs.SetRestartBehavior(key, behavior);
		foreach (var other in LogTabs.Where(t => t.SenderKey == key))
			other.RestartBehavior = behavior;
	}

	private void RemoveTakenOverFilters(LogTabViewModel? tab)
	{
		if (tab == null)
			return;

		tab.FilterState = new LogViewerFilterState();
		tab.FiltersTakenOver = false;
	}

	private void CloseTab(LogTabViewModel? tab)
	{
		var index = tab == null ? -1 : LogTabs.IndexOf(tab);
		if (index < 0)
			return;

		var wasSelected = ReferenceEquals(tab, SelectedTab);
		LogTabs.RemoveAt(index);

		if (LogTabs.Count == 0)
			SelectedTab = null;
		else if (wasSelected)
			SelectedTab = LogTabs[Math.Min(index, LogTabs.Count - 1)];
	}

	/// <summary>
	/// Removes every tab except the given one after the user confirms.
	/// </summary>
	private void CloseOtherTabs(LogTabViewModel? tab)
	{
		if (tab == null || !LogTabs.Contains(tab) || LogTabs.Count < 2)
			return;

		var caption = _localizationService.GetString("Confirm_CloseOtherTabs_Caption", "Close all other tabs");
		var message = _localizationService.GetString(
			"Confirm_CloseOtherTabs_Message",
			"Do you really want to close all other tabs?");
		var owner = System.Windows.Application.Current.MainWindow;
		if (MessageDialogWindow.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) !=
		    MessageBoxResult.Yes)
			return;

		foreach (var other in LogTabs.Where(t => !ReferenceEquals(t, tab)).ToList())
			LogTabs.Remove(other);

		SelectedTab = tab;
	}

	/// <summary>
	/// Removes the tabs to the right of the given one after the user confirms.
	/// </summary>
	private void CloseTabsToTheRight(LogTabViewModel? tab)
	{
		var index = tab == null ? -1 : LogTabs.IndexOf(tab);
		if (index < 0 || index == LogTabs.Count - 1)
			return;

		var caption = _localizationService.GetString("Confirm_CloseTabsToTheRight_Caption", "Close tabs to the right");
		var message = _localizationService.GetString(
			"Confirm_CloseTabsToTheRight_Message",
			"Do you really want to close all tabs to the right of this one?");
		var owner = System.Windows.Application.Current.MainWindow;
		if (MessageDialogWindow.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) !=
		    MessageBoxResult.Yes)
			return;

		var selectedIsClosed = LogTabs.IndexOf(SelectedTab!) > index;
		foreach (var right in LogTabs.Skip(index + 1).ToList())
			LogTabs.Remove(right);

		if (selectedIsClosed)
			SelectedTab = tab;
	}

	/// <summary>
	/// Selects the next or the previous tab; after the last one it goes on with the first, as in a browser.
	/// </summary>
	private void SelectTabRelative(int step)
	{
		if (LogTabs.Count == 0)
			return;

		var index = SelectedTab == null ? 0 : LogTabs.IndexOf(SelectedTab);
		SelectedTab = LogTabs[(index + step + LogTabs.Count) % LogTabs.Count];
	}

	/// <summary>
	/// Selects the tab with the given number (1 is the first); 9 is always the last tab, as in a browser.
	/// </summary>
	private void SelectTab(int number)
	{
		if (LogTabs.Count == 0)
			return;

		SelectedTab = LogTabs[number == 9 ? LogTabs.Count - 1 : Math.Min(number, LogTabs.Count) - 1];
	}

	/// <summary>
	/// Saves the entries the tab currently shows (filters applied).
	/// </summary>
	private void SaveFilteredList(LogTabViewModel? tab)
	{
		if (tab == null)
			return;

		if (ReferenceEquals(tab, SelectedTab))
		{
			ExportLogs(tab);
			return;
		}

		// The viewer of a tab exists once the tab has been shown, so select it before exporting.
		SelectedTab = tab;
		System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ExportLogs(tab), DispatcherPriority.Loaded);
	}

	/// <summary>
	/// Clears all tabs after user confirms.
	/// </summary>
	private void CloseAllTabs()
	{
		var caption = _localizationService.GetString("Confirm_CloseAllTabs_Caption", "Close all tabs");
		var message = _localizationService.GetString(
			"Confirm_CloseAllTabs_Message",
			"Do you really want to close all tabs?");
		var owner = System.Windows.Application.Current.MainWindow;
		if (MessageDialogWindow.Show(owner, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) !=
		    MessageBoxResult.Yes)
			return;

		LogTabs.Clear();
		SelectedTab = null;
	}

	/// <summary>
	/// Moves the given tab next to a target tab (e.g. after a header drag-and-drop).
	/// </summary>
	/// <param name="source">Tab being dragged.</param>
	/// <param name="target">Tab header that was dropped on.</param>
	/// <param name="insertBefore">
	/// When true, the source is inserted before the target; when false, after the target.
	/// </param>
	public void MoveTab(LogTabViewModel source, LogTabViewModel target, bool insertBefore)
	{
		if (source == null || target == null || ReferenceEquals(source, target))
			return;

		var oldIndex = LogTabs.IndexOf(source);
		var targetIndex = LogTabs.IndexOf(target);
		if (oldIndex < 0 || targetIndex < 0)
			return;

		var newIndex = insertBefore ? targetIndex : targetIndex + 1;
		if (oldIndex < newIndex)
			newIndex--;

		if (oldIndex == newIndex)
			return;

		newIndex = Math.Clamp(newIndex, 0, LogTabs.Count - 1);
		LogTabs.Move(oldIndex, newIndex);
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

	/// <summary>
	/// The sources that receive logs, for the panel of the status bar
	/// </summary>
	public ObservableCollection<ReceiveSourceViewModel> Sources { get; } = new();

	/// <summary>
	/// What the status bar says about receiving, e.g. "Receiving · 2 of 3 sources"
	/// </summary>
	public string ReceiveStatusText
	{
		get => _receiveStatusText;
		private set
		{
			if (_receiveStatusText != value)
			{
				_receiveStatusText = value;
				OnPropertyChanged();
			}
		}
	}

	public ReceiveStatus ReceiveStatus
	{
		get => _receiveStatus;
		private set
		{
			if (_receiveStatus != value)
			{
				_receiveStatus = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Whether the panel with the sources is open above the status bar
	/// </summary>
	public bool IsSourcesPanelOpen
	{
		get => _sourcesPanelOpen;
		set
		{
			if (_sourcesPanelOpen == value)
				return;

			_sourcesPanelOpen = value;
			if (value)
			{
				RefreshSources();
				_sourcesTimer.Start();
			}
			else
			{
				_sourcesTimer.Stop();
			}

			OnPropertyChanged();
		}
	}

	/// <summary>
	/// What the status bar says about the selected tab, e.g. "Entries: 1,204 of 27,851 · Levels off: 2 · Terms: 1"; empty without a tab
	/// </summary>
	public string TabSummary
	{
		get => _tabSummary;
		private set
		{
			if (_tabSummary != value)
			{
				_tabSummary = value;
				OnPropertyChanged();
			}
		}
	}

	/// <summary>
	/// Whether the selected tab has a filter: a level that is hidden, a term or a rule for loggers
	/// </summary>
	public bool HasActiveFilter
	{
		get => _hasActiveFilter;
		private set
		{
			if (_hasActiveFilter != value)
			{
				_hasActiveFilter = value;
				OnPropertyChanged();
				((RelayCommand)ResetFilterCommand).RaiseCanExecuteChanged();
			}
		}
	}

	/// <summary>
	/// Drops the logs of the application of the tab, from any IP address, from now on and closes its tabs; the parameter is the <see cref="LogTabViewModel"/>
	/// </summary>
	public ICommand IgnoreApplicationCommand { get; }

	/// <summary>
	/// Drops the logs of the application of the tab from the IP address of the tab only, and closes that tab
	/// </summary>
	public ICommand IgnoreApplicationFromSenderCommand { get; }

	/// <summary>
	/// Drops the logs of every application from the IP address of the tab and closes the tabs that come from it
	/// </summary>
	public ICommand IgnoreSenderCommand { get; }

	private static bool HasIpAddress(LogTabViewModel? tab) =>
		tab?.SenderKey != null && System.Net.IPAddress.TryParse(IgnoreRule.IpOf(tab.Sender), out _);

	private bool IsIgnored(LogEvent logEvent)
	{
		foreach (var rule in _ignoreRules)
		{
			if (rule.Matches(logEvent.AppInfo.AppName?.Name, logEvent.AppInfo.Sender))
				return true;
		}

		return false;
	}

	private void ConfirmAndIgnore(LogTabViewModel? tab, IgnoreScope scope)
	{
		if (tab == null)
			return;

		var ip = IgnoreRule.IpOf(tab.Sender);
		var (key, text) = scope switch
		{
			IgnoreScope.Application => ("Ignore_ConfirmApplication", "Ignore the application “{0}” from any IP address? Its tabs are closed and its logs are dropped from now on."),
			IgnoreScope.ApplicationFromSender => ("Ignore_ConfirmApplicationFromSender", "Ignore the application “{0}” from the IP address {1}? Its tab is closed and its logs are dropped from now on."),
			_ => ("Ignore_ConfirmSender", "Ignore the IP address {1}? Its tabs are closed and its logs are dropped from now on.")
		};
		var message = string.Format(CultureInfo.CurrentCulture, _localizationService.GetString(key, text), tab.ApplicationName, ip);
		var owner = System.Windows.Application.Current.MainWindow;
		if (MessageDialogWindow.Show(owner, message, _localizationService.GetString("Ignore_ConfirmCaption", "Ignore sender"),
			    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			Ignore(tab, scope);
	}

	/// <summary>
	/// Adds a rule for the tab, saves it, and closes the tabs that it matches
	/// </summary>
	public void Ignore(LogTabViewModel tab, IgnoreScope scope)
	{
		var ip = IgnoreRule.IpOf(tab.Sender);
		var rule = scope switch
		{
			IgnoreScope.Application => new IgnoreRule { Application = tab.ApplicationName },
			IgnoreScope.ApplicationFromSender => new IgnoreRule { Application = tab.ApplicationName, Ip = ip },
			_ => new IgnoreRule { Ip = ip }
		};
		var configuration = _configService.LoadConfiguration();
		if (!configuration.IgnoreRules.Any(existing => string.Equals(existing.Ip, rule.Ip, StringComparison.OrdinalIgnoreCase)
		                                              && string.Equals(existing.Application, rule.Application, StringComparison.OrdinalIgnoreCase)))
		{
			configuration.IgnoreRules.Add(rule);
			_configService.SaveConfiguration(configuration);
		}

		_ignoreRules = configuration.IgnoreRules.ToArray();
		foreach (var matching in LogTabs.Where(candidate => candidate.IsLive && rule.Matches(candidate.ApplicationName, candidate.Sender)).ToList())
			CloseTab(matching);
	}

	/// <summary>
	/// Takes the levels, terms and logger rules off the selected tab; the fields and the time stamp format stay
	/// </summary>
	public ICommand ResetFilterCommand { get; }

	private void ResetFilter()
	{
		if (SelectedTab is not { } tab)
			return;

		var current = tab.FilterState;
		tab.FilterState = new LogViewerFilterState
		{
			SearchFields = current?.SearchFields ?? SearchFields.Default,
			TimeStampFormat = current?.TimeStampFormat
		};
	}

	private void FollowSummaryOfSelectedTab()
	{
		if (_summaryTab != null)
			_summaryTab.PropertyChanged -= OnSummaryTabChanged;

		_summaryTab = _selectedTab;
		if (_summaryTab != null)
			_summaryTab.PropertyChanged += OnSummaryTabChanged;

		UpdateTabSummary();
	}

	private void OnSummaryTabChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(LogTabViewModel.LogCount) or nameof(LogTabViewModel.VisibleEntryCount) or nameof(LogTabViewModel.FilterState))
			UpdateTabSummary();
	}

	private void UpdateTabSummary()
	{
		if (_summaryTab is not { } tab)
		{
			TabSummary = string.Empty;
			HasActiveFilter = false;
			return;
		}

		var state = tab.FilterState;
		var levels = state?.HiddenLevels.Count ?? 0;
		var terms = state?.Terms.Count ?? 0;
		var loggers = state?.LoggerRules.Count ?? 0;
		var filtered = levels + terms + loggers > 0;

		string Text(string key, string defaultValue, params object[] arguments) =>
			string.Format(CultureInfo.CurrentCulture, _localizationService.GetString(key, defaultValue), arguments);

		var parts = new List<string>
		{
			filtered
				? Text("Summary_EntriesOf", "Entries: {0:N0} of {1:N0}", tab.VisibleEntryCount, tab.LogCount)
				: Text("Summary_Entries", "Entries: {0:N0}", tab.LogCount)
		};
		if (levels > 0)
			parts.Add(Text("Summary_LevelsOff", "Levels off: {0}", levels));
		if (terms > 0)
			parts.Add(Text("Summary_Terms", "Terms: {0}", terms));
		if (loggers > 0)
			parts.Add(Text("Summary_Loggers", "Logger rules: {0}", loggers));

		TabSummary = string.Join(" · ", parts);
		HasActiveFilter = filtered;
	}

	/// <summary>
	/// Starts all sources, or stops them when some receive
	/// </summary>
	public ICommand ToggleReceivingCommand { get; }

	public ICommand ToggleSourcesPanelCommand { get; }

	/// <summary>
	/// Opens the settings where the addresses are
	/// </summary>
	public ICommand ManageSourcesCommand { get; }

	private void OnSourcesChanged(object? sender, EventArgs e)
	{
		var dispatcher = System.Windows.Application.Current?.Dispatcher;
		if (dispatcher == null || dispatcher.CheckAccess())
			RefreshSources();
		else
			dispatcher.InvokeAsync(RefreshSources);
	}

	/// <summary>
	/// Makes the rows of the panel the sources of the receiver and works out what the status bar says
	/// </summary>
	private void RefreshSources()
	{
		var current = _udpReceiverService.Sources;
		for (var i = Sources.Count - 1; i >= 0; i--)
		{
			if (!current.Contains(Sources[i].Source))
				Sources.RemoveAt(i);
		}

		for (var i = 0; i < current.Count; i++)
		{
			var existing = Sources.FirstOrDefault(row => ReferenceEquals(row.Source, current[i]));
			if (existing == null)
			{
				Sources.Insert(i, new ReceiveSourceViewModel(current[i], _udpReceiverService, _localizationService));
				continue;
			}

			var index = Sources.IndexOf(existing);
			if (index != i)
				Sources.Move(index, i);

			existing.Refresh();
		}

		var listening = current.Count(source => source.State == ReceiveSourceState.Listening);
		IsListening = listening > 0;
		if (current.Count == 0)
		{
			ReceiveStatus = ReceiveStatus.Stopped;
			ReceiveStatusText = _localizationService.GetString("Receive_NoSources", "No sources");
		}
		else if (listening == 0)
		{
			if (current.Any(source => source.State == ReceiveSourceState.Failed))
			{
				ReceiveStatus = ReceiveStatus.Failed;
				ReceiveStatusText = _localizationService.GetString("Receive_Failed", "Could not start");
			}
			else if (_hasStarted)
			{
				ReceiveStatus = ReceiveStatus.Stopped;
				ReceiveStatusText = _localizationService.GetString("Receive_Stopped", "Stopped");
			}
			else
			{
				ReceiveStatus = ReceiveStatus.Ready;
				ReceiveStatusText = _localizationService.GetString("Receive_Ready", "Ready");
			}
		}
		else if (listening == current.Count)
		{
			ReceiveStatus = ReceiveStatus.Active;
			ReceiveStatusText = current.Count == 1
				? _localizationService.GetString("Receive_Active", "Listening")
				: string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("Receive_ActiveMany", "Listening · {0} sources"), current.Count);
		}
		else
		{
			ReceiveStatus = ReceiveStatus.Partial;
			ReceiveStatusText = string.Format(CultureInfo.CurrentCulture,
				_localizationService.GetString("Receive_ActivePartial", "Listening · {0} of {1} sources"), listening, current.Count);
		}
	}

	/// <summary>
	/// The card at the bottom of the window for hints and errors
	/// </summary>
	public SnackbarViewModel Snackbar { get; } = new();

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
	public ICommand OpenFileWithMappingCommand { get; }
	public ICommand OpenSettingsCommand { get; }

	/// <summary>
	/// The saved filter presets, for the menu
	/// </summary>
	public ObservableCollection<FilterPreset> FilterPresets { get; } = new();

	public bool HasFilterPresets => FilterPresets.Count > 0;

	public ICommand ManageFilterPresetsCommand { get; }
	public ICommand SaveFilterPresetCommand { get; }

	/// <summary>
	/// Applies a preset to the selected tab; the parameter is the <see cref="FilterPreset"/>
	/// </summary>
	public ICommand ApplyFilterPresetCommand { get; }
	public ICommand ExitCommand { get; }
	public ICommand AboutCommand { get; }

	/// <summary>
	/// Status bar text announcing an available update, or null
	/// </summary>
	public string? UpdateNotice
	{
		get => _updateNotice;
		private set
		{
			if (_updateNotice != value)
			{
				_updateNotice = value;
				OnPropertyChanged();
			}
		}
	}
	public ICommand ChangeLanguageCommand { get; }
	public ICommand ExportLogsCommand { get; }
	public ICommand CloseTabCommand { get; }

	/// <summary>Switches whether new tabs of the application of the tab take over its filters; the parameter is the tab</summary>
	public ICommand ToggleTakeOverFiltersCommand { get; }

	/// <summary>Removes the filters that the tab took over from an earlier tab; the parameter is the tab</summary>
	public ICommand RemoveTakenOverFiltersCommand { get; }
	public ICommand CloseOtherTabsCommand { get; }
	public ICommand SaveFilteredListCommand { get; }
	public ICommand CloseCurrentTabCommand { get; }

	/// <summary>Closes the tabs to the right of the tab; the parameter is the tab</summary>
	public ICommand CloseTabsToTheRightCommand { get; }

	public ICommand SelectNextTabCommand { get; }
	public ICommand SelectPreviousTabCommand { get; }

	/// <summary>Selects a tab by its number, 1 to 9 (9 is the last tab); the parameter is the number</summary>
	public ICommand SelectTabCommand { get; }
	public ICommand CloseAllTabsCommand { get; }

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
		DefaultTimeStampFormat = TimeStampFormats.Parse(config.TimeStampFormat);
		_listFontFamilyName = config.ListFontFamily;
		_listFontSize = config.ListFontSize;
		_fontZoom = 0;
		NotifyFont();
		_udpReceiverService.Configure(config.Ports);
		_ignoreRules = config.IgnoreRules.ToArray();

		if (config.CheckForUpdates && UpdateNotice == null)
			_ = CheckForUpdatesInBackgroundAsync(UpdateService.IncludesPrereleases(config));
            
		if (config.AutoStartListening)
		{
			Task.Run(async () =>
			{
				await Task.Delay(500); // Small delay to ensure UI is ready
				System.Windows.Application.Current.Dispatcher.Invoke(() => { _ = StartListeningAsync(); });
			});
		}
	}

	/// <summary>
	/// Starts all sources. A source that cannot be started (e.g. the port is in use) shows why in the panel; when none can be started,
	/// a message says so.
	/// </summary>
	private async Task StartListeningAsync()
	{
		try
		{
			_hasStarted = true;
			var config = _configService.LoadConfiguration();
			var result = await _udpReceiverService.StartListeningAsync(config.Ports);
			RefreshSources();

			if (!result.AnyStarted && !string.IsNullOrEmpty(result.ErrorMessage))
			{
				var caption = _localizationService.GetString("Error_StartingListenerCaption", "Error starting listener");
				MessageDialogWindow.Show(null, result.ErrorMessage, caption, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		catch (Exception ex)
		{
			RefreshSources();
			var caption = _localizationService.GetString("Error_StartingListenerCaption", "Error starting listener");
			MessageDialogWindow.Show(null, ex.Message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void StopListening()
	{
		try
		{
			_udpReceiverService.StopListening();
			RefreshSources();
		}
		catch (Exception ex)
		{
			Snackbar.ShowError(_localizationService.GetString("Error_StoppingListener", $"Error stopping listener: {ex.Message}"));
		}
	}

	private void OpenFile(bool askColumnMapping)
	{
		var dialog = new OpenFileDialog
		{
			Filter = "Log Files (*.xml;*.log;*.txt;*.json)|*.xml;*.log;*.txt;*.json|XML Files (*.xml)|*.xml|Text Files (*.txt;*.log)|*.txt;*.log|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
			Title = _localizationService.GetString("Import_OpenTitle", "Open Log File"),
			Multiselect = true
		};

		if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
			BeginImportPathsFromUi(dialog.FileNames, askColumnMapping);
	}

	/// <summary>
	/// Starts unified import for paths given by the open-file dialog or drag-drop (filters and asks destination).
	/// </summary>
	/// <param name="rawPaths">Paths to import</param>
	/// <param name="askColumnMapping">Whether to show the column mapping dialog for text files</param>
	public void BeginImportPathsFromUi(IEnumerable<string> rawPaths, bool askColumnMapping = false)
	{
		var filtered = (rawPaths ?? Enumerable.Empty<string>())
			.Where(IsSupportedImportPath)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (filtered.Count == 0)
		{
			Snackbar.Show(_localizationService.GetString("Import_NoSupportedFiles", "No supported log files were found."));
			return;
		}

		if (IsLoading)
			return;

		if (!TryResolveImportBatchContext(filtered, out var ctx) || ctx == null)
			return;

		ctx.AskColumnMapping = askColumnMapping;
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
				Snackbar.ShowError(string.Format(System.Globalization.CultureInfo.CurrentCulture, tpl, pathName, ex.Message));
			}
			}
		}
		finally
		{
			IsLoading = false;
			LoadingProgress = string.Empty;
		}
	}

	/// <summary>
	/// Parses a single path in a batch, asking for the column mapping first when requested.
	/// </summary>
	private async Task ImportSinglePathAsync(string filePath, LogFileImportBatchContext ctx, int index, int total)
	{
		var extension = Path.GetExtension(filePath).ToLowerInvariant();
		var isTextFile = extension == ".txt" || extension == ".log" || extension == "";
		var format = ctx.AskColumnMapping && isTextFile
			? await AskColumnMappingAsync(filePath).ConfigureAwait(true)
			: null;

		await ImportParseFlowAsync(filePath, ctx, index, total, format).ConfigureAwait(true);
	}

	/// <summary>
	/// Shows the column-mapping dialog, preset with the saved or detected format.
	/// </summary>
	/// <returns>The format chosen by the user, or null when the dialog was cancelled</returns>
	private async Task<TextFileFormat?> AskColumnMappingAsync(string filePath)
	{
		var dispatcher = System.Windows.Application.Current.Dispatcher;
		TextFileFormat? format = null;
		try
		{
			format = await Task.Run(() => _fileParserService.GetTextFileFormat(filePath)).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await dispatcher.InvokeAsync(() =>
			{
				Snackbar.ShowError(_localizationService.GetString("Import_FormatDetectError", "Error detecting file format") + $": {ex.Message}");
			}).Task.ConfigureAwait(false);
		}

		// Nothing detected: start with a tab separator and let the user pick the right one.
		format ??= new TextFileFormat { Separator = "\t" };

		var sampleLines =
			await Task.Run(() => File.ReadLines(filePath).Take(20).ToList()).ConfigureAwait(false);

		return await dispatcher.InvokeAsync(() =>
		{
			var viewModel = new ColumnMappingViewModel(format, sampleLines, filePath, _localizationService);
			var mappingWindow = new ColumnMappingWindow(viewModel);
			var mainWindow = System.Windows.Application.Current.MainWindow;

			if (mappingWindow.ShowDialog(mainWindow) != true)
				return null;

			var userFormat = mappingWindow.FinalFormat;

			if (mappingWindow.SaveForPattern && !string.IsNullOrEmpty(mappingWindow.FilePattern))
				_fileParserService.SaveTextFileFormat(mappingWindow.FilePattern, userFormat);

			return userFormat;
		}).Task.ConfigureAwait(false);
	}

	/// <summary>
	/// Parses the file and applies the result to the batch target.
	/// </summary>
	/// <param name="textFormat">Format for text files; when null the saved or detected format is used</param>
	private async Task ImportParseFlowAsync(string filePath, LogFileImportBatchContext ctx, int index, int total,
		TextFileFormat? textFormat)
	{
		var dispatcher = System.Windows.Application.Current.Dispatcher;

		var progress = new Progress<(int current, int total)>(p =>
		{
			var percentage = p.total > 0 ? p.current * 100 / p.total : 0;
			dispatcher.Invoke(() => { LoadingProgress = FormatParseProgress(filePath, index, total, p.current, p.total, percentage); });
		});

		List<LogEntry> logEvents;
		try
		{
			logEvents = await _fileParserService.ParseFileAsync(filePath, progress, textFormat).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await dispatcher.InvokeAsync(() =>
			{
				Snackbar.ShowError(_localizationService.GetString("Import_ParseError", "Error parsing file") + $": {ex.Message}");
			}).Task.ConfigureAwait(false);
			return;
		}

		await dispatcher.InvokeAsync(() =>
		{
			ApplyParsedLogs(logEvents, filePath, ctx);
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

	/// <summary>
	/// Appends parsed rows to the tab chosen for this import batch.
	/// </summary>
	private void ApplyParsedLogs(List<LogEntry> logEvents, string filePath, LogFileImportBatchContext ctx)
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
				tab.AddLogEntry(logEvent);

			if (i + batchSize < logEvents.Count)
			{
				var progress = (i + batchSize) * 100 / logEvents.Count;
				LoadingProgress = string.Format(System.Globalization.CultureInfo.CurrentCulture,
					_localizationService.GetString("Import_ApplyProgress_Format", "Adding to view: {0}%"), progress);
			}
		}

		LastLogTimestamp = DateTime.Now.ToString("HH:mm:ss");
	}

	private void OpenSettings(string? categoryId = null)
	{
		var provider = App.ServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
		using var scope = provider.CreateScope();
		var settingsWindow = scope.ServiceProvider.GetRequiredService<SettingsWindow>();
		if (categoryId != null)
			settingsWindow.ShowCategory(categoryId);

		settingsWindow.ShowDialog(System.Windows.Application.Current.MainWindow);

		// Settings are saved as they change, so the window has nothing to confirm.
		if (settingsWindow.HasChanges)
		{
			LoadConfiguration();
			RefreshFilterPresets();
		}
	}

	/// <summary>
	/// Reads the saved presets again, e.g. after they were changed in the settings
	/// </summary>
	public void RefreshFilterPresets()
	{
		FilterPresets.Clear();
		foreach (var preset in _filterPresets.GetAll())
			FilterPresets.Add(preset);

		OnPropertyChanged(nameof(HasFilterPresets));
	}

	/// <summary>
	/// Asks for a name and saves the filters of the selected tab as a preset
	/// </summary>
	private void SaveFilterPreset()
	{
		if (SelectedTab == null)
			return;

		var owner = System.Windows.Application.Current.MainWindow;
		var caption = _localizationService.GetString("FilterPreset_SaveTitle", "Save filter preset");
		var name = FilterPresetService.CleanName(TextPromptWindow.Show(owner,
			_localizationService.GetString("FilterPreset_SaveMessage", "Name of the preset:"), caption));
		if (name.Length == 0)
			return;

		if (_filterPresets.Exists(name))
		{
			var question = string.Format(CultureInfo.CurrentCulture,
				_localizationService.GetString("FilterPreset_Overwrite", "A preset named “{0}” exists. Replace it?"), name);
			if (MessageDialogWindow.Show(owner, question, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
		}

		_filterPresets.Save(name, SelectedTab.FilterState ?? new LogViewerFilterState());
		RefreshFilterPresets();
	}

	/// <summary>
	/// Sets the filters of the selected tab to the ones of the preset. The format of the time stamp stays, and the fields of the preset
	/// are not saved as the default of new tabs: only a choice that the user makes in a tab does that.
	/// </summary>
	private void ApplyFilterPreset(FilterPreset? preset)
	{
		if (SelectedTab is not { } tab || preset == null)
			return;

		var filters = preset.Filters.Clone();
		filters.TimeStampFormat = tab.FilterState?.TimeStampFormat;

		_applyingFilterPreset = true;
		try
		{
			tab.FilterState = filters;
		}
		finally
		{
			_applyingFilterPreset = false;
		}

	}

	/// <summary>
	/// Opens the changelog when the application was updated since the last start, and remembers the version that runs now.
	/// Builds that are not releases (of a branch) neither open it nor count as the version that ran last.
	/// </summary>
	public void AnnounceUpdate()
	{
		var current = UpdateService.CurrentVersion;
		if (!WhatsNew.IsReleaseVersion(current))
			return;

		var configuration = _configService.LoadConfiguration();
		var previous = WhatsNew.PreviousVersionToShow(configuration, current);
		var currentText = current.ToString();
		if (configuration.LastSeenVersion != currentText)
		{
			configuration.LastSeenVersion = currentText;
			_configService.SaveConfiguration(configuration);
		}

		if (previous != null)
			ChangelogWindow.Show(System.Windows.Application.Current.MainWindow, previous);
	}

	private void ShowAbout()
	{
		var provider = App.ServiceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");
		var aboutWindow = provider.GetRequiredService<AboutWindow>();
		aboutWindow.ShowDialog(System.Windows.Application.Current.MainWindow);
	}

	/// <summary>
	/// Shows a status bar notice when GitHub has a newer release. Failures are ignored; the About dialog reports them.
	/// </summary>
	private async Task CheckForUpdatesInBackgroundAsync(bool includePrereleases)
	{
		try
		{
			var update = await _updateService.CheckForUpdateAsync(includePrereleases);
			if (update != null)
			{
				UpdateNotice = string.Format(System.Globalization.CultureInfo.CurrentCulture,
					_localizationService.GetString("Status_UpdateAvailable", "Update to {0} available"), update.Version);
			}
		}
		catch (Exception)
		{
			// Offline or rate limited: no notice.
		}
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
			Snackbar.Show(_localizationService.GetString("Message_LanguageChanged", "Language changed. Restart the application for the change to take full effect."));
		}
		catch (Exception ex)
		{
			Snackbar.ShowError(string.Format(CultureInfo.CurrentCulture,
				_localizationService.GetString("Message_LanguageChangeError", "Could not change the language: {0}"), ex.Message));
		}
	}

	/// <summary>
	/// Exports filtered log entries from the currently selected tab to a file
	/// </summary>
	private void ExportLogs(LogTabViewModel? tab)
	{
		if (tab == null)
			return;

		// Generate default filename from tab header (remove whitespaces) + timestamp
		var headerWithoutSpaces = tab.Header?.Replace(" ", string.Empty) ?? "Logs";
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
			var logViewer = FindLogViewerInTab(tab);

			if (logViewer == null)
			{
				Snackbar.ShowError(_localizationService.GetString("Message_ViewerNotFound", "The log view of the tab could not be found."));
				return;
			}

			var exportParameter = new ExportParameter
			{
				FilePath = filePath,
				Format = ExportFormat.Log
			};

			// Execute the export command on the LogViewer
			if (logViewer.ExportCommand?.CanExecute(exportParameter) == true)
			{
				logViewer.ExportCommand.Execute(exportParameter);
				Snackbar.Show(string.Format(CultureInfo.CurrentCulture,
					_localizationService.GetString("Message_Exported", "Exported to {0}"), Path.GetFileName(filePath)));
			}
			else
			{
				Snackbar.ShowError(_localizationService.GetString("Message_ExportUnavailable", "Export is not available for this tab."));
			}
		}
	}

	/// <summary>
	/// Finds the LogViewer instance in the currently selected tab's visual tree
	/// </summary>
	/// <returns>The LogViewer instance if found, null otherwise</returns>
	private Wpf.LogViewer? FindLogViewerInTab(LogTabViewModel tab)
	{
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
			if (contentControl != null && contentControl.DataContext == tab)
			{
				// Found the content control for the selected tab, now find LogViewer
				var logViewer = FindVisualChild<Wpf.LogViewer>(contentControl);
				if (logViewer != null)
					return logViewer;
			}
		}

		// Fallback: Search directly in TabControl for LogViewer; only the selected tab's viewer is certain to be there
		return ReferenceEquals(tab, SelectedTab) ? FindVisualChild<Wpf.LogViewer>(tabControl) : null;
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
		LogTabs.CollectionChanged -= OnLogTabsCollectionChanged;
		_udpReceiverService.SourcesChanged -= OnSourcesChanged;
		_sourcesTimer.Stop();
		StopListening();
		//_udpReceiverService.LogReceived -= OnLogReceived;
		//_fileParserService.LogParsed -= OnLogReceived;
		_udpReceiverService?.Dispose();
		_fileParserService?.Dispose();
		_subscriptions.Dispose();
	}
}