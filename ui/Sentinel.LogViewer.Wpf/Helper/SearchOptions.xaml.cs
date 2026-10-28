using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The options that the filter and the search have in common; each of them binds its own flags and its own command for the patterns
    /// </summary>
    public partial class SearchOptions : UserControl
    {
        public static readonly DependencyProperty MatchCaseProperty = Register(nameof(MatchCase), typeof(bool), false);

        public static readonly DependencyProperty WholeWordProperty = Register(nameof(WholeWord), typeof(bool), false);

        public static readonly DependencyProperty UseRegexProperty = Register(nameof(UseRegex), typeof(bool), false);

        public static readonly DependencyProperty PatternCommandProperty = Register(nameof(PatternCommand), typeof(ICommand), null);

        public static readonly DependencyProperty IsSearchProperty = DependencyProperty.Register(nameof(IsSearch), typeof(bool), typeof(SearchOptions),
            new PropertyMetadata(false, (d, _) => ((SearchOptions)d).UpdateNames()));

        public SearchOptions()
        {
            InitializeComponent();
            UpdateNames();

            // The names that a screen reader reads follow the language.
            PropertyChangedEventManager.AddHandler(LogViewerTexts.Source, OnTextsChanged, string.Empty);
        }

        public bool MatchCase
        {
            get => (bool)GetValue(MatchCaseProperty);
            set => SetValue(MatchCaseProperty, value);
        }

        public bool WholeWord
        {
            get => (bool)GetValue(WholeWordProperty);
            set => SetValue(WholeWordProperty, value);
        }

        public bool UseRegex
        {
            get => (bool)GetValue(UseRegexProperty);
            set => SetValue(UseRegexProperty, value);
        }

        /// <summary>Puts a ready-made regular expression into the input; the parameter is the template</summary>
        public ICommand? PatternCommand
        {
            get => (ICommand?)GetValue(PatternCommandProperty);
            set => SetValue(PatternCommandProperty, value);
        }

        /// <summary>Whether these are the options of the search bar, which names them "... of the search" so that a screen reader can tell the two rows apart</summary>
        public bool IsSearch
        {
            get => (bool)GetValue(IsSearchProperty);
            set => SetValue(IsSearchProperty, value);
        }

        private void OnTextsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
                UpdateNames();
            else
                Dispatcher.BeginInvoke(UpdateNames);
        }

        private void UpdateNames()
        {
            // The names are made again, because the language can change while the control is on the screen.
            AutomationProperties.SetName(MatchCaseBox, LogViewerTexts.Get(IsSearch ? "Opt_MatchCaseSearch" : "Opt_MatchCase"));
            AutomationProperties.SetName(WholeWordBox, LogViewerTexts.Get(IsSearch ? "Opt_WholeWordSearch" : "Opt_WholeWord"));
            AutomationProperties.SetName(RegexBox, LogViewerTexts.Get(IsSearch ? "Opt_RegexSearch" : "Opt_RegexName"));
        }

        private static DependencyProperty Register(string name, System.Type type, object? defaultValue) =>
            DependencyProperty.Register(name, type, typeof(SearchOptions), new PropertyMetadata(defaultValue));
    }
}
