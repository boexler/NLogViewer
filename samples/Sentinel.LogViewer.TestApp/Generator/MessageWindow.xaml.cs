using System.Windows;
using System.Windows.Controls;

namespace Sentinel.LogViewer.TestApp.Generator;

/// <summary>
/// Writes one message and sends it. The XML that is sent is shown, and can be edited as raw text.
/// </summary>
public partial class MessageWindow : Window
{
    private readonly GeneratorSettings _settings;
    private XmlTransport? _transport;

    public MessageWindow(GeneratorSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Title = $"Send a message · {settings.Address}";
        LevelBox.ItemsSource = Enum.GetValues<TestLogLevel>();
        LevelBox.SelectedItem = TestLogLevel.Error;
        Closed += (_, _) => _transport?.Dispose();
    }

    private void OnFieldChanged(object sender, RoutedEventArgs e)
    {
        // The fields change the XML only while it is not edited by hand.
        if (XmlBox != null && RawBox.IsChecked != true)
            ShowXml();
    }

    private void OnRawChanged(object sender, RoutedEventArgs e)
    {
        XmlBox.IsReadOnly = RawBox.IsChecked != true;
        if (RawBox.IsChecked != true)
            ShowXml();
    }

    /// <summary>
    /// Shows the XML of the fields. A character that XML cannot hold (e.g. ESC from a coloured console line) is explained, not thrown.
    /// </summary>
    private void ShowXml()
    {
        try
        {
            XmlBox.Text = BuildXml();
            StatusText.Text = string.Empty;
        }
        catch (ArgumentException ex)
        {
            XmlBox.Text = string.Empty;
            StatusText.Text = $"Not valid in XML: {ex.Message} To send it anyway, edit the XML as raw text.";
        }
    }

    private string BuildXml()
    {
        var properties = PropertiesBox.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim().Length > 0)
            .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));

        return Log4JXmlWriter.Event(
            (TestLogLevel)(LevelBox.SelectedItem ?? TestLogLevel.Info),
            LoggerBox.Text,
            MessageBox.Text,
            ExceptionBox.Text.Trim(),
            ApplicationBox.Text.Trim(),
            Environment.MachineName,
            properties);
    }

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(XmlBox.Text))
        {
            StatusText.Text = "There is nothing to send.";
            return;
        }

        try
        {
            _transport ??= new XmlTransport(_settings.Protocol, _settings.Host, _settings.Port);
            var transport = _transport;
            var xml = XmlBox.Text;
            await Task.Run(() => transport.Send(xml));
            StatusText.Text = $"Sent at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _transport?.Dispose();
            _transport = null;
            StatusText.Text = ex.Message;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
