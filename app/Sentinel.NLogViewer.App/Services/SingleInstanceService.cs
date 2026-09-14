using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sentinel.NLogViewer.App.Services;

/// <summary>
/// Ensures that only one application instance runs and forwards invocations to the primary instance.
/// </summary>
internal sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\Boexler.Sentinel.NLogViewer.SingleInstance";
    private const string PipeName = "Boexler.Sentinel.NLogViewer.SingleInstance";
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Initializes the instance coordinator and attempts to become the primary instance.
    /// </summary>
    public SingleInstanceService() : this(MutexName, PipeName)
    {
    }

    /// <summary>
    /// Initializes an instance coordinator with explicit operating-system object names.
    /// </summary>
    internal SingleInstanceService(string mutexName, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        _pipeName = pipeName;
        _mutex = new Mutex(initiallyOwned: false, mutexName, out var isPrimaryInstance);
        IsPrimaryInstance = isPrimaryInstance;
    }

    /// <summary>
    /// Occurs when another process forwards command-line paths to the primary instance.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? InvocationReceived;

    /// <summary>
    /// Gets whether this process owns the application instance mutex.
    /// </summary>
    public bool IsPrimaryInstance { get; }

    /// <summary>
    /// Starts accepting forwarded invocations in the primary instance.
    /// </summary>
    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsPrimaryInstance || _listenerTask != null)
            return;

        _listenerTask = ListenAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Forwards an invocation to the primary instance.
    /// </summary>
    public async Task ForwardInvocationAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var payload = JsonSerializer.Serialize(arguments);
        using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        await pipe.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);
        await using var writer = new StreamWriter(pipe);
        await writer.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Accepts invocation messages until application shutdown.
    /// </summary>
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(pipe);
                var payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var arguments = JsonSerializer.Deserialize<string[]>(payload) ?? Array.Empty<string>();
                InvocationReceived?.Invoke(this, arguments);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // A disconnected client must not stop the listener for subsequent invocations.
            }
            catch (JsonException)
            {
                // Ignore malformed messages from unrelated or outdated clients.
            }
        }
    }

    /// <summary>
    /// Stops the listener and releases owned operating-system resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationTokenSource.Cancel();

        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(
                   innerException => innerException is OperationCanceledException))
        {
            // Cancellation is the expected listener termination path.
        }

        _cancellationTokenSource.Dispose();

        _mutex.Dispose();
    }
}
