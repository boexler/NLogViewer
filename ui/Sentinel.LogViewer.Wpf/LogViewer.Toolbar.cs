using System.ComponentModel;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The tool row, the search bar and the result row at the top of the control: how the level buttons look, when the search bar is open,
    /// what the result row says, and the routed commands that a host calls (<see cref="LogViewerCommands"/>)
    /// </summary>
    public partial class LogViewer
    {
        private ICommand? _openFindCommand;
        private ICommand? _closeFindCommand;
        private ICommand? _resetFilterCommand;

        /// <summary>
        /// Whether the level buttons show the symbol of the level, a letter, or both
        /// </summary>
        [Category("LogViewerControls")]
        [Description("Whether the level buttons show the symbol of the level, a letter, or both")]
        public LevelDisplayMode LevelButtonDisplay
        {
            get => (LevelDisplayMode)GetValue(LevelButtonDisplayProperty);
            set => SetValue(LevelButtonDisplayProperty, value);
        }

        /// <summary>
        /// The <see cref="LevelButtonDisplay"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LevelButtonDisplayProperty =
            DependencyProperty.Register(nameof(LevelButtonDisplay), typeof(LevelDisplayMode), typeof(LogViewer), new PropertyMetadata(LevelDisplayMode.Symbol));

        /// <summary>
        /// Whether the search bar under the tool row is open
        /// </summary>
        [Category("LogViewerFind")]
        public bool IsFindOpen
        {
            get => (bool)GetValue(IsFindOpenProperty);
            set => SetValue(IsFindOpenProperty, value);
        }

        /// <summary>
        /// The <see cref="IsFindOpen"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty IsFindOpenProperty =
            DependencyProperty.Register(nameof(IsFindOpen), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        private static readonly DependencyPropertyKey HasActiveFilterPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasActiveFilter), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// The <see cref="HasActiveFilter"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty HasActiveFilterProperty = HasActiveFilterPropertyKey.DependencyProperty;

        /// <summary>
        /// Whether a level is hidden, a term or a rule for loggers is on, or a time range is set
        /// </summary>
        [Category("LogViewerControls")]
        public bool HasActiveFilter => (bool)GetValue(HasActiveFilterProperty);

        private static readonly DependencyPropertyKey FilterResultTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FilterResultText), typeof(string), typeof(LogViewer), new PropertyMetadata(string.Empty));

        /// <summary>
        /// The <see cref="FilterResultText"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FilterResultTextProperty = FilterResultTextPropertyKey.DependencyProperty;

        /// <summary>
        /// What the result row says about the entries, e.g. "Entries: 6 of 10,992", or "Entries: 10,992" without a filter
        /// </summary>
        [Category("LogViewerControls")]
        public string FilterResultText => (string)GetValue(FilterResultTextProperty);

        private static readonly DependencyPropertyKey LoggerRuleCountPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(LoggerRuleCount), typeof(int), typeof(LogViewer), new PropertyMetadata(0));

        /// <summary>
        /// The <see cref="LoggerRuleCount"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty LoggerRuleCountProperty = LoggerRuleCountPropertyKey.DependencyProperty;

        /// <summary>
        /// How many rules for loggers are on (show only, hide)
        /// </summary>
        [Category("LogViewerControls")]
        public int LoggerRuleCount => (int)GetValue(LoggerRuleCountProperty);

        /// <summary>
        /// Opens the search bar and puts the cursor into its box (Ctrl+F)
        /// </summary>
        public ICommand OpenFindCommand => _openFindCommand ??= new RelayCommand(OpenFind, () => ShowFindControls);

        /// <summary>
        /// Closes the search bar and clears the search (Esc in the box)
        /// </summary>
        public ICommand CloseFindCommand => _closeFindCommand ??= new RelayCommand(CloseFind);

        /// <summary>
        /// Takes the levels, terms, rules for loggers and the time range off; the fields of the search and the time stamp format stay
        /// </summary>
        public ICommand ResetFilterCommand => _resetFilterCommand ??= new RelayCommand(ResetFilter, () => HasActiveFilter);

        private void OpenFind()
        {
            IsFindOpen = true;
            Dispatcher.BeginInvoke(() =>
            {
                if (GetTemplateChild("PART_FindBox") is TextBox box)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }, DispatcherPriority.Input);
        }

        private void CloseFind()
        {
            IsFindOpen = false;
            FindText = string.Empty;
            PART_ListView?.Focus();
        }

        private void ResetFilter() =>
            ApplyFilterState(new LogViewerFilterState { SearchFields = SearchFields, TimeStampFormat = CustomTimeStampFormat });

        private void OnTextsChanged(object? sender, PropertyChangedEventArgs e)
        {
            // The language can be set from any thread; the viewer belongs to the thread of its window.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => OnTextsChanged(sender, e));
                return;
            }

            UpdateFilterSummary();
            UpdateFilterHits();
            UpdateFindResultText();
            UpdateTimeRangeText();
            UpdateHeaders();
        }

        /// <summary>
        /// Updates what the result row says; the entries are counted once the current batch of changes is done
        /// </summary>
        private void UpdateFilterSummary()
        {
            var total = GetEffectiveEntries().Count;
            var shown = LogEvents?.View is CollectionView view ? view.Count : 0;
            var loggerRules = LoggerTree.Rules.Count;
            var hasFilters = LevelNames.Any(IsLevelHidden) || ActiveSearchTerms.Any(term => term.IsEnabled) || loggerRules > 0 || TimeFrom != null || TimeTo != null;

            SetValue(HasActiveFilterPropertyKey, hasFilters);
            SetValue(LoggerRuleCountPropertyKey, loggerRules);
            SetValue(FilterResultTextPropertyKey, hasFilters
                ? LogViewerTexts.Format("Result_Of", shown, total)
                : LogViewerTexts.Format("Result_All", total));
        }

        /// <summary>
        /// Lets the commands of <see cref="LogViewerCommands"/> run the commands of the viewer, when the viewer is their target
        /// </summary>
        private void AddRoutedCommandBindings()
        {
            Bind(ApplicationCommands.Find, OpenFindCommand);
            Bind(LogViewerCommands.GoToTime, GoToTimeCommand);
            Bind(LogViewerCommands.ChooseTimeRange, ChooseTimeRangeCommand);
            Bind(LogViewerCommands.ClearTimeRange, ClearTimeRangeCommand);
            Bind(LogViewerCommands.TimeRangeLast, TimeRangeLastCommand);
            Bind(LogViewerCommands.NextLevel, NextLevelCommand);
            Bind(LogViewerCommands.PreviousLevel, PreviousLevelCommand);
            Bind(LogViewerCommands.ResetFilter, ResetFilterCommand);

            void Bind(RoutedCommand routed, ICommand command) => CommandBindings.Add(new CommandBinding(routed,
                (_, e) => command.Execute(e.Parameter),
                (_, e) =>
                {
                    e.CanExecute = command.CanExecute(e.Parameter);
                    e.Handled = true;
                }));
        }
    }
}
