using Sentinel.LogViewer.Models;
using Xunit;

namespace Sentinel.LogViewer.Tests.Models
{
	/// <summary>
	/// Unit tests for Log4JEvent class, specifically testing application name extraction from log4japp property.
	/// </summary>
	public class Log4JEventTests
	{
		[Fact]
		public void ToLogEvent_SimpleAppName_ExtractsNameCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "MyApp" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp", result.AppInfo.AppName.Name);
			Assert.Equal("", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithPackagePrefix_ExtractsNameCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "com.example.MyApp" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp", result.AppInfo.AppName.Name);
			Assert.Equal("", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithProcessId_ExtractsNameAndIdCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "MyApp(12345)" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp", result.AppInfo.AppName.Name);
			Assert.Equal("(12345)", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithPackagePrefixAndProcessId_ExtractsNameAndIdCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "com.example.MyApp(31368)" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp", result.AppInfo.AppName.Name);
			Assert.Equal("(31368)", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_MissingLog4JAppProperty_ReturnsNullAppName()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>()
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.Null(result.AppInfo.AppName);
		}

		[Fact]
		public void ToLogEvent_EmptyLog4JAppProperty_ReturnsNullAppName()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.Null(result.AppInfo.AppName);
		}

		[Fact]
		public void ToLogEvent_InvalidLog4JAppFormat_ReturnsOriginalValueAsFallback()
		{
			// Arrange
			var invalidAppName = "123InvalidApp"; // Starts with number, doesn't match regex
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", invalidAppName }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			// Should return original value as fallback when regex doesn't match
			Assert.Equal(invalidAppName, result.AppInfo.AppName.Name);
			Assert.Equal("", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithUnderscore_ExtractsNameCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "My_App(999)" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("My_App", result.AppInfo.AppName.Name);
			Assert.Equal("(999)", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithNestedPackagePrefix_ExtractsNameCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "com.company.product.module.MyApp(12345)" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp", result.AppInfo.AppName.Name);
			Assert.Equal("(12345)", result.AppInfo.AppName.Id);
		}

		[Fact]
		public void ToLogEvent_AppNameWithNumbersInName_ExtractsNameCorrectly()
		{
			// Arrange
			var log4JEvent = new Log4JEvent
			{
				Logger = "TestLogger",
				Level = Log4JLevel.Info,
				Timestamp = 1234567890000,
				Message = "Test message",
				Properties = new Dictionary<string, string>
				{
					{ "log4japp", "MyApp2(100)" }
				}
			};

			// Act
			var result = log4JEvent.ToLogEvent("sender");

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.AppInfo);
			Assert.NotNull(result.AppInfo.AppName);
			Assert.Equal("MyApp2", result.AppInfo.AppName.Name);
			Assert.Equal("(100)", result.AppInfo.AppName.Id);
		}
	}
}

