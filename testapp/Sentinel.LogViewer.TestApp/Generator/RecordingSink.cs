using Sentinel.LogViewer.TestLogging;

namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>
/// Passes the logs on to the sink that sends them, and tells the window what was sent or why it failed (the runner ignores errors)
/// </summary>
public sealed class RecordingSink(ILogSink inner, Action<string> report, Action failed) : ILogSink
{
    public void Start(TestLoggingOptions options) => inner.Start(options);

    public void Write(GeneratedLog log)
    {
        try
        {
            inner.Write(log);
            report($"{DateTime.Now:HH:mm:ss}  {log.Level,-5}  {log.Logger}  {log.Message}");
        }
        catch (Exception ex)
        {
            failed();
            report($"{DateTime.Now:HH:mm:ss}  FAILED  {ex.Message}");
        }
    }

    public void Dispose() => inner.Dispose();
}
