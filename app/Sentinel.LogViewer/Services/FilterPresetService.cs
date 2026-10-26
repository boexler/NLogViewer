using System;
using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// A named combination of filters that can be applied to a tab: the levels, the filter terms with their options, the fields they look
/// into and the hidden loggers. The format of the time stamp is not part of it; that is a choice of the view, not of the filter.
/// </summary>
public class FilterPreset
{
	public string Name { get; set; } = string.Empty;

	public LogViewerFilterState Filters { get; set; } = new();
}

/// <summary>
/// The saved filter presets. They are part of the settings, so they are exported and imported with them.
/// </summary>
public class FilterPresetService(ConfigurationService configurationService)
{
	public IReadOnlyList<FilterPreset> GetAll() => configurationService.LoadConfiguration().FilterPresets.ToList();

	public bool Exists(string name) => Find(GetAll(), name) != null;

	/// <summary>
	/// Saves the filters under the name; a preset of the same name is replaced. Returns whether one was replaced.
	/// </summary>
	public bool Save(string name, LogViewerFilterState filters)
	{
		var configuration = configurationService.LoadConfiguration();
		var replaced = Upsert(configuration.FilterPresets, name, filters);
		configurationService.SaveConfiguration(configuration);
		return replaced;
	}

	public static string CleanName(string? name) => name?.Trim() ?? string.Empty;

	public static FilterPreset? Find(IEnumerable<FilterPreset> presets, string name) =>
		presets.FirstOrDefault(preset => string.Equals(preset.Name, CleanName(name), StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Puts the filters in the list under the name, in the place of the preset of that name or at the end. Returns whether one was replaced.
	/// </summary>
	public static bool Upsert(List<FilterPreset> presets, string name, LogViewerFilterState filters)
	{
		var preset = new FilterPreset { Name = CleanName(name), Filters = ForPreset(filters) };
		if (preset.Name.Length == 0)
			throw new ArgumentException("A preset needs a name.", nameof(name));

		var existing = Find(presets, preset.Name);
		if (existing == null)
		{
			presets.Add(preset);
			return false;
		}

		presets[presets.IndexOf(existing)] = preset;
		return true;
	}

	/// <summary>
	/// Gives a preset another name; it does nothing and returns false when the new name is empty or another preset has it
	/// </summary>
	public static bool TryRename(List<FilterPreset> presets, FilterPreset preset, string newName)
	{
		var name = CleanName(newName);
		if (name.Length == 0 || Find(presets.Where(other => !ReferenceEquals(other, preset)), name) != null)
			return false;

		preset.Name = name;
		return true;
	}

	/// <summary>
	/// A copy of the filters without what does not belong to a preset
	/// </summary>
	public static LogViewerFilterState ForPreset(LogViewerFilterState filters)
	{
		var copy = filters.WithoutTimeRange();
		copy.TimeStampFormat = null;
		return copy;
	}

	/// <summary>
	/// Drops what a hand-edited file could contain: presets without a name or without filters, and a second preset of the same name
	/// </summary>
	public static void Sanitize(AppConfiguration configuration)
	{
		var clean = new List<FilterPreset>();
		foreach (var preset in configuration.FilterPresets ?? new List<FilterPreset>())
		{
			var name = CleanName(preset?.Name);
			if (preset?.Filters == null || name.Length == 0 || Find(clean, name) != null)
				continue;

			clean.Add(new FilterPreset { Name = name, Filters = ForPreset(preset.Filters) });
		}

		configuration.FilterPresets = clean;
	}
}
