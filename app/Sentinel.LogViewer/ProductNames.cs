namespace Sentinel.LogViewer;

/// <summary>
/// Product display and technical identity strings for the desktop application.
/// </summary>
public static class ProductNames
{
	/// <summary>User-visible product name (window title, ARP, shortcuts).</summary>
	public const string Display = "Sentinel LogViewer";

	/// <summary>Technical product id used for exe name, install folder, and AppData.</summary>
	public const string Technical = "Sentinel.LogViewer";

	/// <summary>Legacy AppData folder name before the Sentinel.LogViewer rename.</summary>
	public const string LegacyAppDataFolder = "Sentinel.NLogViewer.App";
}
