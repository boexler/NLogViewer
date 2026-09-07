namespace Sentinel.NLogViewer.App.Models;

/// <summary>
/// Represents location information about where a log event was generated in the source code.
/// This class models the structure defined in the log4j.dtd specification for log4j:locationInfo elements.
/// </summary>
public class Log4JLocationInfo
{
	/// <summary>
	/// Gets or sets the fully qualified name of the class where the log event was generated.
	/// Corresponds to the "class" attribute in the log4j:locationInfo element.
	/// </summary>
	public string Class { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the method where the log event was generated.
	/// Corresponds to the "method" attribute in the log4j:locationInfo element.
	/// </summary>
	public string Method { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the source file where the log event was generated.
	/// Corresponds to the "file" attribute in the log4j:locationInfo element.
	/// </summary>
	public string File { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the line number in the source file where the log event was generated.
	/// Corresponds to the "line" attribute in the log4j:locationInfo element.
	/// May be null if line information is not available.
	/// </summary>
	public int? Line { get; set; }
}