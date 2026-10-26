using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels;
using Sentinel.LogViewer.Wpf;
using Xunit;
using static Sentinel.LogViewer.Tests.WpfTestHelper;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests of the tabs in the main window: the filters that a new tab takes over, what a restart does, and the logger tree of new tabs
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class TabsInTheMainWindowTests : IDisposable
{
	private readonly WpfStaContextFixture _context;
	private readonly SettingsTestEnvironment _environment = new();

	public TabsInTheMainWindowTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	public void Dispose() => _environment.Dispose();

	private sealed class Session : IDisposable
	{
		public required MainWindow Window { get; init; }
		public required MainViewModel ViewModel { get; init; }
		public required Subject<LogEvent> Events { get; init; }
		public required ApplicationTabService Tabs { get; init; }

		public void Dispose() => Window.Close();
	}

	/// <summary>
	/// The resources that App.xaml adds to the ones of the test application
	/// </summary>
	private static void AddResourcesOfTheApplication()
	{
		var resources = Application.Current.Resources;
		if (resources.Contains("SvgImageSourceConverter"))
			return;

		resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("pack://application:,,,/Sentinel.LogViewer.Wpf;component/Themes/Generic.xaml")
		});
		resources.Add("BoolToVisibilityConverter", new Sentinel.LogViewer.Wpf.XamlConverter.BoolToVisibilityConverter());
		resources.Add("ILogEntryResolverToStringConverter", new Sentinel.LogViewer.Wpf.XamlMultiValueConverter.ILogEntryResolverToStringConverter());
		resources.Add("SvgImageSourceConverter", new Sentinel.LogViewer.Converters.SvgImageSourceConverter());
	}

	/// <param name="invisible">For tests that minimize or maximize the window: it has no frame and is fully transparent, so it neither shows nor catches clicks on the screen of the user</param>
	private Session Start(UpdateService? updates = null, ITrayIcon? tray = null, bool invisible = false)
	{
		AddResourcesOfTheApplication();
		var xml = new Log4JEventParser();
		var receiver = new LogReceiverService(xml);
		var formats = _environment.Formats;
		var files = new LogFileParserService(xml, new PlainTextParser(), new JsonLogParser(), new TextFileFormatDetector(), formats);
		var tabs = new ApplicationTabService(_environment.Configuration);
		var viewModel = new MainViewModel(receiver, files, _environment.Configuration, _environment.Localization, updates ?? new UpdateService(), tabs);
		var events = (Subject<LogEvent>)typeof(LogReceiverService)
			.GetField("_log4JEventObservable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(receiver)!;
		var window = new MainWindow(viewModel, tray) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, WindowState = System.Windows.WindowState.Normal, Width = 900, Height = 500 };
		if (invisible)
		{
			window.WindowStyle = System.Windows.WindowStyle.None;
			window.AllowsTransparency = true;
			window.Opacity = 0;
		}

		window.Show();
		WpfTestHelper.Settle();
		return new Session { Window = window, ViewModel = viewModel, Events = events, Tabs = tabs };
	}

	private static AppInfo App(string processId, string name = "MyApp", string sender = "10.0.0.5") =>
		new() { AppName = new AppName(name, processId), Sender = sender };

	private static void Send(Session session, AppInfo app, string message = "hello", LogEntryLevel? level = null)
	{
		session.Events.OnNext(new LogEvent { AppInfo = app, Entry = new LogEntry(level ?? LogEntryLevel.Info, "Acme.Logger", message) });
		for (var i = 0; i < 10; i++)
		{
			Thread.Sleep(60);
			WpfTestHelper.Settle();
		}
	}

	private static Wpf.LogViewer ViewerOf(Session session, LogTabViewModel tab)
	{
		session.ViewModel.SelectedTab = tab;
		WpfTestHelper.Settle();
		return Descendants<Wpf.LogViewer>(session.Window).First(viewer => ReferenceEquals(viewer.ItemsSource, tab.LogEntries));
	}

	[Fact]
	public void ANewTabOfARestartedApplication_TakesOverTheFilters_AndTheyCanBeRemoved()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var first = Assert.Single(session.ViewModel.LogTabs);
			var firstViewer = ViewerOf(session, first);
			Assert.False(first.FiltersTakenOver);

			firstViewer.TraceFilter = true;
			firstViewer.AddRegexSearchTermCommand.Execute("timeout");
			WpfTestHelper.Settle();

			Send(session, App("(200)"));

			Assert.Equal(2, session.ViewModel.LogTabs.Count);
			var second = session.ViewModel.LogTabs[1];
			Assert.True(second.FiltersTakenOver);
			var secondViewer = ViewerOf(session, second);
			Assert.True(secondViewer.TraceFilter);
			Assert.Equal("timeout", Assert.Single(secondViewer.ActiveSearchTerms).Text);
			Assert.Equal("timeout", Assert.Single(second.FilterState!.Terms).Text);

			session.ViewModel.RemoveTakenOverFiltersCommand.Execute(second);
			WpfTestHelper.Settle();

			Assert.False(second.FiltersTakenOver);
			Assert.False(secondViewer.TraceFilter);
			Assert.Empty(secondViewer.ActiveSearchTerms);
			Assert.Null(session.Tabs.FiltersForNewTab(second.SenderKey!));
		});
	}

	[Fact]
	public void ANewTab_StartsWithTheLoggerTreeOfTheSettings()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.LoggerTreeOnLeft = true;
			configuration.LoggerTreeOpen = true;
			configuration.LoggerTreeWidth = 333;
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();

			Send(session, App("(100)"));

			var viewer = ViewerOf(session, session.ViewModel.LogTabs[0]);
			Assert.True(viewer.IsLoggerTreeOnLeft);
			Assert.True(viewer.IsLoggerTreeExpanded);
			Assert.Equal(333, viewer.LoggerTreeWidth);
		});
	}

	[Fact]
	public void WhatTheUserChangesInATab_IsSaved_AndUsedByTheNextTab_WhileOpenTabsStayAsTheyAre()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var first = ViewerOf(session, session.ViewModel.LogTabs[0]);
			Assert.False(first.IsLoggerTreeOnLeft);

			first.IsLoggerTreeOnLeft = true;
			first.IsLoggerTreeExpanded = true;
			first.LoggerTreeWidth = 301.6;

			var saved = _environment.Saved;
			Assert.True(saved.LoggerTreeOnLeft);
			Assert.True(saved.LoggerTreeOpen);
			Assert.Equal(302, saved.LoggerTreeWidth);

			Send(session, App("(100)", name: "Orders"));
			var second = ViewerOf(session, session.ViewModel.LogTabs[1]);
			Assert.True(second.IsLoggerTreeOnLeft);
			Assert.True(second.IsLoggerTreeExpanded);
			Assert.Equal(302, second.LoggerTreeWidth);

			second.IsLoggerTreeOnLeft = false;
			Assert.True(ViewerOf(session, session.ViewModel.LogTabs[0]).IsLoggerTreeOnLeft);
			Assert.False(_environment.Saved.LoggerTreeOnLeft);
		});
	}

	[Fact]
	public void TheSearchFields_StartAsTheSettingsSay_AndWhatTheUserChoosesInATab_IsSaved()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.SearchFields = (SearchFields.Message | SearchFields.Thread).ToString();
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();

			Send(session, App("(100)"));
			var first = ViewerOf(session, session.ViewModel.LogTabs[0]);
			Assert.Equal(SearchFields.Message | SearchFields.Thread, first.SearchFields);

			first.SearchFields = SearchFields.All;
			Assert.Equal(SearchFields.All, SettingsRules.ParseSearchFields(_environment.Saved.SearchFields));

			Send(session, App("(100)", name: "Orders"));
			var second = ViewerOf(session, session.ViewModel.LogTabs[1]);
			Assert.Equal(SearchFields.All, second.SearchFields);
			Assert.Equal(SearchFields.All, ViewerOf(session, session.ViewModel.LogTabs[0]).SearchFields);
		});
	}

	[Fact]
	public void AFilterPreset_IsAppliedToTheSelectedTab_WithoutChangingTheDefaultFieldsOrTheTimeStampFormat()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var viewer = ViewerOf(session, session.ViewModel.LogTabs[0]);
			viewer.CustomTimeStampFormat = TimeStampFormat.German;
			var service = new FilterPresetService(_environment.Configuration);
			service.Save("Timeouts", new LogViewerFilterState
			{
				HiddenLevels = { "Debug" },
				Terms = { new LogViewerFilterTerm { Text = "timeout", IsExclude = true, MatchCase = true } },
				SearchFields = SearchFields.All,
				TimeStampFormat = TimeStampFormat.Iso
			});
			session.ViewModel.RefreshFilterPresets();
			var preset = Assert.Single(session.ViewModel.FilterPresets);

			session.ViewModel.ApplyFilterPresetCommand.Execute(preset);

			Assert.True(viewer.DebugFilter);
			var term = Assert.Single(viewer.ActiveSearchTerms);
			Assert.Equal(("timeout", true, true), (term.Text, term.IsExclude, term.MatchCase));
			Assert.Equal(SearchFields.All, viewer.SearchFields);
			Assert.Equal(TimeStampFormat.German, viewer.CustomTimeStampFormat);
			Assert.Equal("Default", _environment.Saved.SearchFields);

			// A choice that the user makes in the tab is still saved as the default.
			viewer.SearchFields = SearchFields.Message;
			Assert.Equal("Message", _environment.Saved.SearchFields);
		});
	}

	private static int FreeUdpPort()
	{
		using var probe = new System.Net.Sockets.UdpClient(0);
		return ((System.Net.IPEndPoint)probe.Client.LocalEndPoint!).Port;
	}

	[Fact]
	public void TheStatusBar_SaysWhatReceives_AndEverySourceCanBeSwitchedOnAndOff()
	{
		_context.RunOnSta(() =>
		{
			var busy = new System.Net.Sockets.UdpClient(0);
			var busyPort = ((System.Net.IPEndPoint)busy.Client.LocalEndPoint!).Port;
			var configuration = _environment.Saved;
			configuration.Ports = new() { $"udp://0.0.0.0:{FreeUdpPort()}", $"udp://0.0.0.0:{FreeUdpPort()}", $"udp://0.0.0.0:{busyPort}" };
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();
			var viewModel = session.ViewModel;
			Assert.Equal(("Ready", ReceiveStatus.Ready), (viewModel.ReceiveStatusText, viewModel.ReceiveStatus));
			Assert.Equal(3, viewModel.Sources.Count);
			Assert.False(viewModel.IsSourcesPanelOpen);

			viewModel.StartListeningCommand.Execute(null);
			WpfTestHelper.Settle();

			Assert.Equal(("Listening · 2 of 3 sources", ReceiveStatus.Partial), (viewModel.ReceiveStatusText, viewModel.ReceiveStatus));
			var failed = viewModel.Sources[2];
			Assert.True(failed.CanRetry);
			Assert.StartsWith("Port in use", failed.StateText);
			Assert.False(failed.IsOn);

			viewModel.Sources[0].IsOn = false;
			Assert.Equal("Listening · 1 of 3 sources", viewModel.ReceiveStatusText);

			busy.Close();
			failed.RetryCommand.Execute(null);
			Assert.Equal("Listening · 2 of 3 sources", viewModel.ReceiveStatusText);
			viewModel.Sources[0].IsOn = true;
			Assert.Equal(("Listening · 3 sources", ReceiveStatus.Active), (viewModel.ReceiveStatusText, viewModel.ReceiveStatus));

			viewModel.ToggleSourcesPanelCommand.Execute(null);
			Assert.True(viewModel.IsSourcesPanelOpen);

			viewModel.ToggleReceivingCommand.Execute(null);

			Assert.Equal(("Stopped", ReceiveStatus.Stopped), (viewModel.ReceiveStatusText, viewModel.ReceiveStatus));
			Assert.False(viewModel.IsListening);
			Assert.All(viewModel.Sources, source => Assert.False(source.IsOn));
		});
	}

	[Fact]
	public void TheStatusBar_SummarizesTheSelectedTab_AndTheFilterCanBeReset()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			var viewModel = session.ViewModel;
			Assert.Equal(string.Empty, viewModel.TabSummary);
			Assert.False(viewModel.HasActiveFilter);

			Send(session, App("(100)"), "alpha");
			Send(session, App("(100)"), "beta", LogEntryLevel.Error);
			Send(session, App("(100)"), "beta again");
			var viewer = ViewerOf(session, viewModel.LogTabs[0]);
			Assert.Equal("Entries: 3", viewModel.TabSummary);
			Assert.False(viewModel.HasActiveFilter);

			viewer.AddRegexSearchTerm("beta");
			viewer.InfoFilter = true;
			WpfTestHelper.Settle();

			Assert.True(viewModel.HasActiveFilter);
			Assert.Equal("Entries: 1 of 3 · Levels off: 1 · Terms: 1", viewModel.TabSummary);
			Assert.Equal(1, viewModel.LogTabs[0].VisibleEntryCount);

			viewer.SearchFields = SearchFields.All;
			viewer.CustomTimeStampFormat = TimeStampFormat.German;
			viewModel.ResetFilterCommand.Execute(null);
			WpfTestHelper.Settle();

			Assert.False(viewModel.HasActiveFilter);
			Assert.Equal("Entries: 3", viewModel.TabSummary);
			Assert.Empty(viewer.ActiveSearchTerms);
			Assert.False(viewer.InfoFilter);
			Assert.Equal((SearchFields.All, TimeStampFormat.German), (viewer.SearchFields, viewer.CustomTimeStampFormat));

			viewModel.LogTabs.Clear();
			Assert.Equal(string.Empty, viewModel.TabSummary);
		});
	}

	[Fact]
	public void LogsOfAnIgnoredSender_DoNotOpenATab_AndIgnoringATabClosesItsTabs()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.IgnoreRules.Add(new IgnoreRule { Ip = "10.0.0.9" });
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();

			Send(session, App("(1)", "Noisy", "10.0.0.9"));
			Send(session, App("(2)", "Orders", "10.0.0.5"));
			Send(session, App("(3)", "Billing", "10.0.0.5"));
			Send(session, App("(4)", "Billing", "10.0.0.7"));

			Assert.Equal(new[] { "Orders", "Billing", "Billing" }, session.ViewModel.LogTabs.Select(tab => tab.ApplicationName));

			session.ViewModel.Ignore(session.ViewModel.LogTabs[1], IgnoreScope.Sender);

			Assert.Equal(new[] { "Billing" }, session.ViewModel.LogTabs.Select(tab => tab.ApplicationName));
			Send(session, App("(5)", "Orders", "10.0.0.5"));
			Assert.Single(session.ViewModel.LogTabs);

			session.ViewModel.Ignore(session.ViewModel.LogTabs[0], IgnoreScope.Application);

			Assert.Empty(session.ViewModel.LogTabs);
			Assert.Equal(new[] { ("10.0.0.9", ""), ("10.0.0.5", ""), ("", "Billing") },
				_environment.Saved.IgnoreRules.Select(rule => (rule.Ip, rule.Application)));
		});
	}

	[Fact]
	public void IgnoringAnApplicationFromAnIp_KeepsItsOtherIps_AndWorksForASenderWithAMachineName()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(1)", "Orders", "NB1 (10.0.0.5)"));
			Send(session, App("(2)", "Orders", "10.0.0.7"));
			var tab = session.ViewModel.LogTabs[0];
			Assert.True(session.ViewModel.IgnoreApplicationFromSenderCommand.CanExecute(tab));
			Assert.True(session.ViewModel.IgnoreSenderCommand.CanExecute(tab));

			session.ViewModel.Ignore(tab, IgnoreScope.ApplicationFromSender);

			var rule = Assert.Single(_environment.Saved.IgnoreRules);
			Assert.Equal(("10.0.0.5", "Orders"), (rule.Ip, rule.Application));
			Assert.Equal(new[] { "10.0.0.7" }, session.ViewModel.LogTabs.Select(other => other.Sender));
			Send(session, App("(3)", "Orders", "NB1 (10.0.0.5)"));
			Send(session, App("(4)", "Orders", "10.0.0.8"));
			Assert.Equal(new[] { "10.0.0.7", "10.0.0.8" }, session.ViewModel.LogTabs.Select(other => other.Sender));
			Assert.False(session.ViewModel.IgnoreSenderCommand.CanExecute(new Sentinel.LogViewer.Models.LogTabViewModel { SenderKey = "x", Sender = "Local" }));
		});
	}

	[Fact]
	public void TheColumns_StartAsTheSettingsSay_AndWhatTheUserChoosesInATab_IsSavedForTheNextTab()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.Columns = new() { LogColumns.Level, LogColumns.Thread, LogColumns.Message };
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();

			Send(session, App("(100)"));
			var first = ViewerOf(session, session.ViewModel.LogTabs[0]);
			Assert.Equal(new[] { LogColumns.Level, LogColumns.Thread, LogColumns.Message }, first.ColumnKeys);

			first.SetColumnVisible(LogColumns.Property("Class"), true);
			Assert.Equal(new[] { LogColumns.Level, LogColumns.Thread, LogColumns.Property("Class"), LogColumns.Message }, _environment.Saved.Columns);

			// A header that is dragged to another place reaches the tab and the settings as well.
			((GridView)((ListView)first.Template.FindName("PART_ListView", first)).View).Columns.Move(0, 1);
			Assert.Equal(new[] { LogColumns.Thread, LogColumns.Level, LogColumns.Property("Class"), LogColumns.Message }, session.ViewModel.LogTabs[0].ColumnKeys);
			Assert.Equal(session.ViewModel.LogTabs[0].ColumnKeys, _environment.Saved.Columns);
			first.ColumnKeys = new[] { LogColumns.Level, LogColumns.Thread, LogColumns.Property("Class"), LogColumns.Message };

			Send(session, App("(100)", name: "Orders"));
			var second = ViewerOf(session, session.ViewModel.LogTabs[1]);
			Assert.Equal(_environment.Saved.Columns, second.ColumnKeys);
			Assert.Equal(new[] { LogColumns.Level, LogColumns.Thread, LogColumns.Property("Class"), LogColumns.Message }, first.ColumnKeys);
		});
	}

	private Session StartWithTabs(int count)
	{
		var session = Start();
		for (var i = 1; i <= count; i++)
			Send(session, App($"({i}00)", name: $"App{i}"));

		Assert.Equal(count, session.ViewModel.LogTabs.Count);
		return session;
	}

	private static void Press(Session session, Key key, ModifierKeys modifiers)
	{
		var binding = session.Window.InputBindings.OfType<KeyBinding>().First(candidate => candidate.Key == key && candidate.Modifiers == modifiers);
		Assert.True(binding.Command.CanExecute(binding.CommandParameter));
		binding.Command.Execute(binding.CommandParameter);
		WpfTestHelper.Settle();
	}

	[Fact]
	public void TheTabs_ChangeWithTheKeysOfABrowser_AndGoAroundAtTheEnds()
	{
		_context.RunOnSta(() =>
		{
			using var session = StartWithTabs(4);
			var tabs = session.ViewModel.LogTabs;

			Press(session, Key.D2, ModifierKeys.Alt);
			Assert.Same(tabs[1], session.ViewModel.SelectedTab);

			// Alt and numpad digits enter a character by its code in a text box, so they do not change the tab.
			Assert.DoesNotContain(session.Window.InputBindings.OfType<KeyBinding>(), binding => binding.Modifiers == ModifierKeys.Alt && binding.Key is >= Key.NumPad0 and <= Key.NumPad9);

			Press(session, Key.D9, ModifierKeys.Alt);
			Assert.Same(tabs[3], session.ViewModel.SelectedTab);

			Press(session, Key.PageDown, ModifierKeys.Control);
			Assert.Same(tabs[0], session.ViewModel.SelectedTab);

			Press(session, Key.PageUp, ModifierKeys.Control);
			Assert.Same(tabs[3], session.ViewModel.SelectedTab);

			Press(session, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);
			Assert.Same(tabs[2], session.ViewModel.SelectedTab);

			Press(session, Key.Tab, ModifierKeys.Control);
			Assert.Same(tabs[3], session.ViewModel.SelectedTab);

			// A number beyond the last tab selects the last one.
			session.ViewModel.SelectTabCommand.Execute(8);
			Assert.Same(tabs[3], session.ViewModel.SelectedTab);
		});
	}

	[Fact]
	public void TheCurrentTab_IsClosedWithCtrlW_AndWithCtrlF4_WithoutAsking()
	{
		_context.RunOnSta(() =>
		{
			using var session = StartWithTabs(3);
			var tabs = session.ViewModel.LogTabs;
			session.ViewModel.SelectedTab = tabs[1];
			var second = tabs[1];

			Press(session, Key.W, ModifierKeys.Control);
			Assert.Equal(2, tabs.Count);
			Assert.DoesNotContain(second, tabs);

			Press(session, Key.F4, ModifierKeys.Control);
			Assert.Single(tabs);
		});
	}

	[Fact]
	public void AMiddleClickOnATab_ClosesIt_AndOtherButtonsDoNot()
	{
		_context.RunOnSta(() =>
		{
			using var session = StartWithTabs(3);
			var tabs = session.ViewModel.LogTabs;
			var first = tabs[0];
			var item = Descendants<TabItem>(session.Window).First(candidate => ReferenceEquals(candidate.DataContext, first));

			item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = Mouse.PreviewMouseUpEvent, Source = item });
			Assert.Equal(3, tabs.Count);

			item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Middle) { RoutedEvent = Mouse.PreviewMouseUpEvent, Source = item });
			Assert.Equal(2, tabs.Count);
			Assert.DoesNotContain(first, tabs);
		});
	}

	[Fact]
	public void CloseTheTabsToTheRight_IsOnlyOfferedWhereThereAreTabsToTheRight()
	{
		_context.RunOnSta(() =>
		{
			using var session = StartWithTabs(3);
			var tabs = session.ViewModel.LogTabs;
			var command = session.ViewModel.CloseTabsToTheRightCommand;

			Assert.True(command.CanExecute(tabs[0]));
			Assert.True(command.CanExecute(tabs[1]));
			Assert.False(command.CanExecute(tabs[2]));
		});
	}

	private sealed class FakeTray : ITrayIcon
	{
		public event EventHandler? RestoreRequested;
		public event EventHandler? NotificationClicked;
		public event EventHandler? ExitRequested { add { } remove { } }
		public bool IsVisible { get; private set; }
		public string ToolTip { get; set; } = string.Empty;
		public List<string> Notifications { get; } = new();
		public List<string> Titles { get; } = new();
		public void Show() => IsVisible = true;
		public void Hide() => IsVisible = false;
		public List<bool> Warnings { get; } = new();
		public void Notify(string title, string text, bool isWarning)
		{
			Titles.Add(title);
			Notifications.Add(text);
			Warnings.Add(isWarning);
		}
		public void Restore() => RestoreRequested?.Invoke(this, EventArgs.Empty);
		public void Click() => NotificationClicked?.Invoke(this, EventArgs.Empty);
	}

	private void ConfigureTray(bool minimizeToTray, string notifyLevel)
	{
		var configuration = _environment.Saved;
		configuration.MinimizeToTray = minimizeToTray;
		configuration.TrayNotifyLevel = notifyLevel;
		_environment.Configuration.SaveConfiguration(configuration);
	}

	[Fact]
	public void MinimizingWithTheTraySetting_HidesTheWindow_ShowsTheIcon_AndTheIconBringsTheWindowBack()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Off");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);

			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Assert.False(session.Window.IsVisible);
			Assert.True(tray.IsVisible);
			Assert.Contains(session.ViewModel.ReceiveStatusText, tray.ToolTip);

			tray.Restore();
			WpfTestHelper.Settle();

			Assert.True(session.Window.IsVisible);
			Assert.Equal(System.Windows.WindowState.Normal, session.Window.WindowState);
			Assert.False(tray.IsVisible);
		});
	}

	/// <summary>The way the list of the shortcuts writes the gesture of a key binding</summary>
	private static string Gesture(KeyBinding binding)
	{
		var key = binding.Key switch
		{
			>= Key.D0 and <= Key.D9 => (binding.Key - Key.D0).ToString(),
			>= Key.NumPad0 and <= Key.NumPad9 => (binding.Key - Key.NumPad0).ToString(),
			Key.OemPlus or Key.Add => "Plus",
			Key.OemMinus or Key.Subtract => "Minus",
			Key.OemComma => ",",
			Key.Next => "PageDown",
			Key.Prior => "PageUp",
			_ => binding.Key.ToString()
		};
		if (key is "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" && binding.Modifiers == ModifierKeys.Alt)
			key = "1…8";

		var modifiers = (binding.Modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "")
			+ (binding.Modifiers.HasFlag(ModifierKeys.Alt) ? "Alt+" : "")
			+ (binding.Modifiers.HasFlag(ModifierKeys.Shift) && key != "Plus" ? "Shift+" : "");
		return modifiers + key;
	}

	[Fact]
	public void EveryKeyBindingOfTheMainWindow_IsInTheListOfTheShortcuts()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();

			var listed = ShortcutCatalog.Groups.SelectMany(group => group.Shortcuts).SelectMany(shortcut => shortcut.Keys.Split(" / ")).ToHashSet();
			var missing = session.Window.InputBindings.OfType<KeyBinding>().Select(Gesture).Distinct().Where(gesture => !listed.Contains(gesture)).ToList();

			Assert.Empty(missing);
		});
	}

	[Fact]
	public void TheNotifyIconOfWindows_HasTheIconOfTheApplication_AndAToolTipThatFits()
	{
		_context.RunOnSta(() =>
		{
			using var tray = new NotifyIconTray(_environment.Localization);

			tray.ToolTip = new string('x', 300);
			tray.Show();
			Assert.True(tray.IsVisible);

			tray.Hide();
			Assert.False(tray.IsVisible);
		});
	}

	[Fact]
	public void MinimizingWithoutTheTraySetting_LeavesTheWindowInTheTaskbar()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: false, notifyLevel: "Error");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);

			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Assert.True(session.Window.IsVisible);
			Assert.False(tray.IsVisible);
		});
	}

	[Fact]
	public void TheTrayNotification_FollowsTheLevelOfTheSetting_AndOnlyWhileTheWindowIsInTheTray()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Fatal");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);

			Send(session, App("(100)"), level: LogEntryLevel.Fatal);
			Assert.Empty(tray.Notifications);

			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();
			Send(session, App("(100)"), level: LogEntryLevel.Error);
			Assert.Empty(tray.Notifications);

			Send(session, App("(100)"), level: LogEntryLevel.Fatal);
			Assert.Equal(new[] { "Acme.Logger: hello" }, tray.Notifications);
			Assert.Equal(new[] { "MyApp · Fatal" }, tray.Titles);
			Assert.Equal(new[] { false }, tray.Warnings);
		});
	}

	[Fact]
	public void WarningsAreAnnounced_WhenTheSettingSaysSo_AndWhatArrivesWithinTheIntervalIsAnnouncedAfterIt()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Warn");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);
			session.Window.TrayNotificationInterval = TimeSpan.FromSeconds(1.5);
			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Send(session, App("(100)"), level: LogEntryLevel.Info);
			Assert.Empty(tray.Notifications);

			Send(session, App("(100)"), level: LogEntryLevel.Warn);
			Assert.Equal(new[] { "Acme.Logger: hello" }, tray.Notifications);
			Assert.Equal(new[] { true }, tray.Warnings);

			// Within the interval: nothing yet, but it is not lost.
			Send(session, App("(100)"), level: LogEntryLevel.Error);
			Assert.Single(tray.Notifications);

			for (var i = 0; i < 60 && tray.Notifications.Count < 2; i++)
			{
				Thread.Sleep(50);
				WpfTestHelper.Settle();
			}

			Assert.Equal(new[] { "MyApp · Warn", "MyApp · Error" }, tray.Titles);

			// A warning has the symbol of a warning, an error that of an error.
			Assert.Equal(new[] { true, false }, tray.Warnings);
		});
	}

	/// <summary>Sends entries of one application at once, so that they come as one batch</summary>
	private static void SendBatch(Session session, AppInfo app, params (LogEntryLevel Level, string Message)[] entries)
	{
		foreach (var (level, message) in entries)
			session.Events.OnNext(new LogEvent { AppInfo = app, Entry = new LogEntry(level, "Acme.Logger", message) });

		for (var i = 0; i < 10; i++)
		{
			Thread.Sleep(60);
			WpfTestHelper.Settle();
		}
	}

	[Fact]
	public void SeveralEntriesInThePause_ShowTheMostImportantOne_AndHowManyMoreThereAre()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Warn");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);
			session.Window.TrayNotificationInterval = TimeSpan.FromSeconds(2);
			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Send(session, App("(100)"), "first", LogEntryLevel.Warn);
			SendBatch(session, App("(100)"), (LogEntryLevel.Warn, "second"), (LogEntryLevel.Error, "boom\n  at Billing.Charge()"), (LogEntryLevel.Warn, "third"));
			Assert.Equal(new[] { "Acme.Logger: first" }, tray.Notifications);

			for (var i = 0; i < 60 && tray.Notifications.Count < 2; i++)
			{
				Thread.Sleep(50);
				WpfTestHelper.Settle();
			}

			Assert.Equal(new[] { "MyApp · Warn", "MyApp · Error" }, tray.Titles);
			Assert.Equal("Acme.Logger: boom at Billing.Charge() (+2 more)", tray.Notifications[1]);
		});
	}

	[Fact]
	public void AnApplicationWithoutAName_IsShownBySenderInTheNotification()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Error");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);
			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Send(session, new AppInfo { AppName = null, Sender = "10.0.0.7" }, "boom", LogEntryLevel.Error);

			Assert.Equal(new[] { "10.0.0.7 · Error" }, tray.Titles);
		});
	}

	[Fact]
	public void AClickOnTheNotification_OpensTheWindowAndTheTabOfTheEntry_AlsoWhenTheTabIsGone()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Error");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);
			Send(session, App("(1)", name: "First"));
			Send(session, App("(2)", name: "Second"));
			var tabs = session.ViewModel.LogTabs;
			Assert.Equal(2, tabs.Count);
			session.ViewModel.SelectedTab = tabs[0];
			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();

			Send(session, App("(2)", name: "Second"), "boom", LogEntryLevel.Error);
			Assert.Equal(new[] { "Second · Error" }, tray.Titles);
			tray.Click();
			WpfTestHelper.Settle();

			Assert.True(session.Window.IsVisible);
			Assert.False(tray.IsVisible);
			Assert.Same(tabs[1], session.ViewModel.SelectedTab);

			// The tab of the last notification was closed meanwhile: the window opens, nothing else happens.
			session.ViewModel.SelectedTab = tabs[0];
			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();
			session.Window.TrayNotificationInterval = TimeSpan.Zero;
			Send(session, App("(2)", name: "Second"), "again", LogEntryLevel.Error);
			session.ViewModel.CloseTabCommand.Execute(tabs[1]);
			tray.Click();
			WpfTestHelper.Settle();

			Assert.True(session.Window.IsVisible);
			Assert.Same(session.ViewModel.LogTabs[0], session.ViewModel.SelectedTab);
		});
	}

	[Fact]
	public void TheTimeBetweenNotifications_IsTheOneOfTheSettings_AndAWrongOneBecomesTheDefault()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.TrayNotifyIntervalSeconds = 12;
			_environment.Configuration.SaveConfiguration(configuration);
			using (var session = Start())
				Assert.Equal(TimeSpan.FromSeconds(12), session.ViewModel.TrayNotifyInterval);

			configuration.TrayNotifyIntervalSeconds = 0;
			_environment.Configuration.SaveConfiguration(configuration);
			using (var session = Start())
				Assert.Equal(TimeSpan.FromSeconds(30), session.ViewModel.TrayNotifyInterval);
		});
	}

	[Fact]
	public void TheWindowComesBackFromTheTray_AsItWasBefore()
	{
		_context.RunOnSta(() =>
		{
			ConfigureTray(minimizeToTray: true, notifyLevel: "Off");
			var tray = new FakeTray();
			using var session = Start(tray: tray, invisible: true);
			session.Window.WindowState = System.Windows.WindowState.Maximized;
			WpfTestHelper.Settle();

			session.Window.WindowState = System.Windows.WindowState.Minimized;
			WpfTestHelper.Settle();
			tray.Restore();
			WpfTestHelper.Settle();

			Assert.Equal(System.Windows.WindowState.Maximized, session.Window.WindowState);
		});
	}

	[Fact]
	public void AMissingFontOfTheSettings_IsReplacedByTheDefault_WithAMessage()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.ListFontFamily = "Consolas";
			_environment.Configuration.SaveConfiguration(configuration);
			using (var installed = Start())
			{
				Assert.Equal("Consolas", _environment.Saved.ListFontFamily);
				Assert.DoesNotContain("not installed", installed.ViewModel.Snackbar.Message);
			}

			configuration.ListFontFamily = "No Such Font 123";
			_environment.Configuration.SaveConfiguration(configuration);
			using var missing = Start();

			Assert.Equal(string.Empty, _environment.Saved.ListFontFamily);
			Assert.Contains("No Such Font 123", missing.ViewModel.Snackbar.Message);
			Assert.Equal(System.Windows.SystemFonts.MessageFontFamily.Source, missing.ViewModel.ListFontFamily.Source);
		});
	}

	private sealed class FeedHandler(string tag) : HttpMessageHandler
	{
		public int Requests { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests++;
			var feed = $"""<feed xmlns="http://www.w3.org/2005/Atom"><entry><link rel="alternate" href="https://github.com/boexler/NLogViewer/releases/tag/{tag}"/></entry></feed>""";
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(feed) });
		}
	}

	/// <summary>Starts the window with a feed that offers the given release, and waits until the check has run</summary>
	private Session StartWithRelease(string tag)
	{
		var feed = new FeedHandler(tag);
		var session = Start(new UpdateService(feed, InstallType.Msi));
		for (var i = 0; i < 100 && feed.Requests == 0; i++)
		{
			Thread.Sleep(50);
			WpfTestHelper.Settle();
		}

		Assert.True(feed.Requests > 0);
		for (var i = 0; i < 5; i++)
		{
			Thread.Sleep(50);
			WpfTestHelper.Settle();
		}

		return session;
	}

	[Fact]
	public void TheUpdateNoticeOfTheStatusBar_CanBeSkipped_AndComesBackForANewerVersion()
	{
		_context.RunOnSta(() =>
		{
			using (var first = StartWithRelease("v99.0.0"))
			{
				Assert.NotNull(first.ViewModel.UpdateNotice);
				first.ViewModel.SkipUpdateCommand.Execute(null);
				Assert.Null(first.ViewModel.UpdateNotice);
				Assert.Equal("99.0.0", _environment.Saved.SkippedVersion);
			}

			using (var second = StartWithRelease("v99.0.0"))
				Assert.Null(second.ViewModel.UpdateNotice);

			using var third = StartWithRelease("v100.0.0");
			Assert.NotNull(third.ViewModel.UpdateNotice);
		});
	}

	[Fact]
	public void TheLookOfTheRows_StartsAsTheSettingsSay()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.LevelDisplay = "SymbolAndText";
			configuration.RowHeight = "Compact";
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();
			Send(session, App("(100)"));

			var viewer = ViewerOf(session, session.ViewModel.LogTabs[0]);

			Assert.Equal(Sentinel.LogViewer.Wpf.LevelDisplayMode.SymbolAndText, viewer.LevelDisplay);
			Assert.Equal(Sentinel.LogViewer.Wpf.RowHeightMode.Compact, viewer.RowHeight);
		});
	}

	[Fact]
	public void TheFontOfTheList_IsZoomedForTheSession_AndTheStatusBarOffersToSaveIt()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.ListFontFamily = "Consolas";
			configuration.ListFontSize = 14;
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();
			Send(session, App("(100)"));
			var viewer = ViewerOf(session, session.ViewModel.LogTabs[0]);
			Assert.Equal(14, viewer.ListFontSize);
			Assert.Equal("Consolas", viewer.ListFontFamily.Source);
			Assert.False(session.ViewModel.IsFontZoomed);

			Press(session, Key.OemPlus, ModifierKeys.Control);
			Press(session, Key.Add, ModifierKeys.Control);
			Assert.Equal(16, viewer.ListFontSize);
			Assert.True(session.ViewModel.IsFontZoomed);
			Assert.Equal(14, _environment.Saved.ListFontSize);

			Press(session, Key.D0, ModifierKeys.Control);
			Assert.Equal(14, viewer.ListFontSize);
			Assert.False(session.ViewModel.IsFontZoomed);

			Press(session, Key.OemMinus, ModifierKeys.Control);
			Press(session, Key.Subtract, ModifierKeys.Control);
			session.ViewModel.SaveFontZoomCommand.Execute(null);
			Assert.Equal(12, _environment.Saved.ListFontSize);
			Assert.Equal(12, viewer.ListFontSize);
			Assert.False(session.ViewModel.IsFontZoomed);

			for (var i = 0; i < 50; i++)
				session.ViewModel.ZoomOutCommand.Execute(null);

			Assert.Equal(6, viewer.ListFontSize);
		});
	}

	[Fact]
	public void TheSourcesOfTheStatusBar_FollowTheAddressesOfTheSettings()
	{
		_context.RunOnSta(() =>
		{
			var configuration = _environment.Saved;
			configuration.Ports = new() { "udp://0.0.0.0:4511" };
			_environment.Configuration.SaveConfiguration(configuration);
			using var session = Start();
			Assert.Equal(new[] { "UDP 4511" }, session.ViewModel.Sources.Select(source => source.Name));
			Assert.Equal("Ready", session.ViewModel.ReceiveStatusText);
		});
	}

	[Fact]
	public void ADraggedWidth_ThatIsOutsideTheLimits_IsSavedAsTheNearestValidWidth()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var viewer = ViewerOf(session, session.ViewModel.LogTabs[0]);

			viewer.LoggerTreeWidth = 5000;
			Assert.Equal(SettingsRules.MaxLoggerTreeWidth, _environment.Saved.LoggerTreeWidth);

			viewer.LoggerTreeWidth = 10;
			Assert.Equal(SettingsRules.MinLoggerTreeWidth, _environment.Saved.LoggerTreeWidth);
		});
	}

	[Fact]
	public void AnotherApplication_DoesNotGetTheFilters()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			ViewerOf(session, session.ViewModel.LogTabs[0]).TraceFilter = true;
			WpfTestHelper.Settle();

			Send(session, App("(100)", sender: "10.0.0.6"));

			var other = session.ViewModel.LogTabs[1];
			Assert.False(other.FiltersTakenOver);
			Assert.False(ViewerOf(session, other).TraceFilter);
		});
	}

	[Fact]
	public void TheMenuOfTheTab_SwitchesTheTakeoverOffForOneApplication()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var first = session.ViewModel.LogTabs[0];
			ViewerOf(session, first).TraceFilter = true;
			WpfTestHelper.Settle();
			Assert.True(first.TakesOverFilters);

			session.ViewModel.ToggleTakeOverFiltersCommand.Execute(first);
			Send(session, App("(200)"));

			Assert.False(first.TakesOverFilters);
			var second = session.ViewModel.LogTabs[1];
			Assert.False(second.TakesOverFilters);
			Assert.False(second.FiltersTakenOver);
			Assert.False(ViewerOf(session, second).TraceFilter);
		});
	}

	[Fact]
	public void ARestart_CanContinueInTheExistingTab()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var tab = session.ViewModel.LogTabs[0];
			session.ViewModel.SetRestartBehavior(tab, RestartBehavior.ExtendTab);
			Assert.True(tab.RestartExtendsTab);

			Send(session, App("(200)"), "after the restart");

			Assert.Same(tab, Assert.Single(session.ViewModel.LogTabs));
			Assert.Equal(2, tab.LogEntries.Count);
			Assert.Equal(App("(200)").Id, tab.TargetName);
		});
	}

	[Fact]
	public void ARestart_CanReplaceTheOldTab()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var old = session.ViewModel.LogTabs[0];
			session.ViewModel.SetRestartBehavior(old, RestartBehavior.ReplaceTab);
			old.LastEventUtc = DateTime.UtcNow.AddMinutes(-1);

			Send(session, App("(200)"), "after the restart");

			var current = Assert.Single(session.ViewModel.LogTabs);
			Assert.NotSame(old, current);
			Assert.Equal(App("(200)").Id, current.TargetName);
			Assert.Same(current, session.ViewModel.SelectedTab);
		});
	}

	[Fact]
	public void TheMenuOfATab_HasTheEntriesForTheApplication_AndTheArrowOpensIt()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var tab = session.ViewModel.LogTabs[0];
			var tabItem = Descendants<TabItem>(session.Window).First(item => ReferenceEquals(item.DataContext, tab));

			var headers = tabItem.ContextMenu!.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).ToList();
			Assert.Contains("Take over the filters into new tabs of this application", headers);
			var restart = tabItem.ContextMenu.Items.OfType<MenuItem>().Single(item => item.Items.Count == 3);
			Assert.Equal(new[] { "Continue in the existing tab", "Open a new tab", "Open a new tab and close the old one" },
				restart.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()));

			var arrow = Descendants<Button>(tabItem).First(button => Equals(button.ToolTip, "Tab menu"));
			arrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			Assert.True(tabItem.ContextMenu.IsOpen);
			Assert.Same(tabItem, tabItem.ContextMenu.PlacementTarget);
			tabItem.ContextMenu.IsOpen = false;
			for (var i = 0; i < 5; i++)
			{
				Thread.Sleep(60);
				WpfTestHelper.Settle();
			}
		});
	}

	[Fact]
	public void TheMenuOfAFileTab_HasNoEntriesForAnApplication_AndTheMenuOfALiveTab_HasThem()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			Send(session, App("(100)"));
			var liveTab = session.ViewModel.LogTabs[0];
			var fileTab = new LogTabViewModel { Header = "tokyo.log", TargetName = "FileImport_x", MaxCount = int.MaxValue };
			session.ViewModel.LogTabs.Add(fileTab);
			session.ViewModel.SelectedTab = fileTab;
			WpfTestHelper.Settle();

			string[] ofAnApplication =
			{
				"Take over the filters into new tabs of this application",
				"After a restart of this application",
				"Ignore this application (from this IP address)",
				"Ignore this application (from any IP address)",
				"Ignore this IP address"
			};

			System.Windows.Visibility VisibilityOf(LogTabViewModel tab, string header)
			{
				var tabItem = Descendants<TabItem>(session.Window).First(item => ReferenceEquals(item.DataContext, tab));
				var menu = tabItem.ContextMenu!;
				menu.PlacementTarget = tabItem;
				menu.IsOpen = true;
				WpfTestHelper.Settle();
				try
				{
					return menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == header).Visibility;
				}
				finally
				{
					menu.IsOpen = false;
				}
			}

			foreach (var header in ofAnApplication)
			{
				Assert.Equal(System.Windows.Visibility.Collapsed, VisibilityOf(fileTab, header));
				Assert.Equal(System.Windows.Visibility.Visible, VisibilityOf(liveTab, header));
			}

			Assert.Equal(System.Windows.Visibility.Visible, VisibilityOf(fileTab, "Time zone..."));
			Assert.Equal(System.Windows.Visibility.Visible, VisibilityOf(liveTab, "Time zone..."));
		});
	}

	[Fact]
	public void TheZonesOfATab_AndOfItsViewer_FollowEachOther_AndTheViewerOpensTheDialogOfTheApplication()
	{
		_context.RunOnSta(() =>
		{
			using var session = Start();
			var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
			var tab = new LogTabViewModel { Header = "tokyo.log", TargetName = "FileImport_y", MaxCount = int.MaxValue };
			session.ViewModel.LogTabs.Add(tab);
			session.ViewModel.SelectedTab = tab;
			WpfTestHelper.Settle();
			var viewer = ViewerOf(session, tab);

			tab.FileTimeZone = tokyo;
			Assert.Equal(tokyo, viewer.FileTimeZone);

			viewer.DisplayTimeZone = TimeZoneInfo.Utc;
			Assert.Equal(TimeZoneInfo.Utc, tab.DisplayTimeZone);
			Assert.Same(session.ViewModel.ChangeTimeZoneCommand, viewer.ChangeTimeZoneCommand);
		});
	}
}
