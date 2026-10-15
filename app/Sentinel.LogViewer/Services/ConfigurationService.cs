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
                AutoStartTestLogging = false,
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
        public bool AutoStartTestLogging { get; set; } = false;

        /// <summary>When true, GitHub Releases are checked for a newer version on startup.</summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>Whether prerelease versions are offered as updates; null follows the running version.</summary>
        public bool? IncludePrereleaseUpdates { get; set; }

        /// <summary>Whether the application is light, dark or follows Windows, see <see cref="Theming.ThemeCatalog"/>.</summary>
        public string ThemeMode { get; set; } = Theming.ThemeCatalog.ModeSystem;

        /// <summary>Id of the colour theme that is used in light mode, see <see cref="Theming.ThemeCatalog"/>.</summary>
        public string LightTheme { get; set; } = Theming.ThemeCatalog.DefaultThemeId;

        /// <summary>Colours the user changed: theme id, then token key, then the colour as #RRGGBB or #AARRGGBB.</summary>
        public Dictionary<string, Dictionary<string, string>> ThemeColors { get; set; } = new();
    }
}
