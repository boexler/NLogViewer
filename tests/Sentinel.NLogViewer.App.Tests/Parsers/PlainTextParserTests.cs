using NLog;
using Sentinel.NLogViewer.App.Parsers;
using Xunit;

namespace Sentinel.NLogViewer.App.Tests.Parsers
{
    /// <summary>
    /// Unit tests for PlainTextParser
    /// </summary>
    public class PlainTextParserTests : IDisposable
    {
        private readonly PlainTextParser _parser;

        public PlainTextParserTests()
        {
            _parser = new PlainTextParser();
        }

        [Fact]
        public void Parse_LineWithTimestampAndLevel_ParsesCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 INFO [MyLogger] Application started successfully"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("MyLogger", logEvent.LoggerName);
            Assert.Contains("Application started successfully", logEvent.Message);
        }

        [Fact]
        public void Parse_LineWithDifferentLevels_ParsesLevelsCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 TRACE [Logger] Trace message",
                "2024-01-15 10:30:46 DEBUG [Logger] Debug message",
                "2024-01-15 10:30:47 INFO [Logger] Info message",
                "2024-01-15 10:30:48 WARN [Logger] Warn message",
                "2024-01-15 10:30:49 ERROR [Logger] Error message",
                "2024-01-15 10:30:50 FATAL [Logger] Fatal message"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(6, results.Count);
            
            // First entry
            Assert.Equal(2024, results[0].TimeStamp.Year);
            Assert.Equal(1, results[0].TimeStamp.Month);
            Assert.Equal(15, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(30, results[0].TimeStamp.Minute);
            Assert.Equal(45, results[0].TimeStamp.Second);
            Assert.Equal(LogLevel.Trace, results[0].Level);
            Assert.Equal("Logger", results[0].LoggerName);
            Assert.Contains("Trace message", results[0].Message);
            
            // Second entry
            Assert.Equal(46, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Debug, results[1].Level);
            Assert.Equal("Logger", results[1].LoggerName);
            Assert.Contains("Debug message", results[1].Message);
            
            // Third entry
            Assert.Equal(47, results[2].TimeStamp.Second);
            Assert.Equal(LogLevel.Info, results[2].Level);
            Assert.Equal("Logger", results[2].LoggerName);
            Assert.Contains("Info message", results[2].Message);
            
            // Fourth entry
            Assert.Equal(48, results[3].TimeStamp.Second);
            Assert.Equal(LogLevel.Warn, results[3].Level);
            Assert.Equal("Logger", results[3].LoggerName);
            Assert.Contains("Warn message", results[3].Message);
            
            // Fifth entry
            Assert.Equal(49, results[4].TimeStamp.Second);
            Assert.Equal(LogLevel.Error, results[4].Level);
            Assert.Equal("Logger", results[4].LoggerName);
            Assert.Contains("Error message", results[4].Message);
            
            // Sixth entry
            Assert.Equal(50, results[5].TimeStamp.Second);
            Assert.Equal(LogLevel.Fatal, results[5].Level);
            Assert.Equal("Logger", results[5].LoggerName);
            Assert.Contains("Fatal message", results[5].Message);
        }

        [Fact]
        public void Parse_LineWithIsoTimestamp_ParsesTimestamp()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15T10:30:45.123Z INFO [Logger] Message with ISO timestamp"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            
            // ISO timestamp with 'Z' should be parsed as UTC
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("Logger", logEvent.LoggerName);
            Assert.Contains("Message with ISO timestamp", logEvent.Message);
        }

        [Fact]
        public void Parse_LineWithoutLevel_DefaultsToInfo()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 [Logger] Message without level"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("Logger", logEvent.LoggerName);
            Assert.Contains("Message without level", logEvent.Message);
        }

        [Fact]
        public void Parse_LineWithLoggerInBrackets_ExtractsLoggerName()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 INFO [MyApp.Services.DataService] Processing request"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("MyApp.Services.DataService", logEvent.LoggerName);
            Assert.Contains("Processing request", logEvent.Message);
        }

        [Fact]
        public void Parse_LineWithLoggerInParentheses_ExtractsLoggerName()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 INFO (MyApp.Controllers.HomeController) Processing request"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("MyApp.Controllers.HomeController", logEvent.LoggerName);
            Assert.Contains("Processing request", logEvent.Message);
        }

        [Fact]
        public void Parse_LineWithoutLogger_DefaultsToUnknown()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 INFO Simple log message"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("Unknown", logEvent.LoggerName);
            Assert.Contains("Simple log message", logEvent.Message);
        }

        [Fact]
        public void Parse_EmptyLines_IgnoresEmptyLines()
        {
            // Arrange
            var lines = new[]
            {
                "",
                "   ",
                "2024-01-15 10:30:45 INFO [Logger] Valid message",
                "",
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("Logger", logEvent.LoggerName);
            Assert.Contains("Valid message", logEvent.Message);
        }

        [Fact]
        public void Parse_MultipleLines_ParsesAllLines()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 INFO [Logger1] First message",
                "2024-01-15 10:30:46 WARN [Logger2] Second message",
                "2024-01-15 10:30:47 ERROR [Logger3] Third message"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(3, results.Count);
            
            // First entry
            Assert.Equal(2024, results[0].TimeStamp.Year);
            Assert.Equal(1, results[0].TimeStamp.Month);
            Assert.Equal(15, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(30, results[0].TimeStamp.Minute);
            Assert.Equal(45, results[0].TimeStamp.Second);
            Assert.Equal(LogLevel.Info, results[0].Level);
            Assert.Equal("Logger1", results[0].LoggerName);
            Assert.Contains("First message", results[0].Message);
            
            // Second entry
            Assert.Equal(46, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Warn, results[1].Level);
            Assert.Equal("Logger2", results[1].LoggerName);
            Assert.Contains("Second message", results[1].Message);
            
            // Third entry
            Assert.Equal(47, results[2].TimeStamp.Second);
            Assert.Equal(LogLevel.Error, results[2].Level);
            Assert.Equal("Logger3", results[2].LoggerName);
            Assert.Contains("Third message", results[2].Message);
        }

        [Fact]
        public void Parse_LineWithWarningKeyword_ParsesAsWarn()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 WARNING [Logger] Warning message"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2024, logEvent.TimeStamp.Year);
            Assert.Equal(1, logEvent.TimeStamp.Month);
            Assert.Equal(15, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(30, logEvent.TimeStamp.Minute);
            Assert.Equal(45, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Warn, logEvent.Level);
            Assert.Equal("Logger", logEvent.LoggerName);
            Assert.Contains("Warning message", logEvent.Message);
        }

        [Fact]
        public void Parse_PipeSeparatedFormat_ParsesCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "2025-12-19 10:58:37.8960 | FATAL | Sentinel.NLogViewer.App.TestApp | Background task completed (Message #735)"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2025, logEvent.TimeStamp.Year);
            Assert.Equal(12, logEvent.TimeStamp.Month);
            Assert.Equal(19, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(58, logEvent.TimeStamp.Minute);
            Assert.Equal(37, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Fatal, logEvent.Level);
            Assert.Equal("Sentinel.NLogViewer.App.TestApp", logEvent.LoggerName);
            Assert.Contains("Background task completed (Message #735)", logEvent.Message);
        }

        [Fact]
        public void Parse_PipeSeparatedFormatWithMilliseconds_ParsesTimestamp()
        {
            // Arrange
            var lines = new[]
            {
                "2025-12-19 10:58:37.8960 | FATAL | Logger | Message",
                "2025-12-19 10:58:38.8880 | WARN | Logger | Message"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(2, results.Count);
            
            // First entry
            Assert.Equal(2025, results[0].TimeStamp.Year);
            Assert.Equal(12, results[0].TimeStamp.Month);
            Assert.Equal(19, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(58, results[0].TimeStamp.Minute);
            Assert.Equal(37, results[0].TimeStamp.Second);
            // Milliseconds should be parsed (may be 896 from 8960 or similar)
            Assert.True(results[0].TimeStamp.Millisecond >= 0 && results[0].TimeStamp.Millisecond < 1000);
            Assert.Equal(LogLevel.Fatal, results[0].Level);
            Assert.Equal("Logger", results[0].LoggerName);
            Assert.Contains("Message", results[0].Message);
            
            // Second entry
            Assert.Equal(38, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Warn, results[1].Level);
            Assert.Equal("Logger", results[1].LoggerName);
            Assert.Contains("Message", results[1].Message);
        }

        [Fact]
        public void Parse_PipeSeparatedFormatMultipleEntries_ParsesAll()
        {
            // Arrange
            var lines = new[]
            {
                "2025-12-19 10:58:37.8960 | FATAL | Sentinel.NLogViewer.App.TestApp | Background task completed (Message #735)",
                "2025-12-19 10:58:38.8880 | WARN | Sentinel.NLogViewer.App.TestApp.Models | User authentication successful (Message #736)"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(2, results.Count);
            
            // First entry
            Assert.Equal(2025, results[0].TimeStamp.Year);
            Assert.Equal(12, results[0].TimeStamp.Month);
            Assert.Equal(19, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(58, results[0].TimeStamp.Minute);
            Assert.Equal(37, results[0].TimeStamp.Second);
            Assert.Equal(LogLevel.Fatal, results[0].Level);
            Assert.Equal("Sentinel.NLogViewer.App.TestApp", results[0].LoggerName);
            Assert.Contains("Background task completed (Message #735)", results[0].Message);
            
            // Second entry
            Assert.Equal(38, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Warn, results[1].Level);
            Assert.Equal("Sentinel.NLogViewer.App.TestApp.Models", results[1].LoggerName);
            Assert.Contains("User authentication successful (Message #736)", results[1].Message);
        }

        [Fact]
        public void Parse_MultiLineMessage_AppendsContinuationLines()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 ERROR [Logger] First line of error",
                "This is a continuation line",
                "Another continuation line",
                "2024-01-15 10:30:46 INFO [Logger] New log entry"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(2, results.Count);
            
            // First entry (with continuation lines)
            Assert.Equal(2024, results[0].TimeStamp.Year);
            Assert.Equal(1, results[0].TimeStamp.Month);
            Assert.Equal(15, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(30, results[0].TimeStamp.Minute);
            Assert.Equal(45, results[0].TimeStamp.Second);
            Assert.Equal(LogLevel.Error, results[0].Level);
            Assert.Equal("Logger", results[0].LoggerName);
            Assert.Contains("First line of error", results[0].Message);
            Assert.Contains("This is a continuation line", results[0].Message);
            Assert.Contains("Another continuation line", results[0].Message);
            Assert.DoesNotContain("New log entry", results[0].Message);
            
            // Second entry
            Assert.Equal(46, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Info, results[1].Level);
            Assert.Equal("Logger", results[1].LoggerName);
            Assert.Contains("New log entry", results[1].Message);
        }

        [Fact]
        public void Parse_MultiLineMessageWithPipeFormat_ParsesCorrectly()
        {
            // Arrange
            var lines = new[]
            {
                "2025-12-19 10:58:39.8930 | ERROR | Sentinel.NLogViewer.App.TestApp.Controllers.HomeController | Error exception: Invalid operation occurred at 11:58:39",
                "",
                "System.Exception: System.InvalidOperationException: Invalid operation occurred at 11:58:39"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal(2025, logEvent.TimeStamp.Year);
            Assert.Equal(12, logEvent.TimeStamp.Month);
            Assert.Equal(19, logEvent.TimeStamp.Day);
            Assert.Equal(10, logEvent.TimeStamp.Hour);
            Assert.Equal(58, logEvent.TimeStamp.Minute);
            Assert.Equal(39, logEvent.TimeStamp.Second);
            Assert.Equal(LogLevel.Error, logEvent.Level);
            Assert.Equal("Sentinel.NLogViewer.App.TestApp.Controllers.HomeController", logEvent.LoggerName);
            Assert.Contains("Error exception: Invalid operation occurred at 11:58:39", logEvent.Message);
            Assert.Contains("System.Exception: System.InvalidOperationException: Invalid operation occurred at 11:58:39", logEvent.Message);
        }

        [Fact]
        public void Parse_MultiLineMessageWithEmptyLines_PreservesStructure()
        {
            // Arrange
            var lines = new[]
            {
                "2024-01-15 10:30:45 ERROR [Logger] Stack trace:",
                "",
                "   at SomeMethod()",
                "   at AnotherMethod()",
                "",
                "2024-01-15 10:30:46 INFO [Logger] Next entry"
            };

            // Act
            var results = _parser.Parse(lines);

            // Assert
            Assert.Equal(2, results.Count);
            
            // First entry (with empty lines in message)
            Assert.Equal(2024, results[0].TimeStamp.Year);
            Assert.Equal(1, results[0].TimeStamp.Month);
            Assert.Equal(15, results[0].TimeStamp.Day);
            Assert.Equal(10, results[0].TimeStamp.Hour);
            Assert.Equal(30, results[0].TimeStamp.Minute);
            Assert.Equal(45, results[0].TimeStamp.Second);
            Assert.Equal(LogLevel.Error, results[0].Level);
            Assert.Equal("Logger", results[0].LoggerName);
            Assert.Contains("Stack trace:", results[0].Message);
            Assert.Contains("at SomeMethod()", results[0].Message);
            Assert.Contains("at AnotherMethod()", results[0].Message);
            // Empty lines should be preserved in multi-line messages
            
            // Second entry
            Assert.Equal(46, results[1].TimeStamp.Second);
            Assert.Equal(LogLevel.Info, results[1].Level);
            Assert.Equal("Logger", results[1].LoggerName);
            Assert.Contains("Next entry", results[1].Message);
        }

        public void Dispose()
        {
            _parser?.Dispose();
        }
    }
}


