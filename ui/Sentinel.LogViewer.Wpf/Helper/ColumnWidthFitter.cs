using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NLog;
using Sentinel.LogViewer.Wpf.Helper.ListViewLayoutManager;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// Sizes GridView columns to their widest text across all items. The GridView itself only measures the
    /// rows that are realized, so with virtualization the texts of other rows would be cut off.
    /// </summary>
    internal sealed class ColumnWidthFitter
    {
        // Cell margin of the GridViewRowPresenter (6 on each side) plus a reserve for rounding.
        private const double CellChromeWidth = 16;

        private readonly ListView _listView;
        private readonly Typeface _typeface;
        private readonly Dictionary<char, double> _charWidths = new();
        private readonly List<FittedColumn> _columns = new();
        private bool _applyPending;

        public ColumnWidthFitter(ListView listView)
        {
            _listView = listView;
            _typeface = new Typeface(listView.FontFamily, listView.FontStyle, listView.FontWeight, listView.FontStretch);
        }

        public void AddColumn(GridViewColumn column, Func<LogEventInfo, string?> getText)
        {
            _columns.Add(new FittedColumn(column, getText));
        }

        /// <summary>
        /// Widens the columns for the given additional items
        /// </summary>
        public void Include(IEnumerable items)
        {
            var grown = false;
            foreach (var item in items)
            {
                if (item is not LogEventInfo logEvent)
                    continue;

                foreach (var column in _columns)
                {
                    var textWidth = MeasureWidestLine(column.GetText(logEvent));
                    if (textWidth > column.TextWidth)
                    {
                        column.TextWidth = textWidth;
                        grown = true;
                    }
                }
            }

            if (grown)
                ScheduleApply();
        }

        /// <summary>
        /// Sizes the columns for exactly the given items, e.g. after the list was cleared or replaced
        /// </summary>
        public void Reset(IEnumerable items)
        {
            foreach (var column in _columns)
            {
                column.TextWidth = 0;
                column.Column.ClearValue(RangeColumn.MinWidthProperty);
                // Assigning the same width again is ignored, so leave it first to get the column measured anew.
                column.Column.Width = column.Column.ActualWidth;
                column.Column.Width = column.InitialWidth;
            }

            Include(items);
            ScheduleApply();
        }

        /// <summary>
        /// Widens every column that is narrower than its widest text
        /// </summary>
        public void Apply()
        {
            _applyPending = false;
            foreach (var column in _columns)
            {
                if (column.TextWidth <= 0)
                    continue;

                var width = column.TextWidth + CellChromeWidth;
                if (RangeColumn.GetIsFillColumn(column.Column))
                {
                    // The layout manager stretches a fill column to the viewport, but not below its minimum.
                    RangeColumn.SetMinWidth(column.Column, width);
                }

                var currentWidth = double.IsNaN(column.Column.Width) ? column.Column.ActualWidth : column.Column.Width;
                if (currentWidth < width)
                    column.Column.Width = width;
            }
        }

        private void ScheduleApply()
        {
            if (_applyPending)
                return;

            // After layout, so that auto sized columns have their header width.
            _applyPending = true;
            _listView.Dispatcher.InvokeAsync(Apply, DispatcherPriority.Loaded);
        }

        private double MeasureWidestLine(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            double widest = 0;
            double current = 0;
            foreach (var character in text)
            {
                if (character == '\n')
                {
                    widest = Math.Max(widest, current);
                    current = 0;
                }
                else if (character != '\r')
                {
                    current += MeasureCharacter(character);
                }
            }

            return Math.Max(widest, current);
        }

        /// <summary>
        /// Laying out every text would be far too slow for large logs, so texts are measured per character.
        /// </summary>
        private double MeasureCharacter(char character)
        {
            if (!_charWidths.TryGetValue(character, out var width))
            {
                width = new FormattedText(
                    character.ToString(),
                    CultureInfo.CurrentUICulture,
                    _listView.FlowDirection,
                    _typeface,
                    _listView.FontSize,
                    Brushes.Black,
                    null,
                    TextOptions.GetTextFormattingMode(_listView),
                    VisualTreeHelper.GetDpi(_listView).PixelsPerDip).WidthIncludingTrailingWhitespace;
                _charWidths[character] = width;
            }

            return width;
        }

        private sealed class FittedColumn
        {
            public FittedColumn(GridViewColumn column, Func<LogEventInfo, string?> getText)
            {
                Column = column;
                GetText = getText;
                InitialWidth = column.Width;
            }

            public GridViewColumn Column { get; }
            public Func<LogEventInfo, string?> GetText { get; }
            public double InitialWidth { get; }
            public double TextWidth { get; set; }
        }
    }
}
