using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// ViewModel for the Language Selection window
    /// </summary>
    public class LanguageSelectionViewModel : INotifyPropertyChanged
    {
        private LanguageItem? _selectedLanguage;

        /// <summary>
        /// Language names mapping
        /// </summary>
        private static readonly Dictionary<string, string> LanguageNames = new()
        {
            { "de", "Deutsch" },
            { "en", "English" },
            { "es", "Español" },
            { "fr", "Français" },
            { "hu", "Magyar" },
            { "pl", "Polski" },
            { "ro", "Română" }
        };

        private readonly LocalizationService _localizationService;

        public LanguageSelectionViewModel(LocalizationService localizationService)
        {
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
            Languages = new ObservableCollection<LanguageItem>();
            
            foreach (var lang in Services.LocalizationService.AvailableLanguages)
            {
                Languages.Add(new LanguageItem
                {
                    Code = lang.Key,
                    Name = LanguageNames.TryGetValue(lang.Key, out var name) ? name : lang.Key,
                    FlagPath = lang.Value
                });
            }

            // Set current language as selected
            var currentCode = _localizationService.CurrentLanguageCode;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == currentCode) ?? Languages.First();
        }

        public ObservableCollection<LanguageItem> Languages { get; }

        public LanguageItem? SelectedLanguage
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a language item with code, name, and flag path
    /// </summary>
    public class LanguageItem
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FlagPath { get; set; } = string.Empty;
    }
}

