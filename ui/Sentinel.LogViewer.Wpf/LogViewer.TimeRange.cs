using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The time range: only the entries of a period are shown, set in a dialog or from a row, and cleared with one click
    /// </summary>
    public partial class LogViewer
    {
        private ICommand? _timeFromHereCommand;
        private ICommand? _timeUntilHereCommand;
        private ICommand? _clearTimeRangeCommand;
        private ICommand? _chooseTimeRangeCommand;
        private bool _applyingTimeRange;

        #region Time Range Properties

        /// <summary>
        /// The start of the time range: only the entries from this moment on (inclusive) are shown; null for no start. A moment in UTC, so
        /// it stays the same when the zone of the display changes.
        /// </summary>
        [Category("LogViewerFilters")]
        [Description("Only the entries from this moment on are shown; null for no start")]
        public DateTime? TimeFrom
        {
            get => (DateTime?)GetValue(TimeFromProperty);
            set => SetValue(TimeFromProperty, value);
        }

        /// <summary>
        /// The <see cref="TimeFrom"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TimeFromProperty =
            DependencyProperty.Register(nameof(TimeFrom), typeof(DateTime?), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeRangeChanged));

        /// <summary>
        /// The end of the time range: only the entries up to this moment (inclusive) are shown; null for no end. A moment in UTC.
        /// </summary>
        [Category("LogViewerFilters")]
        [Description("Only the entries up to this moment are shown; null for no end")]
        public DateTime? TimeTo
        {
            get => (DateTime?)GetValue(TimeToProperty);
            set => SetValue(TimeToProperty, value);
        }

        /// <summary>
        /// The <see cref="TimeTo"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TimeToProperty =
            DependencyProperty.Register(nameof(TimeTo), typeof(DateTime?), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTimeRangeChanged));

        private static readonly DependencyPropertyKey HasTimeRangePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasTimeRange), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// The <see cref="HasTimeRange"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty HasTimeRangeProperty = HasTimeRangePropertyKey.DependencyProperty;

        /// <summary>
        /// Whether a time range limits the entries
        /// </summary>
        public bool HasTimeRange => (bool)GetValue(HasTimeRangeProperty);

        private static readonly DependencyPropertyKey TimeRangeTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(TimeRangeText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="TimeRangeText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TimeRangeTextProperty = TimeRangeTextPropertyKey.DependencyProperty;

        /// <summary>
        /// The time range as one line in the zone of the display, empty without one
        /// </summary>
        public string TimeRangeText => (string)GetValue(TimeRangeTextProperty);

        #endregion

        #region Time Range Commands

        /// <summary>
        /// Starts the time range at an entry (the parameter). A later end is cleared when the start would be behind it.
        /// </summary>
        public ICommand TimeFromHereCommand => _timeFromHereCommand ??= new RelayCommand<LogEntry>(entry =>
        {
            if (entry == null)
                return;

            var from = EntryTime.ToUtc(entry.TimeStamp, FileTimeZone);
            ApplyTimeRange(from, TimeTo is { } to && to < from ? null : TimeTo);
        });

        /// <summary>
        /// Ends the time range at an entry (the parameter). An earlier start is cleared when the end would be before it.
        /// </summary>
        public ICommand TimeUntilHereCommand => _timeUntilHereCommand ??= new RelayCommand<LogEntry>(entry =>
        {
            if (entry == null)
                return;

            var to = EntryTime.ToUtc(entry.TimeStamp, FileTimeZone);
            ApplyTimeRange(TimeFrom is { } from && from > to ? null : TimeFrom, to);
        });

        /// <summary>
        /// Shows all the entries again
        /// </summary>
        public ICommand ClearTimeRangeCommand => _clearTimeRangeCommand ??= new RelayCommand(
            () => ApplyTimeRange(null, null), () => HasTimeRange);

        /// <summary>
        /// Opens the dialog for the time range (Ctrl+Shift+T)
        /// </summary>
        public ICommand ChooseTimeRangeCommand => _chooseTimeRangeCommand ??= new RelayCommand(ChooseTimeRange);

        #endregion

        private static void OnTimeRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { _applyingTimeRange: false } viewer)
                viewer.TimeRangeChanged();
        }

        /// <summary>
        /// Sets both ends at once, so that the list is filtered once
        /// </summary>
        private void ApplyTimeRange(DateTime? from, DateTime? to)
        {
            _applyingTimeRange = true;
            try
            {
                TimeFrom = from;
                TimeTo = to;
            }
            finally
            {
                _applyingTimeRange = false;
            }

            TimeRangeChanged();
        }

        private void TimeRangeChanged()
        {
            UpdateTimeRangeText();
            UpdateFilter();
        }

        private void UpdateTimeRangeText()
        {
            SetValue(HasTimeRangePropertyKey, TimeFrom != null || TimeTo != null);
            // It is written like the list is, in the format of the time stamp column.
            var resolver = TimeStampResolver as Resolver.TimeStampResolver;
            Func<DateTime, string>? format = resolver == null ? null : resolver.FormatClock;
            SetValue(TimeRangeTextPropertyKey, TimeRangeInput.Describe(TimeFrom, TimeTo, DisplayTimeZone, format));
        }

        /// <summary>
        /// The times of the earliest and of the latest of all the entries, moments in UTC; null when there are none
        /// </summary>
        internal (DateTime? First, DateTime? Last) EntryTimeSpan()
        {
            DateTime? first = null;
            DateTime? last = null;
            foreach (var entry in GetEffectiveEntries())
            {
                var moment = EntryTime.ToUtc(entry.TimeStamp, FileTimeZone);
                if (first == null || moment < first)
                    first = moment;

                if (last == null || moment > last)
                    last = moment;
            }

            return (first, last);
        }

        private void ChooseTimeRange()
        {
            var (first, last) = EntryTimeSpan();
            var dialog = new TimeRangeDialog(DisplayTimeZone, TimeFrom, TimeTo, first, last) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
                ApplyTimeRange(dialog.From, dialog.To);
        }
    }
}
