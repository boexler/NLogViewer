using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Models
{
    /// <summary>
    /// ViewModel for a log tab in the TabControl
    /// </summary>
    public class LogTabViewModel : INotifyPropertyChanged
    {
        private string _header = string.Empty;
        private string _targetName = string.Empty;
        private int _maxCount = 10000;
        private LogViewerFilterState? _filterState;
        private bool _filtersTakenOver;
        private bool _takesOverFilters = true;
        private RestartBehavior _restartBehavior;
        private bool _loggerTreeOnLeft;
        private bool _loggerTreeOpen;
        private double _loggerTreeWidth = Services.SettingsRules.DefaultLoggerTreeWidth;
        private SearchFields _searchFields = SearchFields.Default;
        private IReadOnlyList<string> _columnKeys = Wpf.LogColumns.Default;
        private TimeZoneInfo? _fileTimeZone;
        private TimeZoneInfo? _displayTimeZone;
        private string? _sortKey;
        private bool _sortDescending;
        private static long _lastSequence;
        private int _colourIndex = -1;
        private TabLook _look;
        private int? _pinOrder;

        /// <summary>
        /// Backing collection bound to <see cref="Sentinel.LogViewer.Wpf.LogViewer.ItemsSource"/>.
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public LogTabViewModel()
        {
            LogEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogCount));
        }

        public string Header
        {
            get => _header;
            set
            {
                if (_header != value)
                {
                    _header = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TargetName
        {
            get => _targetName;
            set
            {
                if (_targetName != value)
                {
                    _targetName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Number of entries in the tab: it follows <see cref="LogEntries"/>, also when the viewer clears it or removes the oldest entries
        /// </summary>
        public int LogCount => LogEntries.Count;

        public int MaxCount
        {
            get => _maxCount;
            set
            {
                if (_maxCount != value)
                {
                    _maxCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The application name and the sender of a tab that receives logs, the same for every process of the application; null for imported files
        /// </summary>
        public string? SenderKey { get; set; }

        /// <summary>Counts the tabs in the order they were made, so tabs can be put in the order of time</summary>
        public long Sequence { get; } = System.Threading.Interlocked.Increment(ref _lastSequence);

        /// <summary>The number of the colour of the tab of an application that sends logs, see <see cref="Services.TabColors"/>; -1 for the tab of a file</summary>
        public int ColourIndex
        {
            get => _colourIndex;
            set
            {
                if (_colourIndex == value)
                    return;

                _colourIndex = value;
                NotifyLook();
            }
        }

        /// <summary>How the colour is shown, which the application sets for all tabs</summary>
        public TabLook Look
        {
            get => _look;
            set
            {
                if (_look == value)
                    return;

                _look = value;
                NotifyLook();
            }
        }

        private bool HasColour => IsLive && _colourIndex >= 0;

        /// <summary>The stripe over the whole tab in the dark shade; null when the tab is shown tinted or has no colour</summary>
        public System.Windows.Media.Brush? StripeBrush => HasColour && _look == TabLook.Stripe ? Services.TabColors.DarkBrush(_colourIndex) : null;

        /// <summary>Whether the tab is tinted</summary>
        public bool IsTinted => HasColour && _look == TabLook.Tinted;

        /// <summary>The round dot in the dark shade, for the tinted tab</summary>
        public System.Windows.Media.Brush? DotBrush => HasColour && _look == TabLook.Tinted ? Services.TabColors.DarkBrush(_colourIndex) : null;

        /// <summary>The light shade as the background of a tinted tab</summary>
        public System.Windows.Media.Brush? TintBrush => HasColour && _look == TabLook.Tinted ? Services.TabColors.LightBrush(_colourIndex) : null;

        /// <summary>The background of a tinted tab that is selected: stronger than the tint</summary>
        public System.Windows.Media.Brush? TintSelectedBrush => HasColour && _look == TabLook.Tinted ? Services.TabColors.StrongBrush(_colourIndex) : null;

        private void NotifyLook()
        {
            OnPropertyChanged(nameof(ColourIndex));
            OnPropertyChanged(nameof(Look));
            OnPropertyChanged(nameof(StripeBrush));
            OnPropertyChanged(nameof(IsTinted));
            OnPropertyChanged(nameof(DotBrush));
            OnPropertyChanged(nameof(TintBrush));
            OnPropertyChanged(nameof(TintSelectedBrush));
        }

        /// <summary>Where the tab stands among the pinned tabs; null when it is not pinned</summary>
        public int? PinOrder
        {
            get => _pinOrder;
            set
            {
                if (_pinOrder == value)
                    return;

                _pinOrder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPinned));
                OnPropertyChanged(nameof(ShowPin));
                OnPropertyChanged(nameof(ShowClose));
            }
        }

        public bool IsPinned => _pinOrder != null;

        /// <summary>Whether the menu offers to pin the tab: only the tab of an application can be pinned, a file has no key to keep it by</summary>
        public bool ShowPin => IsLive && !IsPinned;

        /// <summary>A pinned tab has no close button</summary>
        public bool ShowClose => !IsPinned;

        /// <summary>The name of the application that sends to the tab; empty for the tab of a file</summary>
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>The address that the logs of the tab come from; empty for the tab of a file</summary>
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// When the tab got the last entry
        /// </summary>
        public DateTime LastEventUtc { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// The filters of the viewer in the tab. The viewer keeps it up to date and applies it when it is set.
        /// </summary>
        public LogViewerFilterState? FilterState
        {
            get => _filterState;
            set
            {
                if (ReferenceEquals(_filterState, value))
                    return;

                _filterState = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the tab started with the filters of the last tab of its application and they are still shown to the user
        /// </summary>
        public bool FiltersTakenOver
        {
            get => _filtersTakenOver;
            set
            {
                if (_filtersTakenOver != value)
                {
                    _filtersTakenOver = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether a new tab of the application of this tab takes over the filters
        /// </summary>
        public bool TakesOverFilters
        {
            get => _takesOverFilters;
            set
            {
                if (_takesOverFilters != value)
                {
                    _takesOverFilters = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// What happens when the application of this tab starts again
        /// </summary>
        public RestartBehavior RestartBehavior
        {
            get => _restartBehavior;
            set
            {
                if (_restartBehavior == value)
                    return;

                _restartBehavior = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RestartsInNewTab));
                OnPropertyChanged(nameof(RestartExtendsTab));
                OnPropertyChanged(nameof(RestartReplacesTab));
            }
        }

        /// <summary>
        /// Whether the tab receives logs, as opposed to a tab of an imported file
        /// </summary>
        public bool IsLive => SenderKey != null;

        /// <summary>
        /// The key of the column that the list in the tab is sorted by, or null for the order in which the entries came in; the viewer keeps it up to
        /// date and takes it when it is set. It is not saved: a sort lasts as long as the tab is open.
        /// </summary>
        public string? SortKey
        {
            get => _sortKey;
            set
            {
                if (_sortKey != value)
                {
                    _sortKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether the sort goes from the largest value to the smallest
        /// </summary>
        public bool SortDescending
        {
            get => _sortDescending;
            set
            {
                if (_sortDescending != value)
                {
                    _sortDescending = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether the logger tree of the viewer in the tab is on the left of the list; the viewer keeps it up to date and takes it when it is set
        /// </summary>
        public bool LoggerTreeOnLeft
        {
            get => _loggerTreeOnLeft;
            set
            {
                if (_loggerTreeOnLeft != value)
                {
                    _loggerTreeOnLeft = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether the logger tree of the viewer in the tab is open
        /// </summary>
        public bool LoggerTreeOpen
        {
            get => _loggerTreeOpen;
            set
            {
                if (_loggerTreeOpen != value)
                {
                    _loggerTreeOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Width of the logger tree of the viewer in the tab
        /// </summary>
        public double LoggerTreeWidth
        {
            get => _loggerTreeWidth;
            set
            {
                if (_loggerTreeWidth != value)
                {
                    _loggerTreeWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The fields that the filter and the search of the viewer in the tab look into; the viewer keeps it up to date and takes it when it is set
        /// </summary>
        /// <summary>
        /// The columns of the viewer in the tab, in their order; the viewer keeps it up to date and takes it when it is set
        /// </summary>
        public IReadOnlyList<string> ColumnKeys
        {
            get => _columnKeys;
            set
            {
                if (!_columnKeys.SequenceEqual(value))
                {
                    _columnKeys = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The zone in which the times without a zone of this tab were written (a file); null for the local time. Only what is shown changes.
        /// </summary>
        public TimeZoneInfo? FileTimeZone
        {
            get => _fileTimeZone;
            set
            {
                if (!Equals(_fileTimeZone, value))
                {
                    _fileTimeZone = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The zone in which the times of this tab are shown; null for the local time
        /// </summary>
        public TimeZoneInfo? DisplayTimeZone
        {
            get => _displayTimeZone;
            set
            {
                if (!Equals(_displayTimeZone, value))
                {
                    _displayTimeZone = value;
                    OnPropertyChanged();
                }
            }
        }

        public SearchFields SearchFields
        {
            get => _searchFields;
            set
            {
                if (_searchFields != value)
                {
                    _searchFields = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RestartsInNewTab => RestartBehavior == RestartBehavior.NewTab;
        public bool RestartExtendsTab => RestartBehavior == RestartBehavior.ExtendTab;
        public bool RestartReplacesTab => RestartBehavior == RestartBehavior.ReplaceTab;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Adds a log event to <see cref="LogEntries"/> (same reference the viewer binds to).
        /// </summary>
        public void AddLogEntry(LogEntry logEntry)
        {
            LogEntries.Add(logEntry);
            LastEventUtc = DateTime.UtcNow;
            while (MaxCount >= 0 && LogEntries.Count > MaxCount)
                LogEntries.RemoveAt(0);
        }
    }
}
