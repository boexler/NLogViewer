using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The texts of the control in the language that the host chooses. The control does not use the language of the application or of the
    /// computer, so it is translated on its own; the host sets <see cref="Language"/> (the application does it from its own setting) and
    /// everything that the control shows follows at once, also the texts that are already on the screen.
    /// </summary>
    public static class LogViewerTexts
    {
        private static readonly ResourceManager Manager = new("Sentinel.LogViewer.Wpf.Texts.Texts", typeof(LogViewerTexts).Assembly);
        private static LogViewerLanguage _language = LogViewerLanguage.English;

        /// <summary>
        /// The language of all the texts of the control in this process; English until the host sets it. The control is one library with one set
        /// of texts, so there is one language for all of its viewers and dialogs, including the menus and tool tips that do not belong to a viewer.
        /// </summary>
        public static LogViewerLanguage Language
        {
            get => _language;
            set
            {
                if (_language == value)
                    return;

                _language = value;
                Source.Changed();
            }
        }

        /// <summary>
        /// Tells that the language was changed; the texts that the control keeps (the result row, the headers) are made again
        /// </summary>
        internal static TextSource Source { get; } = new();

        /// <summary>
        /// The culture of the language, for the numbers and dates in the texts
        /// </summary>
        public static CultureInfo Culture => CultureOf(_language);

        /// <summary>
        /// The text of the key in the current language; the English text when the language has none, the key when there is none at all
        /// </summary>
        public static string Get(string key) => Manager.GetString(key, Culture) ?? key;

        /// <summary>
        /// The text of the key with its arguments filled in, in the culture of the language
        /// </summary>
        public static string Format(string key, params object?[] arguments) => string.Format(Culture, Get(key), arguments);

        /// <summary>
        /// The names of all the keys, for tests
        /// </summary>
        internal static ResourceManager ResourceManager => Manager;

        internal static CultureInfo CultureOf(LogViewerLanguage language) => CultureInfo.GetCultureInfo(language switch
        {
            LogViewerLanguage.German => "de",
            LogViewerLanguage.Spanish => "es",
            LogViewerLanguage.French => "fr",
            LogViewerLanguage.Hungarian => "hu",
            LogViewerLanguage.Polish => "pl",
            LogViewerLanguage.Romanian => "ro",
            _ => "en"
        });

        /// <summary>
        /// Maps the code of a language ("de", "fr", ...) to the language of the control; English for a code that the control does not have
        /// </summary>
        public static LogViewerLanguage FromCode(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "de" => LogViewerLanguage.German,
            "es" => LogViewerLanguage.Spanish,
            "fr" => LogViewerLanguage.French,
            "hu" => LogViewerLanguage.Hungarian,
            "pl" => LogViewerLanguage.Polish,
            "ro" => LogViewerLanguage.Romanian,
            _ => LogViewerLanguage.English
        };
    }

    /// <summary>
    /// What a binding looks up a text in: <c>[Key]</c> is the text of the key, and every binding to it is refreshed when the language changes
    /// </summary>
    internal sealed class TextSource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string this[string key] => LogViewerTexts.Get(key);

        /// <summary>The language, so that a text with arguments can be bound to it and made again</summary>
        public LogViewerLanguage Language => LogViewerTexts.Language;

        internal void Changed()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }
}