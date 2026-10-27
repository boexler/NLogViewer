using System.Windows;
using System.Windows.Controls;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// A text box with a symbol in front and a hint while it is empty, so that a filter box and a search box can be told apart
    /// </summary>
    public class WatermarkTextBox : TextBox
    {
        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register(nameof(Hint), typeof(string), typeof(WatermarkTextBox), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(WatermarkTextBox), new PropertyMetadata(string.Empty));

        /// <summary>The hint that is shown while the text box is empty</summary>
        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        /// <summary>A character of the icon font "Segoe MDL2 Assets" that is shown in front of the text</summary>
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }
    }
}