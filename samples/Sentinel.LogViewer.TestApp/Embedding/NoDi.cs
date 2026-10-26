using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Extensions.Logging;

namespace Sentinel.LogViewer.TestApp.Embedding;

/// <summary>
/// An application without dependency injection: build the logger factory once at the start, add the log viewer to it, and log with
/// the loggers of the factory. The control in the window needs nothing else.
/// </summary>
internal sealed class NoDi : IEmbedding
{
    public ILoggerFactory Factory { get; } = LoggerFactory.Create(builder => builder
        // Without this, Microsoft.Extensions.Logging drops everything below Information.
        .SetMinimumLevel(LogLevel.Trace)
        .AddLogViewer(options => options.Capacity = 5000));

    public void Dispose() => Factory.Dispose();
}
