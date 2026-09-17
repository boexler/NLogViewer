using NLog;

namespace Sentinel.NLogViewer.TestLogging;

/// <summary>
/// Generates random log messages and exception logs for test logging (no console output).
/// </summary>
public static class TestLogMessageGenerator
{
    private static readonly string[] LoggerNames =
    {
        "Sentinel.NLogViewer.TestLogging",
        "Sentinel.LogViewer.Services",
        "Sentinel.LogViewer.Services.DataService",
        "Sentinel.LogViewer.Services.NetworkService",
        "Sentinel.LogViewer.Controllers",
        "Sentinel.LogViewer.Controllers.HomeController",
        "Sentinel.LogViewer.Models",
        "Sentinel.LogViewer.Utils"
    };

    private static readonly string[] Messages =
    {
        "Application started successfully",
        "Processing user request",
        "Database connection established",
        "Cache updated with new data",
        "Configuration loaded from file",
        "User authentication successful",
        "File uploaded successfully",
        "Background task completed",
        "Memory usage: {0} MB",
        "CPU usage: {0}%",
        "Network request sent to {0}",
        "Response received in {0}ms",
        "Validation passed for user input",
        "Session created for user {0}",
        "Data synchronized with server"
    };

    private static readonly LogLevel[] LogLevels =
    {
        LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal
    };

    /// <summary>
    /// Generates one log message (normal or exception based on exceptionProbability) and returns true if it was an exception log.
    /// </summary>
    public static bool GenerateOne(Random random, int messageCounter, double exceptionProbability)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        bool isException = exceptionProbability > 0 && random.NextDouble() < exceptionProbability;
        if (isException)
            GenerateExceptionLog(random);
        else
            GenerateNormalLog(random, messageCounter);
        return isException;
    }

    private static void GenerateNormalLog(Random random, int messageCounter)
    {
        var logLevel = LogLevels[random.Next(LogLevels.Length)];
        var message = GenerateRandomMessage(random, messageCounter);
        var loggerName = LoggerNames[random.Next(LoggerNames.Length)];
        var logger = LogManager.GetLogger(loggerName);

        switch (logLevel.Name)
        {
            case "Trace": logger.Trace(message); break;
            case "Debug": logger.Debug(message); break;
            case "Info": logger.Info(message); break;
            case "Warn": logger.Warn(message); break;
            case "Error": logger.Error(message); break;
            case "Fatal": logger.Fatal(message); break;
            default: logger.Info(message); break;
        }
    }

    private static void GenerateExceptionLog(Random random)
    {
        var exceptionType = random.Next(1, 4);
        Exception exception;
        LogLevel logLevel;

        switch (exceptionType)
        {
            case 1:
                exception = new InvalidOperationException($"Invalid operation occurred at {DateTime.Now:HH:mm:ss}");
                logLevel = LogLevel.Error;
                break;
            case 2:
                exception = new ArgumentNullException("testParameter", "Test parameter was null");
                logLevel = LogLevel.Error;
                break;
            case 3:
                exception = new DivideByZeroException("Division by zero attempted");
                logLevel = LogLevel.Fatal;
                break;
            default:
                exception = new Exception("Generic exception occurred");
                logLevel = LogLevel.Error;
                break;
        }

        var logger = LogManager.GetLogger(LoggerNames[random.Next(LoggerNames.Length)]);

        if (logLevel == LogLevel.Fatal)
            logger.Fatal(exception, $"Fatal exception: {exception.Message}");
        else
            logger.Error(exception, $"Error exception: {exception.Message}");
    }

    private static string GenerateRandomMessage(Random random, int messageCounter)
    {
        var message = Messages[random.Next(Messages.Length)];
        message = message.Replace("{0}", random.Next(1, 100).ToString());
        return $"{message} (Message #{messageCounter + 1})";
    }
}
