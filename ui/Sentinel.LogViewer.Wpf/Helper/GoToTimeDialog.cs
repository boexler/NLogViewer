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
        private readonly MomentPicker _time = new("Time", withCheckBox: false);
        private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 400 };
        private readonly TimeZoneInfo? _zone;

        /// <param name="zone">The zone of the display; the date and the time are in it</param>
        /// <param name="initial">The moment (UTC) that the pickers start with, or null for today</param>
        public GoToTimeDialog(TimeZoneInfo? zone, DateTime? initial)
        {
            _zone = zone;
            Title = "Go to time";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (initial is { } moment)
                _time.Clock = TimeRangeInput.ToClock(moment, zone);

            var hint = new TextBlock
            {
                Text = $"Date and time of the list ({EntryTime.Label(zone)}). The first entry from then on is selected.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
                Opacity = 0.7,
                Margin = new Thickness(0, 6, 0, 8)
            };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) =>
            {
                if (TryAccept())
                    DialogResult = true;
            };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };

            Content = new StackPanel { Margin = new Thickness(14), Children = { _time, hint, _error, buttons } };
        }

        internal MomentPicker Picker => _time;

        internal string Error => _error.Text;

        /// <summary>
        /// Takes the time from the picker into <see cref="Moment"/>; without a date nothing is taken and <see cref="Error"/> says so
        /// </summary>
        /// <returns>Whether the input is right</returns>
        internal bool TryAccept()
        {
            if (!_time.HasDate)
            {
                _error.Text = "Pick a date.";
                return false;
            }

            _error.Text = string.Empty;
            Moment = TimeRangeInput.StartOf(_time.Clock, _zone, null);
            return true;
        }

        /// <summary>The time that was given, a moment in UTC</summary>
        public DateTime? Moment { get; private set; }
    }
}
