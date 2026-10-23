using Microsoft.Extensions.Logging;

namespace Sentinel.LogViewer.TestApp.Embedding;

/// <summary>The ways to connect the logging of an application to the log viewer control</summary>
public enum EmbeddingMode
{
    NoDi,
    HostWithDi,
    HostWithNLog
}

/// <summary>
/// The logging of an application that embeds the control. The control itself needs no setting: it shows what the logging writes to
/// <c>LogBuffer.Default</c>.
/// </summary>
public interface IEmbedding : IDisposable
{
    ILoggerFactory Factory { get; }
}

public static class EmbeddingModes
{
    public static IEmbedding Start(EmbeddingMode mode) => mode switch
    {
        EmbeddingMode.HostWithDi => new HostWithDi(),
        EmbeddingMode.HostWithNLog => new HostWithNLog(),
        _ => new NoDi()
    };

    public static string Describe(EmbeddingMode mode) => mode switch
    {
        EmbeddingMode.HostWithDi => "Generic Host with dependency injection: services.AddLogViewer(...)",
        EmbeddingMode.HostWithNLog => $"Host and NLog behind ILogger: the entries are also written to {HostWithNLog.LogFile}",
        _ => "Logging of .NET without dependency injection: LoggerFactory.Create(b => b.AddLogViewer(...))"
    };
}
