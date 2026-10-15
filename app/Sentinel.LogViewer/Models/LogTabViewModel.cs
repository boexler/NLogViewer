using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NLog;

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
            while (MaxCount >= 0 && LogEntries.Count > MaxCount)
                LogEntries.RemoveAt(0);
        }
    }
}
