using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Picks a date and a time of day: a date picker with a calendar, and the hour, the minute and the second in lists. It works with the clock
    /// time that the viewer shows; the zone is the business of the dialog. Optionally it has a check box in front, for a limit that may be left out.
    /// </summary>
    internal sealed class MomentPicker : StackPanel
    {
        private readonly CheckBox? _active;
        private readonly DatePicker _date = new() { Width = 120, VerticalAlignment = VerticalAlignment.Center };
        private readonly ComboBox _hour = Part(24);
        private readonly ComboBox _minute = Part(60);
        private readonly ComboBox _second = Part(60);
        private bool _loading;

        /// <param name="label">The text of the check box, or of a label when there is no check box</param>
        /// <param name="withCheckBox">Whether the value can be switched off; it is switched on by every change of the date or the time</param>
        public MomentPicker(string label, bool withCheckBox)
        {
            Orientation = Orientation.Horizontal;
            if (withCheckBox)
            {
                _active = new CheckBox { Content = label, Width = 64, VerticalAlignment = VerticalAlignment.Center };
                Children.Add(_active);
            }
            else
            {
                Children.Add(new TextBlock { Text = label, Width = 64, VerticalAlignment = VerticalAlignment.Center });
            }

            Children.Add(_date);
            Children.Add(Spacer());
            Children.Add(_hour);
            Children.Add(Colon());
            Children.Add(_minute);
            Children.Add(Colon());
            Children.Add(_second);

            AutomationProperties.SetName(_date, $"{label} date");
            AutomationProperties.SetName(_hour, $"{label} hour");
            AutomationProperties.SetName(_minute, $"{label} minute");
            AutomationProperties.SetName(_second, $"{label} second");

            _date.SelectedDateChanged += (_, _) => Changed();
            foreach (var part in new[] { _hour, _minute, _second })
                part.SelectionChanged += (_, _) => Changed();

            Clock = DateTime.Today;
        }

        internal DatePicker DateBox => _date;

        internal ComboBox HourBox => _hour;

        /// <summary>Whether a date is in the date picker; without one the clock time would silently be today</summary>
        public bool HasDate => _date.SelectedDate != null;

        /// <summary>Whether the value is switched on; always true for a picker without a check box</summary>
        public bool IsActive
        {
            get => _active?.IsChecked != false;
            set
            {
                if (_active != null)
                    _active.IsChecked = value;
            }
        }

        /// <summary>The date and the time of day that are picked, to the second</summary>
        public DateTime Clock
        {
            get => (_date.SelectedDate ?? DateTime.Today).Date.AddHours(Math.Max(_hour.SelectedIndex, 0)).AddMinutes(Math.Max(_minute.SelectedIndex, 0)).AddSeconds(Math.Max(_second.SelectedIndex, 0));
            set
            {
                _loading = true;
                try
                {
                    _date.SelectedDate = value.Date;
                    _date.DisplayDate = value.Date;
                    _hour.SelectedIndex = value.Hour;
                    _minute.SelectedIndex = value.Minute;
                    _second.SelectedIndex = value.Second;
                }
                finally
                {
                    _loading = false;
                }
            }
        }

        /// <summary>Sets the value and switches it on, e.g. from a button that offers the first or the last entry</summary>
        public void Set(DateTime clock)
        {
            Clock = clock;
            IsActive = true;
        }

        private void Changed()
        {
            if (!_loading)
                IsActive = true;
        }

        private static ComboBox Part(int count) => new()
        {
            ItemsSource = Enumerable.Range(0, count).Select(i => i.ToString("00")).ToList(),
            Width = 52,
            VerticalAlignment = VerticalAlignment.Center,
            MaxDropDownHeight = 240
        };

        private static FrameworkElement Spacer() => new Border { Width = 10 };

        private static FrameworkElement Colon() => new TextBlock { Text = ":", Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
    }
}
