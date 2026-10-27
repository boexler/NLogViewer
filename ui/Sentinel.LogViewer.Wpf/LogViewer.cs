using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sentinel.LogViewer.Wpf.Models;
using Sentinel.LogViewer.Wpf.Extensions;
using Sentinel.LogViewer.Wpf.Helper;
using Sentinel.LogViewer.Wpf.Resolver;
using Sentinel.LogViewer.Core;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The log viewer control: the entries with filter, search, logger tree and colours. Its entries come from an external
    /// <see cref="ItemsSource"/>, from a <see cref="Source"/>, or, without either, from <see cref="LogBuffer.Default"/>.
    /// </summary>
    public partial class LogViewer : Control
    {
        /// <summary>
        /// Parses a brush for dependency-property default metadata (hex string).
        /// </summary>
        private static Brush MetadataBrush(string hex) => (Brush)new BrushConverter().ConvertFrom(hex)!;

        // ##############################################################################################################################
        // Dependency Properties
        // ##############################################################################################################################

        #region Dependency Properties
            
        // ##########################################################################################
        // Colors
        // ##########################################################################################

        #region Colors

        /// <summary>
        /// The background for the trace output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Trace level log entries")] 
        public Brush TraceBackground
        {
            get => (Brush) GetValue(TraceBackgroundProperty);
            set => SetValue(TraceBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="TraceBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TraceBackgroundProperty =
            DependencyProperty.Register(nameof(TraceBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#D3D3D3")));

        /// <summary>
        /// The foreground for the trace output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Trace level log entries")] 
        public Brush TraceForeground
        {
            get => (Brush) GetValue(TraceForegroundProperty);
            set => SetValue(TraceForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="TraceForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TraceForegroundProperty =
            DependencyProperty.Register(nameof(TraceForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#042271")));

        /// <summary>
        /// The background for the debug output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Debug level log entries")] 
        public Brush DebugBackground
        {
            get => (Brush) GetValue(DebugBackgroundProperty);
            set => SetValue(DebugBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="DebugBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty DebugBackgroundProperty =
            DependencyProperty.Register(nameof(DebugBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#90EE90")));

        /// <summary>
        /// The foreground for the debug output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Debug level log entries")] 
        public Brush DebugForeground
        {
            get => (Brush) GetValue(DebugForegroundProperty);
            set => SetValue(DebugForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="DebugForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty DebugForegroundProperty =
            DependencyProperty.Register(nameof(DebugForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#042271")));

        /// <summary>
        /// The background for the info output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Info level log entries")] 
        public Brush InfoBackground
        {
            get => (Brush) GetValue(InfoBackgroundProperty);
            set => SetValue(InfoBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="InfoBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty InfoBackgroundProperty = DependencyProperty.Register(nameof(InfoBackground),
            typeof(Brush), typeof(LogViewer),
new PropertyMetadata(MetadataBrush("#0000FF")));

        /// <summary>
        /// The foreground for the info output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Info level log entries")] 
        public Brush InfoForeground
        {
            get => (Brush) GetValue(InfoForegroundProperty);
            set => SetValue(InfoForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="InfoForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty InfoForegroundProperty = DependencyProperty.Register(nameof(InfoForeground),
            typeof(Brush), typeof(LogViewer), new PropertyMetadata(Brushes.White));

        /// <summary>
        /// The background for the warn output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Warn level log entries")] 
        public Brush WarnBackground
        {
            get => (Brush) GetValue(WarnBackgroundProperty);
            set => SetValue(WarnBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="WarnBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty WarnBackgroundProperty = DependencyProperty.Register(nameof(WarnBackground),
            typeof(Brush), typeof(LogViewer),
new PropertyMetadata(MetadataBrush("#FFFF00")));

        /// <summary>
        /// The foreground for the warn output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Warn level log entries")] 
        public Brush WarnForeground
        {
            get => (Brush) GetValue(WarnForegroundProperty);
            set => SetValue(WarnForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="WarnForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty WarnForegroundProperty = DependencyProperty.Register(nameof(WarnForeground),
            typeof(Brush), typeof(LogViewer),
new PropertyMetadata(MetadataBrush("#324B5C")));

        /// <summary>
        /// The background for the error output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Error level log entries")] 
        public Brush ErrorBackground
        {
            get => (Brush) GetValue(ErrorBackgroundProperty);
            set => SetValue(ErrorBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="ErrorBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ErrorBackgroundProperty =
            DependencyProperty.Register(nameof(ErrorBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.Red));

        /// <summary>
        /// The foreground for the error output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Error level log entries")] 
        public Brush ErrorForeground
        {
            get => (Brush) GetValue(ErrorForegroundProperty);
            set => SetValue(ErrorForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="ErrorForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ErrorForegroundProperty =
            DependencyProperty.Register(nameof(ErrorForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.White));

        /// <summary>
        /// The background for the fatal output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for Fatal level log entries")] 
        public Brush FatalBackground
        {
            get => (Brush) GetValue(FatalBackgroundProperty);
            set => SetValue(FatalBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="FatalBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FatalBackgroundProperty =
            DependencyProperty.Register(nameof(FatalBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// The foreground for the fatal output
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for Fatal level log entries")] 
        public Brush FatalForeground
        {
            get => (Brush) GetValue(FatalForegroundProperty);
            set => SetValue(FatalForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="FatalForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FatalForegroundProperty =
            DependencyProperty.Register(nameof(FatalForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.Yellow));

        // ------------------------------------------------------------------------------------------
        // Search Term Chip Colors
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Background brush for plain text search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for plain text search term chips")] 
        public Brush TextSearchTermBackground
        {
            get => (Brush)GetValue(TextSearchTermBackgroundProperty);
            set => SetValue(TextSearchTermBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="TextSearchTermBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TextSearchTermBackgroundProperty =
            DependencyProperty.Register(nameof(TextSearchTermBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFFFBE6")));

        /// <summary>
        /// Border brush for plain text search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Border brush for plain text search term chips")] 
        public Brush TextSearchTermBorderBrush
        {
            get => (Brush)GetValue(TextSearchTermBorderBrushProperty);
            set => SetValue(TextSearchTermBorderBrushProperty, value);
        }

        /// <summary>
        /// The <see cref="TextSearchTermBorderBrush"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TextSearchTermBorderBrushProperty =
            DependencyProperty.Register(nameof(TextSearchTermBorderBrush), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFFC107")));

        /// <summary>
        /// Foreground brush for plain text search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for plain text search term chips")] 
        public Brush TextSearchTermForeground
        {
            get => (Brush)GetValue(TextSearchTermForegroundProperty);
            set => SetValue(TextSearchTermForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="TextSearchTermForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TextSearchTermForegroundProperty =
            DependencyProperty.Register(nameof(TextSearchTermForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// Background brush for regex search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush for regex search term chips")] 
        public Brush RegexSearchTermBackground
        {
            get => (Brush)GetValue(RegexSearchTermBackgroundProperty);
            set => SetValue(RegexSearchTermBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="RegexSearchTermBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RegexSearchTermBackgroundProperty =
            DependencyProperty.Register(nameof(RegexSearchTermBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFFFBE6")));

        /// <summary>
        /// Border brush for regex search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Border brush for regex search term chips")] 
        public Brush RegexSearchTermBorderBrush
        {
            get => (Brush)GetValue(RegexSearchTermBorderBrushProperty);
            set => SetValue(RegexSearchTermBorderBrushProperty, value);
        }

        /// <summary>
        /// The <see cref="RegexSearchTermBorderBrush"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RegexSearchTermBorderBrushProperty =
            DependencyProperty.Register(nameof(RegexSearchTermBorderBrush), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFFC107")));

        /// <summary>
        /// Foreground brush for regex search term chips
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for regex search term chips")] 
        public Brush RegexSearchTermForeground
        {
            get => (Brush)GetValue(RegexSearchTermForegroundProperty);
            set => SetValue(RegexSearchTermForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="RegexSearchTermForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RegexSearchTermForegroundProperty =
            DependencyProperty.Register(nameof(RegexSearchTermForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// Foreground brush for the "Regex:" prefix label
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Foreground brush for the 'Regex:' prefix label")] 
        public Brush RegexPrefixForeground
        {
            get => (Brush)GetValue(RegexPrefixForegroundProperty);
            set => SetValue(RegexPrefixForegroundProperty, value);
        }

        /// <summary>
        /// The <see cref="RegexPrefixForeground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RegexPrefixForegroundProperty =
            DependencyProperty.Register(nameof(RegexPrefixForeground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFF8F00")));

        /// <summary>
        /// Background brush used to highlight matched search text in the message
        /// </summary>
        [Category("LogViewerColors")]
        [Description("Background brush used to highlight matched search text in the message")] 
        public Brush SearchHighlightBackground
        {
            get => (Brush)GetValue(SearchHighlightBackgroundProperty);
            set => SetValue(SearchHighlightBackgroundProperty, value);
        }

        /// <summary>
        /// The <see cref="SearchHighlightBackground"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SearchHighlightBackgroundProperty =
            DependencyProperty.Register(nameof(SearchHighlightBackground), typeof(Brush), typeof(LogViewer),
                new PropertyMetadata(MetadataBrush("#FFFF80C0")));

        #endregion
        
        // ##########################################################################################
        // LogViewer
        // ##########################################################################################

        #region LogViewer

        /// <summary>
        /// When set, the viewer binds its list to this collection so callers can push <see cref="LogEntry"/> directly.
        /// When null, an internal collection is used.
        /// </summary>
        [Category("LogViewer")]
        [Browsable(true)]
        [Description("Observable collection of log entries displayed by the viewer.")]
        public ObservableCollection<LogEntry>? ItemsSource
        {
            get => (ObservableCollection<LogEntry>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// The <see cref="ItemsSource"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<LogEntry>),
            typeof(LogViewer),
            new PropertyMetadata(null, OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer)
                return;
            viewer.SyncItemsSourceToView();
        }

        /// <summary>
        /// Private DP to bind to the gui
        /// </summary>
        [Category("LogViewer")]
        public CollectionViewSource LogEvents
        {
            get => (CollectionViewSource) GetValue(LogEventsProperty);
            private set => SetValue(LogEventsProperty, value);
        }

        /// <summary>
        /// The <see cref="LogEvents"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LogEventsProperty = DependencyProperty.Register(nameof(LogEvents),
            typeof(CollectionViewSource), typeof(LogViewer), new PropertyMetadata(null));
        
        /// <summary>
        /// Automatically scroll to the newest entry
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Automatically scroll to the newest log entry when new entries are added")]
        public bool AutoScroll
        {
            get => (bool)GetValue(AutoScrollProperty);
            set => SetValue(AutoScrollProperty, value);
        }

        /// <summary>
        /// The <see cref="AutoScroll"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(LogViewer), new PropertyMetadata(true, AutoScrollChangedCallback));

        private static void AutoScrollChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is LogViewer instance)
            {
                instance.OnAutoScrollChanged();
            }
        }

        protected virtual void OnAutoScrollChanged()
        {
            if (AutoScroll)
                PART_ListView?.ScrollToEnd();
        }
		
        private void UpdateFilter()
        {
            if (LogEvents?.View is not CollectionView collectionView) return;

            var listView = PART_ListView;
            if (listView != null)
                _scrollAnchorKeeper.Capture(listView);

            var timeFrom = TimeFrom;
            var timeTo = TimeTo;
            var fileZone = FileTimeZone;

            collectionView.Filter = item =>
            {
                if (item is not LogEntry logEvent) return true;

                if (!LoggerTree.IsVisible(logEvent.LoggerName)) return false;

                // The time range: only the entries of the period, a time without a zone read in the zone of the file.
                if ((timeFrom != null || timeTo != null) && !EntryTime.IsInRange(logEvent.TimeStamp, fileZone, timeFrom, timeTo)) return false;

                // Apply level filters first
                var levelFilterPassed = logEvent.Level switch
                {
                    LogEntryLevel.Trace => !TraceFilter,  // If TraceFilter is true, hide Trace entries (!true = false)
                    LogEntryLevel.Debug => !DebugFilter,  // If DebugFilter is true, hide Debug entries (!true = false)
                    LogEntryLevel.Info => !InfoFilter,    // If InfoFilter is true, hide Info entries (!true = false)
                    LogEntryLevel.Warn => !WarnFilter,     // If WarnFilter is true, hide Warn entries (!true = false)
                    LogEntryLevel.Error => !ErrorFilter,   // If ErrorFilter is true, hide Error entries (!true = false)
                    LogEntryLevel.Fatal => !FatalFilter,   // If FatalFilter is true, hide Fatal entries (!true = false)
                    _ => true
                };

                if (!levelFilterPassed) return false;

                // The filter terms: every term that includes must match, and no term that excludes may match.
                var terms = ActiveSearchTerms;
                if (terms != null)
                {
                    var fields = SearchFields;
                    foreach (var term in terms)
                    {
                        if (term.Matches(logEvent, fields) == term.IsExclude)
                            return false;
                    }
                }

                return true;
            };
            
            // Refresh the view to apply the filter
            collectionView.Refresh();
            if (listView != null)
                _scrollAnchorKeeper.Restore(listView, GetEffectiveEntries(), collectionView.Filter, shownOrder: SortKey != null);
            ScheduleFindUpdate();
            PublishFilterState();
        }
        
        /// <summary>
        /// Delete all entries
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to clear all log entries from the viewer")]
        public ICommand ClearCommand
        {
            get => (ICommand) GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="ClearCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.Register(nameof(ClearCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a new search term
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to add a new search term")]
        public ICommand AddSearchTermCommand
        {
            get => (ICommand) GetValue(AddSearchTermCommandProperty);
            set => SetValue(AddSearchTermCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="AddSearchTermCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty AddSearchTermCommandProperty = DependencyProperty.Register(nameof(AddSearchTermCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to clear all search terms
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to clear all search terms")]
        public ICommand ClearAllSearchTermsCommand
        {
            get => (ICommand) GetValue(ClearAllSearchTermsCommandProperty);
            set => SetValue(ClearAllSearchTermsCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="ClearAllSearchTermsCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ClearAllSearchTermsCommandProperty = DependencyProperty.Register(nameof(ClearAllSearchTermsCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a regex search term from a provided text
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to add a regex search term from the provided text")]
        public ICommand AddRegexSearchTermCommand
        {
            get => (ICommand) GetValue(AddRegexSearchTermCommandProperty);
            set => SetValue(AddRegexSearchTermCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="AddRegexSearchTermCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty AddRegexSearchTermCommandProperty = DependencyProperty.Register(nameof(AddRegexSearchTermCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a regex search term as an exclude filter from a provided text
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to add a regex search term as an exclude filter from the provided text")]
        public ICommand AddRegexSearchTermExcludeCommand
        {
            get => (ICommand) GetValue(AddRegexSearchTermExcludeCommandProperty);
            set => SetValue(AddRegexSearchTermExcludeCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="AddRegexSearchTermExcludeCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty AddRegexSearchTermExcludeCommandProperty = DependencyProperty.Register(nameof(AddRegexSearchTermExcludeCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to remove a specific search term
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to remove a specific search term")]
        public ICommand RemoveSearchTermCommand
        {
            get => (ICommand) GetValue(RemoveSearchTermCommandProperty);
            set => SetValue(RemoveSearchTermCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="RemoveSearchTermCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty RemoveSearchTermCommandProperty = DependencyProperty.Register(nameof(RemoveSearchTermCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to edit a search term by copying it to the current search text
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to edit a search term by copying it to the current search text")]
        public ICommand EditSearchTermCommand
        {
            get => (ICommand) GetValue(EditSearchTermCommandProperty);
            set => SetValue(EditSearchTermCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="EditSearchTermCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty EditSearchTermCommandProperty = DependencyProperty.Register(nameof(EditSearchTermCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to copy text to clipboard
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to copy text to clipboard")]
        public ICommand CopyToClipboardCommand
        {
            get => (ICommand) GetValue(CopyToClipboardCommandProperty);
            set => SetValue(CopyToClipboardCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="CopyToClipboardCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty CopyToClipboardCommandProperty = DependencyProperty.Register(nameof(CopyToClipboardCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to export filtered log entries to a file
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to export filtered log entries to a file")]
        public ICommand ExportCommand
        {
            get => (ICommand) GetValue(ExportCommandProperty);
            set => SetValue(ExportCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="ExportCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ExportCommandProperty = DependencyProperty.Register(nameof(ExportCommand),
            typeof(ICommand), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// Command to scroll to the end of the log entries
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Command to scroll to the end of the log entries")]
        public ICommand ScrollToEndCommand
        {
            get => (ICommand) GetValue(ScrollToEndCommandProperty);
            set => SetValue(ScrollToEndCommandProperty, value);
        }

        /// <summary>
        /// The <see cref="ScrollToEndCommand"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ScrollToEndCommandProperty = DependencyProperty.Register(nameof(ScrollToEndCommand),
            typeof(ICommand), typeof(LogViewer), new FrameworkPropertyMetadata
            {
	            BindsTwoWayByDefault = true,
	            DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        
        /// <summary>
        /// Stop logging
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Pause or resume logging to improve performance when not actively monitoring")]
        public bool Pause
        {
            get => (bool)GetValue(PauseProperty);
            set => SetValue(PauseProperty, value);
        }

        /// <summary>
        /// The <see cref="Pause"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty PauseProperty = DependencyProperty.Register(nameof(Pause), typeof(bool), typeof(LogViewer), new PropertyMetadata(false, OnPauseChanged));

        private static void OnPauseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer viewer && !DesignerProperties.GetIsInDesignMode(viewer))
                viewer.OnPausePropertyChanged((bool)e.NewValue);
        }

        /// <summary>
        /// The maximum number of entries before automatic cleaning is performed. There is a hysteresis of 100 entries which must be exceeded.
        /// Example: <see cref="MaxCount"/> is '1000'. Then after '1100' entries, everything until '1000' is deleted.
        /// If set to '0' or less, it is deactivated
        /// </summary>
        [Category("LogViewer")]
        public int MaxCount
        {
            get => (int)GetValue(MaxCountProperty);
            set => SetValue(MaxCountProperty, value);
        }

        /// <summary>
        /// The <see cref="MaxCount"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty MaxCountProperty = DependencyProperty.Register(nameof(MaxCount), typeof(int), typeof(LogViewer),
            new PropertyMetadata(5000, OnMaxCountChanged));

        private static void OnMaxCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer || DesignerProperties.GetIsInDesignMode(viewer))
                return;
            viewer.ApplyMaxCountTrim(viewer.GetEffectiveEntries());
        }


        #endregion

        // ##########################################################################################
        // Resolver
        // ##########################################################################################

        #region Resolver
        
        /// <summary>
        /// The <see cref="ILogEntryResolver"/> to format the id
        /// </summary>
        [Category("LogViewerResolver")]
        public ILogEntryResolver IdResolver
        {
            get => (ILogEntryResolver)GetValue(IdResolverProperty);
            set => SetValue(IdResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="IdResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IdResolverProperty = DependencyProperty.Register(nameof(IdResolver), typeof(ILogEntryResolver), typeof(LogViewer), new PropertyMetadata(new IdResolver(), OnSortResolverChanged));
        
        /// <summary>
        /// The <see cref="ILogEntryResolver"/> to format the timestamp output
        /// </summary>
        [Category("LogViewerResolver")]
        public ILogEntryResolver TimeStampResolver
        {
            get => (ILogEntryResolver)GetValue(TimeStampResolverProperty);
            set => SetValue(TimeStampResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="TimeStampResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TimeStampResolverProperty = DependencyProperty.Register(nameof(TimeStampResolver), typeof(ILogEntryResolver), typeof(LogViewer), new PropertyMetadata(new TimeStampResolver(), OnTimeStampResolverChanged));
        
        /// <summary>
        /// The <see cref="ILogEntryResolver"/> to format the loggername
        /// </summary>
        [Category("LogViewerResolver")]
        public ILogEntryResolver LoggerNameResolver
        {
            get => (ILogEntryResolver)GetValue(LoggerNameResolverProperty);
            set => SetValue(LoggerNameResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="LoggerNameResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LoggerNameResolverProperty = DependencyProperty.Register(nameof(LoggerNameResolver), typeof(ILogEntryResolver), typeof(LogViewer), new PropertyMetadata(new LoggerNameResolver(), OnSortResolverChanged));
        
        /// <summary>
        /// The <see cref="ILogEntryResolver"/> to format the message
        /// </summary>
        [Category("LogViewerResolver")]
        public ILogEntryResolver MessageResolver
        {
            get => (ILogEntryResolver)GetValue(MessageResolverProperty);
            set => SetValue(MessageResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="MessageResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty MessageResolverProperty = DependencyProperty.Register(nameof(MessageResolver), typeof(ILogEntryResolver), typeof(LogViewer), new PropertyMetadata(new MessageResolver(), OnSortResolverChanged));

        /// <summary>
        /// The <see cref="ILogExportFormatter"/> to format log entries for export
        /// </summary>
        [Category("LogViewerResolver")]
        public ILogExportFormatter ExportFormatter
        {
            get => (ILogExportFormatter)GetValue(ExportFormatterProperty);
            set => SetValue(ExportFormatterProperty, value);
        }

        /// <summary>
        /// The <see cref="ExportFormatter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ExportFormatterProperty = DependencyProperty.Register(nameof(ExportFormatter), typeof(ILogExportFormatter), typeof(LogViewer), new PropertyMetadata(new DefaultLogExportFormatter()));
        
        #endregion

        // ##########################################################################################
        // Column Visibility
        // ##########################################################################################

        #region Column Visibility

        /// <summary>
        /// Controls the visibility of the ID column
        /// </summary>
        [Category("LogViewerColumns")]
        public bool ShowIdColumn
        {
            get => (bool)GetValue(ShowIdColumnProperty);
            set => SetValue(ShowIdColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowIdColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowIdColumnProperty = 
            DependencyProperty.Register(nameof(ShowIdColumn), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the Level column
        /// </summary>
        [Category("LogViewerColumns")]
        public bool ShowLevelColumn
        {
            get => (bool)GetValue(ShowLevelColumnProperty);
            set => SetValue(ShowLevelColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowLevelColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowLevelColumnProperty = 
            DependencyProperty.Register(nameof(ShowLevelColumn), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the TimeStamp column
        /// </summary>
        [Category("LogViewerColumns")]
        public bool ShowTimeStampColumn
        {
            get => (bool)GetValue(ShowTimeStampColumnProperty);
            set => SetValue(ShowTimeStampColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowTimeStampColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowTimeStampColumnProperty = 
            DependencyProperty.Register(nameof(ShowTimeStampColumn), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the LoggerName column
        /// </summary>
        [Category("LogViewerColumns")]
        public bool ShowLoggerNameColumn
        {
            get => (bool)GetValue(ShowLoggerNameColumnProperty);
            set => SetValue(ShowLoggerNameColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowLoggerNameColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowLoggerNameColumnProperty = 
            DependencyProperty.Register(nameof(ShowLoggerNameColumn), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

		#endregion

		// ##########################################################################################
		// Filter Properties
		// ##########################################################################################

		#region Filter Properties

		/// <summary>
		/// Filter for Trace log level
		/// </summary>
		[Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Trace level log entries")]
        public bool TraceFilter
        {
            get => (bool)GetValue(TraceFilterProperty);
            set => SetValue(TraceFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="TraceFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TraceFilterProperty = 
            DependencyProperty.Register(nameof(TraceFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Debug log level
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Debug level log entries")]
        public bool DebugFilter
        {
            get => (bool)GetValue(DebugFilterProperty);
            set => SetValue(DebugFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="DebugFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty DebugFilterProperty = 
            DependencyProperty.Register(nameof(DebugFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Info log level
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Info level log entries")]
        public bool InfoFilter
        {
            get => (bool)GetValue(InfoFilterProperty);
            set => SetValue(InfoFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="InfoFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty InfoFilterProperty = 
            DependencyProperty.Register(nameof(InfoFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Warn log level
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Warn level log entries")]
        public bool WarnFilter
        {
            get => (bool)GetValue(WarnFilterProperty);
            set => SetValue(WarnFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="WarnFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty WarnFilterProperty = 
            DependencyProperty.Register(nameof(WarnFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Error log level
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Error level log entries")]
        public bool ErrorFilter
        {
            get => (bool)GetValue(ErrorFilterProperty);
            set => SetValue(ErrorFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="ErrorFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ErrorFilterProperty = 
            DependencyProperty.Register(nameof(ErrorFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Fatal log level
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Hide/show Fatal level log entries")]
        public bool FatalFilter
        {
            get => (bool)GetValue(FatalFilterProperty);
            set => SetValue(FatalFilterProperty, value);
        }

        /// <summary>
        /// The <see cref="FatalFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FatalFilterProperty = 
            DependencyProperty.Register(nameof(FatalFilter), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Current search text being typed (not yet activated)
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Current search text being typed (activated with Enter key)")]
        public string CurrentSearchText
        {
            get => (string)GetValue(CurrentSearchTextProperty);
            set => SetValue(CurrentSearchTextProperty, value);
        }

        /// <summary>
        /// The <see cref="CurrentSearchText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty CurrentSearchTextProperty = 
            DependencyProperty.Register(nameof(CurrentSearchText), typeof(string), typeof(LogViewer), 
                new PropertyMetadata(string.Empty, (d, _) => (d as LogViewer)?.UpdateFilterError()));

        /// <summary>
        /// Whether to use regex pattern matching for search
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Use regex pattern matching instead of free text search")]
        public bool UseRegexSearch
        {
            get => (bool)GetValue(UseRegexSearchProperty);
            set => SetValue(UseRegexSearchProperty, value);
        }

        /// <summary>
        /// The <see cref="UseRegexSearch"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty UseRegexSearchProperty = 
            DependencyProperty.Register(nameof(UseRegexSearch), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(false, (d, _) => (d as LogViewer)?.UpdateFilterError()));

        /// <summary>
        /// Collection of active search terms
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(false)]
        [Description("Collection of active search terms")]
        public ObservableCollection<FilterTerm> ActiveSearchTerms
        {
            get => (ObservableCollection<FilterTerm>)GetValue(ActiveSearchTermsProperty);
            private set => SetValue(ActiveSearchTermsProperty, value);
        }

        /// <summary>
        /// The <see cref="ActiveSearchTerms"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ActiveSearchTermsProperty = 
            DependencyProperty.Register(nameof(ActiveSearchTerms), typeof(ObservableCollection<FilterTerm>), typeof(LogViewer), 
                new PropertyMetadata(null));

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is LogViewer instance)
            {
                instance.UpdateFilter();
            }
        }


        /// <summary>
        /// Adds the text of the filter input as a filter term, with the options of the input. Nothing happens while the text is empty or is
        /// not a valid regular expression (<see cref="FilterError"/> says why).
        /// </summary>
        public void AddSearchTerm()
        {
            if (string.IsNullOrWhiteSpace(CurrentSearchText) || FilterTerm.Validate(CurrentSearchText, UseRegexSearch) != null)
                return;

            ActiveSearchTerms.Add(new FilterTerm(CurrentSearchText, FilterIsExclude, FilterMatchCase, FilterWholeWord, UseRegexSearch, FilterLevel));

            // Clear the input
            CurrentSearchText = string.Empty;

            // Update filter
            UpdateFilter();
        }

        /// <summary>
        /// Removes a filter term from the active filter terms
        /// </summary>
        public void RemoveSearchTerm(FilterTerm searchTerm)
        {
            ActiveSearchTerms.Remove(searchTerm);
            UpdateFilter();
        }

        /// <summary>
        /// Clears all active filter terms
        /// </summary>
        public void ClearAllSearchTerms()
        {
            ActiveSearchTerms.Clear();
            UpdateFilter();
        }

        /// <summary>
        /// Adds the text as a filter term that shows only the entries that contain it (a plain text, without regard to case)
        /// </summary>
        /// <param name="text">The text to look for</param>
        public void AddRegexSearchTerm(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            ActiveSearchTerms.Add(new FilterTerm(text));
            UpdateFilter();
        }

        /// <summary>
        /// Adds the text as a filter term that hides the entries that contain it (a plain text, without regard to case)
        /// </summary>
        /// <param name="text">The text to look for</param>
        public void AddRegexSearchTermExclude(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            ActiveSearchTerms.Add(new FilterTerm(text, isExclude: true));
            UpdateFilter();
        }

        /// <summary>
        /// Edits a filter term by moving its text and options to the filter input; the term itself is removed
        /// </summary>
        /// <param name="searchTerm">The filter term to edit</param>
        public void EditSearchTerm(FilterTerm searchTerm)
        {
            if (searchTerm == null)
                return;

            CurrentSearchText = searchTerm.Text;
            UseRegexSearch = searchTerm.IsRegex;
            FilterIsExclude = searchTerm.IsExclude;
            FilterMatchCase = searchTerm.MatchCase;
            FilterWholeWord = searchTerm.WholeWord;
            FilterLevel = searchTerm.Level;

            ActiveSearchTerms.Remove(searchTerm);
            UpdateFilter();
        }

        /// <summary>
        /// Copies text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to clipboard</param>
        public void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Clipboard Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Gets all filtered log entries from the current view
        /// </summary>
        /// <returns>An enumerable collection of filtered LogEntry entries</returns>
        public IEnumerable<LogEntry> GetFilteredLogEntries()
        {
            if (LogEvents?.View == null)
                return Enumerable.Empty<LogEntry>();

            return LogEvents.View.Cast<LogEntry>();
        }

        /// <summary>
        /// Exports filtered log entries to a file in the specified format
        /// </summary>
        /// <param name="parameter">Export parameters containing file path and format</param>
        public void ExportLogs(ExportParameter parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (string.IsNullOrWhiteSpace(parameter.FilePath))
                throw new ArgumentException("File path cannot be empty", nameof(parameter));

            try
            {
                var filteredEntries = GetFilteredLogEntries().ToList();
                
                if (filteredEntries.Count == 0)
                {
                    MessageBox.Show("No log entries to export.", "Export", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                switch (parameter.Format)
                {
                    case ExportFormat.Log:
                        ExportToLogFormat(parameter.FilePath, filteredEntries, parameter.CustomFormatter);
                        break;
                    default:
                        throw new NotSupportedException($"Export format {parameter.Format} is not supported.");
                }

                MessageBox.Show($"Successfully exported {filteredEntries.Count} log entry(ies) to {parameter.FilePath}", 
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Exports log entries to a standard log file format
        /// </summary>
        /// <param name="filePath">Target file path</param>
        /// <param name="logEntries">Log entries to export</param>
        /// <param name="customFormatter">Optional custom formatter to override the default formatter</param>
        private void ExportToLogFormat(string filePath, IEnumerable<LogEntry> logEntries, ILogExportFormatter? customFormatter = null)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            
            // Use custom formatter if provided, otherwise use ExportFormatter, fall back to default formatter
            var formatter = customFormatter ?? ExportFormatter ?? new DefaultLogExportFormatter();
            
            foreach (var logEvent in logEntries)
            {
                var formattedLine = formatter.Format(
                    logEvent,
                    TimeStampResolver ?? new TimeStampResolver(),
                    LoggerNameResolver ?? new LoggerNameResolver(),
                    MessageResolver ?? new MessageResolver());
                
                writer.WriteLine(formattedLine);
            }
        }

        #endregion

        // ##########################################################################################
        // Filter Visibility
        // ##########################################################################################

        #region Filter Visibility

        /// <summary>
        /// Controls the visibility of the filter buttons
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Show/hide the filter buttons")]
        public bool ShowFilterButtons
        {
            get => (bool)GetValue(ShowFilterButtonsProperty);
            set => SetValue(ShowFilterButtonsProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowFilterButtons"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowFilterButtonsProperty = 
            DependencyProperty.Register(nameof(ShowFilterButtons), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true));

        /// <summary>
        /// Controls the visibility of the search controls
        /// </summary>
        [Category("LogViewerFilters")]
        [Browsable(true)]
        [Description("Show/hide the search controls")]
        public bool ShowSearchControls
        {
            get => (bool)GetValue(ShowSearchControlsProperty);
            set => SetValue(ShowSearchControlsProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowSearchControls"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowSearchControlsProperty = 
            DependencyProperty.Register(nameof(ShowSearchControls), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true));

        #endregion

        // ##########################################################################################
        // Controls Visibility
        // ##########################################################################################

        #region Controls Visibility

        /// <summary>
        /// Controls the visibility of the control buttons (Auto-Scroll, Clear, Pause)
        /// </summary>
        [Category("LogViewerControls")]
        [Browsable(true)]
        [Description("Show/hide the control buttons (Auto-Scroll, Clear, Pause)")]
        public bool ShowControlButtons
        {
            get => (bool)GetValue(ShowControlButtonsProperty);
            set => SetValue(ShowControlButtonsProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowControlButtons"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowControlButtonsProperty = 
            DependencyProperty.Register(nameof(ShowControlButtons), typeof(bool), typeof(LogViewer), 
                new PropertyMetadata(true));

        #endregion

        #endregion

        // ##############################################################################################################################
        // Properties
        // ##############################################################################################################################

        #region Properties

        // ##########################################################################################
        // Public Properties
        // ##########################################################################################



        // ##########################################################################################
        // Private Properties
        // ##########################################################################################

        private readonly ObservableCollection<LogEntry> _internalLogEntries = new();
        private ObservableCollection<LogEntry>? _collectionChangedSubscriber;
        private Window? _ParentWindow;

        private ColumnWidthFitter? _columnWidthFitter;
        private readonly ScrollAnchorKeeper _scrollAnchorKeeper = new();

        /// <summary>
        /// Reference to the ListView template part for programmatic access
        /// </summary>
        private ListView? PART_ListView => GetTemplateChild("PART_ListView") as ListView;

        #endregion

        // ##############################################################################################################################
        // Collection / ItemsSource
        // ##############################################################################################################################

        /// <summary>
        /// Returns the collection currently feeding <see cref="LogEvents"/> (external <see cref="ItemsSource"/> or the internal list).
        /// </summary>
        protected ObservableCollection<LogEntry> GetEffectiveEntries()
            => ItemsSource ?? _internalLogEntries;

        /// <summary>
        /// Adds log entries on the UI thread batch path (used by <see cref="LogViewer"/> when subscribing to a <see cref="Source"/>).
        /// </summary>
        protected internal void AppendLogEntriesBatch(IEnumerable<LogEntry> infos)
        {
            if (LogEvents == null)
                return;

            var coll = GetEffectiveEntries();
            using (LogEvents.DeferRefresh())
            {
                foreach (var info in infos)
                    coll.Add(info);
                ApplyMaxCountTrim(coll);
            }

            if (AutoScroll)
                PART_ListView?.ScrollToEnd();
        }

        private void SyncItemsSourceToView()
        {
            if (LogEvents != null)
                LogEvents.Source = GetEffectiveEntries();

            AttachEffectiveCollectionChangedHandler();
            LoggerTree.Rebuild(GetEffectiveEntries());
            SetViewSort();
            LogEvents?.View?.Refresh();
            UpdateFilter();
            CollectPropertyNames(GetEffectiveEntries());
            _columnWidthFitter?.Reset(GetEffectiveEntries());
        }

        private void AttachEffectiveCollectionChangedHandler()
        {
            var effective = GetEffectiveEntries();
            if (_collectionChangedSubscriber == effective)
                return;

            if (_collectionChangedSubscriber != null)
                _collectionChangedSubscriber.CollectionChanged -= OnLogEntriesCollectionChanged;

            _collectionChangedSubscriber = effective;
            _collectionChangedSubscriber.CollectionChanged += OnLogEntriesCollectionChanged;
        }

        private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is not ObservableCollection<LogEntry> coll)
                return;

            UpdateLoggerTree(e, coll);

            if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Replace)
                ApplyMaxCountTrim(coll);

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                CollectPropertyNames(e.NewItems);
                _columnWidthFitter?.Include(e.NewItems);
            }
            else if (e.Action is NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Replace)
            {
                CollectPropertyNames(coll);
                _columnWidthFitter?.Reset(coll);
            }

            ScheduleFindUpdate();

            if (AutoScroll)
                PART_ListView?.ScrollToEnd();
        }

        private void ApplyMaxCountTrim(ObservableCollection<LogEntry> coll)
        {
            if (MaxCount < 0 || LogEvents == null)
                return;

            if (coll.Count - 100 <= MaxCount)
                return;

            using (LogEvents.DeferRefresh())
            {
                while (coll.Count > MaxCount)
                    coll.RemoveAt(0);
            }
        }

        // ##############################################################################################################################
        // Constructor
        // ##############################################################################################################################

        #region Constructor

        static LogViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LogViewer), new FrameworkPropertyMetadata(typeof(LogViewer)));
        }

        /// <summary>
        /// Initializes a new instance of the log viewer base control.
        /// </summary>
        public LogViewer()
        {
            DataContext = this;

            Uid = GetHashCode().ToString();

            LoggerTree = new LoggerTree(ScheduleLoggerTreeUpdate);
            LoggerTree.RulesChanged += OnLoggerTreeRulesChanged;

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            LogEvents = new CollectionViewSource { Source = _internalLogEntries };
            ActiveSearchTerms = new ObservableCollection<FilterTerm>();
            UpdateFilter();

            AttachEffectiveCollectionChangedHandler();

            SetOwner(this, this);

            Loaded += _OnLoaded;
            Unloaded += _OnUnloaded;

            ClearCommand = new RelayCommand(() => GetEffectiveEntries().Clear());
            AddSearchTermCommand = new RelayCommand(AddSearchTerm);
            ClearAllSearchTermsCommand = new RelayCommand(ClearAllSearchTerms);
            RemoveSearchTermCommand = new RelayCommand<FilterTerm>(RemoveSearchTerm);
            AddRegexSearchTermCommand = new RelayCommand<string>(AddRegexSearchTerm);
            AddRegexSearchTermExcludeCommand = new RelayCommand<string>(AddRegexSearchTermExclude);
            EditSearchTermCommand = new RelayCommand<FilterTerm>(EditSearchTerm);
            CopyToClipboardCommand = new RelayCommand<string>(CopyToClipboard);
            ExportCommand = new RelayCommand<ExportParameter>(ExportLogs);
            FindNextCommand = new RelayCommand(() => FindNext());
            FindPreviousCommand = new RelayCommand(() => FindNext(forward: false));
            InputBindings.Add(new KeyBinding(FindNextCommand, Key.F3, ModifierKeys.None));
            AddJumpKeyBindings();
            AddRoutedCommandBindings();
            InputBindings.Add(new KeyBinding(FindPreviousCommand, Key.F3, ModifierKeys.Shift));
            ScrollToEndCommand = new RelayCommand(() =>
            {
                PART_ListView?.ScrollToEnd();
            });
        }

        private void _OnUnloaded(object sender, RoutedEventArgs e)
        {
            var window = _ParentWindow ?? Window.GetWindow(this);
            if (window == null || window.FindChildByUid<LogViewer>(Uid) == null)
                DisposeViewerResources();
        }

        private void _ParentWindowOnClosed(object? sender, EventArgs e)
        {
            DisposeViewerResources();
        }

        private void _OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (_ParentWindow == null && window != null)
            {
                _ParentWindow = window;
                _ParentWindow.Closed += _ParentWindowOnClosed;
            }

            // The control is loaded again when it comes back into the window, e.g. after a change of the tab. The setup of the columns runs
            // once, but the subscription to the source starts every time.
            if (_columnsInitialized)
            {
                OnViewerLoaded();
                return;
            }

            _columnsInitialized = true;

            PART_ListView?.ScrollToEnd();

            InitializeColumns();

            OnViewerLoaded();
        }

        #endregion
    }
}
