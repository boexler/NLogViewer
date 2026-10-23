using System.Diagnostics;
using System.Windows;
using Sentinel.LogViewer.TestApp.ControlTest;
using Sentinel.LogViewer.TestApp.Embedding;
using Sentinel.LogViewer.TestApp.Generator;

namespace Sentinel.LogViewer.TestApp;

/// <summary>
/// The start window asks what to do; the application then opens the log generator or the control test, set up for that choice
/// </summary>
public partial class App : Application
{
    /// <summary>How the control test connects the logging; the control test reads its logger factory from it</summary>
    public static IEmbedding? Embedding { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var start = new StartWindow();
        if (start.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        Window window;
        if (start.Generator is { } settings)
        {
            window = new GeneratorWindow(settings);
        }
        else
        {
            Embedding = EmbeddingModes.Start(start.EmbeddingMode);
            window = new ControlTestWindow(start.EmbeddingMode, Embedding.Factory);
        }

        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Embedding?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Starts the application again, so that the start window asks once more; the logging is built once and cannot be switched while running
    /// </summary>
    public static void BackToStart()
    {
        if (Environment.ProcessPath is { } path)
            Process.Start(path);

        Current.Shutdown();
    }
}
