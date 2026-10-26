using System;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The dialog for the time range of a viewer: a date and a time for the start and for the end, either of them may be switched off. The first
    /// and the last entry are offered, and they are what an end that is switched off starts with.
    /// </summary>
    internal sealed class TimeRangeDialog : Window
    {
        private readonly MomentPicker _from = new("From", withCheckBox: true);
        private readonly MomentPicker _to = new("Until", withCheckBox: true);
        private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
        private readonly TimeZoneInfo? _zone;
        private readonly DateTime? _fromBefore;
        private readonly DateTime? _toBefore;

        /// <param name="zone">The zone of the display; the dates and times are in it</param>
        /// <param name="from">The start now, a moment in UTC</param>
        /// <param name="to">The end now, a moment in UTC</param>
        /// <param name="first">The time of the first entry of the viewer, a moment in UTC, or null when there is none</param>
        /// <param name="last">The time of the last entry of the viewer, a moment in UTC, or null when there is none</param>
        public TimeRangeDialog(TimeZoneInfo? zone, DateTime? from, DateTime? to, DateTime? first, DateTime? last)
        {
            _zone = zone;
            _fromBefore = from;
            _toBefore = to;
            Title = "Time range";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // A switched off end shows the first or the last entry, so that a pick starts from something useful; changing it switches it on.
            Prepare(_from, from ?? first, from != null);
            Prepare(_to, to ?? last, to != null);

            var hint = new TextBlock
            {
                Text = $"Dates and times of the list ({EntryTime.Label(zone)}). An end that is switched off has no limit; changing it switches it on.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 8)
            };
            _error.Margin = new Thickness(0, 0, 0, 8);

            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) => Accept();
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };

            Content = new StackPanel
            {
                Margin = new Thickness(14),
                Children =
                {
                    Row(_from, "First entry", first),
                    Row(_to, "Last entry", last),
                    hint,
                    _error,
                    buttons
                }
            };
        }

        /// <summary>The start that was chosen, a moment in UTC; null for none</summary>
        public DateTime? From { get; private set; }

        /// <summary>The end that was chosen, a moment in UTC; null for none</summary>
        public DateTime? To { get; private set; }

        private void Prepare(MomentPicker picker, DateTime? moment, bool active)
        {
            if (moment is { } value)
                picker.Clock = TimeRangeInput.ToClock(value, _zone);

            picker.IsActive = active;
        }

        private UIElement Row(MomentPicker picker, string buttonText, DateTime? offered)
        {
            var button = new Button
            {
                Content = buttonText,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = offered != null,
                ToolTip = "Take the time of this entry"
            };
            button.Click += (_, _) => picker.Set(TimeRangeInput.ToClock(offered!.Value, _zone));
            return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6), Children = { picker, button } };
        }

        private void Accept()
        {
            var from = _from.IsActive ? TimeRangeInput.StartOf(_from.Clock, _zone, _fromBefore) : (DateTime?)null;
            var to = _to.IsActive ? TimeRangeInput.EndOf(_to.Clock, _zone, _toBefore) : (DateTime?)null;
            if (from > to)
            {
                _error.Text = "The start is after the end.";
                return;
            }

            From = from;
            To = to;
            DialogResult = true;
        }
    }
}
