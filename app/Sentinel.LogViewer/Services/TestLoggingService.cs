using System.ComponentModel;
using Sentinel.NLogViewer.TestLogging;

namespace Sentinel.LogViewer.Services;

/// <summary>
/// Holds and controls the test log generator runner for the debug panel and optional auto-start.
/// </summary>
public sealed class TestLoggingService : INotifyPropertyChanged
{
    private TestLogGeneratorRunner? _runner;

    /// <summary>Whether the test log generator is currently running.</summary>
    public bool IsRunning => _runner?.IsRunning ?? false;

    /// <summary>Total normal messages generated (0 if not running).</summary>
    public int MessageCount => _runner?.MessageCount ?? 0;

    /// <summary>Total exception logs generated (0 if not running).</summary>
    public int ExceptionCount => _runner?.ExceptionCount ?? 0;

    /// <summary>
    /// Starts the test log generator with the given options. Stops any existing runner first.
    /// </summary>
    public void Start(TestLoggingOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        Stop();
        _runner = new TestLogGeneratorRunner(options);
        _runner.CountsChanged += OnCountsChanged;
        _runner.Start();
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(ExceptionCount));
    }

    /// <summary>Stops and disposes the current runner.</summary>
    public void Stop()
    {
        if (_runner == null) return;
        _runner.CountsChanged -= OnCountsChanged;
        _runner.Dispose();
        _runner = null;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(ExceptionCount));
    }

    private void OnCountsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MessageCount));
        OnPropertyChanged(nameof(ExceptionCount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
