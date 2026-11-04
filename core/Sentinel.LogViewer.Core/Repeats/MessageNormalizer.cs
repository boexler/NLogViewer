using System.Text.RegularExpressions;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// Makes messages comparable that differ only in what changes from call to call: GUIDs, dates, times and long numbers are replaced by a word in
/// braces, so "Request 4f9c… took 1523 ms at 10:04:37" and the same line a minute later are one message
/// </summary>
public static partial class MessageNormalizer
{
	// Dates before numbers, so that 20260515 is a number and 2026-05-15 a date; a date with "T", time and zone (ISO 8601) is one date.
	private static readonly (Regex Pattern, string Word)[] Rules =
	{
		(Guid(), "{guid}"),
		(Date(), "{date}"),
		(Time(), "{time}"),
		(Number(), "{number}")
	};

	public static string Normalize(string message)
	{
		foreach (var (pattern, word) in Rules)
			message = pattern.Replace(message, word);

		return message;
	}

	[GeneratedRegex(@"\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b")]
	private static partial Regex Guid();

	[GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?\b|\b\d{1,2}[./]\d{1,2}[./]\d{2,4}\b")]
	private static partial Regex Date();

	[GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?\b")]
	private static partial Regex Time();

	// shortcut: five digits or more; shorter numbers (HTTP codes, years, counts) stay, so "error 404" and "error 500" are two messages. Change when users say otherwise.
	[GeneratedRegex(@"\b\d{5,}\b")]
	private static partial Regex Number();
}
