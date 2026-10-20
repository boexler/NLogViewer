using System.Windows;

namespace Sentinel.LogViewer
{
    /// <summary>
    /// Asks for a line of text in the look of the application
    /// </summary>
    public partial class TextPromptWindow : Window
    {
        public TextPromptWindow(string message, string? caption, string initialText)
        {
            InitializeComponent();
            Title = caption ?? ProductNames.Display;
            MessageText.Text = message;
            InputBox.Text = initialText;
            InputBox.SelectAll();
        }

        /// <summary>
        /// The text that was entered, or null when the window was cancelled
        /// </summary>
        public string? Result { get; private set; }

        /// <summary>
        /// Shows the window on top of the owner, or of the main window, and returns the text, or null when it was cancelled
        /// </summary>
        public static string? Show(Window? owner, string message, string? caption = null, string initialText = "")
        {
            var dialog = new TextPromptWindow(message, caption, initialText);
            owner ??= Application.Current?.MainWindow;
            if (owner is { IsVisible: true })
                dialog.Owner = owner;
            else
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dialog.ShowDialog();
            return dialog.Result;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
            Close();
        }
    }
}
