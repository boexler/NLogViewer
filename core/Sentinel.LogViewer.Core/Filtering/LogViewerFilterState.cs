using System;
using System.Collections.Generic;
using System.Linq;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// A filter term of a viewer: a text or a regular expression
/// </summary>
public sealed class LogViewerFilterTerm
{
	public string Text { get; set; } = string.Empty;

	public bool IsRegex { get; set; }

	/// <summary>Whether the entries that match are hidden instead of the others</summary>
	public bool IsExclude { get; set; }

	public bool MatchCase { get; set; }

	public bool WholeWord { get; set; }

	/// <summary>When given, the term only matches entries of this level</summary>
	public LogEntryLevel? Level { get; set; }

	/// <summary>Whether the term is applied; a term that is off stays in the list. A state that was saved before the switch existed has all its terms on.</summary>
	public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// What a viewer filters: the hidden levels, the filter terms, the hidden and shown loggers and the format of the time stamp column.
/// The search box is not part of it. A copy of it can be saved, handed to another viewer and applied there.
/// </summary>
public sealed class LogViewerFilterState
{
	/// <summary>
	/// Names of the hidden levels, e.g. <c>Trace</c> and <c>Debug</c>
	/// </summary>
	public List<string> HiddenLevels { get; set; } = new();

	public List<LogViewerFilterTerm> Terms { get; set; } = new();

	/// <summary>
	/// The parts of an entry that the terms look into
	/// </summary>
	public SearchFields SearchFields { get; set; } = SearchFields.Default;

	/// <summary>
	/// The hidden and shown loggers as rules like <c>-Company.Product</c> and <c>+Company.Product.Service</c>
	/// </summary>
	public List<string> LoggerRules { get; set; } = new();

	/// <summary>
	/// The format of the time stamp column that the viewer has chosen, or null for the default
	/// </summary>
	public TimeStampFormat? TimeStampFormat { get; set; }

	/// <summary>
	/// The start of the time range: only entries from this moment on (inclusive) are shown; null for no start. A moment in UTC, so it does not
	/// change when the zone of the display does. It is not saved in presets and not taken over into new tabs: it belongs to the entries that are
	/// in the tab now.
	/// </summary>
	public DateTime? TimeFrom { get; set; }

	/// <summary>
	/// The end of the time range: only entries up to this moment (inclusive) are shown; null for no end. A moment in UTC.
	/// </summary>
	public DateTime? TimeTo { get; set; }

	/// <summary>
	/// Whether nothing is filtered and the time stamp format is the default
	/// </summary>
	public bool IsEmpty => HiddenLevels.Count == 0 && Terms.Count == 0 && LoggerRules.Count == 0 && TimeStampFormat == null && SearchFields == SearchFields.Default
		&& TimeFrom == null && TimeTo == null;

	/// <summary>
	/// A copy of the filters without the time range
	/// </summary>
	public LogViewerFilterState WithoutTimeRange()
	{
		var copy = Clone();
		copy.TimeFrom = null;
		copy.TimeTo = null;
		return copy;
	}

	public bool IsSameAs(LogViewerFilterState? other) =>
		other != null
		&& TimeStampFormat == other.TimeStampFormat
		&& SearchFields == other.SearchFields
		&& TimeFrom == other.TimeFrom
		&& TimeTo == other.TimeTo
		&& HiddenLevels.SequenceEqual(other.HiddenLevels, StringComparer.Ordinal)
		&& LoggerRules.SequenceEqual(other.LoggerRules, StringComparer.Ordinal)
		&& Terms.Select(TermKey).SequenceEqual(other.Terms.Select(TermKey));

	private static (string, bool, bool, bool, bool, LogEntryLevel?, bool) TermKey(LogViewerFilterTerm term) =>
		(term.Text, term.IsRegex, term.IsExclude, term.MatchCase, term.WholeWord, term.Level, term.IsEnabled);

	public LogViewerFilterState Clone() => new()
	{
		HiddenLevels = HiddenLevels.ToList(),
		Terms = Terms.Select(term => new LogViewerFilterTerm
		{
			Text = term.Text, IsRegex = term.IsRegex, IsExclude = term.IsExclude, MatchCase = term.MatchCase, WholeWord = term.WholeWord, Level = term.Level,
			IsEnabled = term.IsEnabled
		}).ToList(),
		SearchFields = SearchFields,
		LoggerRules = LoggerRules.ToList(),
		TimeStampFormat = TimeStampFormat,
		TimeFrom = TimeFrom,
		TimeTo = TimeTo
	};
}
