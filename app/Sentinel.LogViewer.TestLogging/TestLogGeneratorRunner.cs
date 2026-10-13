using System.Threading;

namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Runs the test log generator: applies NLog config and a timer that generates log messages at a configurable interval.
/// </summary>
public sealed class TestLogGeneratorRunner : IDisposable
{
    private readonly TestLoggingOptions _options;
    private readonly Random _random = new();
    private Timer? _timer;
    private int _messageCounter;
    private int _exceptionCounter;
    private bool _disposed;

    /// <summary>Total number of normal log messages generated.</summary>
    public int MessageCount => _messageCounter;

    /// <summary>Total number of exception log messages generated.</summary>
    public int ExceptionCount => _exceptionCounter;

    /// <summary>Whether the runner is currently generating logs.</summary>
    public bool IsRunning => _timer != null;

    /// <summary>Raised when message or exception counts change (optional for UI).</summary>
    public event EventHandler? CountsChanged;

    public TestLogGeneratorRunner(TestLoggingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Applies NLog config and starts the timer. Idempotent: if already running, does nothing.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestLogGeneratorRunner));
        if (_timer != null)
            return;

        NLogTestLoggingConfig.Apply(_options);
        var intervalMs = Math.Max(1, _options.MessageIntervalMs);
        _timer = new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMs));
    }

    /// <summary>
    /// Stops the timer. Idempotent.
    /// </summary>
    public void Stop()
    {
        var t = Interlocked.Exchange(ref _timer, null);
        t?.Dispose();
    }

    private void OnTick(object? state)
    {
        try
        {
            var totalCount = _messageCounter + _exceptionCounter;
            var isException = TestLogMessageGenerator.GenerateOne(
                _random,
                totalCount,
                Math.Clamp(_options.ExceptionProbability, 0, 1));

            if (isException)
                Interlocked.Increment(ref _exceptionCounter);
            else
                Interlocked.Increment(ref _messageCounter);

            CountsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Avoid crashing the timer; ignore generation errors
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
