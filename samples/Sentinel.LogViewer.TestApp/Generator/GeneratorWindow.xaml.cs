using System.Collections.ObjectModel;
using System.Windows;

namespace Sentinel.LogViewer.TestApp.Generator;

public partial class GeneratorWindow : Window
{
    private const int MaxLines = 300;

    private readonly GeneratorSettings _settings;
    private readonly ObservableCollection<string> _lines = new();
    private TestLogGeneratorRunner? _runner;
    private int _failed;

    public GeneratorWindow(GeneratorSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Title = $"Log generator · {settings.LibraryName} · {settings.Address}";
        LinesList.ItemsSource = _lines;
        Closed += (_, _) => _runner?.Dispose();
    }

    private ILogSink CreateSink() => _settings.Library switch
    {
        GeneratorLibrary.Log4Net => new Log4NetSink(),
        GeneratorLibrary.PlainXml => new PlainXmlSink(),
        _ => new NLogSink()
    };

    private void OnStartStop(object sender, RoutedEventArgs e)
    {
        if (_runner != null)
        {
            _runner.Dispose();
            _runner = null;
            StartStopButton.Content = "Start";
            return;
        }

        if (!int.TryParse(IntervalBox.Text, out var interval) || interval < 1 ||
            !int.TryParse(ExceptionBox.Text, out var percent) || percent is < 0 or > 100)
        {
            StatusText.Text = "The interval is a number of milliseconds, the exceptions a percentage from 0 to 100.";
            return;
        }

        var options = new TestLoggingOptions
        {
            Protocol = _settings.Protocol,
            UdpHost = _settings.Host,
            UdpPort = _settings.Port,
            MessageIntervalMs = interval,
            ExceptionProbability = percent / 100.0,
            ApplicationName = ApplicationBox.Text.Trim()
        };

        try
        {
            _failed = 0;
            _runner = new TestLogGeneratorRunner(options, new RecordingSink(CreateSink(), AddLine, () => Interlocked.Increment(ref _failed)));
            _runner.CountsChanged += (_, _) => Dispatcher.BeginInvoke(UpdateCounts);
            _runner.Start();
            StartStopButton.Content = "Stop";
            StatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _runner?.Dispose();
            _runner = null;
            StatusText.Text = ex.Message;
        }
    }

    private void AddLine(string line) => Dispatcher.BeginInvoke(() =>
    {
        _lines.Add(line);
        while (_lines.Count > MaxLines)
            _lines.RemoveAt(0);

        LinesList.ScrollIntoView(line);
    });

    private void UpdateCounts()
    {
        if (_runner != null)
            CountsText.Text = $"Sent: {_runner.MessageCount + _runner.ExceptionCount}   With exception: {_runner.ExceptionCount}   Failed: {_failed}";
    }

    private void OnSendMessage(object sender, RoutedEventArgs e) => new MessageWindow(_settings) { Owner = this }.Show();

    private void OnEdgeCases(object sender, RoutedEventArgs e)
    {
        EdgeButton.ContextMenu.PlacementTarget = EdgeButton;
        EdgeButton.ContextMenu.IsOpen = true;
    }

    private static string Event(string message) =>
        Log4JXmlWriter.Event(TestLogLevel.Info, "EdgeCase", message, null, "TestApp", Environment.MachineName);

    private void OnEdgeInPieces(object sender, RoutedEventArgs e)
    {
        if (_settings.Protocol != "tcp")
        {
            StatusText.Text = "An event in pieces needs TCP. Choose TCP in the start window.";
            return;
        }

        SendEdgeCase("One event, sent in 4 pieces with a pause after each", transport =>
            transport.SendInPieces(Event("This event arrived in pieces"), 4, TimeSpan.FromMilliseconds(400)));
    }

    private void OnEdgeBroken(object sender, RoutedEventArgs e) =>
        SendEdgeCase("Broken XML (the event is never closed)", transport =>
            transport.Send("<log4j:event logger=\"EdgeCase\" level=\"INFO\" timestamp=\"0\"><log4j:message>this event is never closed"));

    private void OnEdgeLarge(object sender, RoutedEventArgs e)
    {
        // One datagram holds 65,507 bytes at most; a TCP connection can carry more, up to the limit of the receiver (4 MB).
        var size = _settings.Protocol == "tcp" ? 2_000_000 : 60_000;
        SendEdgeCase($"A very large event ({size:N0} characters)", transport => transport.Send(Event(new string('x', size))));
    }

    private void OnEdgeSpecial(object sender, RoutedEventArgs e) =>
        SendEdgeCase("Special characters and emoji", transport =>
            transport.Send(Event("Ünïcödé ✓ 😀 <tag> & \"quotes\" 'single' \t tab")));

    /// <summary>
    /// Sends on its own connection, away from the user interface thread, and tells the result
    /// </summary>
    private async void SendEdgeCase(string what, Action<XmlTransport> send)
    {
        try
        {
            await Task.Run(() =>
            {
                using var transport = new XmlTransport(_settings.Protocol, _settings.Host, _settings.Port);
                send(transport);
            });
            StatusText.Text = $"Sent: {what}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed: {what}. {ex.Message}";
        }
    }

    private void OnBackToStart(object sender, RoutedEventArgs e) => App.BackToStart();
}
