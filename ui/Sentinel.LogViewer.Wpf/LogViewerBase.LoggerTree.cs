using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using NLog;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// Logger tree: the loggers of the entries as a tree on the right of the list, to show or hide them
    /// </summary>
    public partial class LogViewerBase
    {
        private bool _syncingLoggerFilterRules;

        /// <summary>
        /// The loggers of all entries, with their counters, and the rules that show or hide them
        /// </summary>
        public LoggerTree LoggerTree { get; }

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
            DependencyProperty.Register(nameof(ShowLoggerTree), typeof(bool), typeof(LogViewerBase), new PropertyMetadata(true));

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
            DependencyProperty.Register(nameof(IsLoggerTreeExpanded), typeof(bool), typeof(LogViewerBase),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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
            DependencyProperty.Register(nameof(LoggerTreeWidth), typeof(double), typeof(LogViewerBase), new PropertyMetadata(260d));

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
            DependencyProperty.Register(nameof(LoggerFilterRules), typeof(IReadOnlyList<string>), typeof(LogViewerBase),
                new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLoggerFilterRulesChanged));

        private static void OnLoggerFilterRulesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewerBase { _syncingLoggerFilterRules: false } viewer)
                viewer.LoggerTree.Rules.Load(e.NewValue as IEnumerable<string>);
        }

        #endregion

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
        private void UpdateLoggerTree(NotifyCollectionChangedEventArgs e, ObservableCollection<LogEventInfo> entries)
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

        private static IEnumerable<LogEventInfo> OfEntries(IList? items)
        {
            if (items == null)
                yield break;

            foreach (var item in items)
            {
                if (item is LogEventInfo entry)
                    yield return entry;
            }
        }

        private void ScheduleLoggerTreeUpdate(Action update) => Dispatcher.InvokeAsync(update, DispatcherPriority.Background);
    }
}
