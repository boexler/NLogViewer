namespace Sentinel.LogViewer.Wpf.Models;

/// <summary>
/// How the time stamp column shows the time of an entry. All of them have a fixed width and show milliseconds.
/// </summary>
public enum TimeStampFormat
{
	/// <summary>
	/// The short date and the long time of the culture of the computer, e.g. <c>18.12.2025 15:11:48.385</c>
	/// </summary>
	System,

	/// <summary>
	/// <c>2025-12-18 15:11:48.385</c>
	/// </summary>
	Iso,

	/// <summary>
	/// <c>18.12.2025 15:11:48.385</c>
	/// </summary>
	German,

	/// <summary>
	/// <c>12/18/2025 03:11:48.385 PM</c>
	/// </summary>
	English
}
