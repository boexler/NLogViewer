namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Sends the generated logs as log4j XML that it writes itself (<see cref="Log4JXmlWriter"/>), without a logging library. The
/// application names can be several, separated by commas; each log gets one of them.
/// </summary>
public sealed class PlainXmlSink : ILogSink
{
    private readonly Random _random = new();
    private XmlTransport? _transport;
    private string[] _applications = { "TestApp" };

    public void Start(TestLoggingOptions options)
    {
        _transport?.Dispose();
        _transport = new XmlTransport(options.Protocol, options.UdpHost, options.UdpPort);
        var names = options.ApplicationName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _applications = names.Length > 0 ? names : _applications;
    }

    public void Write(GeneratedLog log)
    {
        var application = _applications[_random.Next(_applications.Length)];
        _transport!.Send(Log4JXmlWriter.Event(log, application, Environment.MachineName));
    }

    public void Dispose() => _transport?.Dispose();
}
