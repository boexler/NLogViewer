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

	/// <summary>
	/// Reads the fields that filter and search look into from the settings; anything that is not a combination of the known fields
	/// (or no field at all) gives the default
	/// </summary>
	public static SearchFields ParseSearchFields(string? value) =>
		Enum.TryParse<SearchFields>(value, true, out var fields) && fields != SearchFields.None && (fields & ~SearchFields.All) == 0
			? fields
			: SearchFields.Default;

	/// <summary>
	/// Reads the port of an address like <c>udp://0.0.0.0:4000</c>; only the port is used for listening
	/// </summary>
	public static bool TryGetUdpPort(string? address, out int port)
	{
		port = 0;
		if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "udp" || uri.Host.Length == 0)
			return false;

		port = uri.Port is >= 1 and <= 65535 ? uri.Port : 0;
		return port != 0;
	}
}
