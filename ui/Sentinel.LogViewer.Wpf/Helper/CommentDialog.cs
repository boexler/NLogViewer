using System.Windows;
using System.Windows.Controls;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The dialog for the comment of a bookmark: a text of one or more lines; an empty text takes the comment off
    /// </summary>
    internal sealed class CommentDialog : Window
    {
        private readonly TextBox _text = new()
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 380,
            MinHeight = 80,
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        public CommentDialog(string comment)
        {
            Title = LogViewerTexts.Get("Bm_DialogTitle");
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _text.Text = comment;

            var hint = new TextBlock { Text = LogViewerTexts.Get("Bm_DialogText"), Opacity = 0.7, Margin = new Thickness(0, 0, 0, 6) };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) => DialogResult = true;
            var cancel = new Button { Content = LogViewerTexts.Get("Dlg_Cancel"), IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0),
                Children = { ok, cancel }
            };

            Content = new StackPanel { Margin = new Thickness(14), Children = { hint, _text, buttons } };
            Loaded += (_, _) =>
            {
                _text.Focus();
                _text.SelectAll();
            };
        }

        /// <summary>The text that was typed</summary>
        public string Comment => _text.Text;
    }
}
