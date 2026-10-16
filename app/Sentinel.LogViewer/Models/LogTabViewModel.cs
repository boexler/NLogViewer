using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NLog;
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

        /// <summary>
        /// Backing collection bound to <see cref="Sentinel.LogViewer.Wpf.LogViewerBase.ItemsSource"/>.
        /// </summary>
        public ObservableCollection<LogEventInfo> LogEntries { get; } = new();

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
        public void AddLogEvent(LogEventInfo logEvent)
        {
            LogEntries.Add(logEvent);
            LastEventUtc = DateTime.UtcNow;
            while (MaxCount >= 0 && LogEntries.Count > MaxCount)
                LogEntries.RemoveAt(0);
        }
    }
}
