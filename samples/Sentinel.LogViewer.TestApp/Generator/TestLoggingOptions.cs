namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>
/// Options for the test log generator (NLog target, UDP address, interval, exception probability).
/// </summary>
public class TestLoggingOptions
{
    /// <summary>NLog target name: "chainsaw" or "log4jxml".</summary>
    public string TargetName { get; set; } = "chainsaw";

    /// <summary>"udp" or "tcp".</summary>
    public string Protocol { get; set; } = "udp";

    /// <summary>The name of the application that the logs say they come from; empty is the name of the process.</summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Host (e.g. 127.0.0.1).</summary>
    public string UdpHost { get; set; } = "127.0.0.1";

    /// <summary>UDP port (e.g. 4000).</summary>
    public int UdpPort { get; set; } = 4000;

    /// <summary>Message generation interval in milliseconds.</summary>
    public int MessageIntervalMs { get; set; } = 1000;

    /// <summary>Probability of generating an exception log (0.0 to 1.0).</summary>
    public double ExceptionProbability { get; set; } = 0.2;

    /// <summary>Address string for the target (udp://host:port or tcp://host:port).</summary>
    public string UdpAddress => $"{Protocol}://{UdpHost}:{UdpPort}";
}
