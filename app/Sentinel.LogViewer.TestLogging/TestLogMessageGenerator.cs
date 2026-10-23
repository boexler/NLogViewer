namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Generates random log messages and exception logs for test logging. It only makes the data; an <see cref="ILogSink"/> sends it.
/// </summary>
public static class TestLogMessageGenerator
{
    private static readonly string[] LoggerNames =
    {
        "Sentinel.LogViewer.TestLogging",
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

    private static readonly TestLogLevel[] LogLevels = Enum.GetValues<TestLogLevel>();

    /// <summary>
    /// Makes one log (an exception log with the probability <paramref name="exceptionProbability"/>)
    /// </summary>
    public static GeneratedLog Create(Random random, int messageCounter, double exceptionProbability)
    {
        ArgumentNullException.ThrowIfNull(random);

        var logger = LoggerNames[random.Next(LoggerNames.Length)];
        if (exceptionProbability > 0 && random.NextDouble() < exceptionProbability)
            return CreateException(random, logger);

        return new GeneratedLog(LogLevels[random.Next(LogLevels.Length)], logger, GenerateRandomMessage(random, messageCounter), null);
    }

    private static GeneratedLog CreateException(Random random, string logger)
    {
        var (exception, level) = random.Next(1, 4) switch
        {
            1 => ((Exception)new InvalidOperationException($"Invalid operation occurred at {DateTime.Now:HH:mm:ss}"), TestLogLevel.Error),
            2 => (new ArgumentNullException("testParameter", "Test parameter was null"), TestLogLevel.Error),
            _ => (new DivideByZeroException("Division by zero attempted"), TestLogLevel.Fatal)
        };

        var kind = level == TestLogLevel.Fatal ? "Fatal" : "Error";
        return new GeneratedLog(level, logger, $"{kind} exception: {exception.Message}", exception);
    }

    private static string GenerateRandomMessage(Random random, int messageCounter)
    {
        var message = Messages[random.Next(Messages.Length)];
        message = message.Replace("{0}", random.Next(1, 100).ToString());
        return $"{message} (Message #{messageCounter + 1})";
    }
}
