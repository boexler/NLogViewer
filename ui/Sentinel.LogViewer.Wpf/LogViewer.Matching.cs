using System.ComponentModel;
using System.Windows;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The options of the filter and of the search, and the fields that they look into. What matches is decided by <see cref="FilterTerm"/>,
    /// so the filter, the search and the highlighting agree.
    /// </summary>
    public partial class LogViewer
    {
        private FilterTerm? _findTerm;

        #region Fields

        /// <summary>
        /// The parts of an entry that the filter terms and the search look into: the message and the logger by default
        /// </summary>
        [Category("LogViewerFilters")]
        [Description("The parts of an entry that the filter and the search look into")]
        public SearchFields SearchFields
        {
            get => (SearchFields)GetValue(SearchFieldsProperty);
            set => SetValue(SearchFieldsProperty, value);
        }

        /// <summary>
        /// The <see cref="SearchFields"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty SearchFieldsProperty =
            DependencyProperty.Register(nameof(SearchFields), typeof(SearchFields), typeof(LogViewer),
                new FrameworkPropertyMetadata(SearchFields.Default, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchFieldsChanged));

        private static void OnSearchFieldsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewer viewer)
                return;

            viewer.UpdateFilter();
            viewer.UpdateFindMatches();
            if (viewer.PART_ListView != null)
                MessageHighlighter.Refresh(viewer.PART_ListView);
        }

        #endregion

        #region Options of the filter input

        /// <summary>
        /// Whether the next filter term hides the entries that match it instead of the others
        /// </summary>
        [Category("LogViewerFilters")]
        public bool FilterIsExclude
        {
            get => (bool)GetValue(FilterIsExcludeProperty);
            set => SetValue(FilterIsExcludeProperty, value);
        }

        public static readonly DependencyProperty FilterIsExcludeProperty =
            DependencyProperty.Register(nameof(FilterIsExclude), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// Whether the next filter term tells upper and lower case letters apart
        /// </summary>
        [Category("LogViewerFilters")]
        public bool FilterMatchCase
        {
            get => (bool)GetValue(FilterMatchCaseProperty);
            set => SetValue(FilterMatchCaseProperty, value);
        }

        public static readonly DependencyProperty FilterMatchCaseProperty =
            DependencyProperty.Register(nameof(FilterMatchCase), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// Whether the next filter term only counts for whole words
        /// </summary>
        [Category("LogViewerFilters")]
        public bool FilterWholeWord
        {
            get => (bool)GetValue(FilterWholeWordProperty);
            set => SetValue(FilterWholeWordProperty, value);
        }

        public static readonly DependencyProperty FilterWholeWordProperty =
            DependencyProperty.Register(nameof(FilterWholeWord), typeof(bool), typeof(LogViewer), new PropertyMetadata(false));

        /// <summary>
        /// The level that the next filter term is limited to, or null for every level
        /// </summary>
        [Category("LogViewerFilters")]
        public LogEntryLevel? FilterLevel
        {
            get => (LogEntryLevel?)GetValue(FilterLevelProperty);
            set => SetValue(FilterLevelProperty, value);
        }

        public static readonly DependencyProperty FilterLevelProperty =
            DependencyProperty.Register(nameof(FilterLevel), typeof(LogEntryLevel?), typeof(LogViewer), new PropertyMetadata(null));

        private static readonly DependencyPropertyKey FilterErrorPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FilterError), typeof(string), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// The <see cref="FilterError"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FilterErrorProperty = FilterErrorPropertyKey.DependencyProperty;

        /// <summary>
        /// Why the text of the filter input is not valid, e.g. a regular expression with a bracket that is not closed; null when it is valid
        /// </summary>
        public string? FilterError => (string?)GetValue(FilterErrorProperty);

        /// <summary>
        /// Checks the text of the filter input against its options
        /// </summary>
        private void UpdateFilterError() =>
            SetValue(FilterErrorPropertyKey, FilterTerm.Validate(CurrentSearchText, UseRegexSearch));

        #endregion

        #region Options of the search

        /// <summary>
        /// Whether the search text is a regular expression
        /// </summary>
        [Category("LogViewerFind")]
        public bool FindUseRegex
        {
            get => (bool)GetValue(FindUseRegexProperty);
            set => SetValue(FindUseRegexProperty, value);
        }

        public static readonly DependencyProperty FindUseRegexProperty =
            DependencyProperty.Register(nameof(FindUseRegex), typeof(bool), typeof(LogViewer), new PropertyMetadata(false, OnFindOptionChanged));

        /// <summary>
        /// Whether the search tells upper and lower case letters apart
        /// </summary>
        [Category("LogViewerFind")]
        public bool FindMatchCase
        {
            get => (bool)GetValue(FindMatchCaseProperty);
            set => SetValue(FindMatchCaseProperty, value);
        }

        public static readonly DependencyProperty FindMatchCaseProperty =
            DependencyProperty.Register(nameof(FindMatchCase), typeof(bool), typeof(LogViewer), new PropertyMetadata(false, OnFindOptionChanged));

        /// <summary>
        /// Whether the search only finds whole words
        /// </summary>
        [Category("LogViewerFind")]
        public bool FindWholeWord
        {
            get => (bool)GetValue(FindWholeWordProperty);
            set => SetValue(FindWholeWordProperty, value);
        }

        public static readonly DependencyProperty FindWholeWordProperty =
            DependencyProperty.Register(nameof(FindWholeWord), typeof(bool), typeof(LogViewer), new PropertyMetadata(false, OnFindOptionChanged));

        /// <summary>
        /// The level that the search is limited to, or null for every level
        /// </summary>
        [Category("LogViewerFind")]
        public LogEntryLevel? FindLevel
        {
            get => (LogEntryLevel?)GetValue(FindLevelProperty);
            set => SetValue(FindLevelProperty, value);
        }

        public static readonly DependencyProperty FindLevelProperty =
            DependencyProperty.Register(nameof(FindLevel), typeof(LogEntryLevel?), typeof(LogViewer), new PropertyMetadata(null, OnFindOptionChanged));

        private static readonly DependencyPropertyKey FindErrorPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FindError), typeof(string), typeof(LogViewer), new PropertyMetadata(null));

        /// <summary>
        /// The <see cref="FindError"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FindErrorProperty = FindErrorPropertyKey.DependencyProperty;

        /// <summary>
        /// Why the search text is not valid; null when it is valid
        /// </summary>
        public string? FindError => (string?)GetValue(FindErrorProperty);

        private static void OnFindOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer viewer)
                viewer.OnFindChanged();
        }

        /// <summary>
        /// The term that the search looks for, or null while there is no search text or it is not valid
        /// </summary>
        internal FilterTerm? FindTerm => _findTerm;

        /// <summary>
        /// Builds the term of the search from the search text and its options
        /// </summary>
        private void RebuildFindTerm()
        {
            var error = FilterTerm.Validate(FindText, FindUseRegex);
            SetValue(FindErrorPropertyKey, error);

            _findTerm = string.IsNullOrEmpty(FindText) || error != null
                ? null
                : new FilterTerm(FindText, matchCase: FindMatchCase, wholeWord: FindWholeWord, isRegex: FindUseRegex, level: FindLevel);
        }

        #endregion
    }
}
