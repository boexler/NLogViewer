using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Logger tree: the loggers of the entries as a tree on the right of the list, to show or hide them
    /// </summary>
    public partial class LogViewer
    {
        private bool _syncingLoggerFilterRules;
        private GridSplitter? _loggerTreeSplitter;

        /// <summary>
        /// The loggers of all entries, with their counters, and the rules that show or hide them
        /// </summary>
        public LoggerTree LoggerTree { get; }

        private ICommand? _showOnlyLoggerCommand;
        private ICommand? _hideLoggerCommand;

        /// <summary>
        /// Hides everything except one logger and everything below it; the parameter is the logger name.
        /// The same rules as in the logger tree, used by the context menu of the logger column.
        /// </summary>
        public ICommand ShowOnlyLoggerCommand => _showOnlyLoggerCommand ??=
            new RelayCommand<string>(name => LoggerTree.Rules.ShowOnly(name), name => !string.IsNullOrEmpty(name));

        /// <summary>
        /// Hides a logger and everything below it; the parameter is the logger name
        /// </summary>
        public ICommand HideLoggerCommand => _hideLoggerCommand ??=
            new RelayCommand<string>(name => LoggerTree.Rules.SetVisible(name, false), name => !string.IsNullOrEmpty(name));

        #region Logger Tree Properties

        /// <summary>
        /// Whether the logger tree can be opened; its button is on the right edge of the list
        /// </summary>
        [Category("LogViewerLoggerTree")]
        [Description("Whether the logger tree can be opened")]
        public bool ShowLoggerTree
        {
            get => (bool)GetValue(ShowLoggerTreeProperty);
            set => SetValue(ShowLoggerTreeProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowLoggerTree"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowLoggerTreeProperty =
            DependencyProperty.Register(nameof(ShowLoggerTree), typeof(bool), typeof(LogViewer), new PropertyMetadata(true, OnLoggerTreeLayoutChanged));

        /// <summary>
        /// Whether the logger tree is open
        /// </summary>
        [Category("LogViewerLoggerTree")]
        [Description("Whether the logger tree is open")]
        public bool IsLoggerTreeExpanded
        {
            get => (bool)GetValue(IsLoggerTreeExpandedProperty);
            set => SetValue(IsLoggerTreeExpandedProperty, value);
        }

        /// <summary>
        /// The <see cref="IsLoggerTreeExpanded"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IsLoggerTreeExpandedProperty =
            DependencyProperty.Register(nameof(IsLoggerTreeExpanded), typeof(bool), typeof(LogViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLoggerTreeLayoutChanged));

        /// <summary>
        /// Width of the open logger tree
        /// </summary>
        [Category("LogViewerLoggerTree")]
        [Description("Width of the open logger tree")]
        public double LoggerTreeWidth
        {
            get => (double)GetValue(LoggerTreeWidthProperty);
            set => SetValue(LoggerTreeWidthProperty, value);
        }

        /// <summary>
        /// The <see cref="LoggerTreeWidth"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LoggerTreeWidthProperty =
            DependencyProperty.Register(nameof(LoggerTreeWidth), typeof(double), typeof(LogViewer),
                new FrameworkPropertyMetadata(260d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLoggerTreeLayoutChanged));

        /// <summary>
        /// Whether the logger tree is on the left of the list instead of the right
        /// </summary>
        [Category("LogViewerLoggerTree")]
        [Description("Whether the logger tree is on the left of the list instead of the right")]
        public bool IsLoggerTreeOnLeft
        {
            get => (bool)GetValue(IsLoggerTreeOnLeftProperty);
            set => SetValue(IsLoggerTreeOnLeftProperty, value);
        }

        /// <summary>
        /// The <see cref="IsLoggerTreeOnLeft"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IsLoggerTreeOnLeftProperty =
            DependencyProperty.Register(nameof(IsLoggerTreeOnLeft), typeof(bool), typeof(LogViewer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLoggerTreeLayoutChanged));

        private static void OnLoggerTreeLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as LogViewer)?.ApplyLoggerTreeLayout();
        }

        /// <summary>
        /// The loggers that are hidden or shown, as text: <c>-Company.Product</c> hides that logger and everything below it,
        /// <c>+Company.Product.Service</c> shows one of them again. Part of the state of the filter, so it can be saved and restored,
        /// also for loggers that did not log anything yet.
        /// </summary>
        [Category("LogViewerLoggerTree")]
        [Description("The hidden and shown loggers as rules like -Company.Product and +Company.Product.Service")]
        public IReadOnlyList<string> LoggerFilterRules
        {
            get => (IReadOnlyList<string>)GetValue(LoggerFilterRulesProperty);
            set => SetValue(LoggerFilterRulesProperty, value);
        }

        /// <summary>
        /// The <see cref="LoggerFilterRules"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LoggerFilterRulesProperty =
            DependencyProperty.Register(nameof(LoggerFilterRules), typeof(IReadOnlyList<string>), typeof(LogViewer),
                new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLoggerFilterRulesChanged));

        private static void OnLoggerFilterRulesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { _syncingLoggerFilterRules: false } viewer)
                viewer.LoggerTree.Rules.Load(e.NewValue as IEnumerable<string>);
        }

        #endregion

        /// <summary>
        /// The smallest width of the open logger tree
        /// </summary>
        public const double MinLoggerTreeWidth = 140;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_loggerTreeSplitter != null)
                _loggerTreeSplitter.DragCompleted -= OnLoggerTreeSplitterDragCompleted;

            _loggerTreeSplitter = GetTemplateChild("PART_LoggerTreeSplitter") as GridSplitter;
            if (_loggerTreeSplitter != null)
                _loggerTreeSplitter.DragCompleted += OnLoggerTreeSplitterDragCompleted;

            ApplyLoggerTreeLayout();
        }

        /// <summary>
        /// Gives the open tree its width on the side it is on, and none to the other side
        /// </summary>
        private void ApplyLoggerTreeLayout()
        {
            if (GetTemplateChild("PART_LoggerTreeLeftColumn") is not ColumnDefinition left ||
                GetTemplateChild("PART_LoggerTreeRightColumn") is not ColumnDefinition right)
                return;

            var open = ShowLoggerTree && IsLoggerTreeExpanded;
            SetLoggerTreeColumn(left, open && IsLoggerTreeOnLeft);
            SetLoggerTreeColumn(right, open && !IsLoggerTreeOnLeft);
        }

        private void SetLoggerTreeColumn(ColumnDefinition column, bool open)
        {
            column.MinWidth = open ? MinLoggerTreeWidth : 0;
            column.Width = new GridLength(open ? Math.Max(LoggerTreeWidth, MinLoggerTreeWidth) : 0);
        }

        /// <summary>
        /// Takes over the width that the splitter set
        /// </summary>
        private void OnLoggerTreeSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            var column = GetTemplateChild(IsLoggerTreeOnLeft ? "PART_LoggerTreeLeftColumn" : "PART_LoggerTreeRightColumn") as ColumnDefinition;
            if (column is { ActualWidth: > 0 })
                LoggerTreeWidth = column.ActualWidth;
        }

        private void OnLoggerTreeRulesChanged(object? sender, EventArgs e)
        {
            _syncingLoggerFilterRules = true;
            try
            {
                SetCurrentValue(LoggerFilterRulesProperty, LoggerTree.Rules.ToList());
            }
            finally
            {
                _syncingLoggerFilterRules = false;
            }

            UpdateFilter();
        }

        /// <summary>
        /// Keeps the counters of the loggers in step with the entries
        /// </summary>
        private void UpdateLoggerTree(NotifyCollectionChangedEventArgs e, ObservableCollection<LogEntry> entries)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var entry in OfEntries(e.NewItems))
                        LoggerTree.Add(entry);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var entry in OfEntries(e.OldItems))
                        LoggerTree.Remove(entry);
                    break;
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Replace:
                    LoggerTree.Rebuild(entries);
                    break;
            }
        }

        private static IEnumerable<LogEntry> OfEntries(IList? items)
        {
            if (items == null)
                yield break;

            foreach (var item in items)
            {
                if (item is LogEntry entry)
                    yield return entry;
            }
        }

        private void ScheduleLoggerTreeUpdate(Action update) => Dispatcher.InvokeAsync(update, DispatcherPriority.Background);
    }
}
