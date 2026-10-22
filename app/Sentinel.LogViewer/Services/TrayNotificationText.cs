using System.Text.RegularExpressions;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// The title and the text of a notification in the tray: the application and the level, then the logger and the message of the most important
/// entry, and how many more came
/// </summary>
public static class TrayNotificationText
{
	/// <summary>Windows cuts the title of a balloon after this many characters</summary>
	public const int MaxTitleLength = 63;

	/// <summary>Windows cuts the text of a balloon after this many characters</summary>
	public const int MaxTextLength = 255;

	/// <param name="count">How many entries the notification stands for; the others are named after the message</param>
	/// <param name="moreFormat">The text for the others, with their number as {0}, e.g. <c>(+{0} more)</c></param>
	public static (string Title, string Text) Build(string appName, LogEntry entry, int count, string moreFormat)
	{
		// A long name of an application is cut, not the level behind it; without a name there is only the level.
		var level = appName.Length == 0 ? entry.Level.ToString() : $" · {entry.Level}";
		var title = appName.Length == 0 ? level : Shorten(appName, MaxTitleLength - level.Length) + level;

		var message = Regex.Replace(entry.Message, @"\s+", " ").Trim();
		var body = string.IsNullOrEmpty(entry.LoggerName) ? message : $"{entry.LoggerName}: {message}";
		var more = count > 1 ? " " + string.Format(System.Globalization.CultureInfo.CurrentCulture, moreFormat, count - 1) : string.Empty;
		return (title, Shorten(body, MaxTextLength - more.Length) + more);
	}

	private static string Shorten(string text, int length)
	{
		if (text.Length <= length)
			return text;

		// A character outside the basic range, e.g. an emoji, is two chars; the cut must not leave the first half alone.
		var cut = length - 1;
		if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
			cut--;

		return text[..cut].TrimEnd() + "…";
	}
}
