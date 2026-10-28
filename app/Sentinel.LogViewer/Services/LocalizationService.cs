using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows;

namespace Sentinel.LogViewer.Services
{
    /// <summary>
    /// Service for managing application localization
    /// </summary>
    public class LocalizationService : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configService;
        private ResourceManager? _resourceManager;
        private CultureInfo _currentCulture;

        /// <summary>
        /// Available languages with their flag SVG paths
        /// </summary>
        public static readonly Dictionary<string, string> AvailableLanguages = new()
        {
            { "de", "pack://application:,,,/Resources/flags/de.svg" },  // German
            { "en", "pack://application:,,,/Resources/flags/gb.svg" },  // English (GB)
            { "es", "pack://application:,,,/Resources/flags/es.svg" },  // Spanish
            { "fr", "pack://application:,,,/Resources/flags/fr.svg" },  // French
            { "hu", "pack://application:,,,/Resources/flags/hu.svg" },  // Hungarian
            { "pl", "pack://application:,,,/Resources/flags/pl.svg" },  // Polish
            { "ro", "pack://application:,,,/Resources/flags/ro.svg" }   // Romanian
        };

        /// <summary>
        /// Names of the languages in the language itself
        /// </summary>
        public static readonly Dictionary<string, string> LanguageNames = new()
        {
            { "de", "Deutsch" },
            { "en", "English" },
            { "es", "Español" },
            { "fr", "Français" },
            { "hu", "Magyar" },
            { "pl", "Polski" },
            { "ro", "Română" }
        };

        public LocalizationService(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _currentCulture = CultureInfo.CurrentCulture;
        }

        /// <summary>
        /// Detects the best matching language based on Windows culture settings
        /// </summary>
        /// <returns>Language code (e.g., "en", "de") or "en" as default</returns>
        public static string DetectWindowsLanguage()
        {
            try
            {
                var windowsCulture = CultureInfo.CurrentUICulture;
                var languageCode = windowsCulture.TwoLetterISOLanguageName.ToLowerInvariant();

                // Check if we have resources for this language
                if (AvailableLanguages.ContainsKey(languageCode))
                {
                    return languageCode;
                }

                // Try to find a match by checking parent cultures
                var culture = windowsCulture;
                while (culture != null && culture != CultureInfo.InvariantCulture)
                {
                    var code = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                    if (AvailableLanguages.ContainsKey(code))
                    {
                        return code;
                    }
                    culture = culture.Parent;
                }

                // Default to English if no match found
                return "en";
            }
            catch
            {
                return "en";
            }
        }

        public void Initialize()
        {
            try
            {
                // Try to load resources
                _resourceManager = new ResourceManager(
                    "Sentinel.LogViewer.Resources.Resources",
                    typeof(LocalizationService).Assembly);

                // Set culture based on configuration or system default
                var config = _configService.LoadConfiguration();
                
                string languageToUse;
                if (string.IsNullOrEmpty(config.Language))
                {
                    // Auto-detect language from Windows settings
                    languageToUse = DetectWindowsLanguage();
                    // Save the detected language to configuration
                    config.Language = languageToUse;
                    _configService.SaveConfiguration(config);
                }
                else
                {
                    languageToUse = config.Language;
                }

                SetLanguage(languageToUse);
            }
            catch (Exception)
            {
                // If resources are not available, continue with default culture
                SetLanguage("en");
            }
        }

        /// <summary>
        /// Whether setting the language also sets the language of the control, which has its own texts. Only the application turns it on:
        /// the language of the control is one for the whole process, and tests of the settings must not change it.
        /// </summary>
        public bool SetsLanguageOfControl { get; set; }

        public void SetLanguage(string languageCode)
        {
            try
            {
                // Validate that the language is available
                if (!AvailableLanguages.ContainsKey(languageCode))
                {
                    languageCode = "en";
                }

                _currentCulture = new CultureInfo(languageCode);
                Thread.CurrentThread.CurrentCulture = _currentCulture;
                Thread.CurrentThread.CurrentUICulture = _currentCulture;
                CultureInfo.DefaultThreadCurrentCulture = _currentCulture;
                CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;

                // Save to configuration
                var config = _configService.LoadConfiguration();
                config.Language = languageCode;
                _configService.SaveConfiguration(config);
            }
            catch
            {
                // Invalid language code, use default
                _currentCulture = new CultureInfo("en");
                Thread.CurrentThread.CurrentCulture = _currentCulture;
                Thread.CurrentThread.CurrentUICulture = _currentCulture;
            }

            // Every text that is bound to the indexer, and every view model that keeps a text, takes the new language.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(System.Windows.Data.Binding.IndexerName));

            if (SetsLanguageOfControl)
                Sentinel.LogViewer.Wpf.LogViewerTexts.Language = Sentinel.LogViewer.Wpf.LogViewerTexts.FromCode(CurrentLanguageCode);
        }

        /// <summary>
        /// The text of the key in the current language, or null when there is none; a binding to <c>[key]</c> shows the new text as soon as the
        /// language is set
        /// </summary>
        public string? this[string key]
        {
            get
            {
                try
                {
                    return _resourceManager?.GetString(key, _currentCulture);
                }
                catch
                {
                    return null;
                }
            }
        }

        public string GetString(string key, string defaultValue = "") => this[key] ?? defaultValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        public CultureInfo CurrentCulture => _currentCulture;

        /// <summary>
        /// Gets the current language code (e.g., "en", "de")
        /// </summary>
        public string CurrentLanguageCode => _currentCulture.TwoLetterISOLanguageName.ToLowerInvariant();

        /// <summary>
        /// Gets the flag SVG path for the current language
        /// </summary>
        public string CurrentLanguageFlag => AvailableLanguages.TryGetValue(CurrentLanguageCode, out var flag) ? flag : AvailableLanguages["en"];
    }
}
