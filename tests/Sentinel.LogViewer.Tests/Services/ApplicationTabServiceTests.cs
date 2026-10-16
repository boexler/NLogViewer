using System;
using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Tests.ViewModels;
using Sentinel.LogViewer.Wpf.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="ApplicationTabService"/>: which tab the logs of a restarted application go to, and the filters that a new tab takes over
/// </summary>
public class ApplicationTabServiceTests : IDisposable
{
	private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

	private readonly SettingsTestEnvironment _environment = new();
	private readonly ApplicationTabService _service;

	public ApplicationTabServiceTests()
	{
		_service = new ApplicationTabService(_environment.Configuration);
	}

	public void Dispose() => _environment.Dispose();

	private static AppInfo App(string name = "MyApp", string processId = "(100)", string sender = "10.0.0.5") =>
		new() { AppName = new AppName(name, processId), Sender = sender };

	private static LogTabViewModel Tab(AppInfo app, DateTime lastEvent) => new()
	{
		TargetName = app.Id,
		SenderKey = ApplicationTabService.KeyOf(app),
		LastEventUtc = lastEvent
	};

	private static LogViewerFilterState Filters(params string[] hiddenLevels) => new() { HiddenLevels = hiddenLevels.ToList() };

	private void SetGlobalBehavior(RestartBehavior behavior)
	{
		var configuration = _environment.Saved;
		configuration.RestartBehavior = behavior.ToString();
		_environment.Configuration.SaveConfiguration(configuration);
	}

	[Fact]
	public void TheKeyOfAnApplication_IgnoresTheProcess_ButNotTheSender()
	{
		var first = ApplicationTabService.KeyOf(App(processId: "(100)"));
		var restarted = ApplicationTabService.KeyOf(App(processId: "(200)"));
		var other = ApplicationTabService.KeyOf(App(sender: "10.0.0.6"));

		Assert.Equal(first, restarted);
		Assert.NotEqual(first, other);
		Assert.Equal(first, ApplicationTabService.KeyOf(App(name: "MYAPP")));
	}

	[Fact]
	public void TheTabOfTheSameProcess_IsUsed()
	{
		var app = App();
		var tab = Tab(app, Now);

		var route = _service.Route(new[] { tab }, app, Now);

		Assert.Same(tab, route.Tab);
		Assert.Empty(route.TabsToClose);
	}

	[Fact]
	public void AnApplicationWithoutATab_GetsANewOne()
	{
		var route = _service.Route(new[] { Tab(App(name: "Other"), Now) }, App(), Now);

		Assert.Null(route.Tab);
		Assert.Empty(route.TabsToClose);
	}

	[Fact]
	public void ATabOfAnImportedFile_IsNeverTheTabOfAnApplication()
	{
		var file = new LogTabViewModel { TargetName = "C:\\logs\\a.log" };

		var route = _service.Route(new[] { file }, App(), Now);

		Assert.Null(route.Tab);
	}

	[Fact]
	public void ARestart_OpensANewTab_ByDefault()
	{
		var tab = Tab(App(processId: "(100)"), Now.AddMinutes(-5));

		var route = _service.Route(new[] { tab }, App(processId: "(200)"), Now);

		Assert.Null(route.Tab);
		Assert.Empty(route.TabsToClose);
	}

	[Fact]
	public void ARestart_CanContinueInTheExistingTab_WhateverProcessStartedLast()
	{
		SetGlobalBehavior(RestartBehavior.ExtendTab);
		var old = Tab(App(processId: "(100)"), Now.AddMinutes(-5));
		var newer = Tab(App(processId: "(150)"), Now.AddMinutes(-1));

		var route = _service.Route(new[] { old, newer }, App(processId: "(200)"), Now);

		Assert.Same(newer, route.Tab);
		Assert.Empty(route.TabsToClose);
	}

	[Fact]
	public void ARestart_CanReplaceTheOldTab_WhenItHasBeenQuiet()
	{
		SetGlobalBehavior(RestartBehavior.ReplaceTab);
		var old = Tab(App(processId: "(100)"), Now - ApplicationTabService.QuietTimeBeforeReplacing - TimeSpan.FromSeconds(1));

		var route = _service.Route(new[] { old }, App(processId: "(200)"), Now);

		Assert.Null(route.Tab);
		Assert.Same(old, Assert.Single(route.TabsToClose));
	}

	[Fact]
	public void ATabThatStillGetsEntries_IsNotReplaced_BecauseItsProcessIsStillRunning()
	{
		SetGlobalBehavior(RestartBehavior.ReplaceTab);
		var running = Tab(App(processId: "(100)"), Now.AddMilliseconds(-300));

		var route = _service.Route(new[] { running }, App(processId: "(200)"), Now);

		Assert.Null(route.Tab);
		Assert.Empty(route.TabsToClose);
	}

	[Fact]
	public void TheChoiceForAnApplication_BeatsTheSetting_AndIsOnlySavedWhenItDiffers()
	{
		var key = ApplicationTabService.KeyOf(App());

		_service.SetRestartBehavior(key, RestartBehavior.ExtendTab);
		Assert.Equal(RestartBehavior.ExtendTab, _service.RestartBehaviorOf(key));
		Assert.Equal(RestartBehavior.NewTab, _service.RestartBehaviorOf("another;10.0.0.9"));
		Assert.Contains(key, _environment.Saved.Applications.Keys);

		_service.SetRestartBehavior(key, RestartBehavior.NewTab);
		Assert.DoesNotContain(key, _environment.Saved.Applications.Keys);
	}

	[Fact]
	public void TheFilters_AreTakenOverByDefault_AndTheChoiceForAnApplicationBeatsTheSetting()
	{
		var key = ApplicationTabService.KeyOf(App());
		Assert.True(_service.TakesOverFilters(key));

		_service.SetTakesOverFilters(key, false);
		Assert.False(_service.TakesOverFilters(key));
		Assert.True(_service.TakesOverFilters("another;10.0.0.9"));

		var configuration = _environment.Saved;
		configuration.TakeOverFilters = false;
		_environment.Configuration.SaveConfiguration(configuration);
		Assert.False(_service.TakesOverFilters("another;10.0.0.9"));
		_service.SetTakesOverFilters(key, true);
		Assert.True(_service.TakesOverFilters(key));
	}

	[Fact]
	public void TheFiltersOfATab_AreKeptForTheNextTabOfTheApplication_AndSurviveAReload()
	{
		var key = ApplicationTabService.KeyOf(App());
		var state = new LogViewerFilterState
		{
			HiddenLevels = { "Trace", "Debug" },
			Terms = { new LogViewerFilterTerm { Text = "timeout", IsRegex = true } },
			LoggerRules = { "-Acme.Shop" },
			TimeStampFormat = TimeStampFormat.Iso
		};

		_service.RememberFilters(key, state);

		var restored = new ApplicationTabService(_environment.Configuration).FiltersForNewTab(key);
		Assert.NotNull(restored);
		Assert.True(restored!.IsSameAs(state));
		Assert.NotSame(state, restored);
		Assert.Null(_service.FiltersForNewTab(ApplicationTabService.KeyOf(App(sender: "10.0.0.6"))));
	}

	[Fact]
	public void ClearingTheFilters_ClearsWhatIsKept()
	{
		var key = ApplicationTabService.KeyOf(App());
		_service.RememberFilters(key, Filters("Trace"));

		_service.RememberFilters(key, new LogViewerFilterState());

		Assert.Null(_service.FiltersForNewTab(key));
		Assert.DoesNotContain(key, _environment.Saved.Applications.Keys);
	}

	[Fact]
	public void WithoutTheTakeover_NothingIsKept_AndSwitchingItOffForgetsWhatWasKept()
	{
		var key = ApplicationTabService.KeyOf(App());
		_service.RememberFilters(key, Filters("Trace"));

		_service.SetTakesOverFilters(key, false);
		Assert.Null(_service.FiltersForNewTab(key));

		_service.RememberFilters(key, Filters("Debug"));
		_service.SetTakesOverFilters(key, true);
		Assert.Null(_service.FiltersForNewTab(key));
	}

	[Theory]
	[InlineData("ReplaceTab", RestartBehavior.ReplaceTab)]
	[InlineData("extendtab", RestartBehavior.ExtendTab)]
	[InlineData("nonsense", RestartBehavior.NewTab)]
	[InlineData("", RestartBehavior.NewTab)]
	[InlineData(null, RestartBehavior.NewTab)]
	[InlineData("42", RestartBehavior.NewTab)]
	public void ARestartBehaviorThatIsNotValid_BecomesTheDefault(string? text, RestartBehavior expected)
	{
		Assert.Equal(expected, ApplicationTabService.ParseRestartBehavior(text));
	}

	[Fact]
	public void Sanitize_ReplacesInvalidValuesOfAHandEditedFile()
	{
		var configuration = new AppConfiguration
		{
			RestartBehavior = "sometimes",
			Applications = { ["app;10.0.0.5"] = new ApplicationTabSettings { RestartBehavior = "never" } }
		};

		ApplicationTabService.Sanitize(configuration);

		Assert.Equal(nameof(RestartBehavior.NewTab), configuration.RestartBehavior);
		Assert.Equal(nameof(RestartBehavior.NewTab), configuration.Applications["app;10.0.0.5"].RestartBehavior);
	}
}
