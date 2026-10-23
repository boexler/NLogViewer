using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Extensions.Logging;

namespace Sentinel.LogViewer.TestApp.Embedding;

/// <summary>
/// An application with a Generic Host: register the log viewer with the services. The buffer is also registered, so that it can be
/// injected (<c>LogBuffer</c>, <c>ILogSource</c>); the control in the window still needs no setting.
/// </summary>
internal sealed class HostWithDi : IEmbedding
{
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace))
        .ConfigureServices(services => services.AddLogViewer(options => options.Capacity = 5000))
        .Build();

    public ILoggerFactory Factory => _host.Services.GetRequiredService<ILoggerFactory>();

    public void Dispose() => _host.Dispose();
}
