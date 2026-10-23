using Sentinel.LogViewer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Sentinel.LogViewer.Parsers;
using Sentinel.LogViewer.Services;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for <see cref="LogReceiverService"/> StartListeningAsync result and error handling.
/// </summary>
public class LogReceiverServiceTests
{
	private static LogReceiverService CreateService()
	{
		return new LogReceiverService(new Log4JEventParser());
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
		// Get a free UDP port, then bind it so the service cannot bind (a free TCP port can be forbidden or taken for UDP, e.g. on a CI runner)
		var port = FreeUdpPort();
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

	private static int FreeUdpPort()
	{
		using var probe = new UdpClient(0);
		return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
	}

	private static void Send(int port, string message = "x")
	{
		var xml = $@"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/""><log4j:message>{message}</log4j:message></log4j:event>";
		using var sender = new UdpClient();
		sender.Send(Encoding.UTF8.GetBytes(xml), Encoding.UTF8.GetByteCount(xml), new IPEndPoint(IPAddress.Loopback, port));
	}

	private static async Task<bool> WaitFor(Func<bool> condition)
	{
		for (var i = 0; i < 100 && !condition(); i++)
			await Task.Delay(30);

		return condition();
	}

	[Fact]
	public async Task EveryConfiguredAddress_HasASource_WithItsState()
	{
		var busy = FreeUdpPort();
		using var occupying = new UdpClient(busy);
		var free = FreeUdpPort();
		using var service = CreateService();
		var changes = 0;
		service.SourcesChanged += (_, _) => changes++;

		var result = await service.StartListeningAsync(new List<string> { $"udp://0.0.0.0:{free}", $"udp://0.0.0.0:{busy}", "http://0.0.0.0:1" });

		Assert.True(result.AnyStarted);
		Assert.Equal(2, service.Sources.Count);
		var listening = service.Sources[0];
		Assert.Equal((ReceiveSourceState.Listening, $"UDP {free}", "UDP"), (listening.State, listening.Name, listening.Kind));
		var failed = service.Sources[1];
		Assert.Equal(ReceiveSourceState.Failed, failed.State);
		Assert.Equal(ReceiveSourceFailure.PortInUse, failed.Failure);
		Assert.Contains(failed.Address, failed.Error);
		Assert.True(changes > 0);
	}

	[Fact]
	public async Task ASource_CountsItsEntries_AndRemembersTheLastOne()
	{
		var port = FreeUdpPort();
		using var service = CreateService();
		await service.StartListeningAsync(new List<string> { $"udp://0.0.0.0:{port}" });
		var source = service.Sources.Single();
		Assert.Equal(0, source.EntryCount);
		Assert.Null(source.LastEntryUtc);

		Send(port);
		Send(port);

		Assert.True(await WaitFor(() => source.EntryCount == 2));
		Assert.InRange(source.LastEntryUtc!.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddSeconds(1));
	}

	[Fact]
	public async Task ASource_CanBeStoppedAndStartedAgain_WithoutTheOthers()
	{
		var first = FreeUdpPort();
		var second = FreeUdpPort();
		using var service = CreateService();
		await service.StartListeningAsync(new List<string> { $"udp://0.0.0.0:{first}", $"udp://0.0.0.0:{second}" });
		var (one, two) = (service.Sources[0], service.Sources[1]);

		service.Stop(one);

		Assert.Equal(ReceiveSourceState.Stopped, one.State);
		Assert.Equal(ReceiveSourceState.Listening, two.State);
		Send(second);
		Assert.True(await WaitFor(() => two.EntryCount == 1));

		Assert.True(service.Start(one));
		Assert.Equal(ReceiveSourceState.Listening, one.State);
		Send(first);
		Assert.True(await WaitFor(() => one.EntryCount == 1));
	}

	[Fact]
	public async Task AFailedSource_CanBeTriedAgain_WhenThePortIsFree()
	{
		var port = FreeUdpPort();
		var occupying = new UdpClient(port);
		using var service = CreateService();
		await service.StartListeningAsync(new List<string> { $"udp://0.0.0.0:{port}" });
		var source = service.Sources.Single();
		Assert.Equal(ReceiveSourceState.Failed, source.State);
		Assert.False(service.Start(source));

		occupying.Close();

		Assert.True(service.Start(source));
		Assert.Equal((ReceiveSourceState.Listening, ReceiveSourceFailure.None, (string?)null), (source.State, source.Failure, source.Error));
	}

	[Fact]
	public void Configure_KeepsTheSourcesOfKnownAddresses_AndStopsTheOnesThatAreGone()
	{
		var port = FreeUdpPort();
		using var service = CreateService();
		service.Configure(new List<string> { $"udp://0.0.0.0:{port}", "udp://0.0.0.0:1" });
		var kept = service.Sources[0];
		Assert.All(service.Sources, source => Assert.Equal(ReceiveSourceState.Stopped, source.State));
		Assert.True(service.Start(kept));

		service.Configure(new List<string> { "udp://0.0.0.0:2", $"udp://0.0.0.0:{port}" });

		Assert.Equal(new[] { "UDP 2", kept.Name }, service.Sources.Select(source => source.Name));
		Assert.Same(kept, service.Sources[1]);
		Assert.Equal(ReceiveSourceState.Listening, kept.State);
	}

	private static async Task SendTcp(int port, params string[] pieces)
	{
		using var client = new System.Net.Sockets.TcpClient();
		await client.ConnectAsync(IPAddress.Loopback, port);
		var stream = client.GetStream();
		foreach (var piece in pieces)
		{
			await stream.WriteAsync(Encoding.UTF8.GetBytes(piece));
			await stream.FlushAsync();
			await Task.Delay(30);
		}
	}

	private static string Event(string message) =>
		$@"<log4j:event logger=""L"" level=""INFO"" timestamp=""1"" xmlns:log4j=""http://jakarta.apache.org/log4j/""><log4j:message>{message}</log4j:message></log4j:event>";

	[Fact]
	public async Task ATcpSource_ReceivesEvents_FromSeveralClients_AlsoWhenTheyArriveInPieces()
	{
		var port = FreeUdpPort();
		using var service = CreateService();
		var received = new System.Collections.Concurrent.ConcurrentBag<string>();
		using var subscription = System.ObservableExtensions.Subscribe(service.Log4JEventObservable, e => received.Add(e.Entry.Message));
		await service.StartListeningAsync(new List<string> { $"tcp://0.0.0.0:{port}" });
		var source = service.Sources.Single();
		Assert.Equal(("TCP", ReceiveSourceState.Listening), (source.Kind, source.State));

		var text = Event("one") + "\r\n" + Event("two");
		await Task.WhenAll(SendTcp(port, text[..40], text[40..130], text[130..]), SendTcp(port, Event("three")));

		Assert.True(await WaitFor(() => received.Count == 3));
		Assert.Equal(new[] { "one", "three", "two" }, received.OrderBy(message => message));
		Assert.Equal(3, source.EntryCount);
	}

	[Fact]
	public async Task UdpAndTcpCanUseTheSamePort_AndStoppingStopsTheClients()
	{
		var port = FreeUdpPort();
		using var service = CreateService();
		await service.StartListeningAsync(new List<string> { $"udp://0.0.0.0:{port}", $"tcp://0.0.0.0:{port}" });
		Assert.Equal(new[] { ReceiveSourceState.Listening, ReceiveSourceState.Listening }, service.Sources.Select(source => source.State));

		service.StopListening();

		Assert.All(service.Sources, source => Assert.Equal(ReceiveSourceState.Stopped, source.State));
		using var client = new System.Net.Sockets.TcpClient();
		await Assert.ThrowsAnyAsync<System.Net.Sockets.SocketException>(() => client.ConnectAsync(IPAddress.Loopback, port));
	}

	[Fact]
	public async Task ATcpPortThatIsInUse_FailsLikeAUdpPort()
	{
		var busy = new System.Net.Sockets.TcpListener(IPAddress.Any, 0);
		busy.Start();
		try
		{
			var port = ((IPEndPoint)busy.LocalEndpoint).Port;
			using var service = CreateService();

			await service.StartListeningAsync(new List<string> { $"tcp://0.0.0.0:{port}" });

			var source = service.Sources.Single();
			Assert.Equal((ReceiveSourceState.Failed, ReceiveSourceFailure.PortInUse), (source.State, source.Failure));
		}
		finally
		{
			busy.Stop();
		}
	}
}
