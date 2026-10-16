using System;
using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// What happens when an application that sends logs starts again: its new process is another log source than the old one
/// </summary>
public enum RestartBehavior
{
	/// <summary>A new tab opens and the old one stays</summary>
	NewTab,

	/// <summary>The entries go into the existing tab</summary>
	ExtendTab,

	/// <summary>A new tab opens and the old one is closed</summary>
	ReplaceTab
}

/// <summary>
/// What is saved for an application, which is the application name together with the sender: the choices that differ from
/// the settings, and the filters that new tabs of the application start with
/// </summary>
public class ApplicationTabSettings
{
	/// <summary>Whether new tabs take over the filters; null follows the setting</summary>
	public bool? TakeOverFilters { get; set; }

	/// <summary>The <see cref="Services.RestartBehavior"/> as text; null follows the setting</summary>
	public string? RestartBehavior { get; set; }

	/// <summary>The filters of the last tab of the application</summary>
	public LogViewerFilterState? Filters { get; set; }

	public bool IsEmpty => TakeOverFilters == null && RestartBehavior == null && Filters == null;
}

/// <summary>
/// The tabs of the applications that send logs: which tab an event goes to when an application starts again, and the filters
/// that a new tab takes over
/// </summary>
public class ApplicationTabService(ConfigurationService configurationService)
{
	/// <summary>
	/// How long a tab has to be quiet before a restart replaces it. An application that logs without a break is still running,
	/// so a second process of it is not a restart.
	/// </summary>
	public static readonly TimeSpan QuietTimeBeforeReplacing = TimeSpan.FromSeconds(2);

	/// <summary>
	/// The key of an application: its name and its sender, but not the process id, which changes at every start
	/// </summary>
	public static string KeyOf(AppInfo appInfo) => $"{appInfo.AppName?.Name};{appInfo.Sender}".ToLowerInvariant();

	public static RestartBehavior ParseRestartBehavior(string? text, RestartBehavior fallback = RestartBehavior.NewTab) =>
		Enum.TryParse<RestartBehavior>(text, ignoreCase: true, out var behavior) && Enum.IsDefined(behavior) ? behavior : fallback;

	/// <summary>
	/// Replaces values of a settings file that are not valid
	/// </summary>
	public static void Sanitize(AppConfiguration configuration)
	{
		configuration.RestartBehavior = ParseRestartBehavior(configuration.RestartBehavior).ToString();
		configuration.Applications ??= new();
		foreach (var settings in configuration.Applications.Values)
		{
			if (settings.RestartBehavior != null)
				settings.RestartBehavior = ParseRestartBehavior(settings.RestartBehavior, RestartBehavior.NewTab).ToString();
		}
	}

	public bool TakesOverFilters(string key)
	{
		var configuration = configurationService.LoadConfiguration();
		return TakesOverFilters(configuration, key);
	}

	public RestartBehavior RestartBehaviorOf(string key)
	{
		var configuration = configurationService.LoadConfiguration();
		return RestartBehaviorOf(configuration, key);
	}

	/// <summary>
	/// Sets whether new tabs of the application take over the filters. Only a value that differs from the setting is saved,
	/// and without the takeover nothing is kept of the filters.
	/// </summary>
	public void SetTakesOverFilters(string key, bool value)
	{
		var configuration = configurationService.LoadConfiguration();
		var settings = GetOrCreate(configuration, key);
		settings.TakeOverFilters = value == configuration.TakeOverFilters ? null : value;
		if (!value)
			settings.Filters = null;

		Save(configuration, key, settings);
	}

	public void SetRestartBehavior(string key, RestartBehavior value)
	{
		var configuration = configurationService.LoadConfiguration();
		var settings = GetOrCreate(configuration, key);
		settings.RestartBehavior = value == ParseRestartBehavior(configuration.RestartBehavior) ? null : value.ToString();

		Save(configuration, key, settings);
	}

	/// <summary>
	/// The filters that a new tab of the application starts with, or null when there are none or the application does not take them over
	/// </summary>
	public LogViewerFilterState? FiltersForNewTab(string key)
	{
		var configuration = configurationService.LoadConfiguration();
		if (!TakesOverFilters(configuration, key) || !configuration.Applications.TryGetValue(key, out var settings))
			return null;

		return settings.Filters is { IsEmpty: false } filters ? filters.Clone() : null;
	}

	/// <summary>
	/// Keeps the filters of a tab for the next tab of the application, unless the application does not take them over
	/// </summary>
	public void RememberFilters(string key, LogViewerFilterState? filters)
	{
		var configuration = configurationService.LoadConfiguration();
		if (!TakesOverFilters(configuration, key))
			return;

		configuration.Applications.TryGetValue(key, out var existing);
		var state = filters is { IsEmpty: false } ? filters.Clone() : null;
		if (state == null && existing?.Filters == null)
			return;

		var settings = existing ?? GetOrCreate(configuration, key);
		if (state != null && state.IsSameAs(settings.Filters))
			return;

		settings.Filters = state;
		Save(configuration, key, settings);
	}

	/// <summary>
	/// Decides which tab the events of an application go to: the tab of that process, or after a restart the tab of the old process
	/// when the application continues in it, or a new tab
	/// </summary>
	public TabRoute Route(IReadOnlyList<LogTabViewModel> tabs, AppInfo appInfo, DateTime nowUtc)
	{
		var exact = tabs.FirstOrDefault(tab => tab.TargetName == appInfo.Id);
		if (exact != null)
			return new TabRoute(exact, Array.Empty<LogTabViewModel>());

		var key = KeyOf(appInfo);
		var earlier = tabs.Where(tab => tab.SenderKey == key).ToList();
		if (earlier.Count == 0)
			return new TabRoute(null, Array.Empty<LogTabViewModel>());

		switch (RestartBehaviorOf(key))
		{
			case RestartBehavior.ExtendTab:
				return new TabRoute(earlier.OrderByDescending(tab => tab.LastEventUtc).First(), Array.Empty<LogTabViewModel>());
			case RestartBehavior.ReplaceTab:
				return new TabRoute(null, earlier.Where(tab => nowUtc - tab.LastEventUtc >= QuietTimeBeforeReplacing).ToList());
			default:
				return new TabRoute(null, Array.Empty<LogTabViewModel>());
		}
	}

	private static bool TakesOverFilters(AppConfiguration configuration, string key) =>
		configuration.Applications.TryGetValue(key, out var settings) && settings.TakeOverFilters != null
			? settings.TakeOverFilters.Value
			: configuration.TakeOverFilters;

	private static RestartBehavior RestartBehaviorOf(AppConfiguration configuration, string key)
	{
		var global = ParseRestartBehavior(configuration.RestartBehavior);
		return configuration.Applications.TryGetValue(key, out var settings) && settings.RestartBehavior != null
			? ParseRestartBehavior(settings.RestartBehavior, global)
			: global;
	}

	private static ApplicationTabSettings GetOrCreate(AppConfiguration configuration, string key)
	{
		if (!configuration.Applications.TryGetValue(key, out var settings))
			settings = new ApplicationTabSettings();

		return settings;
	}

	private void Save(AppConfiguration configuration, string key, ApplicationTabSettings settings)
	{
		if (settings.IsEmpty)
			configuration.Applications.Remove(key);
		else
			configuration.Applications[key] = settings;

		configurationService.SaveConfiguration(configuration);
	}
}

/// <summary>
/// The tab that events go to; null for a new tab. Tabs of an application that is replaced are to be closed.
/// </summary>
public sealed record TabRoute(LogTabViewModel? Tab, IReadOnlyList<LogTabViewModel> TabsToClose);
