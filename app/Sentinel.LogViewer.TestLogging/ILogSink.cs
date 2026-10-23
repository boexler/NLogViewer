namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Sends generated logs to the viewer, with a logging library or on its own
/// </summary>
public interface ILogSink : IDisposable
{
    /// <summary>Prepares the sink (configures the logging library, opens the connection)</summary>
    void Start(TestLoggingOptions options);

    /// <summary>Sends one log</summary>
    void Write(GeneratedLog log);
}
