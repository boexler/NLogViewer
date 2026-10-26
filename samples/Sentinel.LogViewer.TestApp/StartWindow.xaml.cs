using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.TestApp.Embedding;
using Sentinel.LogViewer.TestApp.Generator;

namespace Sentinel.LogViewer.TestApp;

public partial class StartWindow : Window
{
    private const string Broadcast = "255.255.255.255";

    public StartWindow()
    {
        InitializeComponent();
        foreach (var host in new[] { "127.0.0.1", Broadcast }.Concat(LocalAddresses()))
            HostBox.Items.Add(host);
    }

    /// <summary>The choice of the generator, or null when the control test was chosen</summary>
    public GeneratorSettings? Generator { get; private set; }

    public EmbeddingMode EmbeddingMode { get; private set; }

    private static IEnumerable<string> LocalAddresses()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .ToList();
        }
        catch (SocketException)
        {
            return Enumerable.Empty<string>();
        }
    }

    private bool IsTcp => ProtocolBox.SelectedIndex == 1;

    private GeneratorLibrary Library => Log4NetRadio.IsChecked == true ? GeneratorLibrary.Log4Net
        : PlainRadio.IsChecked == true ? GeneratorLibrary.PlainXml
        : GeneratorLibrary.NLog;

    /// <summary>
    /// Clears the old message when the library or the protocol changes
    /// </summary>
    private void OnProtocolRulesChanged(object sender, RoutedEventArgs e)
    {
        if (GeneratorError == null)
            return;

        GeneratorError.Text = string.Empty;
    }

    private string? Problem(string host)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
            return "The port is a number from 1 to 65535.";

        if (string.IsNullOrWhiteSpace(host))
            return "Enter a host or choose one.";

        if (IsTcp && Library == GeneratorLibrary.Log4Net)
            return "log4net has no TCP appender. Choose UDP, or another library.";

        if (IsTcp && host == Broadcast)
            return "A broadcast works with UDP only.";

        return null;
    }

    private void OnOpenGenerator(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        if (Problem(host) is { } problem)
        {
            GeneratorError.Text = problem;
            return;
        }

        Generator = new GeneratorSettings(Library, IsTcp ? "tcp" : "udp", host, int.Parse(PortBox.Text));
        DialogResult = true;
    }

    private void OnOpenControlTest(object sender, RoutedEventArgs e)
    {
        EmbeddingMode = NLogHostRadio.IsChecked == true ? EmbeddingMode.HostWithNLog
            : HostRadio.IsChecked == true ? EmbeddingMode.HostWithDi
            : EmbeddingMode.NoDi;
        DialogResult = true;
    }
}
