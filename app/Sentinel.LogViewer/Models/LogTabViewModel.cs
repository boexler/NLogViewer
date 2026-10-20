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
        private int _visibleEntryCount;

        /// <summary>
        /// Backing collection bound to <see cref="Sentinel.LogViewer.Wpf.LogViewer.ItemsSource"/>.
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public LogTabViewModel()
        {
            LogEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogCount));
        }

        /// <summary>
        /// How many entries the viewer in the tab shows after its filters; the viewer keeps it up to date
        /// </summary>
        public int VisibleEntryCount
        {
            get => _visibleEntryCount;
            set
            {
                if (_visibleEntryCount != value)
                {
                    _visibleEntryCount = value;
                    OnPropertyChanged();
                }
            }
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
