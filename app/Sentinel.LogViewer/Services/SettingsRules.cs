using System;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Limits and validation shared by the settings window and the settings import
/// </summary>
public static class SettingsRules
{
	public const int MinEntriesPerTab = 100;
	public const int MaxEntriesPerTab = 1_000_000;

	/// <summary>
	/// Address of the port that is configured by default
	/// </summary>
	public const string DefaultPortAddress = "udp://0.0.0.0:4000";

	public static bool IsValidEntriesPerTab(int value) => value is >= MinEntriesPerTab and <= MaxEntriesPerTab;

	public const int MinLoggerTreeWidth = (int)Wpf.LogViewer.MinLoggerTreeWidth;
	public const int MaxLoggerTreeWidth = 1200;
	public const int DefaultLoggerTreeWidth = 260;

	public static bool IsValidLoggerTreeWidth(int value) => value is >= MinLoggerTreeWidth and <= MaxLoggerTreeWidth;

	/// <summary>The value of the setting for no notification in the tray; the others are the names of the levels Warn, Error and Fatal</summary>
	public const string TrayNotifyOff = "Off";

	public static Core.LogEntryLevel? ParseTrayNotifyLevel(string? text) => text switch
	{
		"Warn" => Core.LogEntryLevel.Warn,
		"Error" => Core.LogEntryLevel.Error,
		"Fatal" => Core.LogEntryLevel.Fatal,
		_ => null
	};

	/// <summary>Whether a font family is installed, by its name in any language</summary>
	public static bool IsInstalledFont(string name) => System.Windows.Media.Fonts.SystemFontFamilies.Any(family =>
		family.FamilyNames.Values.Append(family.Source).Contains(name, StringComparer.OrdinalIgnoreCase));

	public const int MinListFontSize = 6;
	public const int MaxListFontSize = 40;

	/// <summary>The size that the list has without a setting (the size of the message font of Windows)</summary>
	public const int DefaultListFontSize = 12;

	public static bool IsValidListFontSize(int value) => value is >= MinListFontSize and <= MaxListFontSize;

	/// <summary>
	/// Reads the fields that filter and search look into from the settings; anything that is not a combination of the known fields
	/// (or no field at all) gives the default
	/// </summary>
	public static SearchFields ParseSearchFields(string? value) =>
		Enum.TryParse<SearchFields>(value, true, out var fields) && fields != SearchFields.None && (fields & ~SearchFields.All) == 0
			? fields
			: SearchFields.Default;

	/// <summary>
	/// Reads the port of an address like <c>udp://0.0.0.0:4000</c> or <c>tcp://0.0.0.0:4000</c>; only the port is used for listening
	/// </summary>
	public static bool TryGetPort(string? address, out int port)
	{
		port = 0;
		if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("udp" or "tcp") || uri.Host.Length == 0)
			return false;

		port = uri.Port is >= 1 and <= 65535 ? uri.Port : 0;
		return port != 0;
	}

	/// <summary>
	/// The scheme of an address, <c>udp</c> or <c>tcp</c>; null when it is not an address
	/// </summary>
	public static string? SchemeOf(string? address) => Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) ? uri.Scheme : null;
}
