using System;
using System.Threading.Tasks;
using NLog;
using Sentinel.NLogViewer.TestLogging;

namespace Sentinel.LogViewer.TestApp;

/// <summary>
/// Console test app: runs the shared test log generator until a key is pressed.
/// </summary>
internal static class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("NLogViewer Client Application Test App");
        var nlogVersion = typeof(LogManager).Assembly.GetName().Version;
        Console.WriteLine($"NLog Version: {nlogVersion}");

        var targetName = Environment.GetEnvironmentVariable("NLOG_TARGET") ?? "chainsaw";
        Console.WriteLine($"Using target: {targetName}");
        Console.WriteLine("Press any key to stop logging...");
        Console.WriteLine();

        var options = new TestLoggingOptions
        {
            TargetName = targetName,
            UdpHost = "127.0.0.1",
            UdpPort = 4000,
            MessageIntervalMs = 1000,
            ExceptionProbability = 0.2
        };

        using var runner = new TestLogGeneratorRunner(options);
        runner.Start();

        await Task.Run(() => Console.ReadKey(true));

        runner.Stop();

        Console.WriteLine();
        Console.WriteLine($"Total messages logged: {runner.MessageCount}");
        Console.WriteLine($"Total exceptions logged: {runner.ExceptionCount}");
        Console.WriteLine("Shutting down...");

        LogManager.Shutdown();
    }
}
