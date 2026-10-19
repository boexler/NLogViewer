using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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

	private Session Start()
	{
		AddResourcesOfTheApplication();
		var xml = new Log4JEventParser();
		var receiver = new UdpLogReceiverService(xml);
		var formats = _environment.Formats;
		var files = new LogFileParserService(xml, new PlainTextParser(), new JsonLogParser(), new TextFileFormatDetector(), formats);
		var tabs = new ApplicationTabService(_environment.Configuration);
		var viewModel = new MainViewModel(receiver, files, _environment.Configuration, _environment.Localization, new UpdateService(), tabs);
		var events = (Subject<LogEvent>)typeof(UdpLogReceiverService)
			.GetField("_log4JEventObservable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(receiver)!;
		var window = new MainWindow(viewModel) { Left = -10000, Top = -10000, ShowActivated = false, Width = 900, Height = 500 };
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

	private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
				yield return match;

			foreach (var descendant in Descendants<T>(child))
				yield return descendant;
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
}
