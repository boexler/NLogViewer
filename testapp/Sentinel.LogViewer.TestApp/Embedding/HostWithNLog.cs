using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Sentinel.LogViewer.Extensions.Logging;

namespace Sentinel.LogViewer.TestApp.Embedding;

/// <summary>
/// An application that logs with NLog behind <c>ILogger</c>: NLog writes a file, and the log viewer gets the same entries, as both are
/// providers of the same logger factory. (A call of <c>LogManager.GetLogger</c> in the code would reach the file only.)
/// </summary>
internal sealed class HostWithNLog : IEmbedding
{
    public static readonly string LogFile = Path.Combine(Path.GetTempPath(), "Sentinel.LogViewer.TestApp", "controltest.log");

    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddNLog(FileConfiguration()))
        .ConfigureServices(services => services.AddLogViewer(options => options.Capacity = 5000))
        .Build();

    public ILoggerFactory Factory => _host.Services.GetRequiredService<ILoggerFactory>();

    private static NLog.Config.LoggingConfiguration FileConfiguration()
    {
        var configuration = new NLog.Config.LoggingConfiguration();
        configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, new FileTarget("file")
        {
            FileName = LogFile,
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}"
        });
        return configuration;
    }

    public void Dispose()
    {
        _host.Dispose();
        NLog.LogManager.Shutdown();
    }
}
