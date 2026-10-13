using System;
using System.Collections.Generic;
using System.Text;

namespace Sentinel.LogViewer.Parsers;

/// <summary>
/// Splits lines of column-based text log files
/// </summary>
public static class TextLineSplitter
{
	/// <summary>Separator value that stands for "any run of spaces".</summary>
	public const string SpacesSeparator = "  ";

	/// <summary>
	/// Splits a line into its columns
	/// </summary>
	/// <param name="line">Line (or multi-line record) to split</param>
	/// <param name="separator">Column separator</param>
	/// <param name="quotedFields">Whether fields may be wrapped in double quotes (CSV style, inner quotes doubled)</param>
	/// <param name="maxColumns">Maximum number of columns; the last one takes the rest of the line. 0 for no limit</param>
	public static string[] Split(string line, string separator, bool quotedFields = false, int maxColumns = 0)
	{
		var count = maxColumns > 0 ? maxColumns : int.MaxValue;

		if (separator == SpacesSeparator)
			return line.Split(' ', count, StringSplitOptions.RemoveEmptyEntries);

		if (!quotedFields)
			return line.Split(separator, count);

		var fields = new List<string>();
		var position = 0;
		while (true)
		{
			var isLastColumn = fields.Count == count - 1;
			if (TryReadQuotedField(line, separator, position, isLastColumn, out var field, out var end))
			{
				fields.Add(field);
			}
			else
			{
				end = isLastColumn ? -1 : line.IndexOf(separator, position, StringComparison.Ordinal);
				fields.Add(end < 0 ? line[position..] : line[position..end]);
			}

			if (end < 0)
				return fields.ToArray();

			position = end + separator.Length;
		}
	}

	/// <summary>
	/// Returns whether a quoted field is still open at the end of the line, i.e. the record continues on the next line
	/// </summary>
	/// <param name="line">Physical line to scan</param>
	/// <param name="separator">Column separator</param>
	/// <param name="startsInsideQuotedField">Whether the line starts inside a quoted field opened on a previous line</param>
	public static bool EndsInsideQuotedField(string line, string separator, bool startsInsideQuotedField)
	{
		var insideQuotes = startsInsideQuotedField;
		var atFieldStart = !startsInsideQuotedField;

		for (var i = 0; i < line.Length; i++)
		{
			if (insideQuotes)
			{
				if (line[i] != '"')
					continue;

				if (i + 1 < line.Length && line[i + 1] == '"')
					i++;
				else
					insideQuotes = false;
			}
			else if (atFieldStart && line[i] == '"')
			{
				insideQuotes = true;
				atFieldStart = false;
			}
			else if (string.CompareOrdinal(line, i, separator, 0, separator.Length) == 0)
			{
				atFieldStart = true;
				i += separator.Length - 1;
			}
			else
			{
				atFieldStart = false;
			}
		}

		return insideQuotes;
	}

	/// <summary>
	/// Reads a quoted field starting at <paramref name="position"/>. Fails when the field is not quoted or
	/// text follows the closing quote, so that such a field is taken literally.
	/// </summary>
	private static bool TryReadQuotedField(string line, string separator, int position, bool isLastColumn,
		out string field, out int end)
	{
		field = string.Empty;
		end = -1;
		if (position >= line.Length || line[position] != '"')
			return false;

		var builder = new StringBuilder();
		for (var i = position + 1; i < line.Length; i++)
		{
			if (line[i] != '"')
			{
				builder.Append(line[i]);
				continue;
			}

			if (i + 1 < line.Length && line[i + 1] == '"')
			{
				builder.Append('"');
				i++;
				continue;
			}

			var afterQuote = i + 1;
			var atSeparator = !isLastColumn &&
			                  string.CompareOrdinal(line, afterQuote, separator, 0, separator.Length) == 0;
			if (afterQuote != line.Length && !atSeparator)
				return false;

			field = builder.ToString();
			end = atSeparator ? afterQuote : -1;
			return true;
		}

		// Unterminated quote: take the rest as the field content.
		field = builder.ToString();
		return true;
	}
}
