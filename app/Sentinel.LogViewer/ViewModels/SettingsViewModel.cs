using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.NLogViewer.Wpf;
using Sentinel.LogViewer.Models;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings window
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configService;
        private readonly LocalizationService _localizationService;
        private AppConfiguration _configuration;
        private string _selectedLanguage = "en";
        private PortConfiguration? _selectedPort;

        public SettingsViewModel(
            ConfigurationService configService,
            LocalizationService localizationService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _configuration = _configService.LoadConfiguration();
            // Use current language from LocalizationService if config language is empty
            _selectedLanguage = string.IsNullOrEmpty(_configuration.Language) 
                ? _localizationService.CurrentLanguageCode 
                : _configuration.Language;

            Ports = new ObservableCollection<PortConfiguration>();
            foreach (var portAddress in _configuration.Ports)
            {
                Ports.Add(new PortConfiguration { Address = portAddress });
            }

            Languages = new ObservableCollection<string> { "de", "en", "fr", "es", "hu", "ro", "pl" };

            AddPortCommand = new RelayCommand(AddPort, () => true);
            RemovePortCommand = new RelayCommand(RemovePort, () => SelectedPort != null);
            // SaveCommand and CancelCommand will be set by the window
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => { });
        }

        public ObservableCollection<PortConfiguration> Ports { get; }
        public ObservableCollection<string> Languages { get; }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        public PortConfiguration? SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (_selectedPort != value)
                {
                    _selectedPort = value;
                    OnPropertyChanged();
                    ((RelayCommand)RemovePortCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int MaxLogEntriesPerTab
        {
            get => _configuration.MaxLogEntriesPerTab;
            set
            {
                if (_configuration.MaxLogEntriesPerTab != value)
                {
                    _configuration.MaxLogEntriesPerTab = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoStartListening
        {
            get => _configuration.AutoStartListening;
            set
            {
                if (_configuration.AutoStartListening != value)
                {
                    _configuration.AutoStartListening = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand AddPortCommand { get; }
        public ICommand RemovePortCommand { get; }
        public ICommand SaveCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        private void AddPort()
        {
            var newPort = new PortConfiguration { Address = "udp://0.0.0.0:4000" };
            Ports.Add(newPort);
            SelectedPort = newPort;
        }

        private void RemovePort()
        {
            if (SelectedPort != null)
            {
                Ports.Remove(SelectedPort);
                SelectedPort = Ports.FirstOrDefault();
            }
        }

        public void Save()
        {
            _configuration.Ports = Ports.Select(p => p.Address).ToList();
            _configuration.Language = SelectedLanguage;
            _configService.SaveConfiguration(_configuration);

            // Apply language change
            _localizationService.SetLanguage(SelectedLanguage);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

