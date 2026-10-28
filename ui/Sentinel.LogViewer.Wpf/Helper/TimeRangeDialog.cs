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
        private readonly MomentPicker _from = new(LogViewerTexts.Get("Dlg_From"), withCheckBox: true);
        private readonly MomentPicker _to = new(LogViewerTexts.Get("Dlg_Until"), withCheckBox: true);
        private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
        private readonly TimeZoneInfo? _zone;
        private readonly DateTime? _fromBefore;
        private readonly DateTime? _toBefore;
        private readonly DateTime? _first;
        private readonly DateTime? _last;

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
            _first = first;
            _last = last;
            Title = LogViewerTexts.Get("Dlg_TrTitle");
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // A switched off end shows the first or the last entry, so that a pick starts from something useful; changing it switches it on.
            Prepare(_from, from ?? first, from != null);
            Prepare(_to, to ?? last, to != null);

            var hint = new TextBlock
            {
                Text = LogViewerTexts.Format("Dlg_TrText", EntryTime.Label(zone)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 8)
            };
            _error.Margin = new Thickness(0, 0, 0, 8);

            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) =>
            {
                if (TryAccept())
                    DialogResult = true;
            };
            var cancel = new Button { Content = LogViewerTexts.Get("Dlg_Cancel"), IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };

            Content = new StackPanel
            {
                Margin = new Thickness(14),
                Children =
                {
                    Row(_from, LogViewerTexts.Get("Dlg_FirstEntry"), first, UseFirst),
                    Row(_to, LogViewerTexts.Get("Dlg_LastEntry"), last, UseLast),
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

        internal MomentPicker FromPicker => _from;

        internal MomentPicker ToPicker => _to;

        /// <summary>What the dialog says about a wrong input; empty when there is none</summary>
        internal string Error => _error.Text;

        /// <summary>Takes the time of the first entry as the start, and switches the start on</summary>
        internal void UseFirst() => _from.Set(TimeRangeInput.ToClock(_first!.Value, _zone));

        /// <summary>Takes the time of the last entry as the end, and switches the end on</summary>
        internal void UseLast() => _to.Set(TimeRangeInput.ToClock(_last!.Value, _zone));

        private UIElement Row(MomentPicker picker, string buttonText, DateTime? offered, Action use)
        {
            var button = new Button
            {
                Content = buttonText,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = offered != null,
                ToolTip = LogViewerTexts.Get("Dlg_TakeTime")
            };
            button.Click += (_, _) => use();
            return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6), Children = { picker, button } };
        }

        /// <summary>
        /// Takes the start and the end from the pickers: <see cref="From"/> and <see cref="To"/> are set, an end that is switched off is null.
        /// Without a date for an end that is on, or with the start after the end, nothing is taken and <see cref="Error"/> says why.
        /// </summary>
        /// <returns>Whether the input is right</returns>
        internal bool TryAccept()
        {
            _error.Text = string.Empty;
            if (_from.IsActive && !_from.HasDate || _to.IsActive && !_to.HasDate)
            {
                _error.Text = LogViewerTexts.Get("Dlg_PickDate");
                return false;
            }

            var from = _from.IsActive ? TimeRangeInput.StartOf(_from.Clock, _zone, _fromBefore) : (DateTime?)null;
            var to = _to.IsActive ? TimeRangeInput.EndOf(_to.Clock, _zone, _toBefore) : (DateTime?)null;
            if (from > to)
            {
                _error.Text = LogViewerTexts.Get("Dlg_StartAfterEnd");
                return false;
            }

            From = from;
            To = to;
            return true;
        }
    }
}
