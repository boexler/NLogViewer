namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>What sends the logs</summary>
public enum GeneratorLibrary
{
    NLog,
    Log4Net,
    PlainXml
}

/// <summary>What the user chose in the start window for the log generator</summary>
public sealed record GeneratorSettings(GeneratorLibrary Library, string Protocol, string Host, int Port)
{
    public string Address => $"{Protocol}://{Host}:{Port}";

    public string LibraryName => Library switch
    {
        GeneratorLibrary.NLog => "NLog 6",
        GeneratorLibrary.Log4Net => "log4net",
        _ => "plain XML"
    };
}
