using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Search: finds entries in the filtered list and jumps between them without hiding anything
    /// </summary>
    public partial class LogViewer
    {
        // Hits in list order, with their index in the filtered list.
        private readonly List<LogEntry> _findMatches = new();
        private readonly List<int> _findMatchIndices = new();
        private LogEntry? _currentFindMatch;
        private bool _findUpdatePending;

        #region Find Properties

        /// <summary>
        /// Text to search for in the logger name and message of the visible entries (case-insensitive)
        /// </summary>
        [Category("LogViewerFind")]
        [Description("Text to search for in the logger name and message of the visible entries")]
        public string FindText
        {
            get => (string)GetValue(FindTextProperty);
            set => SetValue(FindTextProperty, value);
        }

        /// <summary>
        /// The <see cref="FindText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindTextProperty =
            DependencyProperty.Register(nameof(FindText), typeof(string), typeof(LogViewer),
                new PropertyMetadata(string.Empty, OnFindTextChanged));

        private static readonly DependencyPropertyKey FindResultTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FindResultText), typeof(string), typeof(LogViewer),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="FindResultText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindResultTextProperty = FindResultTextPropertyKey.DependencyProperty;

        /// <summary>
        /// Position of the current hit and number of hits, e.g. "3 / 17"; empty when nothing is searched
        /// </summary>
        public string FindResultText => (string)GetValue(FindResultTextProperty);

        /// <summary>
        /// Background brush used to highlight the search hits
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush used to highlight the search hits")]
        public Brush FindHighlightBackground
        {
            get => (Brush)GetValue(FindHighlightBackgroundProperty);
            set => SetValue(FindHighlightBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="FindHighlightBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindHighlightBackgroundProperty =
            DependencyProperty.Register(nameof(FindHighlightBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFFD54F")));

        /// <summary>
        /// Controls the visibility of the search box
        /// </summary>
        [Category("LogViewerFind")]
        [Browsable(true)]
        [Description("Show/hide the search box")]
        public bool ShowFindControls
        {
            get => (bool)GetValue(ShowFindControlsProperty);
            set => SetValue(ShowFindControlsProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowFindControls"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowFindControlsProperty =
            DependencyProperty.Register(nameof(ShowFindControls), typeof(bool), typeof(LogViewer),
                new PropertyMetadata(true));

        /// <summary>
        /// Selects the next search hit (wraps around)
        /// </summary>
        public ICommand FindNextCommand
        {
            get => (ICommand)GetValue(FindNextCommandProperty);
            set => SetValue(FindNextCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="FindNextCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindNextCommandProperty =
            DependencyProperty.Register(nameof(FindNextCommand), typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Selects the previous search hit (wraps around)
        /// </summary>
        public ICommand FindPreviousCommand
        {
            get => (ICommand)GetValue(FindPreviousCommandProperty);
            set => SetValue(FindPreviousCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="FindPreviousCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindPreviousCommandProperty =
            DependencyProperty.Register(nameof(FindPreviousCommand), typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        #endregion

        /// <summary>
        /// Selects the next (or previous) hit, starting after the selected entry or at the top of the view
        /// </summary>
        public void FindNext(bool forward = true)
        {
            var listView = PART_ListView;
            if (_findMatches.Count == 0 || listView == null)
                return;

            var count = _findMatches.Count;
            var current = _currentFindMatch == null ? -1 : _findMatches.IndexOf(_currentFindMatch);
            int next;
            if (current >= 0)
            {
                next = (current + (forward ? 1 : -1) + count) % count;
            }
            else
            {
                // Without a current hit, continue from what the user is looking at.
                var start = listView.SelectedIndex >= 0 ? listView.SelectedIndex : TopRowIndex(listView);
                next = forward ? _findMatchIndices.FindIndex(i => i > start) : _findMatchIndices.FindLastIndex(i => i < start);
                if (next < 0)
                    next = forward ? 0 : count - 1;
            }

            _currentFindMatch = _findMatches[next];
            // New entries must not move the view away from the hit.
            AutoScroll = false;
            listView.SelectedItem = _currentFindMatch;
            listView.ScrollIntoView(_currentFindMatch);
            UpdateFindResultText();
        }

        private static void OnFindTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer)
                return;

            viewer.OnFindChanged();
            if (viewer.PART_ListView != null)
                MessageHighlighter.Refresh(viewer.PART_ListView);
        }

        /// <summary>
        /// The search text or one of its options changed: builds the term again and looks for the hits
        /// </summary>
        private void OnFindChanged()
        {
            _currentFindMatch = null;
            RebuildFindTerm();
            UpdateFindMatches();
            if (PART_ListView != null)
                MessageHighlighter.Refresh(PART_ListView);
        }

        /// <summary>
        /// Recounts the hits once the current batch of changes (new entries, filter changes) is done
        /// </summary>
        private void ScheduleFindUpdate()
        {
            if (_findUpdatePending)
                return;

            _findUpdatePending = true;
            Dispatcher.InvokeAsync(() =>
            {
                _findUpdatePending = false;
                UpdateFindMatches();
                UpdateFilterHits();
                UpdateVisibleEntryCount();
            }, DispatcherPriority.Background);
        }

        private void UpdateFindMatches()
        {
            _findMatches.Clear();
            _findMatchIndices.Clear();
            if (_findTerm != null && LogEvents?.View != null)
            {
                var fields = SearchFields;
                var index = 0;
                foreach (var item in LogEvents.View)
                {
                    if (item is LogEntry logEvent && _findTerm.Matches(logEvent, fields))
                    {
                        _findMatches.Add(logEvent);
                        _findMatchIndices.Add(index);
                    }

                    index++;
                }
            }

            if (_currentFindMatch != null && !_findMatches.Contains(_currentFindMatch))
                _currentFindMatch = null;

            UpdateFindResultText();
        }

        private void UpdateFindResultText()
        {
            string text;
            if (string.IsNullOrEmpty(FindText))
                text = string.Empty;
            else if (FindError != null)
                text = "Invalid pattern";
            else if (_findMatches.Count == 0)
                text = "No results";
            else
                text = $"{(_currentFindMatch == null ? "–" : (_findMatches.IndexOf(_currentFindMatch) + 1).ToString())} / {_findMatches.Count}";

            SetValue(FindResultTextPropertyKey, text);
        }

        private static int TopRowIndex(ListView listView)
        {
            // With item based scrolling the vertical offset is the index of the top row.
            return ScrollingHelper.GetDescendantByType(listView, typeof(ScrollViewer)) is ScrollViewer { CanContentScroll: true } scrollViewer
                ? (int)scrollViewer.VerticalOffset
                : 0;
        }
    }
}
