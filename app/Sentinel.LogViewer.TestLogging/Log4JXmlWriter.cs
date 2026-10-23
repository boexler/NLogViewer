using System.Xml.Linq;

namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Writes a log as the log4j XML event that the viewer receives, without a logging library, so that it can also be changed
/// and broken on purpose
/// </summary>
public static class Log4JXmlWriter
{
    private static readonly XNamespace Log4J = "http://jakarta.apache.org/log4j/";

    /// <summary>
    /// The XML of an event. <paramref name="throwable"/> is the text of an exception, <paramref name="properties"/> further name and value pairs.
    /// </summary>
    public static string Event(
        TestLogLevel level,
        string logger,
        string message,
        string? throwable,
        string application,
        string machine,
        IEnumerable<KeyValuePair<string, string>>? properties = null,
        DateTime? time = null,
        string thread = "main")
    {
        var timestamp = new DateTimeOffset(time ?? DateTime.Now).ToUnixTimeMilliseconds();
        var data = new XElement(Log4J + "properties", Data("log4japp", application), Data("log4jmachinename", machine));
        foreach (var (name, value) in properties ?? Enumerable.Empty<KeyValuePair<string, string>>())
            data.Add(Data(name, value));

        var xml = new XElement(Log4J + "event",
            new XAttribute(XNamespace.Xmlns + "log4j", Log4J),
            new XAttribute("logger", logger),
            new XAttribute("timestamp", timestamp),
            new XAttribute("level", level.ToString().ToUpperInvariant()),
            new XAttribute("thread", thread),
            new XElement(Log4J + "message", message));
        if (!string.IsNullOrEmpty(throwable))
            xml.Add(new XElement(Log4J + "throwable", throwable));

        xml.Add(data);
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// The XML of a generated log
    /// </summary>
    public static string Event(GeneratedLog log, string application, string machine) =>
        Event(log.Level, log.Logger, log.Message, log.Exception?.ToString(), application, machine);

    private static XElement Data(string name, string value) =>
        new(Log4J + "data", new XAttribute("name", name), new XAttribute("value", value));
}
