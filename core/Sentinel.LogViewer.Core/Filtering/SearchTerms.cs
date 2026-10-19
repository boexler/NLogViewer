using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// Base class for search terms that can match against text
/// </summary>
public abstract class SearchTerm
{
	public string Text { get; protected set; }

	protected SearchTerm(string text)
	{
		Text = text ?? throw new ArgumentNullException(nameof(text));
	}

	/// <summary>
	/// Determines if the search term matches the given text
	/// </summary>
	/// <param name="text">The text to match against</param>
	/// <returns>True if the text matches the search term</returns>
	public abstract bool Match(string text);

	/// <summary>
	/// Determines if the search term matches any of the given texts
	/// </summary>
	/// <param name="texts">The texts to match against</param>
	/// <returns>True if any text matches the search term</returns>
	public bool MatchAny(params string[] texts)
	{
		if (texts == null) return false;
		return texts.Any(text => !string.IsNullOrEmpty(text) && Match(text));
	}

	public override string ToString()
	{
		return Text;
	}
}

/// <summary>
/// A search term that performs case-insensitive substring matching
/// </summary>
public class TextSearchTerm : SearchTerm
{
	private readonly string _lowercaseText;

	public TextSearchTerm(string text) : base(text)
	{
		_lowercaseText = text.ToLowerInvariant();
	}

	public override bool Match(string text)
	{
		if (string.IsNullOrEmpty(text)) return false;
		if (string.IsNullOrEmpty(_lowercaseText)) return false;
		return text.ToLowerInvariant().Contains(_lowercaseText);
	}

	public override string ToString()
	{
		return Text;
	}
}

/// <summary>
/// A search term that performs regex pattern matching
/// </summary>
public class RegexSearchTerm : SearchTerm
{
	private readonly Regex _regex;

	public RegexSearchTerm(string pattern) : base(pattern)
	{
		try
		{
			_regex = new Regex(pattern, RegexOptions.Compiled);
		}
		catch (ArgumentException ex)
		{
			throw new ArgumentException($"Invalid regex pattern: {ex.Message}", nameof(pattern), ex);
		}
	}

	public override bool Match(string text)
	{
		if (string.IsNullOrEmpty(text)) return false;
		return _regex.IsMatch(text);
	}

	public override string ToString()
	{
		return $"/{Text}/";
	}
}
