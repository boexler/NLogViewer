namespace Sentinel.LogViewer.Models;

/// <summary>
/// Represents the log level values as defined in the Log4J specification.
/// Based on the log4j.dtd, valid levels include: all, trace, debug, info, warn, error, fatal, and off.
/// </summary>
public enum Log4JLevel
{
	/// <summary>
	/// Unknown or unparseable log level. Used as fallback when the level cannot be determined.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// All logging statements are enabled. This is the lowest threshold.
	/// </summary>
	All = 1,

	/// <summary>
	/// Trace level logging. Most verbose level for detailed diagnostic information.
	/// </summary>
	Trace = 2,

	/// <summary>
	/// Debug level logging. Detailed information for debugging purposes.
	/// </summary>
	Debug = 3,

	/// <summary>
	/// Info level logging. General informational messages about application flow.
	/// </summary>
	Info = 4,

	/// <summary>
	/// Warn level logging. Warning messages for potentially harmful situations.
	/// </summary>
	Warn = 5,

	/// <summary>
	/// Error level logging. Error events that might still allow the application to continue.
	/// </summary>
	Error = 6,

	/// <summary>
	/// Fatal level logging. Very severe error events that might cause the application to abort.
	/// </summary>
	Fatal = 7,

	/// <summary>
	/// Off level. All logging statements are disabled. This is the highest threshold.
	/// </summary>
	Off = 8
}

