using NLog;
using NLog.Config;
using NLog.Targets;

namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Applies NLog configuration for test logging: UDP target (Log4JXml, compatible with Chainsaw/LogViewer) with configurable address.
/// </summary>
public static class NLogTestLoggingConfig
{
    private const string TargetName = "testlog";

    /// <summary>
    /// Configures NLog to use the specified target (chainsaw or log4jxml) sending to the given UDP address.
    /// Both names use Log4JXmlTarget (NLog 6); Replaces existing rules so only this target is used for the test logger.
    /// </summary>
    public static void Apply(TestLoggingOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var config = new LoggingConfiguration();
        var address = options.UdpAddress;

        var target = new Log4JXmlTarget
        {
            Name = TargetName,
            Address = address
        };

        config.AddRule(LogLevel.Trace, LogLevel.Fatal, target);
        LogManager.Configuration = config;
        LogManager.ReconfigExistingLoggers();
    }
}
