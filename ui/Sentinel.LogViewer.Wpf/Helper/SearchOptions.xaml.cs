using System.Windows;
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

        public static readonly DependencyProperty ScopeProperty = Register(nameof(Scope), typeof(string), string.Empty);

        public SearchOptions()
        {
            InitializeComponent();
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

        /// <summary>What is added to the names that a screen reader reads, e.g. " of the search", so that the two rows can be told apart</summary>
        public string Scope
        {
            get => (string)GetValue(ScopeProperty);
            set => SetValue(ScopeProperty, value);
        }

        private static DependencyProperty Register(string name, System.Type type, object? defaultValue) =>
            DependencyProperty.Register(name, type, typeof(SearchOptions), new PropertyMetadata(defaultValue));
    }
}
