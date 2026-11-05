using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Core.Sessions;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.ViewModels;
using Xunit;

namespace Sentinel.LogViewer.Tests;

/// <summary>
/// Tests of the sessions: all tabs in one file, the recent sessions, and the tabs that are kept when the application closes
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class SessionTests : IDisposable
{
	private static readonly DateTime Start = new(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

	private readonly WpfStaContextFixture _context;
	private readonly SettingsTestEnvironment _environment = new();

	public SessionTests(WpfStaContextFixture fixture)
	{
		_context = fixture;
	}

	public void Dispose() => _environment.Dispose();

	private MainViewModel NewViewModel()
	{
		// The application starts listening by itself; a test must not open the port of the computer.
		var configuration = _environment.Saved;
		configuration.AutoStartListening = false;
		_environment.Configuration.SaveConfiguration(configuration);

		var xml = new Log4JEventParser();
		var files = new LogFileParserService(xml, new PlainTextParser(), new JsonLogParser(), new TextFileFormatDetector(), _environment.Formats);
		return new MainViewModel(new LogReceiverService(xml), files, _environment.Configuration, _environment.Localization, new UpdateService(),
			new ApplicationTabService(_environment.Configuration));
	}

	private void KeepTabs(bool keep)
	{
		var configuration = _environment.Saved;
		configuration.KeepTabsAcrossRestarts = keep;
		_environment.Configuration.SaveConfiguration(configuration);
	}

	private string PathOf(string name) => Path.Combine(_environment.Directory, name);

	private static LogTabViewModel TabWithEntries(string header, int count, string? senderKey = null)
	{
		var tab = new LogTabViewModel { Header = header, TargetName = header, SenderKey = senderKey, MaxCount = 1000 };
		for (var i = 0; i < count; i++)
		{
			tab.LogEntries.Add(new LogEntry(i % 2 == 0 ? LogEntryLevel.Info : LogEntryLevel.Error, "Acme.Shop", $"message {i}")
			{
				TimeStamp = Start.AddMinutes(i),
				Application = senderKey == null ? string.Empty : "Acme",
				Sender = senderKey == null ? string.Empty : "PC1 (10.0.0.5)",
				Properties = new Dictionary<string, object?> { ["Thread"] = "worker " + i }
			});
		}

		return tab;
	}

	// The save and the open run in the background and come back to the window thread, so the test lets the window thread work until they are done.
	private static T Pump<T>(Task<T> task)
	{
		var end = DateTime.UtcNow.AddSeconds(15);
		while (!task.IsCompleted && DateTime.UtcNow < end)
		{
			WpfTestHelper.Settle();
			Thread.Sleep(10);
		}

		Assert.True(task.IsCompleted, "The task did not finish.");
		return task.Result;
	}

	private static void Pump(Task task) => Pump(task.ContinueWith(_ => 0, TaskScheduler.Default));

	[Fact]
	public void ASavedSession_BringsBackTheTabsWithEntriesFiltersAndBookmarks() =>
		_context.RunOnSta(() =>
		{
			var path = PathOf("case" + SessionSerializer.Extension);
			var source = NewViewModel();
			var live = TabWithEntries("Acme (PC1)", 4, senderKey: "acme|pc1");
			live.ColourIndex = 2;
			live.RestartBehavior = RestartBehavior.ExtendTab;
			live.FilterState = new LogViewerFilterState { HiddenLevels = { "Trace" }, LoggerRules = { "-Acme.Noise" } };
			live.Bookmarks.Entries.Add(live.LogEntries[2]);
			live.Bookmarks.Comments[live.LogEntries[2]] = "look here";
			live.Bookmarks.Entries.Add(live.LogEntries[0]);
			var file = TabWithEntries("old.log", 2);
			file.FileTimeZone = TimeZoneInfo.Utc;
			source.LogTabs.Add(live);
			source.LogTabs.Add(file);
			source.SelectedTab = file;

			Pump(source.SaveSessionAsync(path));

			var target = NewViewModel();
			Assert.True(Pump(target.OpenSessionAsync(path)));

			Assert.Equal(new[] { "Acme (PC1)", "old.log" }, target.LogTabs.Select(tab => tab.Header));
			Assert.Equal("old.log", target.SelectedTab?.Header);
			var restored = target.LogTabs[0];
			Assert.Equal("acme|pc1", restored.SenderKey);
			Assert.True(restored.IsLive);
			Assert.Equal(2, restored.ColourIndex);
			Assert.Equal(RestartBehavior.ExtendTab, restored.RestartBehavior);
			Assert.Equal(new[] { "message 0", "message 1", "message 2", "message 3" }, restored.LogEntries.Select(entry => entry.Message));
			Assert.Equal("Acme", restored.LogEntries[1].Application);
			Assert.Equal("PC1 (10.0.0.5)", restored.LogEntries[1].Sender);
			Assert.Equal("worker 3", restored.LogEntries[3].Properties["Thread"]);
			Assert.Equal(new[] { "Trace" }, restored.FilterState!.HiddenLevels);
			Assert.Equal(new[] { "-Acme.Noise" }, restored.FilterState.LoggerRules);
			Assert.Equal(2, restored.Bookmarks.Count);
			Assert.Equal("look here", restored.Bookmarks.Comments[restored.LogEntries[2]]);
			Assert.Contains(restored.LogEntries[0], restored.Bookmarks.Entries);
			Assert.Equal(TimeZoneInfo.Utc, target.LogTabs[1].FileTimeZone);
			Assert.False(target.LogTabs[1].IsLive);
		});

	[Fact]
	public void TheTabsOfASession_DoNotChangeTheViewSettingsOfTheNextTab() =>
		_context.RunOnSta(() =>
		{
			var path = PathOf("view" + SessionSerializer.Extension);
			var source = NewViewModel();
			var tab = TabWithEntries("a.log", 1);
			source.LogTabs.Add(tab);
			// A tab that is in the window saves the columns that the user sets as the setting for the next tab.
			tab.ColumnKeys = new[] { "level", "message" };
			Pump(source.SaveSessionAsync(path));
			var configuration = _environment.Saved;
			configuration.Columns = Wpf.LogColumns.Default.ToList();
			_environment.Configuration.SaveConfiguration(configuration);

			var target = NewViewModel();
			Pump(target.OpenSessionAsync(path));

			Assert.Equal(new[] { "level", "message" }, target.LogTabs.Single().ColumnKeys);
			Assert.Equal(Wpf.LogColumns.Default, _environment.Saved.Columns);
		});

	[Fact]
	public void ASessionThatCannotBeOpened_ShowsAnError_AndAddsNoTab() =>
		_context.RunOnSta(() =>
		{
			var path = PathOf("broken" + SessionSerializer.Extension);
			File.WriteAllText(path, "this is no session");
			var viewModel = NewViewModel();

			Assert.False(Pump(viewModel.OpenSessionAsync(path)));
			Assert.False(Pump(viewModel.OpenSessionAsync(PathOf("missing" + SessionSerializer.Extension))));

			Assert.Empty(viewModel.LogTabs);
			Assert.Empty(viewModel.RecentSessions);
			Assert.False(viewModel.IsLoading);
		});

	[Fact]
	public void TheRecentSessions_AreTheLastTenThatWereSavedOrOpened_TheNewestFirst_AndStayInTheSettings() =>
		_context.RunOnSta(() =>
		{
			var viewModel = NewViewModel();
			var tab = TabWithEntries("a.log", 1);
			viewModel.LogTabs.Add(tab);
			var paths = Enumerable.Range(1, 12).Select(number => PathOf($"s{number}{SessionSerializer.Extension}")).ToList();
			foreach (var path in paths)
				Pump(viewModel.SaveSessionAsync(path));

			Pump(viewModel.OpenSessionAsync(paths[5]));

			Assert.Equal(paths[5], viewModel.RecentSessions[0]);
			Assert.Equal(10, viewModel.RecentSessions.Count);
			Assert.DoesNotContain(paths[0], viewModel.RecentSessions);
			Assert.Equal(viewModel.RecentSessions, _environment.Saved.RecentSessions);
			Assert.Equal(viewModel.RecentSessions, NewViewModel().RecentSessions);
		});

	[Fact]
	public void TheTabsAreKept_OnlyWithTheSetting_AndOnlyWhenTheyHoldSomething() =>
		_context.RunOnSta(() =>
		{
			var viewModel = NewViewModel();
			viewModel.LogTabs.Add(TabWithEntries("full.log", 3));
			viewModel.LogTabs.Add(new LogTabViewModel { Header = "empty" });
			var marked = new LogTabViewModel { Header = "marked only" };
			marked.Bookmarks.Entries.Add(new LogEntry(LogEntryLevel.Info, "A", "m"));
			viewModel.LogTabs.Add(marked);
			var kept = PathOf("tabs" + SessionSerializer.Extension);

			viewModel.SaveTabsOnExit();
			Assert.False(File.Exists(kept));

			KeepTabs(true);
			viewModel.SaveTabsOnExit();

			Assert.True(File.Exists(kept));
			// A bookmark on an entry that is not in the tab is not saved, but the tab is: it holds a bookmark.
			Assert.Equal(new[] { "full.log", "marked only" }, SessionSerializer.Load(kept).Tabs.Select(tab => tab.Header));
		});

	[Fact]
	public void TheKeptTabs_AreOfferedAtTheNextStart_AndComeBackWhenTheUserSaysYes() =>
		_context.RunOnSta(() =>
		{
			KeepTabs(true);
			var closing = NewViewModel();
			closing.LogTabs.Add(TabWithEntries("full.log", 3));
			closing.SaveTabsOnExit();

			var starting = NewViewModel();
			var questions = new List<string>();
			starting.Ask = (message, _, _) =>
			{
				questions.Add(message);
				return MessageBoxResult.Yes;
			};
			Pump(starting.OfferToRestoreTabsAsync());

			Assert.Single(questions);
			Assert.Equal("full.log", Assert.Single(starting.LogTabs).Header);
			Assert.Equal(3, starting.LogTabs[0].LogEntries.Count);
			Assert.Empty(starting.RecentSessions);
			Assert.False(File.Exists(PathOf("tabs" + SessionSerializer.Extension)));
		});

	[Fact]
	public void TheKeptTabs_AreDropped_WhenTheUserSaysNo_AndNothingIsAskedWithTheSettingOff() =>
		_context.RunOnSta(() =>
		{
			KeepTabs(true);
			var closing = NewViewModel();
			closing.LogTabs.Add(TabWithEntries("full.log", 3));
			closing.SaveTabsOnExit();

			var declining = NewViewModel();
			declining.Ask = (_, _, _) => MessageBoxResult.No;
			Pump(declining.OfferToRestoreTabsAsync());
			Assert.Empty(declining.LogTabs);
			Assert.False(File.Exists(PathOf("tabs" + SessionSerializer.Extension)));

			closing = NewViewModel();
			closing.LogTabs.Add(TabWithEntries("full.log", 3));
			closing.SaveTabsOnExit();
			KeepTabs(false);
			var silent = NewViewModel();
			silent.Ask = (_, _, _) => throw new InvalidOperationException("Nothing should be asked.");
			Pump(silent.OfferToRestoreTabsAsync());
			Assert.Empty(silent.LogTabs);
		});

	[Fact]
	public void BeforeTheUpdateRestart_TheTabsAreKeptWithoutAsking_WhenTheSettingIsOn_AndTheUserCanTakeTheRestartBack() =>
		_context.RunOnSta(() =>
		{
			var viewModel = NewViewModel();
			viewModel.Ask = (_, _, _) => throw new InvalidOperationException("Nothing should be asked.");
			Assert.True(viewModel.SaveTabsBeforeUpdate());

			viewModel.LogTabs.Add(TabWithEntries("full.log", 3));
			KeepTabs(true);
			Assert.True(viewModel.SaveTabsBeforeUpdate());
			Assert.True(File.Exists(PathOf("tabs" + SessionSerializer.Extension)));

			KeepTabs(false);
			var asking = NewViewModel();
			asking.LogTabs.Add(TabWithEntries("full.log", 3));
			asking.Ask = (_, _, _) => MessageBoxResult.Cancel;
			Assert.False(asking.SaveTabsBeforeUpdate());
			asking.Ask = (_, _, _) => MessageBoxResult.No;
			Assert.True(asking.SaveTabsBeforeUpdate());
		});
}
