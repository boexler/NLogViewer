using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Parsers
{
    /// <summary>
    /// Parser for JSON log files
    /// </summary>
    public class JsonLogParser : IDisposable
    {
        private bool _disposed;

        public List<LogEntry> Parse(string json)
        {
            var results = new List<LogEntry>();

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

        private LogEntry? ParseJsonElement(JsonElement element)
        {
            try
            {
                // Extract common fields
                var timestamp = element.TryGetProperty("timestamp", out var tsProp)
                    ? ParseTimestamp(tsProp.GetString())
                    : DateTime.Now;

                var level = element.TryGetProperty("level", out var levelProp)
                    ? ParseLogLevel(levelProp.GetString())
                    : LogEntryLevel.Info;

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

                // Parse exception if present
                string? exception = null;
                if (element.TryGetProperty("exception", out var exProp))
                {
                    var exMessage = exProp.GetString();
                    if (!string.IsNullOrEmpty(exMessage))
                    {
                        exception = exMessage;
                    }
                }

                // Add all other properties
                var properties = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name != "timestamp" && prop.Name != "level" &&
                        prop.Name != "logger" && prop.Name != "loggerName" &&
                        prop.Name != "message" && prop.Name != "msg" &&
                        prop.Name != "exception")
                    {
                        properties[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();
                    }
                }

                var logEvent = new LogEntry(level, loggerName, message)
                {
                    TimeStamp = timestamp,
                    Exception = exception,
                    Properties = properties
                };

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

        private LogEntryLevel ParseLogLevel(string? levelString)
        {
            if (string.IsNullOrEmpty(levelString))
                return LogEntryLevel.Info;

            return levelString.ToUpperInvariant() switch
            {
                "TRACE" => LogEntryLevel.Trace,
                "DEBUG" => LogEntryLevel.Debug,
                "INFO" => LogEntryLevel.Info,
                "WARN" or "WARNING" => LogEntryLevel.Warn,
                "ERROR" => LogEntryLevel.Error,
                "FATAL" => LogEntryLevel.Fatal,
                _ => LogEntryLevel.Info
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

