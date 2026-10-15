using System.Collections.Generic;
using System.Linq;
using Sentinel.LogViewer.Services.Theming;
using Sentinel.LogViewer.ViewModels.Settings;

namespace Sentinel.LogViewer.ViewModels
{
    /// <summary>
    /// The appearance settings: the theme and a card for every colour of it
    /// </summary>
    public partial class SettingsViewModel
    {
        private IEnumerable<SettingViewModel> CreateAppearanceSettings()
        {
            yield return new ChoiceSettingViewModel(AppearanceId, "\uE790",
                Text("Settings_Theme", "Theme"),
                Text("Settings_Theme_Description", "Colour scheme of the whole application."),
                needsRestart: false,
                options: ThemeCatalog.Themes.Select(theme => new ChoiceOption(theme.Id, theme.Name)).ToList(),
                get: () => ThemeCatalog.Find(_configuration.Theme).Id,
                set: id =>
                {
                    _configuration.Theme = id;
                    ChangeTheme();
                },
                getDefault: () => ThemeCatalog.DefaultThemeId,
                // Resetting the appearance also takes back every custom colour of every theme.
                reset: () =>
                {
                    _configuration.Theme = ThemeCatalog.DefaultThemeId;
                    _configuration.ThemeColors.Clear();
                    ChangeTheme();
                });

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
                getColor: () => ThemeCatalog.Resolve(_configuration.Theme, CustomColors(), token.Key),
                isCustom: () => CustomColors().ContainsKey(token.Key),
                setCustom: color => SetCustomColor(token.Key, color),
                invalidMessage: Text("Settings_Color_Invalid", "Enter a colour like #1B1B1B or #801B1B1B (with transparency)."))
            {
                Section = section
            };
        }

        /// <summary>
        /// The colours the user changed in the current theme
        /// </summary>
        private IReadOnlyDictionary<string, string> CustomColors() =>
            _configuration.ThemeColors.GetValueOrDefault(ThemeCatalog.Find(_configuration.Theme).Id) ?? new Dictionary<string, string>();

        /// <param name="color">The new colour; null, or the colour the theme has anyway, goes back to the theme</param>
        private void SetCustomColor(string key, string? color)
        {
            var theme = ThemeCatalog.Find(_configuration.Theme);
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
        /// Saves and applies a theme that was chosen or reset; the colours then show those of the theme
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
