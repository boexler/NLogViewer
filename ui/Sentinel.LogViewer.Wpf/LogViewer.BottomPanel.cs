using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The panel below the list with three tabs: the hits of the search, the messages that repeat, and the details of the selected entry. It can be
    /// closed and resized; the tabs themselves are in <see cref="BottomPanel"/>.
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>
        /// The smallest height of the open panel
        /// </summary>
        public const double MinBottomPanelHeight = 90;

        private HitList? _hitList;
        private GridSplitter? _bottomPanelSplitter;
        private ICommand? _toggleBottomPanelCommand;
        private ICommand? _showBottomPanelCommand;
        private ICommand? _goToHitCommand;
        private ICommand? _goToFirstRepeatCommand;
        private ICommand? _goToLastRepeatCommand;
        private ICommand? _copyDetailsCommand;

        #region Properties

        /// <summary>
        /// Whether the panel below the list can be opened; the button of the tool row goes with it
        /// </summary>
        [Category("LogViewerBottomPanel")]
        [Description("Whether the panel below the list can be opened")]
        public bool ShowBottomPanel
        {
            get => (bool)GetValue(ShowBottomPanelProperty);
            set => SetValue(ShowBottomPanelProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowBottomPanel"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowBottomPanelProperty =
            DependencyProperty.Register(nameof(ShowBottomPanel), typeof(bool), typeof(LogViewer), new PropertyMetadata(true, OnBottomPanelChanged));

        /// <summary>
        /// Whether the panel below the list is open
        /// </summary>
        [Category("LogViewerBottomPanel")]
        [Description("Whether the panel below the list is open")]
        public bool IsBottomPanelOpen
        {
            get => (bool)GetValue(IsBottomPanelOpenProperty);
            set => SetValue(IsBottomPanelOpenProperty, value);
        }

        /// <summary>
        /// The <see cref="IsBottomPanelOpen"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IsBottomPanelOpenProperty =
            DependencyProperty.Register(nameof(IsBottomPanelOpen), typeof(bool), typeof(LogViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBottomPanelChanged));

        /// <summary>
        /// The tab that the panel shows
        /// </summary>
        [Category("LogViewerBottomPanel")]
        [Description("The tab that the panel below the list shows")]
        public BottomPanelTab BottomPanelTab
        {
            get => (BottomPanelTab)GetValue(BottomPanelTabProperty);
            set => SetValue(BottomPanelTabProperty, value);
        }

        /// <summary>
        /// The <see cref="BottomPanelTab"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BottomPanelTabProperty =
            DependencyProperty.Register(nameof(BottomPanelTab), typeof(BottomPanelTab), typeof(LogViewer),
                new FrameworkPropertyMetadata(BottomPanelTab.Results, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBottomPanelChanged));

        /// <summary>
        /// The height of the open panel; the splitter above it changes it
        /// </summary>
        [Category("LogViewerBottomPanel")]
        [Description("The height of the open panel below the list")]
        public double BottomPanelHeight
        {
            get => (double)GetValue(BottomPanelHeightProperty);
            set => SetValue(BottomPanelHeightProperty, value);
        }

        /// <summary>
        /// The <see cref="BottomPanelHeight"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty BottomPanelHeightProperty =
            DependencyProperty.Register(nameof(BottomPanelHeight), typeof(double), typeof(LogViewer), new PropertyMetadata(220.0, OnBottomPanelChanged));

        private static readonly DependencyPropertyKey RepeatGroupsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(RepeatGroups), typeof(IReadOnlyList<RepeatGroup>), typeof(LogViewer),
                new PropertyMetadata(Array.Empty<RepeatGroup>()));

        /// <summary>
        /// The <see cref="RepeatGroups"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RepeatGroupsProperty = RepeatGroupsPropertyKey.DependencyProperty;

        /// <summary>
        /// The messages that appear more than once among the entries that are shown, the most frequent first; made while the Repeats tab is shown
        /// </summary>
        [Category("LogViewerBottomPanel")]
        public IReadOnlyList<RepeatGroup> RepeatGroups => (IReadOnlyList<RepeatGroup>)GetValue(RepeatGroupsProperty);

        /// <summary>
        /// The repeated message that is selected in the Repeats tab; it stays selected when the messages are counted again
        /// </summary>
        [Category("LogViewerBottomPanel")]
        public RepeatGroup? SelectedRepeat
        {
            get => (RepeatGroup?)GetValue(SelectedRepeatProperty);
            set => SetValue(SelectedRepeatProperty, value);
        }

        /// <summary>
        /// The <see cref="SelectedRepeat"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SelectedRepeatProperty =
            DependencyProperty.Register(nameof(SelectedRepeat), typeof(RepeatGroup), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private static readonly DependencyPropertyKey RepeatsTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(RepeatsText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="RepeatsText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RepeatsTextProperty = RepeatsTextPropertyKey.DependencyProperty;

        /// <summary>
        /// How many messages repeat, or that the count is running
        /// </summary>
        [Category("LogViewerBottomPanel")]
        public string RepeatsText => (string)GetValue(RepeatsTextProperty);

        private static readonly DependencyPropertyKey DetailsPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Details), typeof(EntryDetails), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// The <see cref="Details"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty DetailsProperty = DetailsPropertyKey.DependencyProperty;

        /// <summary>
        /// The selected entry in full (the first, when several are selected); null when no entry is selected
        /// </summary>
        [Category("LogViewerBottomPanel")]
        public EntryDetails? Details => (EntryDetails?)GetValue(DetailsProperty);

        /// <summary>
        /// All hits of the search in the order of the list; it follows the search text and its options
        /// </summary>
        public IList FindHits => _hitList ??= new HitList(_findMatches);

        #endregion

        #region Commands

        /// <summary>
        /// Opens the panel, or closes it when it is open
        /// </summary>
        public ICommand ToggleBottomPanelCommand => _toggleBottomPanelCommand ??= new RelayCommand(() => IsBottomPanelOpen = !IsBottomPanelOpen);

        /// <summary>
        /// Opens the panel on the tab that is the parameter (a <see cref="BottomPanelTab"/>)
        /// </summary>
        public ICommand ShowBottomPanelCommand => _showBottomPanelCommand ??= new RelayCommand<BottomPanelTab>(OpenBottomPanel);

        /// <summary>
        /// Selects the entry that is the parameter (a hit, or the first or the last of a repeated message) in the list
        /// </summary>
        public ICommand GoToHitCommand => _goToHitCommand ??= new RelayCommand<LogEntry?>(entry =>
        {
            if (entry == null)
                return;

            SelectForJump(entry);
            if (_findMatches.Contains(entry))
            {
                _currentFindMatch = entry;
                UpdateFindResultText();
            }
        });

        /// <summary>
        /// Selects the first entry of the repeated message that is the parameter (a <see cref="RepeatGroup"/>)
        /// </summary>
        public ICommand GoToFirstRepeatCommand => _goToFirstRepeatCommand ??= new RelayCommand<RepeatGroup?>(group => GoToHitCommand.Execute(group?.First));

        /// <summary>
        /// Selects the last entry of the repeated message that is the parameter (a <see cref="RepeatGroup"/>)
        /// </summary>
        public ICommand GoToLastRepeatCommand => _goToLastRepeatCommand ??= new RelayCommand<RepeatGroup?>(group => GoToHitCommand.Execute(group?.Last));

        /// <summary>
        /// Puts a part of the details on the clipboard; the parameter is <c>Message</c>, <c>Exception</c> or <c>Entry</c> (all of it)
        /// </summary>
        public ICommand CopyDetailsCommand => _copyDetailsCommand ??= new RelayCommand<string?>(part =>
        {
            if (Details is not { } details)
                return;

            PutOnClipboard(part switch
            {
                "Message" => details.Message,
                "Exception" => details.Exception,
                _ => details.FullText
            });
        });

        #endregion

        /// <summary>
        /// Opens the panel on a tab
        /// </summary>
        public void OpenBottomPanel(BottomPanelTab tab)
        {
            BottomPanelTab = tab;
            IsBottomPanelOpen = true;
        }

        /// <summary>The time stamp of an entry as the list shows it</summary>
        internal string TimeText(LogEntry entry) => (TimeStampResolver ?? new Resolver.TimeStampResolver()).Resolve(entry);

        /// <summary>The name of a level in the language of the control</summary>
        internal string LevelText(LogEntryLevel level) => LogViewerTexts.Get("Level_" + level);

        private static void OnBottomPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer)
                return;

            viewer.ApplyBottomPanel();
            // The height changes the layout only; the repeats are counted again when the panel or the tab changes.
            if (e.Property != BottomPanelHeightProperty)
                viewer.UpdateRepeats();
        }

        private void AttachBottomPanel()
        {
            if (_bottomPanelSplitter != null)
                _bottomPanelSplitter.DragCompleted -= OnBottomPanelSplitterDragCompleted;

            _bottomPanelSplitter = GetTemplateChild("PART_BottomPanelSplitter") as GridSplitter;
            if (_bottomPanelSplitter != null)
                _bottomPanelSplitter.DragCompleted += OnBottomPanelSplitterDragCompleted;

            ApplyBottomPanel();
            UpdateRepeats();
            UpdateDetails();
        }

        /// <summary>
        /// Gives the open panel its height, and none when it is closed; the tab that is shown gets its content
        /// </summary>
        private void ApplyBottomPanel()
        {
            var open = ShowBottomPanel && IsBottomPanelOpen;
            if (GetTemplateChild("PART_BottomPanelRow") is RowDefinition row)
            {
                row.MinHeight = open ? MinBottomPanelHeight : 0;
                row.Height = new GridLength(open ? Math.Max(BottomPanelHeight, MinBottomPanelHeight) : 0);
            }

            var visibility = open ? Visibility.Visible : Visibility.Collapsed;
            if (GetTemplateChild("PART_BottomPanel") is UIElement panel)
                panel.Visibility = visibility;

            if (_bottomPanelSplitter != null)
                _bottomPanelSplitter.Visibility = visibility;
        }

        /// <summary>
        /// Takes over the height that the splitter set
        /// </summary>
        private void OnBottomPanelSplitterDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (GetTemplateChild("PART_BottomPanelRow") is RowDefinition { ActualHeight: > 0 } row)
                BottomPanelHeight = row.ActualHeight;
        }

        /// <summary>
        /// The hits of the search are a new list
        /// </summary>
        private void NotifyHitsChanged() => _hitList?.Changed();

        /// <summary>
        /// The Details tab follows the selection of the list
        /// </summary>
        private void UpdateDetails()
        {
            var entry = PART_ListView?.SelectedItem as LogEntry;
            if (ReferenceEquals(Details?.Entry, entry))
                return;

            SetValue(DetailsPropertyKey, entry == null ? null : new EntryDetails(entry, TimeText(entry), LevelText(entry.Level)));
        }

        /// <summary>
        /// Counts the repeated messages among the entries that are shown, while the Repeats tab is shown: at once for a small list, in the background
        /// for a big one (without the card over the list, the tab says that it counts). New entries and the filter ask for it again.
        /// </summary>
        private void UpdateRepeats()
        {
            if (!ShowBottomPanel || !IsBottomPanelOpen || BottomPanelTab != BottomPanelTab.Repeats)
            {
                CancelJob(JobKind.Repeats);
                return;
            }

            var count = LogEvents?.View is CollectionView view ? view.Count : 0;
            var before = _recounting;
            _recounting = true;
            try
            {
                if (UseBackground(JobKind.Repeats, count, StartRepeatsJob))
                {
                    SetValue(RepeatsTextPropertyKey, LogViewerTexts.Get("Repeats_Busy"));
                    return;
                }
            }
            finally
            {
                _recounting = before;
            }

            ShowRepeats(RepeatFinder.Group(LogEvents?.View?.OfType<LogEntry>() ?? Enumerable.Empty<LogEntry>()));
        }

        private void StartRepeatsJob()
        {
            if (LogEvents?.View == null)
                return;

            var entries = LogEvents.View.OfType<LogEntry>().ToArray();
            StartJob(JobKind.Repeats, "Busy_Repeats", entries.Length,
                job => RepeatFinder.Group(entries, job.Cancellation.Token, done => job.Done = done),
                ShowRepeats);
        }

        private void ShowRepeats(IReadOnlyList<RepeatGroup> groups)
        {
            // A message that appears once does not repeat.
            var repeated = groups.Where(group => group.Count > 1).ToList();
            var selected = SelectedRepeat;
            SetValue(RepeatGroupsPropertyKey, repeated);
            // The list is a new one and has lost its selection; the group that was selected is the one with the same level, logger and sample.
            if (selected != null)
                SelectedRepeat = repeated.FirstOrDefault(group => group.Level == selected.Level && group.Logger == selected.Logger && group.Sample == selected.Sample);

            SetValue(RepeatsTextPropertyKey, repeated.Count switch
            {
                0 => LogViewerTexts.Get("Repeats_None"),
                1 => LogViewerTexts.Format("Repeats_One", repeated[0].Count),
                _ => LogViewerTexts.Format("Repeats_Many", repeated.Count, repeated.Sum(group => group.Count))
            });
        }

        private static void PutOnClipboard(string text)
        {
            try
            {
                // The clipboard may be in use by another program for a moment; the data object keeps the text after the program ends.
                Clipboard.SetDataObject(text, copy: true);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // shortcut: a clipboard that stays locked loses the copy silently; upgrade: tell the user when it comes up
            }
        }
    }
}
