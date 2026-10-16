using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.Services.Updates;
using Sentinel.LogViewer.ViewModels.Settings;
using Sentinel.LogViewer.Wpf;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings window: categories with described settings that are saved as soon as they change
    /// </summary>
    public partial class SettingsViewModel : INotifyPropertyChanged
    {
        internal const string GeneralId = "General";
        internal const string ReceivingId = "Receiving";
        internal const string AppearanceId = "Appearance";
        internal const string UpdatesId = "Updates";

        /// <summary>
        /// The order leaves room for categories that later features add (e.g. Import, Highlighting)
        /// </summary>
        private sealed record CategoryDefinition(string Id, int Order, string Icon, string TitleKey, string Title, string SubtitleKey, string Subtitle);

        private static readonly CategoryDefinition[] CategoryDefinitions =
        {
            new(GeneralId, 10, "", "Settings_Category_General", "General",
                "Settings_Category_General_Subtitle", "Language and basic behaviour of the application."),
            new(ReceivingId, 20, "", "Settings_Category_Receiving", "Receiving",
                "Settings_Category_Receiving_Subtitle", "How and where logs reach the application."),
            new(AppearanceId, 30, "\uE790", "Settings_Category_Appearance", "Appearance",
                "Settings_Category_Appearance_Subtitle", "Theme and colours of the interface and the log list."),
            new(UpdatesId, 90, "", "Settings_Category_Updates", "Updates",
                "Settings_Category_Updates_Subtitle", "How the application looks for new versions.")
        };

        private readonly ConfigurationService _configService;
        private readonly LocalizationService _localizationService;
        private readonly SettingsTransferService _transferService;
        private readonly ThemeService _themeService;
        private readonly List<SettingViewModel> _settings;
        private AppConfiguration _configuration;
        private SettingsCategoryViewModel? _selectedCategory;
        private SettingsCategoryViewModel? _lastCategory;
        private string _searchText = string.Empty;
        private string _footerText;

        public SettingsViewModel(
            ConfigurationService configService,
            LocalizationService localizationService,
            SettingsTransferService transferService,
            ThemeService themeService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _configuration = _configService.LoadConfiguration();

            _settings = CreateSettings();
            Categories = CreateCategories();
            _footerText = Text("Settings_SavedHint", "Changes are saved immediately");

            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            ResetCategoryCommand = new RelayCommand(ResetCategory, () => CanResetCategory);
            ExportCommand = new RelayCommand(Export);
            ImportCommand = new RelayCommand(Import);

            SelectedCategory = Categories.FirstOrDefault();
        }

        /// <summary>
        /// The categories that have settings
        /// </summary>
        public IReadOnlyList<SettingsCategoryViewModel> Categories { get; }

        /// <summary>
        /// What the right side shows: the page header followed by the settings, or the search results
        /// </summary>
        public ObservableCollection<object> PageItems { get; } = new();

        /// <summary>
        /// Opens file and message dialogs; set by the window
        /// </summary>
        public ISettingsDialogs? Dialogs { get; set; }

        /// <summary>
        /// Whether any setting was changed while the window was open
        /// </summary>
        public bool HasChanges { get; private set; }

        public SettingsCategoryViewModel? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (ReferenceEquals(_selectedCategory, value))
                    return;

                _selectedCategory = value;
                if (value != null)
                {
                    _lastCategory = value;
                    if (_searchText.Length > 0)
                    {
                        _searchText = string.Empty;
                        OnPropertyChanged(nameof(SearchText));
                        OnPropertyChanged(nameof(IsSearching));
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(CanResetCategory));
                OnPropertyChanged(nameof(ResetButtonText));
                RebuildPage();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                value ??= string.Empty;
                if (_searchText == value)
                    return;

                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearching));

                // Searching looks through all categories; clearing the search returns to the last one.
                var category = IsSearching ? null : _selectedCategory ?? _lastCategory ?? Categories.FirstOrDefault();
                if (!ReferenceEquals(_selectedCategory, category))
                {
                    _selectedCategory = category;
                    if (category != null)
                        _lastCategory = category;

                    OnPropertyChanged(nameof(SelectedCategory));
                    OnPropertyChanged(nameof(CanResetCategory));
                    OnPropertyChanged(nameof(ResetButtonText));
                }

                RebuildPage();
            }
        }

        public bool IsSearching => _searchText.Trim().Length > 0;

        /// <summary>
        /// Whether a search found no setting
        /// </summary>
        public bool HasNoResults => IsSearching && !PageItems.OfType<SettingViewModel>().Any();

        public string NoResultsText => Format("Settings_NoResults", "No settings found for “{0}”.", _searchText.Trim());

        public bool CanResetCategory => _selectedCategory != null;

        public string ResetButtonText => _selectedCategory == null
            ? string.Empty
            : Format("Settings_ResetCategory", "Reset {0}", _selectedCategory.Title);

        /// <summary>
        /// Text at the bottom of the window; names the outcome of the last export, import or reset
        /// </summary>
        public string FooterText
        {
            get => _footerText;
            private set
            {
                _footerText = value;
                OnPropertyChanged();
            }
        }

        public ICommand ClearSearchCommand { get; }
        public ICommand ResetCategoryCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }

        private List<SettingViewModel> CreateSettings()
        {
            var entryLimits = new object[]
            {
                SettingsRules.MinEntriesPerTab.ToString("N0", CultureInfo.CurrentCulture),
                SettingsRules.MaxEntriesPerTab.ToString("N0", CultureInfo.CurrentCulture)
            };

            var settings = new List<SettingViewModel>
            {
                new ChoiceSettingViewModel(GeneralId, "",
                    Text("Settings_Language", "Language"),
                    Text("Settings_Language_Description", "Language of the menus and dialogs. Fully applied after restarting the application."),
                    needsRestart: true,
                    options: LocalizationService.AvailableLanguages.Keys
                        .Select(code => new ChoiceOption(code, LocalizationService.LanguageNames.GetValueOrDefault(code, code)))
                        .ToList(),
                    get: () => string.IsNullOrEmpty(_configuration.Language) ? _localizationService.CurrentLanguageCode : _configuration.Language,
                    set: code =>
                    {
                        _configuration.Language = code;
                        Commit();
                        _localizationService.SetLanguage(code);
                    },
                    getDefault: LocalizationService.DetectWindowsLanguage),

                new BoolSettingViewModel(ReceivingId, "",
                    Text("Settings_AutoStartListening", "Start listening on launch"),
                    Text("Settings_AutoStartListening_Description", "Starts receiving logs on all ports as soon as the application opens, without clicking “Start listening”."),
                    get: () => _configuration.AutoStartListening,
                    set: value =>
                    {
                        _configuration.AutoStartListening = value;
                        Commit();
                    },
                    getDefault: () => ConfigurationService.CreateDefaultConfiguration().AutoStartListening),

                new PortListSettingViewModel(ReceivingId, "",
                    Text("Settings_Ports", "Ports"),
                    Text("Settings_Ports_Description", "Addresses the application listens on for logs, for example udp://0.0.0.0:4000. Changes apply the next time listening is started."),
                    get: () => _configuration.Ports,
                    set: ports =>
                    {
                        _configuration.Ports = ports;
                        Commit();
                    },
                    getDefault: () => ConfigurationService.CreateDefaultConfiguration().Ports,
                    invalidAddressMessage: Text("Settings_InvalidPort", "Enter an address like udp://0.0.0.0:4000 with a port from 1 to 65535."),
                    duplicatePortMessage: Text("Settings_DuplicatePort", "This port is already in the list.")),

                new IntSettingViewModel(ReceivingId, "",
                    Text("Settings_MaxEntries", "Maximum entries per tab"),
                    Text("Settings_MaxEntries_Description", "Older entries are removed once a tab holds more. Protects memory during long live sessions. Applies to tabs opened afterwards."),
                    get: () => _configuration.MaxLogEntriesPerTab,
                    set: value =>
                    {
                        _configuration.MaxLogEntriesPerTab = value;
                        Commit();
                    },
                    getDefault: () => ConfigurationService.CreateDefaultConfiguration().MaxLogEntriesPerTab,
                    isValid: SettingsRules.IsValidEntriesPerTab,
                    invalidMessage: string.Format(CultureInfo.CurrentCulture,
                        Text("Settings_InvalidNumber", "Enter a whole number between {0} and {1}."), entryLimits)),

                new BoolSettingViewModel(UpdatesId, "",
                    Text("Settings_CheckForUpdates", "Automatically check for updates"),
                    Text("Settings_CheckForUpdates_Description", "Asks GitHub once when the application starts whether a newer version exists. Help → About always checks."),
                    get: () => _configuration.CheckForUpdates,
                    set: value =>
                    {
                        _configuration.CheckForUpdates = value;
                        Commit();
                    },
                    getDefault: () => ConfigurationService.CreateDefaultConfiguration().CheckForUpdates),

                new BoolSettingViewModel(UpdatesId, "",
                    Text("Settings_IncludePrereleaseUpdates", "Also offer prerelease versions"),
                    Text("Settings_IncludePrereleaseUpdates_Description", "Includes beta versions when looking for updates. Switched on by default while you run a prerelease."),
                    get: () => UpdateService.IncludesPrereleases(_configuration),
                    set: value =>
                    {
                        _configuration.IncludePrereleaseUpdates = value;
                        Commit();
                    },
                    getDefault: () => UpdateService.CurrentVersion.IsPrerelease,
                    // Without an explicit value the default follows the running version.
                    reset: () =>
                    {
                        _configuration.IncludePrereleaseUpdates = null;
                        Commit();
                    }),

                new BoolSettingViewModel(UpdatesId, "\uE81C",
                    Text("Settings_ShowChangelogAfterUpdate", "Show what’s new after an update"),
                    Text("Settings_ShowChangelogAfterUpdate_Description", "Opens the changelog at the first start of a new version, with everything that changed since the version before."),
                    get: () => _configuration.ShowChangelogAfterUpdate,
                    set: value =>
                    {
                        _configuration.ShowChangelogAfterUpdate = value;
                        Commit();
                    },
                    getDefault: () => ConfigurationService.CreateDefaultConfiguration().ShowChangelogAfterUpdate)
            };

            settings.AddRange(CreateAppearanceSettings());
            return settings;
        }

        private IReadOnlyList<SettingsCategoryViewModel> CreateCategories()
        {
            return CategoryDefinitions
                .OrderBy(definition => definition.Order)
                .Select(definition => (Definition: definition, Settings: _settings.Where(s => s.CategoryId == definition.Id).ToList()))
                .Where(entry => entry.Settings.Count > 0)
                .Select(entry => new SettingsCategoryViewModel(
                    entry.Definition.Id,
                    entry.Definition.Icon,
                    Text(entry.Definition.TitleKey, entry.Definition.Title),
                    Text(entry.Definition.SubtitleKey, entry.Definition.Subtitle),
                    entry.Settings))
                .ToList();
        }

        private void Commit()
        {
            _configService.SaveConfiguration(_configuration);
            HasChanges = true;
        }

        private void RebuildPage()
        {
            PageItems.Clear();
            var query = _searchText.Trim();
            if (query.Length == 0)
            {
                if (_selectedCategory != null)
                {
                    PageItems.Add(new SettingsPageHeader(_selectedCategory.Title, _selectedCategory.Subtitle));
                    string? section = null;
                    foreach (var setting in _selectedCategory.Settings)
                    {
                        if (setting.Section != null && setting.Section != section)
                            PageItems.Add(new SettingsSectionLabel(setting.Section));

                        section = setting.Section;
                        PageItems.Add(setting);
                    }
                }
            }
            else
            {
                PageItems.Add(new SettingsPageHeader(
                    Text("Settings_SearchResultsTitle", "Search results"),
                    Format("Settings_SearchResultsSubtitle", "Settings whose title or description contain “{0}”.", query)));

                foreach (var category in Categories)
                {
                    var matches = category.Settings.Where(setting => setting.Matches(query)).ToList();
                    if (matches.Count == 0)
                        continue;

                    PageItems.Add(new SettingsSectionLabel(category.Title));
                    foreach (var setting in matches)
                        PageItems.Add(setting);
                }
            }

            OnPropertyChanged(nameof(HasNoResults));
            OnPropertyChanged(nameof(NoResultsText));
        }

        private void ResetCategory()
        {
            var category = _selectedCategory;
            if (category == null || Dialogs == null)
                return;

            var confirmed = Dialogs.Confirm(
                Text("Settings_ResetConfirmCaption", "Reset settings"),
                Format("Settings_ResetConfirmMessage", "Reset all settings in “{0}” to their defaults?", category.Title));
            if (!confirmed)
                return;

            foreach (var setting in category.Settings)
                setting.ResetToDefault();

            FooterText = Format("Settings_ResetDone", "{0} was reset to its defaults.", category.Title);
        }

        private void Export()
        {
            if (Dialogs == null)
                return;

            var path = Dialogs.PickFileToExport(
                Text("Settings_ExportTitle", "Export settings"),
                Text("Settings_FileFilter", "Settings files (*.json)|*.json|All files (*.*)|*.*"),
                $"{ProductNames.Technical}-settings.json");
            if (path == null)
                return;

            try
            {
                _transferService.Export(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dialogs.ShowError(Text("Settings_ExportTitle", "Export settings"),
                    Format("Settings_ExportFailed", "The settings could not be exported: {0}", ex.Message));
                return;
            }

            FooterText = Format("Settings_Exported", "Settings exported to {0}", Path.GetFileName(path));
        }

        private void Import()
        {
            if (Dialogs == null)
                return;

            var caption = Text("Settings_ImportTitle", "Import settings");
            var path = Dialogs.PickFileToImport(caption, Text("Settings_FileFilter", "Settings files (*.json)|*.json|All files (*.*)|*.*"));
            if (path == null)
                return;

            var confirmed = Dialogs.Confirm(caption,
                Text("Settings_ImportConfirmMessage", "Importing replaces all current settings and saved column mappings. Continue?"));
            if (!confirmed)
                return;

            try
            {
                _transferService.Import(path);
            }
            catch (InvalidDataException)
            {
                Dialogs.ShowError(caption, Text("Settings_ImportInvalidFile", "This is not a settings file exported from Sentinel LogViewer."));
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dialogs.ShowError(caption, Format("Settings_ImportFailed", "The file could not be imported: {0}", ex.Message));
                return;
            }

            _configuration = _configService.LoadConfiguration();
            if (!string.IsNullOrEmpty(_configuration.Language))
                _localizationService.SetLanguage(_configuration.Language);

            _themeService.Apply(_configuration);
            foreach (var setting in _settings)
                setting.Reload();

            HasChanges = true;
            FooterText = Text("Settings_Imported", "Settings imported. Restart the application to apply all changes.");
        }

        private string Text(string key, string defaultValue) => _localizationService.GetString(key, defaultValue);

        private string Format(string key, string defaultValue, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, Text(key, defaultValue), arguments);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
