using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NLog;

namespace Sentinel.LogViewer.Parsers
{
    /// <summary>
    /// Parser for JSON log files
    /// </summary>
    public class JsonLogParser : IDisposable
    {
        private bool _disposed;

        public List<LogEventInfo> Parse(string json)
        {
            var results = new List<LogEventInfo>();

            try
            {
                // Try to parse as array first
                if (json.TrimStart().StartsWith("["))
                {
                    var array = JsonSerializer.Deserialize<JsonElement[]>(json);
                    if (array != null)
                    {
                        foreach (var element in array)
                        {
                            var logEvent = ParseJsonElement(element);
                            if (logEvent != null)
                                results.Add(logEvent);
                        }
                    }
                }
                else
                {
                    // Single object
                    var element = JsonSerializer.Deserialize<JsonElement>(json);
                    var logEvent = ParseJsonElement(element);
                    if (logEvent != null)
                        results.Add(logEvent);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return results;
        }

        private LogEventInfo? ParseJsonElement(JsonElement element)
        {
            try
            {
                // Extract common fields
                var timestamp = element.TryGetProperty("timestamp", out var tsProp)
                    ? ParseTimestamp(tsProp.GetString())
                    : DateTime.Now;

                var level = element.TryGetProperty("level", out var levelProp)
                    ? ParseLogLevel(levelProp.GetString())
                    : LogLevel.Info;

                var loggerName = element.TryGetProperty("logger", out var loggerProp)
                    ? loggerProp.GetString() ?? "Unknown"
                    : element.TryGetProperty("loggerName", out var loggerNameProp)
                        ? loggerNameProp.GetString() ?? "Unknown"
                        : "Unknown";

                var message = element.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString() ?? string.Empty
                    : element.TryGetProperty("msg", out var msgProp2)
                        ? msgProp2.GetString() ?? string.Empty
                        : string.Empty;

                var logEvent = new LogEventInfo(level, loggerName, message)
                {
                    TimeStamp = timestamp
                };

                // Parse exception if present
                if (element.TryGetProperty("exception", out var exProp))
                {
                    var exMessage = exProp.GetString();
                    if (!string.IsNullOrEmpty(exMessage))
                    {
                        logEvent.Exception = new Exception(exMessage);
                    }
                }

                // Add all other properties
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name != "timestamp" && prop.Name != "level" && 
                        prop.Name != "logger" && prop.Name != "loggerName" &&
                        prop.Name != "message" && prop.Name != "msg" &&
                        prop.Name != "exception")
                    {
                        logEvent.Properties[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();
                    }
                }

                return logEvent;
            }
            catch
            {
                return null;
            }
        }

        private DateTime ParseTimestamp(string? timestampString)
        {
            if (string.IsNullOrEmpty(timestampString))
                return DateTime.Now;

            if (DateTime.TryParse(timestampString, out var dt))
                return dt;

            // Try Unix timestamp
            if (long.TryParse(timestampString, out var unixTime))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixTime).DateTime;
            }

            return DateTime.Now;
        }

        private LogLevel ParseLogLevel(string? levelString)
        {
            if (string.IsNullOrEmpty(levelString))
                return LogLevel.Info;

            return levelString.ToUpperInvariant() switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Debug,
                "INFO" => LogLevel.Info,
                "WARN" or "WARNING" => LogLevel.Warn,
                "ERROR" => LogLevel.Error,
                "FATAL" => LogLevel.Fatal,
                _ => LogLevel.Info
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}

