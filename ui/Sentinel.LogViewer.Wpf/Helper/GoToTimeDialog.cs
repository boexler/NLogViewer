using System;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The dialog for a time to go to: a date and a time as the viewer shows them
    /// </summary>
    internal sealed class GoToTimeDialog : Window
    {
        private readonly TextBox _time = new() { MinWidth = 220 };
        private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
        private readonly TimeZoneInfo? _zone;

        public GoToTimeDialog(TimeZoneInfo? zone, string initial)
        {
            _zone = zone;
            Title = "Go to time";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _time.Text = initial;
            _time.SelectAll();

            var hint = new TextBlock
            {
                Text = $"Time of the list ({EntryTime.Label(zone)}), e.g. 2025-12-18 14:30. The first entry from then on is selected.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320,
                Opacity = 0.7,
                Margin = new Thickness(0, 6, 0, 6)
            };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) => Accept();
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { ok, cancel } };

            Content = new StackPanel { Margin = new Thickness(14), Children = { _time, hint, _error, buttons } };
            Loaded += (_, _) => _time.Focus();
        }

        /// <summary>The time that was given, a moment in UTC</summary>
        public DateTime? Moment { get; private set; }

        private void Accept()
        {
            if (!TimeRangeInput.TryParse(_time.Text, _zone, false, out var moment) || moment == null)
            {
                _error.Text = "That is not a date and time.";
                _time.Focus();
                return;
            }

            Moment = moment;
            DialogResult = true;
        }
    }
}
