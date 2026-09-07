using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sentinel.NLogViewer.App.Models;
using Sentinel.NLogViewer.App.Parsers;

namespace Sentinel.NLogViewer.App.Services;

/// <summary>
/// Service for receiving UDP log messages and parsing them
/// </summary>
public class UdpLogReceiverService(Log4JEventParser xmlParser) : IDisposable
{
	private const int WSAEADDRINUSE = 10048;

	private readonly List<UdpClient> _udpClients = new();
	private readonly List<CancellationTokenSource> _cancellationTokens = new();
	private readonly Log4JEventParser _xmlParser = xmlParser ?? throw new ArgumentNullException(nameof(xmlParser));
	private bool _disposed;

	public IObservable<LogEvent> Log4JEventObservable => _log4JEventObservable;
	private readonly Subject<LogEvent> _log4JEventObservable = new();

	/// <summary>
	/// Starts listening on the given UDP addresses. Returns a result indicating success and any error messages.
	/// </summary>
	/// <param name="addresses">List of addresses in format udp://host:port (e.g. udp://0.0.0.0:4000).</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>Result with AnyStarted and aggregated ErrorMessage (e.g. including process name/PID when port is in use).</returns>
	public async Task<StartListeningResult> StartListeningAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
	{
		StopListening();
		var errors = new List<string>();

		foreach (var address in addresses)
		{
			if (cancellationToken.IsCancellationRequested)
				break;
			var uri = new Uri(address);

			try
			{
				if (uri.Scheme != "udp")
					continue;

				var port = uri.Port;
				var udpClient = new UdpClient(port);
				_udpClients.Add(udpClient);

				var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				_cancellationTokens.Add(cts);

				_ = Task.Run(() => ReceiveLoop(udpClient, port, cts.Token), cts.Token);
			}
			catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.NativeErrorCode == WSAEADDRINUSE)
			{
				var msg = $"Error starting listener on {address}: {ex.Message}";
				var portForResolver = GetPortFromAddress(address);
				if (portForResolver != null)
				{
					var (processName, pid) = PortProcessResolver.TryGetProcessUsingPort(portForResolver.Value, udp: true);
					if (pid != null)
						msg += processName != null
							? $"\n\nProcess using port {uri.Port}: {processName} (PID: {pid})"
							: $"\n\nPort {uri.Port} in use by process PID: {pid}";
				}
				errors.Add(msg);
				System.Diagnostics.Debug.WriteLine(msg);
			}
			catch (Exception ex)
			{
				var msg = $"Error starting listener on {address}: {ex.Message}";
				errors.Add(msg);
				System.Diagnostics.Debug.WriteLine(msg);
			}
		}

		var anyStarted = _udpClients.Count > 0;
		var errorMessage = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : string.Empty;
		return await Task.FromResult(new StartListeningResult(anyStarted, errorMessage));
	}

	private static int? GetPortFromAddress(string address)
	{
		try
		{
			var uri = new Uri(address);
			return uri.Port;
		}
		catch
		{
			return null;
		}
	}

	public void StopListening()
	{
		foreach (var cts in _cancellationTokens)
		{
			cts.Cancel();
		}
		_cancellationTokens.Clear();

		foreach (var client in _udpClients)
		{
			try
			{
				client.Close();
			}
			catch
			{
				// Ignore errors during cleanup
			}
		}
		_udpClients.Clear();
	}

	private async Task ReceiveLoop(UdpClient client, int port, CancellationToken cancellationToken)
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

				_log4JEventObservable.OnNext(log4JEvent.ToLogEvent(sender));
			}
			catch (ObjectDisposedException)
			{
				// Client was disposed, exit loop
				break;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error receiving UDP data on port {port}: {ex.Message}");
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