using System.Net;
using System.Net.Sockets;
using System.Text;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.TestApp.Generator;
using Xunit;

namespace Sentinel.LogViewer.Tests.Services;

/// <summary>
/// Tests for what the test application sends: the events that the viewer receives have to be read by the viewer's parser, whichever
/// library or writer sends them. They run on loopback, without a window. NLog has one global setup, and the window tests of the test
/// application use it too, so they run in the same collection, one after the other.
/// </summary>
[Collection(WpfCollection.Name)]
public class TestApplicationSinksTests
{
	private sealed class SlowSink : ILogSink
	{
		private int _running;

		public int MostAtOnce { get; private set; }

		public void Start(TestLoggingOptions options)
		{
		}

		public void Write(GeneratedLog log)
		{
			var now = Interlocked.Increment(ref _running);
			MostAtOnce = Math.Max(MostAtOnce, now);
			Thread.Sleep(150);
			Interlocked.Decrement(ref _running);
		}

		public void Dispose()
		{
		}
	}

	[Fact]
	public void TheGenerator_StartsTheNextLog_OnlyWhenTheLastOneIsSent()
	{
		var sink = new SlowSink();
		using (var runner = new TestLogGeneratorRunner(new TestLoggingOptions { MessageIntervalMs = 5 }, sink))
		{
			runner.Start();
			Thread.Sleep(700);
		}

		Assert.Equal(1, sink.MostAtOnce);
	}

	private static readonly GeneratedLog Error = new(TestLogLevel.Error, "Acme.Billing", "Payment failed: Ünïcödé ✓ 😀 <tag> & \"quotes\"", new InvalidOperationException("boom"));

	private static Log4JEvent Parse(string xml) => new Log4JEventParser().Parse(xml);

	private static string ReceiveDatagram(UdpClient listener)
	{
		listener.Client.ReceiveTimeout = 5000;
		var from = new IPEndPoint(IPAddress.Any, 0);
		return Encoding.UTF8.GetString(listener.Receive(ref from));
	}

	private static (UdpClient Listener, TestLoggingOptions Options) UdpTarget(string application = "")
	{
		var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
		var options = new TestLoggingOptions { Protocol = "udp", UdpHost = "127.0.0.1", UdpPort = ((IPEndPoint)listener.Client.LocalEndPoint!).Port, ApplicationName = application };
		return (listener, options);
	}

	[Fact]
	public void TheXmlOfTheWriter_IsReadByTheParserOfTheViewer_WithSpecialCharacters()
	{
		var xml = Log4JXmlWriter.Event(Error.Level, Error.Logger, Error.Message, Error.Exception!.ToString(), "Shop", "NB01",
			new[] { new KeyValuePair<string, string>("Class", "PaymentClient") });

		var parsed = Parse(xml);

		Assert.Equal("Acme.Billing", parsed.Logger);
		Assert.Equal(Log4JLevel.Error, parsed.Level);
		Assert.Equal(Error.Message, parsed.Message);
		Assert.Contains("boom", parsed.Throwable);
		Assert.Equal("Shop", parsed.Properties["log4japp"]);
		Assert.Equal("PaymentClient", parsed.Properties["Class"]);
	}

	[Fact]
	public void ThePlainSink_SendsTheEvent_OverUdp_WithOneOfTheApplicationNames()
	{
		var (listener, options) = UdpTarget("Shop, Billing");
		using (listener)
		using (var sink = new PlainXmlSink())
		{
			sink.Start(options);
			sink.Write(Error);

			var parsed = Parse(ReceiveDatagram(listener));
			Assert.Equal(Error.Message, parsed.Message);
			Assert.Contains(parsed.Properties["log4japp"], new[] { "Shop", "Billing" });
		}
	}

	[Fact]
	public void ThePlainSink_SendsTheEvent_OverTcp_AndAnEventInPieces_IsPutTogetherByTheViewer()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			using var transport = new XmlTransport("tcp", "127.0.0.1", port);
			var xml = Log4JXmlWriter.Event(TestLogLevel.Info, "Pieces", "arrived in pieces", null, "Shop", "NB01");

			transport.SendInPieces(xml, 4, TimeSpan.FromMilliseconds(20));

			using var client = listener.AcceptTcpClient();
			client.ReceiveTimeout = 5000;
			var framer = new Log4JEventFramer();
			var events = new List<string>();
			var buffer = new byte[4096];
			var stream = client.GetStream();
			while (events.Count == 0)
				events.AddRange(framer.Add(Encoding.UTF8.GetString(buffer, 0, stream.Read(buffer, 0, buffer.Length))));

			Assert.Equal("arrived in pieces", Parse(Assert.Single(events)).Message);
		}
		finally
		{
			listener.Stop();
		}
	}

	[Fact]
	public void TheNLogSink_SendsAnEventThatTheViewerReads_WithTheApplicationName()
	{
		var (listener, options) = UdpTarget("Shop");
		using (listener)
		using (var sink = new NLogSink())
		{
			sink.Start(options);
			sink.Write(Error);

			var parsed = Parse(ReceiveDatagram(listener));
			Assert.Equal("Acme.Billing", parsed.Logger);
			Assert.Equal(Log4JLevel.Error, parsed.Level);
			Assert.Equal(Error.Message, parsed.Message);
			Assert.Equal("Shop", parsed.Properties["log4japp"]);
		}
	}

	[Fact]
	public void TheLog4NetSink_SendsAnEventThatTheViewerReads()
	{
		var (listener, options) = UdpTarget();
		using (listener)
		using (var sink = new Log4NetSink())
		{
			sink.Start(options);
			// log4net replaces characters outside the basic plane, e.g. an emoji, with a question mark; the others it keeps.
			sink.Write(Error with { Message = "Payment failed: Ünïcödé ✓ <tag> & \"quotes\"" });

			var parsed = Parse(ReceiveDatagram(listener));
			Assert.Equal("Acme.Billing", parsed.Logger);
			Assert.Equal(Log4JLevel.Error, parsed.Level);
			Assert.Equal("Payment failed: Ünïcödé ✓ <tag> & \"quotes\"", parsed.Message);
			Assert.Contains("boom", parsed.Throwable);
		}
	}
}
