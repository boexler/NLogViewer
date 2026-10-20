using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sentinel.LogViewer.Core;

/// <summary>
/// Service for receiving UDP log messages and parsing them. It keeps a <see cref="ReceiveSource"/> for every address, which can be
/// started and stopped one by one.
/// </summary>
public class UdpLogReceiverService(Log4JEventParser xmlParser) : IDisposable
{
	private const int WSAEADDRINUSE = 10048;

	private sealed class Listener(ReceiveSource source)
	{
		public ReceiveSource Source { get; } = source;
		public UdpClient? Client { get; set; }
		public CancellationTokenSource? Cancellation { get; set; }
	}

	private readonly object _lock = new();
	private List<Listener> _listeners = new();
	private readonly Log4JEventParser _xmlParser = xmlParser ?? throw new ArgumentNullException(nameof(xmlParser));
	private bool _disposed;

	public IObservable<LogEvent> Log4JEventObservable => _log4JEventObservable;
	private readonly Subject<LogEvent> _log4JEventObservable = new();

	/// <summary>
	/// The sources of the configured addresses, in the order of the addresses
	/// </summary>
	public IReadOnlyList<ReceiveSource> Sources
	{
		get
		{
			lock (_lock)
				return _listeners.Select(listener => listener.Source).ToList();
		}
	}

	/// <summary>
	/// Raised when a source was added, removed, started or stopped; not for the counters, which change all the time
	/// </summary>
	public event EventHandler? SourcesChanged;

	/// <summary>
	/// Makes the sources the ones of the addresses: a source of an address that is in the list stays as it is, a new address gets a source
	/// that is stopped, and a source of an address that is no longer in the list is stopped and removed. Addresses that are not UDP are left out.
	/// </summary>
	public void Configure(IReadOnlyList<string> addresses)
	{
		lock (_lock)
		{
			var next = new List<Listener>();
			foreach (var address in addresses)
			{
				if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "udp" || next.Any(listener => listener.Source.Address == address))
					continue;

				next.Add(_listeners.FirstOrDefault(listener => listener.Source.Address == address) ?? new Listener(new ReceiveSource(address, "UDP", uri.Port)));
			}

			foreach (var removed in _listeners.Except(next))
				StopListener(removed);

			_listeners = next;
		}

		SourcesChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Starts listening on the given UDP addresses. Returns a result indicating success and any error messages.
	/// </summary>
	/// <param name="addresses">List of addresses in format udp://host:port (e.g. udp://0.0.0.0:4000).</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>Result with AnyStarted and aggregated ErrorMessage (e.g. including process name/PID when port is in use).</returns>
	public async Task<StartListeningResult> StartListeningAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
	{
		StopListening();
		Configure(addresses);
		var errors = new List<string>();

		foreach (var source in Sources)
		{
			if (cancellationToken.IsCancellationRequested)
				break;

			var error = TryStart(source, cancellationToken);
			if (error != null)
				errors.Add(error);
		}

		SourcesChanged?.Invoke(this, EventArgs.Empty);
		var anyStarted = Sources.Any(source => source.State == ReceiveSourceState.Listening);
		var errorMessage = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : string.Empty;
		return await Task.FromResult(new StartListeningResult(anyStarted, errorMessage));
	}

	/// <summary>
	/// Starts one source, e.g. after the user switched it on or asked to try again. Returns whether it listens now; if it does not,
	/// <see cref="ReceiveSource.Error"/> says why.
	/// </summary>
	public bool Start(ReceiveSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (source.State != ReceiveSourceState.Listening)
			TryStart(source, CancellationToken.None);

		SourcesChanged?.Invoke(this, EventArgs.Empty);
		return source.State == ReceiveSourceState.Listening;
	}

	/// <summary>
	/// Stops one source; it stays in <see cref="Sources"/> and can be started again
	/// </summary>
	public void Stop(ReceiveSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		lock (_lock)
		{
			var listener = _listeners.FirstOrDefault(candidate => ReferenceEquals(candidate.Source, source));
			if (listener != null)
				StopListener(listener);
		}

		SourcesChanged?.Invoke(this, EventArgs.Empty);
	}

	public void StopListening()
	{
		lock (_lock)
		{
			foreach (var listener in _listeners)
				StopListener(listener);
		}

		SourcesChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Opens the port of the source; returns the message of the failure, or null when it listens
	/// </summary>
	private string? TryStart(ReceiveSource source, CancellationToken cancellationToken)
	{
		Listener? listener;
		lock (_lock)
			listener = _listeners.FirstOrDefault(candidate => ReferenceEquals(candidate.Source, source));
		if (listener == null)
			return null;

		var uri = new Uri(source.Address);
		try
		{
			var client = new UdpClient(uri.Port);
			var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			listener.Client = client;
			listener.Cancellation = cancellation;
			source.Port = ((IPEndPoint)client.Client.LocalEndPoint!).Port;
			source.ResetCounters();
			source.State = ReceiveSourceState.Listening;
			source.Failure = ReceiveSourceFailure.None;
			source.Error = source.BlockingProcess = null;

			_ = Task.Run(() => ReceiveLoop(source, client, cancellation.Token), cancellation.Token);
			return null;
		}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.NativeErrorCode == WSAEADDRINUSE)
		{
			var message = $"Error starting listener on {source.Address}: {ex.Message}";
			var (processName, pid) = PortProcessResolver.TryGetProcessUsingPort(uri.Port, udp: true);
			if (pid != null)
			{
				message += processName != null
					? $"\n\nProcess using port {uri.Port}: {processName} (PID: {pid})"
					: $"\n\nPort {uri.Port} in use by process PID: {pid}";
				source.BlockingProcess = processName != null ? $"{processName} (PID {pid})" : $"PID {pid}";
			}

			Fail(source, ReceiveSourceFailure.PortInUse, message);
			return message;
		}
		catch (Exception ex)
		{
			var message = $"Error starting listener on {source.Address}: {ex.Message}";
			Fail(source, ReceiveSourceFailure.Other, message);
			return message;
		}
	}

	private static void Fail(ReceiveSource source, ReceiveSourceFailure failure, string message)
	{
		source.State = ReceiveSourceState.Failed;
		source.Failure = failure;
		source.Error = message;
		System.Diagnostics.Debug.WriteLine(message);
	}

	private static void StopListener(Listener listener)
	{
		listener.Cancellation?.Cancel();
		listener.Cancellation = null;

		try
		{
			listener.Client?.Close();
		}
		catch
		{
			// Ignore errors during cleanup
		}

		listener.Client = null;
		var source = listener.Source;
		source.State = ReceiveSourceState.Stopped;
		source.Failure = ReceiveSourceFailure.None;
		source.Error = source.BlockingProcess = null;
	}

	private async Task ReceiveLoop(ReceiveSource source, UdpClient client, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				var result = await client.ReceiveAsync();
				var data = result.Buffer;
				var sender = result.RemoteEndPoint.Address.ToString();

				// Parse the received data
				var xml = Encoding.UTF8.GetString(data);
				var log4JEvent = _xmlParser.Parse(xml);

				source.RecordEntry();
				_log4JEventObservable.OnNext(log4JEvent.ToLogEvent(sender));
			}
			catch (ObjectDisposedException)
			{
				// Client was disposed, exit loop
				break;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error receiving UDP data on port {source.Port}: {ex.Message}");
				// Continue receiving despite errors
			}
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			StopListening();
			_log4JEventObservable.Dispose();
			_disposed = true;
		}
	}
}
