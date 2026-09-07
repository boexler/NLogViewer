using NLog;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sentinel.NLogViewer.App.Models;

public interface ILogEvent
{
	public LogEvent ToLogEvent(string sender);
}

/// <summary>
/// Represents a Log4J logging event parsed from XML format.
/// This class models the structure defined in the log4j.dtd specification for log4j:event elements.
/// </summary>
public class Log4JEvent : ILogEvent
{
	/// <summary>
	/// Gets or sets the name of the logger that generated this event.
	/// Corresponds to the "logger" attribute in the log4j:event element.
	/// </summary>
	public string Logger { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the log level of this event.
	/// Corresponds to the "level" attribute in the log4j:event element.
	/// </summary>
	public Log4JLevel Level { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the event in milliseconds since epoch (Unix timestamp).
	/// Corresponds to the "timestamp" attribute in the log4j:event element.
	/// </summary>
	public long Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the name of the thread that generated this event.
	/// Corresponds to the "thread" attribute in the log4j:event element.
	/// </summary>
	public string Thread { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the log message content.
	/// Extracted from the log4j:message child element of the log4j:event.
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception or throwable information (stack trace) associated with this log event.
	/// Extracted from the log4j:throwable child element of the log4j:event. May be null if no exception information is included.
	/// </summary>
	public string? Throwable { get; set; }

	/// <summary>
	/// Gets or sets the location information (class, method, file, line) where the log event was generated.
	/// Extracted from the log4j:locationInfo child element. May be null if location info is not included.
	/// </summary>
	public Log4JLocationInfo? LocationInfo { get; set; }

	/// <summary>
	/// Gets or sets a dictionary of additional properties associated with this log event.
	/// Extracted from the log4j:properties/log4j:data elements. Keys are property names, values are property values.
	/// </summary>
	public Dictionary<string, string> Properties { get; set; } = new();

	/// <summary>
	/// Regular expression to extract the application name from log4japp property.
	/// Matches patterns like "com.example.MyApp(123)" or "MyApp" and extracts the base name and optional process ID.
	/// Pattern breakdown:
	/// - ^(?:.*\\.)? - Optional package prefix (e.g., "com.example.")
	/// - (?<name>[A-Za-z_][A-Za-z0-9_]*) - Named captured group for the app name (must start with letter/underscore)
	/// - (?<id>\\(\\d+\\))?$ - Optional named captured group for process ID suffix like "(123)"
	/// </summary>
	private static readonly Regex Log4JEventAppNameRegex = new("^(?:.*\\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<id>\\(\\d+\\))?$", RegexOptions.Compiled);

	/// <summary>
	/// Converts this Log4JEvent instance to a LogEvent object for use in the NLogViewer application.
	/// Maps all available Log4J event properties to the corresponding LogEvent structure,
	/// including app information, log level, timestamp, message, exception, and additional properties.
	/// </summary>
	/// <returns>A LogEvent object containing the converted log event information.</returns>
	public LogEvent ToLogEvent(string sender)
	{
		// Extract application name safely from log4japp property
		var appName = ExtractAppName();
		
		// Extract machine name safely from properties
		var combinedSender = Properties.TryGetValue("log4jmachinename", out var machineName) 
			? $"{machineName} ({sender})" 
			: sender;

		// Convert Log4J level to NLog LogLevel
		var nlogLevel = ConvertLog4JLevelToNLogLevel(Level);

		// Convert timestamp from milliseconds since epoch to DateTime
		var timestamp = Timestamp > 0 
			? DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).DateTime 
			: DateTime.Now;

		// Create LogEventInfo with core properties
		var logEventInfo = new LogEventInfo(nlogLevel, Logger ?? "Unknown", Message ?? string.Empty)
		{
			TimeStamp = timestamp
		};

		// Add thread information if available
		if (!string.IsNullOrEmpty(Thread))
		{
			logEventInfo.Properties["Thread"] = Thread;
		}

		// Add exception information if throwable data is present
		if (!string.IsNullOrEmpty(Throwable))
		{
			logEventInfo.Exception = new Exception(Throwable);
		}

		// Add location information if available
		if (LocationInfo != null)
		{
			if (!string.IsNullOrEmpty(LocationInfo.Class))
			{
				logEventInfo.Properties["ClassName"] = LocationInfo.Class;
			}
			if (!string.IsNullOrEmpty(LocationInfo.Method))
			{
				logEventInfo.Properties["MethodName"] = LocationInfo.Method;
			}
			if (!string.IsNullOrEmpty(LocationInfo.File))
			{
				logEventInfo.Properties["FileName"] = LocationInfo.File;
			}
			if (LocationInfo.Line.HasValue)
			{
				logEventInfo.Properties["LineNumber"] = LocationInfo.Line.Value;
			}
		}

		// Copy all additional properties from Log4J event to LogEventInfo
		foreach (var property in Properties)
		{
			// Skip properties that are already handled explicitly
			if (property.Key != "log4japp" && property.Key != "log4jmachinename")
			{
				logEventInfo.Properties[property.Key] = property.Value ?? string.Empty;
			}
		}

		// Create and return the complete LogEvent
		var logEvent = new LogEvent
		{
			AppInfo = new AppInfo
			{
				AppName = appName,
				Sender = combinedSender
			},
			LogEventInfo = logEventInfo
		};

		return logEvent;
	}

	/// <summary>
	/// Extracts the application name from the log4japp property using regex pattern matching.
	/// Safely handles missing or invalid log4japp values by returning null.
	/// </summary>
	/// <returns>The extracted application name, or null if the property is missing or doesn't match the expected pattern.</returns>
	private AppName? ExtractAppName()
	{
		if (!Properties.TryGetValue("log4japp", out var log4jApp) || string.IsNullOrEmpty(log4jApp))
		{
			return null;
		}

		var match = Log4JEventAppNameRegex.Match(log4jApp);
		if (match.Success && match.Groups["name"].Success)
		{
			if(match.Groups["id"].Success)
				return new AppName(match.Groups["name"].Value, match.Groups["id"].Value);
			return new AppName(match.Groups["name"].Value, "");
		}

		// If regex doesn't match, return the original value as fallback
		return new AppName(log4jApp, "");
	}

	/// <summary>
	/// Converts a Log4JLevel enum value to the corresponding NLog LogLevel.
	/// Maps Log4J-specific levels (All, Off) to appropriate NLog equivalents.
	/// </summary>
	/// <param name="log4JLevel">The Log4J level to convert.</param>
	/// <returns>The corresponding NLog LogLevel. Defaults to LogLevel.Info for unknown or unmappable levels.</returns>
	private static LogLevel ConvertLog4JLevelToNLogLevel(Log4JLevel log4JLevel)
	{
		return log4JLevel switch
		{
			Log4JLevel.Trace => LogLevel.Trace,
			Log4JLevel.Debug => LogLevel.Debug,
			Log4JLevel.Info => LogLevel.Info,
			Log4JLevel.Warn => LogLevel.Warn,
			Log4JLevel.Error => LogLevel.Error,
			Log4JLevel.Fatal => LogLevel.Fatal,
			Log4JLevel.All => LogLevel.Trace, // Map All to most verbose level
			Log4JLevel.Off => LogLevel.Off,
			_ => LogLevel.Info // Default fallback for Unknown or unexpected values
		};
	}
}