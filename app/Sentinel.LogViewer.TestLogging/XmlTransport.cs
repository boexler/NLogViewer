using System.Net.Sockets;
using System.Text;

namespace Sentinel.LogViewer.TestLogging;

/// <summary>
/// Sends text to the viewer over UDP or TCP as it is, so that events can also be sent in pieces or broken
/// </summary>
public sealed class XmlTransport : IDisposable
{
    private readonly bool _tcp;
    private readonly string _host;
    private readonly int _port;
    private UdpClient? _udp;
    private TcpClient? _client;

    public XmlTransport(string protocol, string host, int port)
    {
        _tcp = string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase);
        _host = host;
        _port = port;
    }

    public bool IsTcp => _tcp;

    /// <summary>
    /// Sends one text (a datagram with UDP, a line on the connection with TCP)
    /// </summary>
    public void Send(string text)
    {
        if (!_tcp)
        {
            _udp ??= new UdpClient { EnableBroadcast = true };
            var bytes = Encoding.UTF8.GetBytes(text);
            _udp.Send(bytes, bytes.Length, _host, _port);
            return;
        }

        Write(Encoding.UTF8.GetBytes(text + "\n"));
    }

    /// <summary>
    /// Sends a text over TCP in pieces, with a pause after each, as a slow network would deliver it
    /// </summary>
    public void SendInPieces(string text, int pieces, TimeSpan pause)
    {
        if (!_tcp)
            throw new InvalidOperationException("Pieces can only be sent over TCP.");

        var bytes = Encoding.UTF8.GetBytes(text + "\n");
        var size = Math.Max(1, bytes.Length / Math.Max(1, pieces));
        for (var start = 0; start < bytes.Length; start += size)
        {
            Write(bytes.AsSpan(start, Math.Min(size, bytes.Length - start)).ToArray());
            Thread.Sleep(pause);
        }
    }

    private void Write(byte[] bytes)
    {
        try
        {
            Connected().GetStream().Write(bytes);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // The connection was closed by the other side: connect once more.
            _client?.Dispose();
            _client = null;
            Connected().GetStream().Write(bytes);
        }
    }

    private TcpClient Connected()
    {
        if (_client is not { Connected: true })
        {
            _client?.Dispose();
            _client = new TcpClient();
            _client.Connect(_host, _port);
        }

        return _client;
    }

    public void Dispose()
    {
        _udp?.Dispose();
        _client?.Dispose();
    }
}
