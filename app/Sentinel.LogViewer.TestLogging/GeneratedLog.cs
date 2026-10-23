namespace Sentinel.LogViewer.TestLogging;

/// <summary>The levels of a generated log</summary>
public enum TestLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

/// <summary>One generated log: what a <see cref="ILogSink"/> sends</summary>
public sealed record GeneratedLog(TestLogLevel Level, string Logger, string Message, Exception? Exception);
