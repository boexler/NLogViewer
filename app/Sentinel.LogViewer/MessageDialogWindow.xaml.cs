using System;
using System.Windows;
using System.Windows.Controls;

namespace Sentinel.LogViewer
{
    /// <summary>
    /// A message with buttons in the look of the application, instead of the message box of Windows
    /// </summary>
    public partial class MessageDialogWindow : Window
    {
        public MessageDialogWindow(string message, string? caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();
            Title = caption ?? ProductNames.Display;
            MessageText.Text = message;
            IconText.Text = GetIcon(image);
            if (image is MessageBoxImage.Error)
                IconText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeError");

            YesButton.Visibility = NoButton.Visibility = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel
                ? Visibility.Visible : Visibility.Collapsed;
            OkButton.Visibility = buttons is MessageBoxButton.OK or MessageBoxButton.OKCancel ? Visibility.Visible : Visibility.Collapsed;
            CancelButton.Visibility = buttons is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel
                ? Visibility.Visible : Visibility.Collapsed;

            // Escape and the close button give the answer that does nothing.
            Result = buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel
            };
            (buttons is MessageBoxButton.OK or MessageBoxButton.OKCancel ? OkButton : YesButton).IsDefault = true;
            (buttons switch
            {
                MessageBoxButton.OK => OkButton,
                MessageBoxButton.YesNo => NoButton,
                _ => CancelButton
            }).IsCancel = true;
        }

        /// <summary>
        /// Shows the message on top of the owner, or of the main window, and returns the button that was chosen
        /// </summary>
        public static MessageBoxResult Show(Window? owner, string message, string? caption = null,
            MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var dialog = new MessageDialogWindow(message, caption, buttons, image);
            owner ??= Application.Current?.MainWindow;
            if (owner is { IsVisible: true })
                dialog.Owner = owner;
            else
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dialog.ShowDialog();
            return dialog.Result;
        }

        /// <summary>
        /// The button that was chosen; closing the window without a choice gives the answer that does nothing
        /// </summary>
        public MessageBoxResult Result { get; private set; }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            Result = Enum.Parse<MessageBoxResult>((string)((Button)sender).Tag);
            Close();
        }

        /// <summary>
        /// Glyph of the Segoe MDL2 Assets font
        /// </summary>
        private static string GetIcon(MessageBoxImage image) => image switch
        {
            MessageBoxImage.Error => "",
            MessageBoxImage.Warning => "",
            MessageBoxImage.Question => "",
            _ => ""
        };
    }
}
