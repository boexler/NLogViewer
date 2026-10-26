using NLog;

namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>
/// Sends the generated logs with NLog (the Log4JXml target of <see cref="NLogTestLoggingConfig"/>)
/// </summary>
public sealed class NLogSink : ILogSink
{
    public void Start(TestLoggingOptions options) => NLogTestLoggingConfig.Apply(options);

    public void Write(GeneratedLog log)
    {
        var logger = LogManager.GetLogger(log.Logger);
        var level = log.Level switch
        {
            TestLogLevel.Trace => LogLevel.Trace,
            TestLogLevel.Debug => LogLevel.Debug,
            TestLogLevel.Info => LogLevel.Info,
            TestLogLevel.Warn => LogLevel.Warn,
            TestLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Fatal
        };

        logger.Log(level, log.Exception, log.Message);
    }

    public void Dispose() => LogManager.Flush();
}
