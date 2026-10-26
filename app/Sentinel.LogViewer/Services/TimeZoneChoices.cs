using System;
using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// One zone in a list of choices. The id is empty for the local time of the computer, so that a list can select it.
/// </summary>
public sealed record TimeZoneChoice(string Id, string Name)
{
	/// <summary>The zone, or null for the local time of the computer</summary>
	public TimeZoneInfo? Zone => EntryTime.FindZone(Id);
}

/// <summary>
/// The zones that can be chosen for the times of a tab or a file: the local time, UTC, and the zones of the computer by their offset
/// </summary>
public static class TimeZoneChoices
{
	/// <param name="localization">Gives the texts in the language that is set; without it they are English</param>
	/// <param name="withNotSet">Adds a first choice with an empty id, for a place that can leave the zone open (the mapping of a file)</param>
	public static IReadOnlyList<TimeZoneChoice> Create(LocalizationService? localization = null, bool withNotSet = false)
	{
		var choices = new List<TimeZoneChoice>();
		if (withNotSet)
			choices.Add(new TimeZoneChoice(string.Empty, localization?.GetString("TimeZone_NotSet", "Not specified (as in the tab)") ?? "Not specified (as in the tab)"));

		var local = localization?.GetString("TimeZone_Local", "Local time (this computer)") ?? "Local time (this computer)";
		choices.Add(new TimeZoneChoice(EntryTime.LocalId, local));
		choices.Add(new TimeZoneChoice(TimeZoneInfo.Utc.Id, "UTC"));
		choices.AddRange(TimeZoneInfo.GetSystemTimeZones()
			.Where(zone => !zone.Equals(TimeZoneInfo.Utc))
			.OrderBy(zone => zone.BaseUtcOffset)
			.ThenBy(zone => zone.DisplayName, StringComparer.CurrentCultureIgnoreCase)
			.Select(zone => new TimeZoneChoice(zone.Id, zone.DisplayName)));
		return choices;
	}

	/// <summary>
	/// The zone that the tab of a file starts with: only when its entries have times without a zone, the zone that is saved with the mapping of
	/// the file (the id <see cref="EntryTime.LocalId"/> is a choice of the local time), and while none is saved, the one that was chosen last;
	/// null (local time) otherwise
	/// </summary>
	public static TimeZoneInfo? ForNewFile(IEnumerable<LogEntry> entries, string? savedZoneId, TimeZoneInfo? lastZone)
	{
		if (!entries.Any(entry => entry.TimeStamp.Kind == DateTimeKind.Unspecified))
			return null;

		return string.IsNullOrEmpty(savedZoneId) ? lastZone : EntryTime.FindZone(savedZoneId);
	}

	/// <summary>
	/// Whether two zones are the same; null and the zone of the computer are the local time
	/// </summary>
	public static bool SameZone(TimeZoneInfo? first, TimeZoneInfo? second) => (first ?? TimeZoneInfo.Local).Id == (second ?? TimeZoneInfo.Local).Id;

	/// <summary>
	/// The entries of a file that is added to a tab that exists. A time without a zone is read in the zone of the tab, which is wrong for a file
	/// of another zone, so such a file keeps its own: its times become moments (the file is not changed). The zone of the file is the one that is
	/// saved with its mapping; when there is none and the tab does not read local time, <paramref name="readLikeTab"/> asks whether to read the
	/// times like the tab does.
	/// </summary>
	/// <returns>The same list when nothing has to change, otherwise a new list</returns>
	public static List<LogEntry> FitToTab(List<LogEntry> entries, string? savedZoneId, TimeZoneInfo? tabZone, Func<bool> readLikeTab)
	{
		if (!entries.Any(entry => entry.TimeStamp.Kind == DateTimeKind.Unspecified))
			return entries;

		TimeZoneInfo? fileZone;
		if (!string.IsNullOrEmpty(savedZoneId))
			fileZone = EntryTime.FindZone(savedZoneId);
		else if (SameZone(tabZone, null) || readLikeTab())
			fileZone = tabZone;
		else
			fileZone = null;

		return SameZone(fileZone, tabZone)
			? entries
			: entries.Select(entry => entry.TimeStamp.Kind == DateTimeKind.Unspecified ? entry.WithTimeStamp(EntryTime.ToMoment(entry.TimeStamp, fileZone)) : entry).ToList();
	}

	/// <summary>
	/// The choice of a zone; the local time for null and for <see cref="TimeZoneInfo.Local"/> itself. Compared by instance, because on a computer
	/// that runs in UTC the local zone is equal to UTC, and a chosen UTC must stay UTC.
	/// </summary>
	public static TimeZoneChoice Find(IReadOnlyList<TimeZoneChoice> choices, TimeZoneInfo? zone)
	{
		var local = choices.First(choice => choice.Id == EntryTime.LocalId);
		if (zone == null || ReferenceEquals(zone, TimeZoneInfo.Local))
			return local;

		var id = zone.Equals(TimeZoneInfo.Utc) ? TimeZoneInfo.Utc.Id : zone.Id;
		return choices.FirstOrDefault(choice => choice.Id == id) ?? local;
	}
}
