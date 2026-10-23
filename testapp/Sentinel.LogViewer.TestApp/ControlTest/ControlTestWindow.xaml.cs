using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.TestApp.Embedding;

namespace Sentinel.LogViewer.TestApp.ControlTest;

/// <summary>
/// A window with log viewers: the application logs with <c>ILogger</c>, and each viewer shows a part of what arrives in the default
/// buffer. Popups and tasks show the same control in other places.
/// </summary>
public partial class ControlTestWindow : Window
{
    private static readonly string[] Orders = { "4711", "4712", "4713", "4714" };

    private readonly ILogger _logger;
    private readonly ILogger _deepLogger;
    private readonly ILoggerFactory _factory;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly CancellationTokenSource _stop = new();
    private readonly Random _random = new();
    private int _counter;
    private int _tasks;

    /// <param name="factory">The logger factory of the application, with the log viewer added to it</param>
    public ControlTestWindow(EmbeddingMode mode, ILoggerFactory factory)
    {
        InitializeComponent();
        ModeText.Text = EmbeddingModes.Describe(mode);

        _factory = factory;
        _logger = _factory.CreateLogger<ControlTestWindow>();
        _deepLogger = _factory.CreateLogger("Lorem.Ipsum.Foo.Hello.World.Lorem.Ipsum");

        // A viewer without a Source shows LogBuffer.Default. These two show a part of it, as a viewer per kind of entries.
        MessagesViewer.Source = FilteredSource.Create(entry => entry.Level <= LogEntryLevel.Info);
        ProblemsViewer.Source = FilteredSource.Create(entry => entry.Level >= LogEntryLevel.Warn);

        _timer.Tick += (_, _) => LogSomething();
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _stop.Cancel();
        };
    }

    private void LogSomething()
    {
        _counter++;
        switch (_random.Next(1, 7))
        {
            case 1:
                _logger.LogTrace("Hello everyone: {Counter}", _counter);
                break;
            case 2:
                _deepLogger.LogDebug("Hello from a deep logger: {Counter}", _counter);
                break;
            case 3:
                // A scope and the values of a message become properties of the entry, which can be shown as columns.
                using (_logger.BeginScope(new Dictionary<string, object?> { ["OrderId"] = Orders[_random.Next(Orders.Length)] }))
                    _logger.LogInformation("Order created in {Elapsed} ms", _random.Next(5, 120));
                break;
            case 4:
                _logger.LogWarning("Hello everyone: {Counter}", _counter);
                break;
            case 5:
                try
                {
                    _ = 1 / (_counter - _counter);
                }
                catch (DivideByZeroException ex)
                {
                    _logger.LogError(ex, "There was an error on division :/ {Counter}", _counter);
                }

                break;
            default:
                _logger.LogCritical(new EventId(42, "Fatal"), "{Counter}\nLorem ipsum dolor sit amet, consectetur adipisicing elit.", _counter);
                break;
        }
    }

    private void OnOpenPopup(object sender, RoutedEventArgs e) => new PopupWindow { Owner = this }.Show();

    /// <summary>
    /// A tab with its own viewer that shows only what one task logs
    /// </summary>
    private void OnAddTask(object sender, RoutedEventArgs e)
    {
        var number = _tasks++;
        var loggerName = $"MyFoo.Logger.{number}";
        var logger = _factory.CreateLogger(loggerName);

        var viewer = new Wpf.LogViewer { Source = FilteredSource.Create(entry => entry.LoggerName == loggerName) };
        var tab = new TabItem { Header = $"Task {number}", Content = viewer };
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(_stop.Token))
                    logger.LogInformation("Hello from task nr. {Number}. It's {Time}", number, DateTime.Now.ToLongTimeString());
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void OnBackToStart(object sender, RoutedEventArgs e) => App.BackToStart();
}
