namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Options for the test log generator (NLog target, UDP address, interval, exception probability).
/// </summary>
public class TestLoggingOptions
{
    /// <summary>NLog target name: "chainsaw" or "log4jxml".</summary>
    public string TargetName { get; set; } = "chainsaw";

    /// <summary>UDP host (e.g. 127.0.0.1).</summary>
    public string UdpHost { get; set; } = "127.0.0.1";

    /// <summary>UDP port (e.g. 4000).</summary>
    public int UdpPort { get; set; } = 4000;

    /// <summary>Message generation interval in milliseconds.</summary>
    public int MessageIntervalMs { get; set; } = 1000;

    /// <summary>Probability of generating an exception log (0.0 to 1.0).</summary>
    public double ExceptionProbability { get; set; } = 0.2;

    /// <summary>UDP address string for NLog (udp://host:port).</summary>
    public string UdpAddress => $"udp://{UdpHost}:{UdpPort}";
}
