using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// The parts of an entry that a filter term or a search looks into
/// </summary>
[Flags]
public enum SearchFields
{
	None = 0,
	Message = 1,
	LoggerName = 2,
	Exception = 4,

	/// <summary>The property <c>Thread</c> of the entry</summary>
	Thread = 8,

	/// <summary>All other properties of the entry, e.g. the fields of a JSON line or the class and method of a Log4J message</summary>
	Properties = 16,

	/// <summary>What is searched when nothing else is chosen</summary>
	Default = Message | LoggerName,

	All = Message | LoggerName | Exception | Thread | Properties
}

/// <summary>
/// A text that entries are filtered or searched for, with the options that belong to it. It is the one place that says whether an entry
/// matches: the filter, the search and the highlighting of the viewer use it.
/// </summary>
public sealed class FilterTerm : INotifyPropertyChanged
{
	/// <summary>The name of the property that holds the thread of an entry</summary>
	public const string ThreadProperty = "Thread";

	private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

	private readonly Regex? _regex;
	private readonly StringComparison _comparison;
	private bool _isEnabled = true;

	/// <param name="text">The text to look for; a regular expression when <paramref name="isRegex"/> is set</param>
	/// <param name="isExclude">Whether entries with a match are hidden instead of the others</param>
	/// <param name="matchCase">Whether upper and lower case letters are different</param>
	/// <param name="wholeWord">Whether the text only counts when it is not part of a longer word</param>
	/// <param name="isRegex">Whether the text is a regular expression</param>
	/// <param name="level">When given, only entries of this level can match</param>
	/// <exception cref="ArgumentException">The text is not a valid regular expression</exception>
	public FilterTerm(string text, bool isExclude = false, bool matchCase = false, bool wholeWord = false, bool isRegex = false,
		LogEntryLevel? level = null)
	{
		Text = text ?? throw new ArgumentNullException(nameof(text));
		IsExclude = isExclude;
		MatchCase = matchCase;
		WholeWord = wholeWord;
		IsRegex = isRegex;
		Level = level;
		_comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

		// A plain text without whole word is found with a string search, which is faster than a regular expression.
		if (isRegex || wholeWord)
		{
			var pattern = isRegex ? text : Regex.Escape(text);
			if (wholeWord)
				pattern = $@"(?<!\w)(?:{pattern})(?!\w)";

			var options = RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
			try
			{
				_regex = new Regex(pattern, options, RegexTimeout);
			}
			catch (ArgumentException ex)
			{
				throw new ArgumentException($"Invalid regex pattern: {ex.Message}", nameof(text), ex);
			}
		}
	}

	public string Text { get; }

	public bool IsExclude { get; }

	public bool MatchCase { get; }

	public bool WholeWord { get; }

	public bool IsRegex { get; }

	public LogEntryLevel? Level { get; }

	/// <summary>
	/// Whether the term is applied. A term that is off stays in the list but does not filter and does not mark anything; <see cref="Matches"/>
	/// does not look at it, whoever applies the terms does.
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set
		{
			if (_isEnabled == value)
				return;

			_isEnabled = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	/// <summary>
	/// Returns why a text is not valid as a term, or null when it is valid (or empty)
	/// </summary>
	public static string? Validate(string? text, bool isRegex)
	{
		if (!isRegex || string.IsNullOrEmpty(text))
			return null;

		try
		{
			_ = new Regex(text, RegexOptions.CultureInvariant, RegexTimeout);
			return null;
		}
		catch (ArgumentException ex)
		{
			return ex.Message;
		}
	}

	/// <summary>
	/// Returns whether the term fits the entry: the entry has the level of the term, if it has one, and the text is found in one of the
	/// fields. For a term that excludes, a match is what hides the entry.
	/// </summary>
	public bool Matches(LogEntry entry, SearchFields fields = SearchFields.Default)
	{
		ArgumentNullException.ThrowIfNull(entry);

		if (Text.Length == 0 || (Level != null && entry.Level != Level))
			return false;

		if (fields.HasFlag(SearchFields.Message) && Contains(entry.Message))
			return true;

		if (fields.HasFlag(SearchFields.LoggerName) && Contains(entry.LoggerName))
			return true;

		if (fields.HasFlag(SearchFields.Exception) && entry.Exception != null && Contains(entry.Exception))
			return true;

		if (fields.HasFlag(SearchFields.Thread) && entry.Properties.TryGetValue(ThreadProperty, out var thread) && thread != null
		    && Contains(Convert.ToString(thread, System.Globalization.CultureInfo.InvariantCulture)))
			return true;

		if (fields.HasFlag(SearchFields.Properties))
		{
			foreach (var (name, value) in entry.Properties)
			{
				if (name != ThreadProperty && value != null && Contains(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)))
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Returns whether the text contains the term
	/// </summary>
	public bool Contains(string? text)
	{
		if (string.IsNullOrEmpty(text) || Text.Length == 0)
			return false;

		if (_regex == null)
			return text.Contains(Text, _comparison);

		try
		{
			return _regex.IsMatch(text);
		}
		catch (RegexMatchTimeoutException)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns where the term is found in the text, as the start and the length of each match; for highlighting
	/// </summary>
	public IEnumerable<(int Start, int Length)> FindMatches(string? text)
	{
		if (string.IsNullOrEmpty(text) || Text.Length == 0)
			yield break;

		if (_regex == null)
		{
			var position = 0;
			while ((position = text.IndexOf(Text, position, _comparison)) >= 0)
			{
				yield return (position, Text.Length);
				position += Text.Length;
			}

			yield break;
		}

		MatchCollection matches;
		try
		{
			matches = _regex.Matches(text);
		}
		catch (RegexMatchTimeoutException)
		{
			yield break;
		}

		foreach (Match match in matches)
		{
			if (match.Success && match.Length > 0)
				yield return (match.Index, match.Length);
		}
	}

	/// <summary>
	/// Returns whether another term is the same: the same text with the same options
	/// </summary>
	public bool IsSameAs(FilterTerm? other) =>
		other != null && Text == other.Text && IsExclude == other.IsExclude && MatchCase == other.MatchCase
		&& WholeWord == other.WholeWord && IsRegex == other.IsRegex && Level == other.Level;

	public override string ToString() => Text;
}
