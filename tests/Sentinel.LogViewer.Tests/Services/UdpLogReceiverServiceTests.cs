using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="UdpLogReceiverService"/> StartListeningAsync result and error handling.
/// </summary>
public class UdpLogReceiverServiceTests
{
	private static UdpLogReceiverService CreateService()
	{
		return new UdpLogReceiverService(new Log4JEventParser());
	}

	[Fact]
	public async Task StartListeningAsync_EmptyAddresses_ReturnsNoStartedAndEmptyError()
	{
		using var service = CreateService();
		var result = await service.StartListeningAsync(new List<string>());

		Assert.False(result.AnyStarted);
		Assert.Equal(string.Empty, result.ErrorMessage);
	}

	[Fact]
	public async Task StartListeningAsync_NonUdpAddresses_SkipsAndReturnsNoStarted()
	{
		using var service = CreateService();
		var addresses = new List<string> { "http://0.0.0.0:4000", "https://localhost:5000" };
		var result = await service.StartListeningAsync(addresses);

		Assert.False(result.AnyStarted);
		Assert.Equal(string.Empty, result.ErrorMessage);
	}

	[Fact]
	public async Task StartListeningAsync_PortInUse_ReturnsNoStartedAndNonEmptyError()
	{
		// Get a free port, then bind it so the service cannot bind
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
		listener.Stop();

		using var occupyingClient = new UdpClient(port);
		var address = $"udp://0.0.0.0:{port}";

		using var service = CreateService();
		var result = await service.StartListeningAsync(new List<string> { address });

		Assert.False(result.AnyStarted);
		Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
		Assert.Contains(address, result.ErrorMessage);
	}

	[Fact]
	public async Task StartListeningAsync_ValidPort_ReturnsStarted()
	{
		using var service = CreateService();
		// Port 0 lets the OS assign an available port
		var address = "udp://0.0.0.0:0";
		var result = await service.StartListeningAsync(new List<string> { address });

		Assert.True(result.AnyStarted);
		service.StopListening();
	}
}
