using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Jumping through the list: to the next or previous entry of a level (F4 to F9) and to a time (Ctrl+T)
    /// </summary>
    public partial class LogViewer
    {
        private static readonly LogEntryLevel[] JumpLevels =
        {
            LogEntryLevel.Trace, LogEntryLevel.Debug, LogEntryLevel.Info, LogEntryLevel.Warn, LogEntryLevel.Error, LogEntryLevel.Fatal
        };

        private ICommand? _nextLevelCommand;
        private ICommand? _previousLevelCommand;
        private ICommand? _goToTimeCommand;
        private DispatcherTimer? _jumpTextTimer;

        private static readonly DependencyPropertyKey JumpTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(JumpText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="JumpText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty JumpTextProperty = JumpTextPropertyKey.DependencyProperty;

        /// <summary>
        /// What a jump has to say, e.g. that there is no further entry of the level; it goes away after a few seconds
        /// </summary>
        [Category("LogViewerControls")]
        public string JumpText => (string)GetValue(JumpTextProperty);

        /// <summary>
        /// Selects the next entry of the level (the parameter), starting after the selected entry (F4 Trace, F5 Debug, F6 Info, F7 Warn, F8 Error, F9 Fatal)
        /// </summary>
        public ICommand NextLevelCommand => _nextLevelCommand ??= new RelayCommand<LogEntryLevel>(level => JumpToLevel(level, forward: true));

        /// <summary>
        /// Selects the previous entry of the level (the parameter) (Shift+F4 to Shift+F9)
        /// </summary>
        public ICommand PreviousLevelCommand => _previousLevelCommand ??= new RelayCommand<LogEntryLevel>(level => JumpToLevel(level, forward: false));

        /// <summary>
        /// Opens the dialog for a time and selects the first entry from then on (Ctrl+T)
        /// </summary>
        public ICommand GoToTimeCommand => _goToTimeCommand ??= new RelayCommand(GoToTimeDialog);

        /// <summary>
        /// Selects the next or the previous entry of a level among the entries that are shown, starting at the selected entry, or at the top or
        /// the bottom of the list when none is selected. Without one in that direction, the selection stays and <see cref="JumpText"/> says so.
        /// </summary>
        /// <returns>Whether an entry was selected</returns>
        public bool JumpToLevel(LogEntryLevel level, bool forward)
        {
            if (PART_ListView is not { } listView)
                return false;

            var items = listView.Items;
            var step = forward ? 1 : -1;
            var start = listView.SelectedIndex >= 0 ? listView.SelectedIndex : forward ? -1 : items.Count;
            for (var i = start + step; i >= 0 && i < items.Count; i += step)
            {
                if (items[i] is LogEntry { } entry && entry.Level == level)
                {
                    SelectForJump(entry);
                    return true;
                }
            }

            ShowJumpText(forward ? $"No further {level} entry" : $"No earlier {level} entry");
            return false;
        }

        /// <summary>
        /// Selects the first of the entries that are shown from a moment on (inclusive). When there is none, the last one is selected and
        /// <see cref="JumpText"/> says so.
        /// </summary>
        /// <param name="moment">A moment in UTC</param>
        /// <returns>Whether an entry from then on was found</returns>
        public bool GoToTime(DateTime moment)
        {
            if (PART_ListView is not { } listView || listView.Items.Count == 0)
                return false;

            var items = listView.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is LogEntry { } entry && EntryTime.ToUtc(entry.TimeStamp, FileTimeZone) >= moment)
                {
                    SelectForJump(entry);
                    return true;
                }
            }

            if (items[items.Count - 1] is LogEntry { } last)
                SelectForJump(last);

            ShowJumpText("No entry from that time on, the last one is selected");
            return false;
        }

        private void GoToTimeDialog()
        {
            // It starts at the selected entry, or at the first one.
            var shown = PART_ListView?.SelectedItem as LogEntry;
            DateTime? initial = shown != null ? EntryTime.ToUtc(shown.TimeStamp, FileTimeZone) : EntryTimeSpan().First;
            var dialog = new GoToTimeDialog(DisplayTimeZone, initial) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && dialog.Moment is { } moment)
                GoToTime(moment);
        }

        private void SelectForJump(LogEntry entry)
        {
            // New entries must not move the view away from the one that was chosen.
            AutoScroll = false;
            if (PART_ListView is not { } listView)
                return;

            listView.SelectedItem = entry;
            listView.ScrollIntoView(entry);
            SetValue(JumpTextPropertyKey, string.Empty);
        }

        private void ShowJumpText(string text)
        {
            SetValue(JumpTextPropertyKey, text);
            _jumpTextTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _jumpTextTimer.Tick -= OnJumpTextTimer;
            _jumpTextTimer.Tick += OnJumpTextTimer;
            _jumpTextTimer.Stop();
            _jumpTextTimer.Start();
        }

        private void OnJumpTextTimer(object? sender, EventArgs e)
        {
            _jumpTextTimer?.Stop();
            SetValue(JumpTextPropertyKey, string.Empty);
        }

        /// <summary>
        /// F4 to F9 jump to the next entry of Trace to Fatal, with Shift to the previous one, and Ctrl+T to a time
        /// </summary>
        private void AddJumpKeyBindings()
        {
            for (var i = 0; i < JumpLevels.Length; i++)
            {
                var key = Key.F4 + i;
                InputBindings.Add(new KeyBinding(NextLevelCommand, key, ModifierKeys.None) { CommandParameter = JumpLevels[i] });
                InputBindings.Add(new KeyBinding(PreviousLevelCommand, key, ModifierKeys.Shift) { CommandParameter = JumpLevels[i] });
            }

        }
    }
}
