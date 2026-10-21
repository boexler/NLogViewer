using Sentinel.LogViewer.Core;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sentinel.LogViewer.Services;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.ViewModels.Settings;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// The appearance settings: light or dark mode, the theme and a card for every colour of the theme in use
    /// </summary>
    public partial class SettingsViewModel
    {
        private const string LoggerTreeLeft = "left";
        private const string LoggerTreeRight = "right";

        private IEnumerable<SettingViewModel> CreateAppearanceSettings()
        {
            yield return new ChoiceSettingViewModel(AppearanceId, "\uE790",
                Text("Settings_ThemeMode", "Colour mode"),
                Text("Settings_ThemeMode_Description", "Light or dark, or the same as Windows. The colours below belong to the theme that is in use."),
                needsRestart: false,
                options: new[]
                {
                    new ChoiceOption(ThemeCatalog.ModeSystem, Text("Settings_ThemeMode_System", "Like Windows")),
                    new ChoiceOption(ThemeCatalog.ModeLight, Text("Settings_ThemeMode_Light", "Light")),
                    new ChoiceOption(ThemeCatalog.ModeDark, Text("Settings_ThemeMode_Dark", "Dark"))
                },
                get: () => _configuration.ThemeMode,
                set: mode =>
                {
                    _configuration.ThemeMode = mode;
                    ChangeTheme();
                },
                getDefault: () => ThemeCatalog.ModeSystem,
                // Resetting the appearance also takes back the theme and every custom colour of every theme.
                reset: () =>
                {
                    _configuration.ThemeMode = ThemeCatalog.ModeSystem;
                    _configuration.Theme = ThemeCatalog.DefaultThemeId;
                    _configuration.ThemeColors.Clear();
                    ChangeTheme();
                });

            yield return new ChoiceSettingViewModel(AppearanceId, "\uE771",
                Text("Settings_Theme", "Theme"),
                Text("Settings_Theme_Description", "Newschool is flat and modern, Oldschool keeps the classic look and the classic colours of the log list. Works in light and in dark mode."),
                needsRestart: false,
                options: ThemeCatalog.Designs.Select(theme => new ChoiceOption(theme.Id, theme.Name)).ToList(),
                get: () => ThemeCatalog.Find(_configuration.Theme).Id,
                set: id =>
                {
                    _configuration.Theme = id;
                    ChangeTheme();
                },
                getDefault: () => ThemeCatalog.DefaultThemeId);

            yield return new ChoiceSettingViewModel(AppearanceId, "\uE121",
                Text("Settings_TimeStampFormat", "Timestamp format"),
                Text("Settings_TimeStampFormat_Description", "Format of the timestamp column in the log list."),
                needsRestart: false,
                options: new[]
                {
                    new ChoiceOption(TimeStampFormats.ToId(TimeStampFormat.System), Text("Settings_TimeStampFormat_System", "System (format of this computer)")),
                    new ChoiceOption(TimeStampFormats.ToId(TimeStampFormat.Iso), Text("Settings_TimeStampFormat_Iso", "ISO 8601 (2025-12-18 15:11:48.385)")),
                    new ChoiceOption(TimeStampFormats.ToId(TimeStampFormat.German), Text("Settings_TimeStampFormat_German", "German (18.12.2025 15:11:48.385)")),
                    new ChoiceOption(TimeStampFormats.ToId(TimeStampFormat.English), Text("Settings_TimeStampFormat_English", "English (12/18/2025 03:11:48.385 PM)"))
                },
                get: () => TimeStampFormats.ToId(TimeStampFormats.Parse(_configuration.TimeStampFormat)),
                set: id =>
                {
                    _configuration.TimeStampFormat = id;
                    Commit();
                },
                getDefault: () => TimeStampFormats.ToId(TimeStampFormat.System));

            yield return new ChoiceSettingViewModel(AppearanceId, "\uE8D2",
                Text("Settings_ListFontFamily", "Font of the log list"),
                Text("Settings_ListFontFamily_Description", "The font of the entries and of the column headers in the log list, e.g. a monospaced font such as Consolas."),
                needsRestart: false,
                options: new[] { new ChoiceOption(string.Empty, Text("Settings_ListFontFamily_Default", "Default (like the rest of the application)")) }
                    .Concat(System.Windows.Media.Fonts.SystemFontFamilies.Select(family => family.Source).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                        .Select(name => new ChoiceOption(name, name))).ToList(),
                get: () => _configuration.ListFontFamily,
                set: family =>
                {
                    _configuration.ListFontFamily = family;
                    Commit();
                },
                getDefault: () => string.Empty);

            yield return new IntSettingViewModel(AppearanceId, "\uE8E9",
                Text("Settings_ListFontSize", "Font size of the log list"),
                Text("Settings_ListFontSize_Description", "The size of the text in the log list. Ctrl+mouse wheel and Ctrl+Plus / Ctrl+Minus change it for the current session, Ctrl+0 goes back to this size; the status bar then offers to save the new size here."),
                get: () => _configuration.ListFontSize,
                set: value =>
                {
                    _configuration.ListFontSize = value;
                    Commit();
                },
                getDefault: () => SettingsRules.DefaultListFontSize,
                isValid: SettingsRules.IsValidListFontSize,
                invalidMessage: string.Format(CultureInfo.CurrentCulture,
                    Text("Settings_InvalidNumber", "Enter a whole number between {0} and {1}."), SettingsRules.MinListFontSize, SettingsRules.MaxListFontSize));

            var loggerTree = new SettingGroup("\uE8FD",
                Text("Settings_Group_LoggerTree", "Logger tree"),
                Text("Settings_Group_LoggerTree_Subtitle", "Default view for new tabs"));

            yield return new ChoiceSettingViewModel(AppearanceId, "",
                Text("Settings_LoggerTreeSide", "Side"),
                Text("Settings_LoggerTreeSide_Description", "Which side of the log list the logger tree is on in new tabs. Changing it in a tab saves it here."),
                needsRestart: false,
                options: new[]
                {
                    new ChoiceOption(LoggerTreeLeft, Text("Settings_LoggerTreeSide_Left", "Left")),
                    new ChoiceOption(LoggerTreeRight, Text("Settings_LoggerTreeSide_Right", "Right"))
                },
                get: () => _configuration.LoggerTreeOnLeft ? LoggerTreeLeft : LoggerTreeRight,
                set: side =>
                {
                    _configuration.LoggerTreeOnLeft = side == LoggerTreeLeft;
                    Commit();
                },
                getDefault: () => ConfigurationService.CreateDefaultConfiguration().LoggerTreeOnLeft ? LoggerTreeLeft : LoggerTreeRight)
            {
                Group = loggerTree
            };

            yield return new BoolSettingViewModel(AppearanceId, "",
                Text("Settings_LoggerTreeOpen", "Open"),
                Text("Settings_LoggerTreeOpen_Description", "New tabs show the logger tree next to the log list. Opening or closing it in a tab saves it here."),
                get: () => _configuration.LoggerTreeOpen,
                set: value =>
                {
                    _configuration.LoggerTreeOpen = value;
                    Commit();
                },
                getDefault: () => ConfigurationService.CreateDefaultConfiguration().LoggerTreeOpen)
            {
                Group = loggerTree
            };

            yield return new IntSettingViewModel(AppearanceId, "",
                Text("Settings_LoggerTreeWidth", "Width in pixels"),
                Text("Settings_LoggerTreeWidth_Description", "Width of the logger tree in pixels. Dragging the splitter in a tab saves it here."),
                get: () => _configuration.LoggerTreeWidth,
                set: value =>
                {
                    _configuration.LoggerTreeWidth = value;
                    Commit();
                },
                getDefault: () => ConfigurationService.CreateDefaultConfiguration().LoggerTreeWidth,
                isValid: SettingsRules.IsValidLoggerTreeWidth,
                invalidMessage: string.Format(CultureInfo.CurrentCulture,
                    Text("Settings_InvalidNumber", "Enter a whole number between {0} and {1}."), SettingsRules.MinLoggerTreeWidth, SettingsRules.MaxLoggerTreeWidth))
            {
                Group = loggerTree
            };

            var search = new SettingGroup("\uE721",
                Text("Settings_Group_Search", "Search and filter"),
                Text("Settings_Group_Search_Subtitle", "Default fields for new tabs"));

            foreach (var (field, key, title) in new[]
            {
                (SearchFields.Message, "Settings_SearchField_Message", "Message"),
                (SearchFields.LoggerName, "Settings_SearchField_Logger", "Logger name"),
                (SearchFields.Exception, "Settings_SearchField_Exception", "Exception"),
                (SearchFields.Thread, "Settings_SearchField_Thread", "Thread"),
                (SearchFields.Properties, "Settings_SearchField_Properties", "Other properties")
            })
            {
                var searchField = field;
                yield return new BoolSettingViewModel(AppearanceId, "",
                    Text(key, title),
                    Format("Settings_SearchField_Description", "The filter and the search look into the field \"{0}\" in new tabs. Changing the fields in a tab saves them here.", Text(key, title)),
                    get: () => SettingsRules.ParseSearchFields(_configuration.SearchFields).HasFlag(searchField),
                    set: value =>
                    {
                        var fields = SettingsRules.ParseSearchFields(_configuration.SearchFields);
                        fields = value ? fields | searchField : fields & ~searchField;

                        // At least one field has to be searched; the last one cannot be switched off.
                        _configuration.SearchFields = (fields == SearchFields.None ? SettingsRules.ParseSearchFields(_configuration.SearchFields) : fields).ToString();
                        Commit();
                    },
                    getDefault: () => SearchFields.Default.HasFlag(searchField))
                {
                    Group = search
                };
            }

            foreach (var token in ThemeCatalog.Tokens)
                yield return CreateColorSetting(token);
        }

        private ColorSettingViewModel CreateColorSetting(ThemeToken token)
        {
            string title, description;
            if (token.Level != null)
            {
                var level = Text("Settings_Level_" + token.Level, token.Level);
                var part = token.IsBackground ? "Background" : "Foreground";
                title = Format("Settings_Level" + part, "{0}", level);
                description = Format("Settings_Level" + part + "_Description", "{0}", level);
            }
            else
            {
                title = Text("Settings_Token_" + token.Name, token.Name);
                description = Text("Settings_Token_" + token.Name + "_Description", string.Empty);
            }

            var section = token.Section switch
            {
                ThemeSection.Interface => Text("Settings_Section_Interface", "Interface"),
                ThemeSection.LogLevels => Text("Settings_Section_Levels", "Log levels"),
                _ => Text("Settings_Section_Highlighting", "Search highlighting")
            };

            return new ColorSettingViewModel(AppearanceId, "", title, description,
                getColor: () => ThemeCatalog.Resolve(EffectiveTheme().Id, CustomColors(), token.Key),
                isCustom: () => CustomColors().ContainsKey(token.Key),
                setCustom: color => SetCustomColor(token.Key, color),
                invalidMessage: Text("Settings_Color_Invalid", "Enter a colour like #1B1B1B or #801B1B1B (with transparency)."))
            {
                Section = section
            };
        }

        /// <summary>
        /// The theme that is in effect: the dark theme, or the light theme, depending on the mode
        /// </summary>
        private ThemeDefinition EffectiveTheme() => ThemeCatalog.Find(_themeService.ResolveThemeId(_configuration));

        /// <summary>
        /// The colours the user changed in the theme that is in effect
        /// </summary>
        private IReadOnlyDictionary<string, string> CustomColors() =>
            _configuration.ThemeColors.GetValueOrDefault(EffectiveTheme().Id) ?? new Dictionary<string, string>();

        /// <param name="color">The new colour; null, or the colour the theme has anyway, goes back to the theme</param>
        private void SetCustomColor(string key, string? color)
        {
            var theme = EffectiveTheme();
            if (color == theme.Colors[key])
                color = null;

            if (color == null)
            {
                if (_configuration.ThemeColors.TryGetValue(theme.Id, out var existing))
                {
                    existing.Remove(key);
                    if (existing.Count == 0)
                        _configuration.ThemeColors.Remove(theme.Id);
                }
            }
            else
            {
                if (!_configuration.ThemeColors.TryGetValue(theme.Id, out var colors))
                    _configuration.ThemeColors[theme.Id] = colors = new Dictionary<string, string>();

                colors[key] = color;
            }

            Commit();
            _themeService.Apply(_configuration);
        }

        /// <summary>
        /// Saves and applies a mode or theme that was chosen or reset; the colours then show those of the theme in effect
        /// </summary>
        private void ChangeTheme()
        {
            Commit();
            _themeService.Apply(_configuration);
            foreach (var color in _settings.OfType<ColorSettingViewModel>())
                color.Reload();
        }
    }
}
