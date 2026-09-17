using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.NLogViewer.TestLogging;
using Sentinel.NLogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels;

/// <summary>
/// ViewModel for the debug Test Logging panel: options and Start/Stop commands.
/// </summary>
public sealed class TestLoggingViewModel : INotifyPropertyChanged
{
    private readonly TestLoggingService _service;
    private string _targetName = "chainsaw";
    private string _udpHost = "127.0.0.1";
    private int _udpPort = 4000;
    private int _messageIntervalMs = 1000;
    private double _exceptionProbability = 0.2;

    /// <summary>Available NLog target names for the dropdown.</summary>
    public IReadOnlyList<string> TargetNames { get; } = new[] { "chainsaw", "log4jxml" };

    public TestLoggingViewModel(TestLoggingService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        StartCommand = new RelayCommand(Start, () => !_service.IsRunning);
        StopCommand = new RelayCommand(Stop, () => _service.IsRunning);

        _service.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(TestLoggingService.IsRunning) or nameof(TestLoggingService.MessageCount) or nameof(TestLoggingService.ExceptionCount))
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged(nameof(StatusText));
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            }
        };
    }

    public string TargetName
    {
        get => _targetName;
        set { _targetName = value ?? "chainsaw"; OnPropertyChanged(nameof(TargetName)); }
    }

    public string UdpHost
    {
        get => _udpHost;
        set { _udpHost = value ?? "127.0.0.1"; OnPropertyChanged(nameof(UdpHost)); }
    }

    public int UdpPort
    {
        get => _udpPort;
        set { _udpPort = value; OnPropertyChanged(nameof(UdpPort)); }
    }

    public int MessageIntervalMs
    {
        get => _messageIntervalMs;
        set { _messageIntervalMs = value; OnPropertyChanged(nameof(MessageIntervalMs)); }
    }

    public double ExceptionProbability
    {
        get => _exceptionProbability;
        set { _exceptionProbability = value; OnPropertyChanged(nameof(ExceptionProbability)); OnPropertyChanged(nameof(ExceptionProbabilityPercent)); }
    }

    /// <summary>Exception probability as percentage 0-100 for UI binding.</summary>
    public string ExceptionProbabilityPercent
    {
        get => ((int)(_exceptionProbability * 100)).ToString();
        set
        {
            if (int.TryParse(value, out var pct))
            {
                _exceptionProbability = Math.Clamp(pct / 100.0, 0, 1);
                OnPropertyChanged(nameof(ExceptionProbability));
            }
            OnPropertyChanged(nameof(ExceptionProbabilityPercent));
        }
    }

    /// <summary>Status text for the panel.</summary>
    public string StatusText => _service.IsRunning ? "Running" : "Stopped";

    public bool IsRunning => _service.IsRunning;
    public int MessageCount => _service.MessageCount;
    public int ExceptionCount => _service.ExceptionCount;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    private void Start()
    {
        var options = new TestLoggingOptions
        {
            TargetName = TargetName,
            UdpHost = UdpHost,
            UdpPort = UdpPort,
            MessageIntervalMs = MessageIntervalMs,
            ExceptionProbability = Math.Clamp(ExceptionProbability, 0, 1)
        };
        _service.Start(options);
    }

    private void Stop() => _service.Stop();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
