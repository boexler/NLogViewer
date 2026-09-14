using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sentinel.NLogViewer.App.Models;
using Sentinel.NLogViewer.Wpf.Extensions;
using Sentinel.NLogViewer.Wpf.Helper;
using Sentinel.NLogViewer.Wpf.Resolver;
using NLog;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Sentinel.NLogViewer.Wpf
{
    /// <summary>
    /// Base class for search terms that can match against text
    /// </summary>
    public abstract class SearchTerm
    {
        public string Text { get; protected set; }
        
        protected SearchTerm(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
        
        /// <summary>
        /// Determines if the search term matches the given text
        /// </summary>
        /// <param name="text">The text to match against</param>
        /// <returns>True if the text matches the search term</returns>
        public abstract bool Match(string text);
        
        /// <summary>
        /// Determines if the search term matches any of the given texts
        /// </summary>
        /// <param name="texts">The texts to match against</param>
        /// <returns>True if any text matches the search term</returns>
        public bool MatchAny(params string[] texts)
        {
            if (texts == null) return false;
            return texts.Any(text => !string.IsNullOrEmpty(text) && Match(text));
        }
        
        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// A search term that performs case-insensitive substring matching
    /// </summary>
    public class TextSearchTerm : SearchTerm
    {
        private readonly string _lowercaseText;
        
        public TextSearchTerm(string text) : base(text)
        {
            _lowercaseText = text.ToLowerInvariant();
        }
        
        public override bool Match(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (string.IsNullOrEmpty(_lowercaseText)) return false;
            return text.ToLowerInvariant().Contains(_lowercaseText);
        }
        
        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// A search term that performs regex pattern matching
    /// </summary>
    public class RegexSearchTerm : SearchTerm
    {
        private readonly Regex _regex;

        public RegexSearchTerm(string pattern) : base(pattern)
        {
            try
            {
                _regex = new Regex(pattern, RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid regex pattern: {ex.Message}", nameof(pattern), ex);
            }
        }
        
        public override bool Match(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return _regex.IsMatch(text);
        }
        
        public override string ToString()
        {
            return $"/{Text}/";
        }
    }

    /// <summary>
    /// Base log viewer control: filtering, styling, and optional external <see cref="ItemsSource"/>.
    /// Use <see cref="NLogViewer"/> to subscribe to NLog <see cref="Targets.CacheTarget"/>.
    /// </summary>
    public class NLogViewerBase : Control
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
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(TraceBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#D3D3D3")));

        /// <summary>
        /// The foreground for the trace output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(TraceForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#042271")));

        /// <summary>
        /// The background for the debug output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(DebugBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#90EE90")));

        /// <summary>
        /// The foreground for the debug output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(DebugForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#042271")));

        /// <summary>
        /// The background for the info output
        /// </summary>
        [Category("NLogViewerColors")]
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
            typeof(Brush), typeof(NLogViewerBase),
new PropertyMetadata(MetadataBrush("#0000FF")));

        /// <summary>
        /// The foreground for the info output
        /// </summary>
        [Category("NLogViewerColors")]
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
            typeof(Brush), typeof(NLogViewerBase), new PropertyMetadata(Brushes.White));

        /// <summary>
        /// The background for the warn output
        /// </summary>
        [Category("NLogViewerColors")]
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
            typeof(Brush), typeof(NLogViewerBase),
new PropertyMetadata(MetadataBrush("#FFFF00")));

        /// <summary>
        /// The foreground for the warn output
        /// </summary>
        [Category("NLogViewerColors")]
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
            typeof(Brush), typeof(NLogViewerBase),
new PropertyMetadata(MetadataBrush("#324B5C")));

        /// <summary>
        /// The background for the error output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(ErrorBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.Red));

        /// <summary>
        /// The foreground for the error output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(ErrorForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.White));

        /// <summary>
        /// The background for the fatal output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(FatalBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// The foreground for the fatal output
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(FatalForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.Yellow));

        // ------------------------------------------------------------------------------------------
        // Search Term Chip Colors
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Background brush for plain text search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(TextSearchTermBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFFFBE6")));

        /// <summary>
        /// Border brush for plain text search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(TextSearchTermBorderBrush), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFFC107")));

        /// <summary>
        /// Foreground brush for plain text search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(TextSearchTermForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// Background brush for regex search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(RegexSearchTermBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFFFBE6")));

        /// <summary>
        /// Border brush for regex search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(RegexSearchTermBorderBrush), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFFC107")));

        /// <summary>
        /// Foreground brush for regex search term chips
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(RegexSearchTermForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(Brushes.Black));

        /// <summary>
        /// Foreground brush for the "Regex:" prefix label
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(RegexPrefixForeground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFF8F00")));

        /// <summary>
        /// Background brush used to highlight matched search text in the message
        /// </summary>
        [Category("NLogViewerColors")]
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
            DependencyProperty.Register(nameof(SearchHighlightBackground), typeof(Brush), typeof(NLogViewerBase),
                new PropertyMetadata(MetadataBrush("#FFFF80C0")));

        #endregion
        
        // ##########################################################################################
        // NLogViewer
        // ##########################################################################################

        #region NLogViewer

        /// <summary>
        /// When set, the viewer binds its list to this collection so callers can push <see cref="LogEventInfo"/> directly.
        /// When null, an internal collection is used.
        /// </summary>
        [Category("NLogViewer")]
        [Browsable(true)]
        [Description("Observable collection of log entries displayed by the viewer.")]
        public ObservableCollection<LogEventInfo>? ItemsSource
        {
            get => (ObservableCollection<LogEventInfo>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// The <see cref="ItemsSource"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<LogEventInfo>),
            typeof(NLogViewerBase),
            new PropertyMetadata(null, OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not NLogViewerBase viewer)
                return;
            viewer.SyncItemsSourceToView();
        }

        /// <summary>
        /// Private DP to bind to the gui
        /// </summary>
        [Category("NLogViewer")]
        public CollectionViewSource LogEvents
        {
            get => (CollectionViewSource) GetValue(LogEventsProperty);
            private set => SetValue(LogEventsProperty, value);
        }

        /// <summary>
        /// The <see cref="LogEvents"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LogEventsProperty = DependencyProperty.Register(nameof(LogEvents),
            typeof(CollectionViewSource), typeof(NLogViewerBase), new PropertyMetadata(null));
        
        /// <summary>
        /// Automatically scroll to the newest entry
        /// </summary>
        [Category("NLogViewerControls")]
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
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(NLogViewerBase), new PropertyMetadata(true, AutoScrollChangedCallback));

        private static void AutoScrollChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is NLogViewerBase instance)
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

            collectionView.Filter = item =>
            {
                if (item is not LogEventInfo logEvent) return true;

                // Apply level filters first
                var levelFilterPassed = logEvent.Level.Name switch
                {
                    "Trace" => !TraceFilter,  // If TraceFilter is true, hide Trace entries (!true = false)
                    "Debug" => !DebugFilter,  // If DebugFilter is true, hide Debug entries (!true = false)
                    "Info" => !InfoFilter,    // If InfoFilter is true, hide Info entries (!true = false)
                    "Warn" => !WarnFilter,     // If WarnFilter is true, hide Warn entries (!true = false)
                    "Error" => !ErrorFilter,   // If ErrorFilter is true, hide Error entries (!true = false)
                    "Fatal" => !FatalFilter,   // If FatalFilter is true, hide Fatal entries (!true = false)
                    _ => true
                };

                if (!levelFilterPassed) return false;

                // Apply search filters if any active search terms exist
                if (ActiveSearchTerms?.Count > 0)
                {
                    var loggerName = logEvent.LoggerName ?? string.Empty;
                    var message = MessageResolver?.Resolve(logEvent) ?? string.Empty;
                    
                    // Separate include and exclude terms based on regex pattern
                    // Exclude patterns start with "^(?!.*" (negative lookahead)
                    var includeTerms = ActiveSearchTerms.Where(t => 
                        !(t is RegexSearchTerm regexTerm && regexTerm.Text.StartsWith("^(?!.*", StringComparison.Ordinal))).ToList();
                    var excludeTerms = ActiveSearchTerms.Where(t => 
                        t is RegexSearchTerm regexTerm && regexTerm.Text.StartsWith("^(?!.*", StringComparison.Ordinal)).ToList();
                    
                    // AND logic: ALL include terms must match
                    foreach (var searchTerm in includeTerms)
                    {
                        if (!searchTerm.MatchAny(loggerName, message))
                            return false;
                    }
                    
                    // Exclude logic: Hide entry if ANY string (loggerName OR message) contains the excluded pattern
                    // Note: For exclude patterns (negative lookahead ^(?!.*pattern)), if the pattern matches,
                    // it means the text does NOT contain the excluded pattern. If it doesn't match, the text contains it.
                    // We need to check each string individually because MatchAny would return true if ANY string matches,
                    // but we want to hide if ANY string contains the pattern.
                    foreach (var searchTerm in excludeTerms)
                    {
                        // Check each string individually: if either contains the pattern, hide the entry
                        var loggerContainsPattern = !searchTerm.Match(loggerName);
                        var messageContainsPattern = !searchTerm.Match(message);
                        
                        if (loggerContainsPattern || messageContainsPattern)
                            return false; // Hide entry if either string contains the excluded pattern
                    }
                    
                    // All include terms matched and no exclude terms matched
                    return true;
                }

                return true;
            };
            
            // Refresh the view to apply the filter
            collectionView.Refresh();
        }
        
        /// <summary>
        /// Delete all entries
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a new search term
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to clear all search terms
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a regex search term from a provided text
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to add a regex search term as an exclude filter from a provided text
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to remove a specific search term
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to edit a search term by copying it to the current search text
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to copy text to clipboard
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to export filtered log entries to a file
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new PropertyMetadata(null));

        /// <summary>
        /// Command to scroll to the end of the log entries
        /// </summary>
        [Category("NLogViewerControls")]
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
            typeof(ICommand), typeof(NLogViewerBase), new FrameworkPropertyMetadata
            {
	            BindsTwoWayByDefault = true,
	            DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
        
        /// <summary>
        /// Stop logging
        /// </summary>
        [Category("NLogViewerControls")]
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
        public static readonly DependencyProperty PauseProperty = DependencyProperty.Register(nameof(Pause), typeof(bool), typeof(NLogViewerBase), new PropertyMetadata(false, OnPauseChanged));

        private static void OnPauseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NLogViewerBase viewer && !DesignerProperties.GetIsInDesignMode(viewer))
                viewer.OnPausePropertyChanged((bool)e.NewValue);
        }

        /// <summary>
        /// Called when <see cref="Pause"/> changes. Default does nothing (external <see cref="ItemsSource"/> is unaffected).
        /// </summary>
        protected virtual void OnPausePropertyChanged(bool isPaused)
        {
        }
        
        /// <summary>
        /// The maximum number of entries before automatic cleaning is performed. There is a hysteresis of 100 entries which must be exceeded.
        /// Example: <see cref="MaxCount"/> is '1000'. Then after '1100' entries, everything until '1000' is deleted.
        /// If set to '0' or less, it is deactivated
        /// </summary>
        [Category("NLogViewer")]
        public int MaxCount
        {
            get => (int)GetValue(MaxCountProperty);
            set => SetValue(MaxCountProperty, value);
        }

        /// <summary>
        /// The <see cref="MaxCount"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty MaxCountProperty = DependencyProperty.Register(nameof(MaxCount), typeof(int), typeof(NLogViewerBase),
            new PropertyMetadata(5000, OnMaxCountChanged));

        private static void OnMaxCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not NLogViewerBase viewer || DesignerProperties.GetIsInDesignMode(viewer))
                return;
            viewer.ApplyMaxCountTrim(viewer.GetEffectiveEntries());
        }


        #endregion

        // ##########################################################################################
        // Resolver
        // ##########################################################################################

        #region Resolver
        
        /// <summary>
        /// The <see cref="ILogEventInfoResolver"/> to format the id
        /// </summary>
        [Category("NLogViewerResolver")]
        public ILogEventInfoResolver IdResolver
        {
            get => (ILogEventInfoResolver)GetValue(IdResolverProperty);
            set => SetValue(IdResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="IdResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IdResolverProperty = DependencyProperty.Register(nameof(IdResolver), typeof(ILogEventInfoResolver), typeof(NLogViewerBase), new PropertyMetadata(new IdResolver()));
        
        /// <summary>
        /// The <see cref="ILogEventInfoResolver"/> to format the timestamp output
        /// </summary>
        [Category("NLogViewerResolver")]
        public ILogEventInfoResolver TimeStampResolver
        {
            get => (ILogEventInfoResolver)GetValue(TimeStampResolverProperty);
            set => SetValue(TimeStampResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="TimeStampResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty TimeStampResolverProperty = DependencyProperty.Register(nameof(TimeStampResolver), typeof(ILogEventInfoResolver), typeof(NLogViewerBase), new PropertyMetadata(new TimeStampResolver()));
        
        /// <summary>
        /// The <see cref="ILogEventInfoResolver"/> to format the loggername
        /// </summary>
        [Category("NLogViewerResolver")]
        public ILogEventInfoResolver LoggerNameResolver
        {
            get => (ILogEventInfoResolver)GetValue(LoggerNameResolverProperty);
            set => SetValue(LoggerNameResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="LoggerNameResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LoggerNameResolverProperty = DependencyProperty.Register(nameof(LoggerNameResolver), typeof(ILogEventInfoResolver), typeof(NLogViewerBase), new PropertyMetadata(new LoggerNameResolver()));
        
        /// <summary>
        /// The <see cref="ILogEventInfoResolver"/> to format the message
        /// </summary>
        [Category("NLogViewerResolver")]
        public ILogEventInfoResolver MessageResolver
        {
            get => (ILogEventInfoResolver)GetValue(MessageResolverProperty);
            set => SetValue(MessageResolverProperty, value);
        }

        /// <summary>
        /// The <see cref="MessageResolver"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty MessageResolverProperty = DependencyProperty.Register(nameof(MessageResolver), typeof(ILogEventInfoResolver), typeof(NLogViewerBase), new PropertyMetadata(new MessageResolver()));

        /// <summary>
        /// The <see cref="ILogExportFormatter"/> to format log entries for export
        /// </summary>
        [Category("NLogViewerResolver")]
        public ILogExportFormatter ExportFormatter
        {
            get => (ILogExportFormatter)GetValue(ExportFormatterProperty);
            set => SetValue(ExportFormatterProperty, value);
        }

        /// <summary>
        /// The <see cref="ExportFormatter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ExportFormatterProperty = DependencyProperty.Register(nameof(ExportFormatter), typeof(ILogExportFormatter), typeof(NLogViewerBase), new PropertyMetadata(new DefaultLogExportFormatter()));
        
        #endregion

        // ##########################################################################################
        // Column Visibility
        // ##########################################################################################

        #region Column Visibility

        /// <summary>
        /// Controls the visibility of the ID column
        /// </summary>
        [Category("NLogViewerColumns")]
        public bool ShowIdColumn
        {
            get => (bool)GetValue(ShowIdColumnProperty);
            set => SetValue(ShowIdColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowIdColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowIdColumnProperty = 
            DependencyProperty.Register(nameof(ShowIdColumn), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the Level column
        /// </summary>
        [Category("NLogViewerColumns")]
        public bool ShowLevelColumn
        {
            get => (bool)GetValue(ShowLevelColumnProperty);
            set => SetValue(ShowLevelColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowLevelColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowLevelColumnProperty = 
            DependencyProperty.Register(nameof(ShowLevelColumn), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the TimeStamp column
        /// </summary>
        [Category("NLogViewerColumns")]
        public bool ShowTimeStampColumn
        {
            get => (bool)GetValue(ShowTimeStampColumnProperty);
            set => SetValue(ShowTimeStampColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowTimeStampColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowTimeStampColumnProperty = 
            DependencyProperty.Register(nameof(ShowTimeStampColumn), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));

        /// <summary>
        /// Controls the visibility of the LoggerName column
        /// </summary>
        [Category("NLogViewerColumns")]
        public bool ShowLoggerNameColumn
        {
            get => (bool)GetValue(ShowLoggerNameColumnProperty);
            set => SetValue(ShowLoggerNameColumnProperty, value);
        }

        /// <summary>
        /// The <see cref="ShowLoggerNameColumn"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ShowLoggerNameColumnProperty = 
            DependencyProperty.Register(nameof(ShowLoggerNameColumn), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true, OnColumnVisibilityChanged));


        private static void OnColumnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is NLogViewerBase instance)
            {
                instance.UpdateColumnVisibility();
            }
        }

        /// <summary>
        /// Updates column visibility by removing or adding columns based on the Show properties.
        /// When a column is hidden, it's removed from the grid. When shown again, it's added back at its original position.
        /// </summary>
        private void UpdateColumnVisibility()
        {
	        if (PART_ListView?.View is not AutoSizedGridView gridView) return;

	        // Update ID column visibility (original index 0)
	        UpdateColumnVisibility(gridView, _originalIdColumn, ShowIdColumn, _originalIdColumnWidth, 0);

	        // Update Level column visibility (original index 1)
	        UpdateColumnVisibility(gridView, _originalLevelColumn, ShowLevelColumn, _originalLevelColumnWidth, 1);

	        // Update TimeStamp column visibility (original index 2)
	        UpdateColumnVisibility(gridView, _originalTimeStampColumn, ShowTimeStampColumn, _originalTimeStampColumnWidth, 2);

	        // Update LoggerName column visibility (original index 3)
	        UpdateColumnVisibility(gridView, _originalLoggerNameColumn, ShowLoggerNameColumn, _originalLoggerNameColumnWidth, 3);

	        // Message column is always visible (no visibility control)
        }

        /// <summary>
        /// Helper method to update individual column visibility by removing or adding the column.
        /// Calculates the correct insertion position based on which columns are currently visible.
        /// </summary>
        /// <param name="gridView">The GridView containing the columns</param>
        /// <param name="originalColumn">The original column reference to add/remove</param>
        /// <param name="showColumn">Whether the column should be visible</param>
        /// <param name="originalWidth">The original width of the column</param>
        /// <param name="originalIndex">The original index of the column (0=ID, 1=Level, 2=TimeStamp, 3=LoggerName)</param>
        private void UpdateColumnVisibility(AutoSizedGridView gridView, GridViewColumn? originalColumn, bool showColumn, double originalWidth, int originalIndex)
        {
	        if (originalColumn == null) return;

	        // Check if column is currently in the grid
	        var columnExists = gridView.Columns.Contains(originalColumn);

	        if (showColumn && !columnExists)
	        {
		        // Column should be visible but is not in the grid - add it back
		        originalColumn.Width = originalWidth;
		        
		        // Calculate the correct insertion index based on which columns are currently visible
		        var insertionIndex = CalculateInsertionIndex(gridView, originalIndex);
		        gridView.Columns.Insert(insertionIndex, originalColumn);
	        }
	        else if (!showColumn && columnExists)
	        {
		        // Column should be hidden but is in the grid - remove it
		        gridView.Columns.Remove(originalColumn);
	        }
        }

        /// <summary>
        /// Calculates the correct insertion index for a column based on its original position
        /// and which columns are currently visible.
        /// </summary>
        /// <param name="gridView">The GridView containing the columns</param>
        /// <param name="originalIndex">The original index of the column to insert</param>
        /// <returns>The correct insertion index</returns>
        private int CalculateInsertionIndex(AutoSizedGridView gridView, int originalIndex)
        {
	        // Count how many visible columns come before this one in the original order
	        var insertionIndex = 0;
	        
	        // Check ID column (original index 0)
	        if (originalIndex > 0 && ShowIdColumn && _originalIdColumn != null && gridView.Columns.Contains(_originalIdColumn))
	        {
		        insertionIndex++;
	        }
	        
	        // Check Level column (original index 1)
	        if (originalIndex > 1 && ShowLevelColumn && _originalLevelColumn != null && gridView.Columns.Contains(_originalLevelColumn))
	        {
		        insertionIndex++;
	        }
	        
	        // Check TimeStamp column (original index 2)
	        if (originalIndex > 2 && ShowTimeStampColumn && _originalTimeStampColumn != null && gridView.Columns.Contains(_originalTimeStampColumn))
	        {
		        insertionIndex++;
	        }
	        
	        // Check LoggerName column (original index 3)
	        if (originalIndex > 3 && ShowLoggerNameColumn && _originalLoggerNameColumn != null && gridView.Columns.Contains(_originalLoggerNameColumn))
	        {
		        insertionIndex++;
	        }
	        
	        return insertionIndex;
        }

		#endregion

		// ##########################################################################################
		// Filter Properties
		// ##########################################################################################

		#region Filter Properties

		/// <summary>
		/// Filter for Trace log level
		/// </summary>
		[Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(TraceFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Debug log level
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(DebugFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Info log level
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(InfoFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Warn log level
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(WarnFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Error log level
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(ErrorFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Filter for Fatal log level
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(FatalFilter), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false, OnFilterChanged));

        /// <summary>
        /// Current search text being typed (not yet activated)
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(CurrentSearchText), typeof(string), typeof(NLogViewerBase), 
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Whether to use regex pattern matching for search
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(UseRegexSearch), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(false));

        /// <summary>
        /// Collection of active search terms
        /// </summary>
        [Category("NLogViewerFilters")]
        [Browsable(false)]
        [Description("Collection of active search terms")]
        public ObservableCollection<SearchTerm> ActiveSearchTerms
        {
            get => (ObservableCollection<SearchTerm>)GetValue(ActiveSearchTermsProperty);
            private set => SetValue(ActiveSearchTermsProperty, value);
        }

        /// <summary>
        /// The <see cref="ActiveSearchTerms"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ActiveSearchTermsProperty = 
            DependencyProperty.Register(nameof(ActiveSearchTerms), typeof(ObservableCollection<SearchTerm>), typeof(NLogViewerBase), 
                new PropertyMetadata(null));

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is NLogViewerBase instance)
            {
                instance.UpdateFilter();
            }
        }


        /// <summary>
        /// Adds a new search term to the active search terms collection
        /// </summary>
        public void AddSearchTerm()
        {
            if (string.IsNullOrWhiteSpace(CurrentSearchText))
                return;

            SearchTerm searchTerm;
            
            if (UseRegexSearch)
            {
                try
                {
                    searchTerm = new RegexSearchTerm(CurrentSearchText);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show($"Invalid regex pattern: {ex.Message}", "Regex Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                searchTerm = new TextSearchTerm(CurrentSearchText);
            }
            
            ActiveSearchTerms.Add(searchTerm);
            
            // Clear the input
            CurrentSearchText = string.Empty;
            
            // Update filter
            UpdateFilter();
        }

        /// <summary>
        /// Removes a search term from the active search terms collection
        /// </summary>
        public void RemoveSearchTerm(SearchTerm searchTerm)
        {
            ActiveSearchTerms.Remove(searchTerm);
            UpdateFilter();
        }

        /// <summary>
        /// Clears all active search terms
        /// </summary>
        public void ClearAllSearchTerms()
        {
            ActiveSearchTerms.Clear();
            UpdateFilter();
        }

        /// <summary>
        /// Adds a RegexSearchTerm from a provided text and updates the filter
        /// Special regex characters are escaped to treat the text as a literal string
        /// </summary>
        /// <param name="text">The text to use as regex pattern (will be escaped)</param>
        public void AddRegexSearchTerm(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                // Escape special regex characters to treat the text as a literal string
                var escapedPattern = Regex.Escape(text);
                var term = new RegexSearchTerm(escapedPattern);
                ActiveSearchTerms.Add(term);
                UpdateFilter();
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show($"Invalid regex pattern: {ex.Message}", "Regex Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Adds a RegexSearchTerm as an exclude filter from a provided text and updates the filter
        /// Special regex characters are escaped and wrapped in a negative lookahead pattern
        /// </summary>
        /// <param name="text">The text to use as regex pattern (will be escaped and wrapped in negative lookahead)</param>
        public void AddRegexSearchTermExclude(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                // Escape special regex characters to treat the text as a literal string
                var escapedPattern = Regex.Escape(text);
                // Wrap in negative lookahead: matches any text that does NOT contain the pattern
                var negativeLookaheadPattern = $"^(?!.*{escapedPattern}$)";
                var term = new RegexSearchTerm(negativeLookaheadPattern);
                ActiveSearchTerms.Add(term);
                UpdateFilter();
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show($"Invalid regex pattern: {ex.Message}", "Regex Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Edits a search term by copying its text to the current search text field
        /// </summary>
        /// <param name="searchTerm">The search term to edit</param>
        public void EditSearchTerm(SearchTerm searchTerm)
        {
            if (searchTerm == null)
                return;

            // Copy the search term text to the current search text field
            CurrentSearchText = searchTerm.Text;
            
            // Set the regex checkbox based on the search term type
            UseRegexSearch = searchTerm is RegexSearchTerm;
            
            // Remove the original search term
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
        /// <returns>An enumerable collection of filtered LogEventInfo entries</returns>
        public IEnumerable<LogEventInfo> GetFilteredLogEntries()
        {
            if (LogEvents?.View == null)
                return Enumerable.Empty<LogEventInfo>();

            return LogEvents.View.Cast<LogEventInfo>();
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
        private void ExportToLogFormat(string filePath, IEnumerable<LogEventInfo> logEntries, ILogExportFormatter? customFormatter = null)
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
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(ShowFilterButtons), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true));

        /// <summary>
        /// Controls the visibility of the search controls
        /// </summary>
        [Category("NLogViewerFilters")]
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
            DependencyProperty.Register(nameof(ShowSearchControls), typeof(bool), typeof(NLogViewerBase), 
                new PropertyMetadata(true));

        #endregion

        // ##########################################################################################
        // Controls Visibility
        // ##########################################################################################

        #region Controls Visibility

        /// <summary>
        /// Controls the visibility of the control buttons (Auto-Scroll, Clear, Pause)
        /// </summary>
        [Category("NLogViewerControls")]
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
            DependencyProperty.Register(nameof(ShowControlButtons), typeof(bool), typeof(NLogViewerBase), 
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

        private readonly ObservableCollection<LogEventInfo> _internalLogEntries = new();
        private ObservableCollection<LogEventInfo>? _collectionChangedSubscriber;
        private Window? _ParentWindow;

        // Store original column references and widths for restoration
        private GridViewColumn? _originalIdColumn;
        private GridViewColumn? _originalLevelColumn;
        private GridViewColumn? _originalTimeStampColumn;
        private GridViewColumn? _originalLoggerNameColumn;
        private double _originalIdColumnWidth = 40;
        private double _originalLevelColumnWidth = double.NaN; // Auto
        private double _originalTimeStampColumnWidth = double.NaN; // Auto
        private double _originalLoggerNameColumnWidth = double.NaN; // Auto

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
        protected ObservableCollection<LogEventInfo> GetEffectiveEntries()
            => ItemsSource ?? _internalLogEntries;

        /// <summary>
        /// Adds log entries on the UI thread batch path (used by <see cref="NLogViewer"/> when subscribing to <see cref="Targets.ICacheTarget"/>).
        /// </summary>
        protected internal void AppendLogEntriesBatch(IEnumerable<LogEventInfo> infos)
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
            LogEvents?.View?.Refresh();
            UpdateFilter();
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
            if (sender is not ObservableCollection<LogEventInfo> coll)
                return;

            if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Replace)
                ApplyMaxCountTrim(coll);

            if (AutoScroll)
                PART_ListView?.ScrollToEnd();
        }

        private void ApplyMaxCountTrim(ObservableCollection<LogEventInfo> coll)
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

        static NLogViewerBase()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NLogViewerBase), new FrameworkPropertyMetadata(typeof(NLogViewerBase)));
        }

        /// <summary>
        /// Initializes a new instance of the log viewer base control.
        /// </summary>
        public NLogViewerBase()
        {
            DataContext = this;

            Uid = GetHashCode().ToString();

            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            LogEvents = new CollectionViewSource { Source = _internalLogEntries };
            ActiveSearchTerms = new ObservableCollection<SearchTerm>();
            UpdateFilter();

            AttachEffectiveCollectionChangedHandler();

            Loaded += _OnLoaded;
            Unloaded += _OnUnloaded;

            ClearCommand = new RelayCommand(() => GetEffectiveEntries().Clear());
            AddSearchTermCommand = new RelayCommand(AddSearchTerm);
            ClearAllSearchTermsCommand = new RelayCommand(ClearAllSearchTerms);
            RemoveSearchTermCommand = new RelayCommand<SearchTerm>(RemoveSearchTerm);
            AddRegexSearchTermCommand = new RelayCommand<string>(AddRegexSearchTerm);
            AddRegexSearchTermExcludeCommand = new RelayCommand<string>(AddRegexSearchTermExclude);
            EditSearchTermCommand = new RelayCommand<SearchTerm>(EditSearchTerm);
            CopyToClipboardCommand = new RelayCommand<string>(CopyToClipboard);
            ExportCommand = new RelayCommand<ExportParameter>(ExportLogs);
            ScrollToEndCommand = new RelayCommand(() =>
            {
                PART_ListView?.ScrollToEnd();
            });
        }

        private void _OnUnloaded(object sender, RoutedEventArgs e)
        {
            var window = _ParentWindow ?? Window.GetWindow(this);
            if (window == null || window.FindChildByUid<NLogViewerBase>(Uid) == null)
                DisposeViewerResources();
        }

        private void _ParentWindowOnClosed(object? sender, EventArgs e)
        {
            DisposeViewerResources();
        }

        /// <summary>
        /// Releases listener-specific resources (overridden by <see cref="NLogViewer"/> for cache subscription disposal).
        /// </summary>
        protected virtual void DisposeViewerResources()
        {
        }

        private void _OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= _OnLoaded;

            var window = Window.GetWindow(this);
            if (_ParentWindow == null && window != null)
            {
                _ParentWindow = window;
                _ParentWindow.Closed += _ParentWindowOnClosed;
            }

            PART_ListView?.ScrollToEnd();

            if (PART_ListView?.View is AutoSizedGridView gridView)
            {
                if (gridView.Columns.Count > 0)
                {
                    _originalIdColumn = gridView.Columns[0];
                    _originalIdColumnWidth = gridView.Columns[0].Width;
                }

                if (gridView.Columns.Count > 1)
                {
                    _originalLevelColumn = gridView.Columns[1];
                    _originalLevelColumnWidth = gridView.Columns[1].Width;
                }

                if (gridView.Columns.Count > 2)
                {
                    _originalTimeStampColumn = gridView.Columns[2];
                    _originalTimeStampColumnWidth = gridView.Columns[2].Width;
                }

                if (gridView.Columns.Count > 3)
                {
                    _originalLoggerNameColumn = gridView.Columns[3];
                    _originalLoggerNameColumnWidth = gridView.Columns[3].Width;
                }

                UpdateColumnVisibility();
            }

            OnViewerLoaded();
        }

        /// <summary>
        /// Called once after the template is applied and columns are initialized.
        /// </summary>
        protected virtual void OnViewerLoaded()
        {
        }

        #endregion
    }
}
