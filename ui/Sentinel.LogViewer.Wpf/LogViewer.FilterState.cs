using Sentinel.LogViewer.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Sentinel.LogViewer.Wpf.Models;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The filters of the viewer as one value that can be read, saved and set on another viewer
    /// </summary>
    public partial class LogViewer
    {
        private static readonly string[] LevelNames = { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

        private bool _applyingFilterState;
        private bool _publishingFilterState;

        /// <summary>
        /// The filters of this viewer. It follows every change of the filters, and setting it applies the filters, so binding it two ways
        /// to a view model lets the view model keep and hand on the filters. The search box is not part of it.
        /// </summary>
        [Category("LogViewerFilters")]
        [Description("The hidden levels, filter terms, hidden loggers and time stamp format of the viewer")]
        public LogViewerFilterState? FilterState
        {
            get => (LogViewerFilterState?)GetValue(FilterStateProperty);
            set => SetValue(FilterStateProperty, value);
        }

        /// <summary>
        /// The <see cref="FilterState"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty FilterStateProperty =
            DependencyProperty.Register(nameof(FilterState), typeof(LogViewerFilterState), typeof(LogViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFilterStateChanged));

        private static void OnFilterStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer { _publishingFilterState: false } viewer && e.NewValue is LogViewerFilterState state)
                viewer.ApplyFilterState(state);
        }

        /// <summary>
        /// Returns a copy of the current filters
        /// </summary>
        public LogViewerFilterState GetFilterState() => new()
        {
            HiddenLevels = LevelNames.Where(IsLevelHidden).ToList(),
            Terms = ActiveSearchTerms.Select(term => new LogViewerFilterTerm { Text = term.Text, IsRegex = term is RegexSearchTerm }).ToList(),
            LoggerRules = new List<string>(LoggerTree.Rules.ToList()),
            TimeStampFormat = CustomTimeStampFormat
        };

        /// <summary>
        /// Sets the filters of this viewer to the ones of the state; a term that is not a valid regular expression is left out
        /// </summary>
        public void ApplyFilterState(LogViewerFilterState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            _applyingFilterState = true;
            try
            {
                foreach (var level in LevelNames)
                    SetLevelHidden(level, state.HiddenLevels.Contains(level, StringComparer.Ordinal));

                var terms = new ObservableCollection<SearchTerm>();
                foreach (var term in state.Terms)
                {
                    try
                    {
                        terms.Add(term.IsRegex ? new RegexSearchTerm(term.Text) : new TextSearchTerm(term.Text));
                    }
                    catch (ArgumentException)
                    {
                    }
                }

                ActiveSearchTerms = terms;
                LoggerTree.Rules.Load(state.LoggerRules);
                CustomTimeStampFormat = state.TimeStampFormat;
            }
            finally
            {
                _applyingFilterState = false;
            }

            UpdateFilter();
        }

        private bool IsLevelHidden(string level) => level switch
        {
            "Trace" => TraceFilter,
            "Debug" => DebugFilter,
            "Info" => InfoFilter,
            "Warn" => WarnFilter,
            "Error" => ErrorFilter,
            _ => FatalFilter
        };

        private void SetLevelHidden(string level, bool hidden)
        {
            switch (level)
            {
                case "Trace": TraceFilter = hidden; break;
                case "Debug": DebugFilter = hidden; break;
                case "Info": InfoFilter = hidden; break;
                case "Warn": WarnFilter = hidden; break;
                case "Error": ErrorFilter = hidden; break;
                default: FatalFilter = hidden; break;
            }
        }

        /// <summary>
        /// Lets <see cref="FilterState"/> follow the filters, unless they are being set from it
        /// </summary>
        private void PublishFilterState()
        {
            if (_applyingFilterState || _publishingFilterState)
                return;

            var state = GetFilterState();
            if (state.IsSameAs(FilterState))
                return;

            _publishingFilterState = true;
            try
            {
                SetCurrentValue(FilterStateProperty, state);
            }
            finally
            {
                _publishingFilterState = false;
            }
        }
    }
}
