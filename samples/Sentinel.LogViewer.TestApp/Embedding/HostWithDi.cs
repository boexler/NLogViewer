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
        .ConfigureLogging(logging => logging
            // Important: Host.CreateDefaultBuilder() adds the console, debug, EventSource and, on Windows, the Windows Event Log as
            // logging providers. Every ILogger call goes to all providers, so without this the warnings and errors of the application
            // would also be written to the Event Log of the computer. Clear them and add only what the application wants.
            .ClearProviders()
            // Microsoft.Extensions.Logging drops everything below Information unless a minimum level is set.
            .SetMinimumLevel(LogLevel.Trace))
        .ConfigureServices(services => services.AddLogViewer(options => options.Capacity = 5000))
        .Build();

    public ILoggerFactory Factory => _host.Services.GetRequiredService<ILoggerFactory>();

    public void Dispose() => _host.Dispose();
}
