using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.ViewModels.Settings;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// The appearance settings: light or dark mode, the light theme and a card for every colour of the theme in use
    /// </summary>
    public partial class SettingsViewModel
    {
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
                // Resetting the appearance also takes back the light theme and every custom colour of every theme.
                reset: () =>
                {
                    _configuration.ThemeMode = ThemeCatalog.ModeSystem;
                    _configuration.LightTheme = ThemeCatalog.DefaultThemeId;
                    _configuration.ThemeColors.Clear();
                    ChangeTheme();
                });

            yield return new ChoiceSettingViewModel(AppearanceId, "\uE771",
                Text("Settings_LightTheme", "Light theme"),
                Text("Settings_LightTheme_Description", "Newschool is light and flat, Oldschool keeps the classic colours of the log list. Used whenever the application is light."),
                needsRestart: false,
                options: ThemeCatalog.LightThemes.Select(theme => new ChoiceOption(theme.Id, theme.Name)).ToList(),
                get: () => ThemeCatalog.Find(_configuration.LightTheme).Id,
                set: id =>
                {
                    _configuration.LightTheme = id;
                    ChangeTheme();
                },
                getDefault: () => ThemeCatalog.DefaultThemeId);

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
