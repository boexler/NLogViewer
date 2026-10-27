using Sentinel.LogViewer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Sentinel.LogViewer;

namespace Sentinel.LogViewer.Services
{
    /// <summary>
    /// Service for loading and saving application configuration.
    /// When portable mode is active, settings are stored next to the application (see <see cref="IsPortableMode"/>).
    /// Otherwise settings use LocalApplicationData (per-user, suitable for installed copies).
    /// </summary>
    public class ConfigurationService
    {
        /// <summary>Name of the marker file shipped with portable ZIP distributions.</summary>
        public const string PortableMarkerFileName = "NLogViewer.portable";

        /// <summary>Alternative portable marker (hidden-style dotfile).</summary>
        public const string PortableMarkerDotFileName = ".portable";

        /// <summary>
        /// When set to <c>1</c> or <c>true</c>, forces portable configuration layout using the app base directory.
        /// </summary>
        public const string PortableEnvironmentVariableName = "NLOGVIEWER_PORTABLE";

        private readonly string _configPath;

        public ConfigurationService()
            : this(AppContext.BaseDirectory)
        {
        }

        /// <summary>
        /// For tests: <paramref name="applicationBaseDirectory"/> is used for portable detection and for the portable config path.
        /// </summary>
        /// <param name="applicationBaseDirectory">Typically <see cref="AppContext.BaseDirectory"/>.</param>
        internal ConfigurationService(string applicationBaseDirectory)
        {
            _configPath = ResolveConfigPath(applicationBaseDirectory);
        }

        /// <summary>
        /// The folder of the configuration: beside the application in portable mode, otherwise in the AppData folder of the user
        /// </summary>
        public string DataDirectory => Path.GetDirectoryName(_configPath)!;

        /// <summary>
        /// Returns true when configuration should be stored beside the application (portable ZIP / USB) rather than in AppData.
        /// </summary>
        public static bool IsPortableMode(string applicationBaseDirectory)
        {
            if (IsPortableEnvironmentOverride())
                return true;
            if (string.IsNullOrEmpty(applicationBaseDirectory))
                return false;
            if (File.Exists(Path.Combine(applicationBaseDirectory, PortableMarkerDotFileName)))
                return true;
            if (File.Exists(Path.Combine(applicationBaseDirectory, PortableMarkerFileName)))
                return true;
            return false;
        }

        /// <summary>
        /// Returns the full path to <c>appsettings.json</c> for either portable or per-user storage.
        /// </summary>
        internal static string ResolveConfigPath(string applicationBaseDirectory)
        {
            if (IsPortableMode(applicationBaseDirectory))
                return Path.Combine(applicationBaseDirectory, "appsettings.json");

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataPath = Path.Combine(localAppData, ProductNames.Technical);
            Directory.CreateDirectory(appDataPath);
            var configPath = Path.Combine(appDataPath, "appsettings.json");
            MigrateLegacyAppDataFile(localAppData, configPath);
            return configPath;
        }

        /// <summary>
        /// Copies the file named like <paramref name="newFilePath"/> from the pre-rename AppData folder once
        /// when the new folder has none.
        /// </summary>
        internal static void MigrateLegacyAppDataFile(string localAppData, string newFilePath)
        {
            if (File.Exists(newFilePath))
                return;

            var legacyFilePath = Path.Combine(localAppData, ProductNames.LegacyAppDataFolder, Path.GetFileName(newFilePath));
            if (!File.Exists(legacyFilePath))
                return;

            try
            {
                File.Copy(legacyFilePath, newFilePath);
            }
            catch (Exception)
            {
                // Keep going with defaults if the one-time migrate fails.
            }
        }

        private static bool IsPortableEnvironmentOverride()
        {
            var value = Environment.GetEnvironmentVariable(PortableEnvironmentVariableName);
            if (string.IsNullOrEmpty(value))
                return false;
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public AppConfiguration LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                    if (config != null)
                    {
                        // Ensure Language is set, default to empty for auto-detection
                        if (string.IsNullOrEmpty(config.Language))
                        {
                            config.Language = string.Empty;
                        }
                        Theming.ThemeCatalog.Sanitize(config);
                        ApplicationTabService.Sanitize(config);
                        if (!SettingsRules.IsValidLoggerTreeWidth(config.LoggerTreeWidth))
                            config.LoggerTreeWidth = SettingsRules.DefaultLoggerTreeWidth;
                        if (!SettingsRules.IsValidListFontSize(config.ListFontSize))
                            config.ListFontSize = SettingsRules.DefaultListFontSize;
                        config.ListFontFamily ??= string.Empty;
                        if (!SettingsRules.IsValidTrayNotifyInterval(config.TrayNotifyIntervalSeconds))
                            config.TrayNotifyIntervalSeconds = SettingsRules.DefaultTrayNotifyInterval;
                        config.LevelDisplay = SettingsRules.ParseLevelDisplay(config.LevelDisplay).ToString();
                        config.RowHeight = SettingsRules.ParseRowHeight(config.RowHeight).ToString();
                        config.SearchFields = SettingsRules.ParseSearchFields(config.SearchFields).ToString();
                        config.Columns = Wpf.LogColumns.Normalize(config.Columns);
                        FilterPresetService.Sanitize(config);
                        IgnoreRule.Sanitize(config);
                        return config;
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to default configuration
            }

            return CreateDefaultConfiguration();
        }

        /// <summary>
        /// Returns the settings of a fresh installation
        /// </summary>
        public static AppConfiguration CreateDefaultConfiguration()
        {
            return new AppConfiguration
            {
                Ports = new List<string> { SettingsRules.DefaultPortAddress },
                Language = string.Empty, // Empty means auto-detect on first start
                MaxLogEntriesPerTab = 10000,
                AutoStartListening = false,
                CheckForUpdates = true
            };
        }

        public void SaveConfiguration(AppConfiguration config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception)
            {
                // Log error but don't throw
            }
        }
    }

    /// <summary>
    /// Application configuration model
    /// </summary>
    public class AppConfiguration
    {
        public List<string> Ports { get; set; } = new();
        public string Language { get; set; } = string.Empty; // Empty means auto-detect
        public int MaxLogEntriesPerTab { get; set; } = 10000;
        public bool AutoStartListening { get; set; } = false;

        /// <summary>When true (Debug only), auto-start the test log generator on startup.</summary>

        /// <summary>When true, GitHub Releases are checked for a newer version on startup.</summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>Whether the changelog opens at the first start after an update.</summary>
        public bool ShowChangelogAfterUpdate { get; set; } = true;

        /// <summary>The release version that ran last, to see that an update happened. Stays on this computer.</summary>
        public string LastSeenVersion { get; set; } = string.Empty;

        /// <summary>Whether prerelease versions are offered as updates; null follows the running version.</summary>
        public bool? IncludePrereleaseUpdates { get; set; }

        /// <summary>Whether the application is light, dark or follows Windows, see <see cref="Theming.ThemeCatalog"/>.</summary>
        public string ThemeMode { get; set; } = Theming.ThemeCatalog.ModeSystem;

        /// <summary>Id of the design, warm, newschool or oldschool; it exists in a light and in a dark version, see <see cref="Theming.ThemeCatalog"/>.</summary>
        public string Theme { get; set; } = Theming.ThemeCatalog.DefaultThemeId;

        /// <summary>Default format of the timestamp column: system, iso, german or english.</summary>
        public string TimeStampFormat { get; set; } = "system";

        /// <summary>Colours the user changed: theme id, then token key, then the colour as #RRGGBB or #AARRGGBB.</summary>
        public Dictionary<string, Dictionary<string, string>> ThemeColors { get; set; } = new();

        /// <summary>Whether the logger tree of a new tab is on the left of the list instead of the right.</summary>
        public bool LoggerTreeOnLeft { get; set; } = false;

        /// <summary>Whether the logger tree of a new tab is open.</summary>
        public bool LoggerTreeOpen { get; set; } = false;

        /// <summary>Width of the open logger tree of a new tab in pixels.</summary>
        public int LoggerTreeWidth { get; set; } = SettingsRules.DefaultLoggerTreeWidth;

        /// <summary>Whether minimizing the window moves it to the notification area, where it keeps receiving.</summary>
        public bool MinimizeToTray { get; set; } = false;

        /// <summary>The lowest level of the entries that show a notification while the window is in the tray: Warn, Error or Fatal; Off for none.</summary>
        public string TrayNotifyLevel { get; set; } = SettingsRules.TrayNotifyOff;

        /// <summary>The shortest time between two notifications in the tray, in seconds.</summary>
        public int TrayNotifyIntervalSeconds { get; set; } = SettingsRules.DefaultTrayNotifyInterval;

        /// <summary>The newest version that the user chose to skip: the application does not announce it or an older one (empty: none).</summary>
        public string SkippedVersion { get; set; } = string.Empty;

        /// <summary>How the level column of the log list shows the level: Symbol, Text or SymbolAndText.</summary>
        public string LevelDisplay { get; set; } = nameof(Wpf.LevelDisplayMode.Symbol);

        /// <summary>How high the rows of the log list are: Normal or Compact.</summary>
        public string RowHeight { get; set; } = nameof(Wpf.RowHeightMode.Normal);

        /// <summary>The font family of the log list; empty is the font of the application.</summary>
        public string ListFontFamily { get; set; } = string.Empty;

        /// <summary>The font size of the log list.</summary>
        public int ListFontSize { get; set; } = SettingsRules.DefaultListFontSize;

        /// <summary>The fields that the filter and the search of a new tab look into, as the names of <see cref="Core.SearchFields"/> (default: Message and logger name).</summary>
        public string SearchFields { get; set; } = nameof(Core.SearchFields.Default);

        /// <summary>The columns of the log list of a new tab and their order, as keys of <see cref="Wpf.LogColumns"/> (<c>thread</c>, <c>property:Class</c>, ...).</summary>
        public List<string> Columns { get; set; } = new(Wpf.LogColumns.Default);

        /// <summary>The saved filter presets, see <see cref="FilterPresetService"/>.</summary>
        public List<FilterPreset> FilterPresets { get; set; } = new();

        /// <summary>The senders whose logs are dropped when they arrive, see <see cref="IgnoreRule"/>.</summary>
        public List<IgnoreRule> IgnoreRules { get; set; } = new();

        /// <summary>What happens when an application starts again: a new tab (NewTab), the existing tab (ExtendTab) or a new tab that replaces the old one (ReplaceTab).</summary>
        public string RestartBehavior { get; set; } = nameof(Services.RestartBehavior.NewTab);

        /// <summary>Whether a new tab of an application starts with the filters of its last tab.</summary>
        public bool TakeOverFilters { get; set; } = true;

        /// <summary>Per application (name and sender): the choices that differ from the settings above and the filters to take over.</summary>
        public Dictionary<string, ApplicationTabSettings> Applications { get; set; } = new();
    }
}
