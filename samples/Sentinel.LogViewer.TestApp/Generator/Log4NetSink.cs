using System.Net;
using System.Net.Sockets;
using System.Text;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;

namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>
/// Sends the generated logs with log4net: a <c>UdpAppender</c> with the log4j XML layout, as a program that uses log4net would send them
/// </summary>
public sealed class Log4NetSink : ILogSink
{
    private ILoggerRepository? _repository;

    public void Start(TestLoggingOptions options)
    {
        var address = Dns.GetHostAddresses(options.UdpHost).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? throw new InvalidOperationException($"No IPv4 address for {options.UdpHost}.");
        var layout = new XmlLayoutSchemaLog4J();
        layout.ActivateOptions();
        var appender = new UdpAppender { Name = "udp", RemoteAddress = address, RemotePort = options.UdpPort, Layout = layout, Encoding = Encoding.UTF8 };
        appender.ActivateOptions();

        _repository = LogManager.CreateRepository("TestApp-" + Guid.NewGuid().ToString("N"));
        BasicConfigurator.Configure(_repository, appender);
    }

    public void Write(GeneratedLog log)
    {
        var level = log.Level switch
        {
            TestLogLevel.Trace => Level.Trace,
            TestLogLevel.Debug => Level.Debug,
            TestLogLevel.Info => Level.Info,
            TestLogLevel.Warn => Level.Warn,
            TestLogLevel.Error => Level.Error,
            _ => Level.Fatal
        };

        _repository!.GetLogger(log.Logger).Log(typeof(Log4NetSink), level, log.Message, log.Exception);
    }

    public void Dispose() => _repository?.Shutdown();
}
