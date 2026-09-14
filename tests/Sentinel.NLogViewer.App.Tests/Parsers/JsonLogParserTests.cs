using NLog;
using Sentinel.NLogViewer.App.Parsers;
using Xunit;

namespace Sentinel.NLogViewer.App.Tests.Parsers
{
    /// <summary>
    /// Unit tests for JsonLogParser
    /// </summary>
    public class JsonLogParserTests : IDisposable
    {
        private readonly JsonLogParser _parser;

        public JsonLogParserTests()
        {
            _parser = new JsonLogParser();
        }

        [Fact]
        public void Parse_ValidJsonObject_ReturnsLogEventInfo()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""INFO"",
                ""logger"": ""TestLogger"",
                ""message"": ""Test message""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.Equal("TestLogger", logEvent.LoggerName);
            Assert.Equal(LogLevel.Info, logEvent.Level);
            Assert.Equal("Test message", logEvent.Message);
        }

        [Fact]
        public void Parse_JsonArray_ReturnsMultipleLogEvents()
        {
            // Arrange
            var json = @"[
                {
                    ""timestamp"": ""2024-01-15T10:30:45Z"",
                    ""level"": ""INFO"",
                    ""logger"": ""Logger1"",
                    ""message"": ""Message 1""
                },
                {
                    ""timestamp"": ""2024-01-15T10:30:46Z"",
                    ""level"": ""WARN"",
                    ""logger"": ""Logger2"",
                    ""message"": ""Message 2""
                }
            ]";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("Logger1", results[0].LoggerName);
            Assert.Equal("Logger2", results[1].LoggerName);
            Assert.Equal(LogLevel.Info, results[0].Level);
            Assert.Equal(LogLevel.Warn, results[1].Level);
        }

        [Fact]
        public void Parse_JsonWithAllLevels_ParsesLevelsCorrectly()
        {
            var levels = new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL" };
            var expectedLogLevels = new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal };

            for (int i = 0; i < levels.Length; i++)
            {
                // Arrange
                var json = $@"{{
                    ""timestamp"": ""2024-01-15T10:30:45Z"",
                    ""level"": ""{levels[i]}"",
                    ""logger"": ""TestLogger"",
                    ""message"": ""Test {levels[i]}""
                }}";

                // Act
                var results = _parser.Parse(json);

                // Assert
                Assert.Single(results);
                Assert.Equal(expectedLogLevels[i], results[0].Level);
            }
        }

        [Fact]
        public void Parse_JsonWithUnixTimestamp_ParsesTimestamp()
        {
            // Arrange
            // Note: The parser expects timestamp as string, so we pass it as string
            var unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var json = $@"{{
                ""timestamp"": ""{unixTime}"",
                ""level"": ""INFO"",
                ""logger"": ""TestLogger"",
                ""message"": ""Test message""
            }}";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            var expectedTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTime).DateTime;
            Assert.True(Math.Abs((results[0].TimeStamp - expectedTime).TotalSeconds) < 1);
        }

        [Fact]
        public void Parse_JsonWithException_ParsesException()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""ERROR"",
                ""logger"": ""TestLogger"",
                ""message"": ""Error occurred"",
                ""exception"": ""System.Exception: Test exception message""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            var exception = results[0].Exception;
            Assert.NotNull(exception);
            Assert.Contains("Test exception message", exception.Message);
        }

        [Fact]
        public void Parse_JsonWithProperties_ParsesProperties()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""INFO"",
                ""logger"": ""TestLogger"",
                ""message"": ""Test message"",
                ""UserId"": ""12345"",
                ""SessionId"": ""abc-123"",
                ""RequestId"": ""req-456""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.True(logEvent.Properties.ContainsKey("UserId"));
            Assert.Equal("12345", logEvent.Properties["UserId"]);
            Assert.True(logEvent.Properties.ContainsKey("SessionId"));
            Assert.Equal("abc-123", logEvent.Properties["SessionId"]);
            Assert.True(logEvent.Properties.ContainsKey("RequestId"));
            Assert.Equal("req-456", logEvent.Properties["RequestId"]);
        }

        [Fact]
        public void Parse_JsonWithLoggerNameField_ParsesLoggerName()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""INFO"",
                ""loggerName"": ""MyLogger"",
                ""message"": ""Test message""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            Assert.Equal("MyLogger", results[0].LoggerName);
        }

        [Fact]
        public void Parse_JsonWithMsgField_ParsesMessage()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""INFO"",
                ""logger"": ""TestLogger"",
                ""msg"": ""Alternative message field""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            Assert.Equal("Alternative message field", results[0].Message);
        }

        [Fact]
        public void Parse_JsonWithoutTimestamp_UsesCurrentTime()
        {
            // Arrange
            var before = DateTime.Now;
            var json = @"{
                ""level"": ""INFO"",
                ""logger"": ""TestLogger"",
                ""message"": ""Test message""
            }";

            // Act
            var results = _parser.Parse(json);
            var after = DateTime.Now;

            // Assert
            Assert.Single(results);
            var logEvent = results[0];
            Assert.True(logEvent.TimeStamp >= before.AddSeconds(-1));
            Assert.True(logEvent.TimeStamp <= after.AddSeconds(1));
        }

        [Fact]
        public void Parse_JsonWithoutLevel_DefaultsToInfo()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""logger"": ""TestLogger"",
                ""message"": ""Test message""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            Assert.Equal(LogLevel.Info, results[0].Level);
        }

        [Fact]
        public void Parse_InvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            var results = _parser.Parse(invalidJson);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void Parse_EmptyJson_ReturnsEmptyList()
        {
            // Arrange
            var emptyJson = "";

            // Act
            var results = _parser.Parse(emptyJson);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void Parse_JsonWithWarningKeyword_ParsesAsWarn()
        {
            // Arrange
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:45Z"",
                ""level"": ""WARNING"",
                ""logger"": ""TestLogger"",
                ""message"": ""Warning message""
            }";

            // Act
            var results = _parser.Parse(json);

            // Assert
            Assert.Single(results);
            Assert.Equal(LogLevel.Warn, results[0].Level);
        }

        public void Dispose()
        {
            _parser?.Dispose();
        }
    }
}

