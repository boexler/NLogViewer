using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Helper;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Wpf
{
    /// <summary>
    /// The columns of the list: which ones are shown and in which order, the thread and the properties of the entries as columns, and the
    /// menu on the column headers
    /// </summary>
    public partial class LogViewer
    {
        /// <summary>The most property names that are remembered for the menu; a log with ever new names must not fill the memory</summary>
        private const int MaxPropertyNames = 200;

        private readonly Dictionary<GridViewColumn, string> _columnKeys = new();
        private readonly Dictionary<string, GridViewColumn> _columnsByKey = new();
        private readonly Dictionary<GridViewColumn, double> _initialColumnWidths = new();
        private readonly HashSet<GridViewColumn> _fittedColumns = new();
        private readonly HashSet<string> _propertyNames = new(StringComparer.OrdinalIgnoreCase);
        private bool _columnsReady;
        private bool _applyingColumns;

        /// <summary>
        /// The columns that the list shows, in their order, as keys (<see cref="LogColumns"/>); the message is always one of them. Setting it
        /// shows exactly these columns, and moving a column header or choosing a column in the menu of the headers changes it.
        /// </summary>
        [Category("LogViewerColumns")]
        [Description("The columns that the list shows, in their order")]
        public IReadOnlyList<string> ColumnKeys
        {
            get => (IReadOnlyList<string>)GetValue(ColumnKeysProperty);
            set => SetValue(ColumnKeysProperty, value);
        }

        /// <summary>
        /// The <see cref="ColumnKeys"/> DependencyProperty.
        /// </summary>
        public static readonly DependencyProperty ColumnKeysProperty =
            DependencyProperty.Register(nameof(ColumnKeys), typeof(IReadOnlyList<string>), typeof(LogViewer),
                new FrameworkPropertyMetadata(LogColumns.Default, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColumnKeysChanged));

        private static void OnColumnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewer viewer)
                viewer.ApplyColumns();
        }

        /// <summary>
        /// A column of the Show...Column properties was switched: the layout follows
        /// </summary>
        private static void OnColumnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            if (d is not LogViewer viewer || viewer._applyingColumns)
                return;

            var key = args.Property == ShowIdColumnProperty ? LogColumns.Id
                : args.Property == ShowLevelColumnProperty ? LogColumns.Level
                : args.Property == ShowTimeStampColumnProperty ? LogColumns.TimeStamp
                : LogColumns.Logger;
            viewer.SetColumnVisible(key, (bool)args.NewValue);
        }

        /// <summary>
        /// Shows or hides a column
        /// </summary>
        /// <param name="key">A key of <see cref="LogColumns"/></param>
        public void SetColumnVisible(string key, bool visible) => ColumnKeys = LogColumns.With(ColumnKeys, key, visible);

        /// <summary>
        /// The names of the properties that the entries have, for the menu of the column headers
        /// </summary>
        internal IReadOnlyCollection<string> KnownPropertyNames => _propertyNames;

        private void InitializeColumns()
        {
            if (PART_ListView?.View is not AutoSizedGridView gridView)
                return;

            // The built-in columns are the ones of the template, in this order.
            var keys = new[] { LogColumns.Id, LogColumns.Level, LogColumns.TimeStamp, LogColumns.Logger, LogColumns.Message };
            for (var i = 0; i < Math.Min(keys.Length, gridView.Columns.Count); i++)
            {
                var column = gridView.Columns[i];
                _columnKeys[column] = keys[i];
                _columnsByKey[keys[i]] = column;
                _initialColumnWidths[column] = column.Width;
            }

            _columnWidthFitter = new ColumnWidthFitter(PART_ListView);
            FitColumn(LogColumns.Id, () => IdResolver);
            FitColumn(LogColumns.TimeStamp, () => TimeStampResolver);
            FitColumn(LogColumns.Logger, () => LoggerNameResolver);
            FitColumn(LogColumns.Message, () => MessageResolver);

            gridView.Columns.CollectionChanged += OnGridColumnsChanged;
            PART_ListView.PreviewMouseRightButtonUp += OnListViewPreviewMouseRightButtonUp;
            PART_ListView.PreviewMouseRightButtonDown += OnListViewPreviewMouseRightButtonDown;
            PART_ListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnColumnHeaderClick));
            _columnsReady = true;
            UpdateTimeStampHeader();

            CollectPropertyNames(GetEffectiveEntries());
            ApplyColumns();
            _columnWidthFitter.Include(GetEffectiveEntries());
        }

        private void FitColumn(string key, Func<ILogEntryResolver?> getResolver)
        {
            if (_columnsByKey.TryGetValue(key, out var column))
                _columnWidthFitter?.AddColumn(column, entry => getResolver()?.Resolve(entry));
        }

        /// <summary>
        /// Makes the columns of the list the ones of <see cref="ColumnKeys"/>, in their order, and tells the Show...Column properties
        /// </summary>
        private void ApplyColumns()
        {
            if (!_columnsReady || PART_ListView?.View is not AutoSizedGridView gridView)
                return;

            _applyingColumns = true;
            try
            {
                var wanted = LogColumns.Normalize(ColumnKeys);
                foreach (var column in gridView.Columns.ToList())
                {
                    if (!wanted.Contains(_columnKeys.GetValueOrDefault(column) ?? string.Empty))
                    {
                        gridView.Columns.Remove(column);
                        if (_fittedColumns.Remove(column))
                            _columnWidthFitter?.RemoveColumn(column);
                    }
                }

                for (var i = 0; i < wanted.Count; i++)
                {
                    var column = ColumnOf(wanted[i]);
                    var at = gridView.Columns.IndexOf(column);
                    if (at < 0)
                    {
                        // A column that is shown again starts with its original width.
                        column.Width = _initialColumnWidths.GetValueOrDefault(column, double.NaN);
                        gridView.Columns.Insert(i, column);
                        RegisterFittedProperty(column, wanted[i]);
                    }
                    else if (at != i)
                    {
                        gridView.Columns.Move(at, i);
                    }
                }

                SetCurrentValue(ShowIdColumnProperty, wanted.Contains(LogColumns.Id));
                SetCurrentValue(ShowLevelColumnProperty, wanted.Contains(LogColumns.Level));
                SetCurrentValue(ShowTimeStampColumnProperty, wanted.Contains(LogColumns.TimeStamp));
                SetCurrentValue(ShowLoggerNameColumnProperty, wanted.Contains(LogColumns.Logger));
            }
            finally
            {
                _applyingColumns = false;
            }

            // A column that is hidden cannot be the one that the list is sorted by.
            if (SortKey != null && !LogColumns.Normalize(ColumnKeys).Contains(SortKey))
                ResetSort();
            else
                UpdateHeaders();

            _columnWidthFitter?.Apply();
        }

        /// <summary>
        /// The column of a key; the columns of the thread and of the properties are made when they are first shown
        /// </summary>
        private GridViewColumn ColumnOf(string key)
        {
            if (_columnsByKey.TryGetValue(key, out var column))
                return column;

            var name = key == LogColumns.Thread ? FilterTerm.ThreadProperty : LogColumns.IsProperty(key, out var property) ? property : key;
            column = new GridViewColumn
            {
                Header = name,
                Width = double.NaN,
                DisplayMemberBinding = new Binding(".") { Converter = PropertyValueConverter.Instance, ConverterParameter = name }
            };
            _columnsByKey[key] = column;
            _columnKeys[column] = key;
            _initialColumnWidths[column] = double.NaN;
            return column;
        }

        /// <summary>
        /// Sizes the column of a property to its texts as long as it is shown
        /// </summary>
        private void RegisterFittedProperty(GridViewColumn column, string key)
        {
            if (_columnWidthFitter == null || _fittedColumns.Contains(column) || !(key == LogColumns.Thread || LogColumns.IsProperty(key, out _)))
                return;

            var name = key == LogColumns.Thread ? FilterTerm.ThreadProperty : key[LogColumns.PropertyPrefix.Length..];
            _fittedColumns.Add(column);
            _columnWidthFitter.AddColumn(column, entry => LogColumns.ValueOf(entry, name));
            _columnWidthFitter.Include(GetEffectiveEntries());
        }

        /// <summary>
        /// A header was dragged to another place: the layout follows
        /// </summary>
        private void OnGridColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_applyingColumns || e.Action != NotifyCollectionChangedAction.Move || PART_ListView?.View is not GridView gridView)
                return;

            var keys = gridView.Columns.Select(column => _columnKeys.GetValueOrDefault(column)).OfType<string>().ToList();
            _applyingColumns = true;
            try
            {
                SetCurrentValue(ColumnKeysProperty, keys);
            }
            finally
            {
                _applyingColumns = false;
            }
        }

        /// <summary>
        /// Remembers the names of the properties of entries, for the menu; it does not look at more than <see cref="MaxPropertyNames"/> names
        /// </summary>
        private void CollectPropertyNames(IEnumerable entries)
        {
            foreach (var item in entries)
            {
                if (_propertyNames.Count >= MaxPropertyNames)
                    return;

                if (item is not LogEntry entry)
                    continue;

                foreach (var name in entry.Properties.Keys)
                {
                    if (_propertyNames.Count >= MaxPropertyNames)
                        break;

                    if (!string.Equals(name, FilterTerm.ThreadProperty, StringComparison.OrdinalIgnoreCase))
                        _propertyNames.Add(name);
                }
            }
        }

        private void OnListViewPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(e.OriginalSource as DependencyObject);
            if (header == null || header.Role == GridViewColumnHeaderRole.Padding || header.Column == null)
                return;

            e.Handled = true;
            BuildColumnMenu(header.Column, header).IsOpen = true;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null && current is not T)
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);

            return current as T;
        }

        /// <summary>
        /// The menu of a column header: the columns with check marks, the properties, and for the time stamp its format
        /// </summary>
        internal ContextMenu BuildColumnMenu(GridViewColumn column, UIElement? placementTarget)
        {
            var keys = LogColumns.Normalize(ColumnKeys);
            var menu = new ContextMenu { PlacementTarget = placementTarget, Placement = PlacementMode.Bottom };

            MenuItem Column(string title, string key, bool enabled = true)
            {
                var item = new MenuItem { Header = title, IsCheckable = true, IsChecked = keys.Contains(key), IsEnabled = enabled };
                item.Click += (_, _) => SetColumnVisible(key, item.IsChecked);
                return item;
            }

            if (_columnKeys.GetValueOrDefault(column) is { } sortKey)
            {
                MenuItem Sort(string title, bool descending) => new()
                {
                    Header = title,
                    IsCheckable = true,
                    IsChecked = SortKey == sortKey && SortDescending == descending,
                    Command = new RelayCommand(() =>
                    {
                        SortDescending = descending;
                        SortKey = sortKey;
                    })
                };

                menu.Items.Add(Sort(LogViewerTexts.Get("Sort_Asc"), descending: false));
                menu.Items.Add(Sort(LogViewerTexts.Get("Sort_Desc"), descending: true));
                if (SortKey != null)
                    menu.Items.Add(new MenuItem { Header = LogViewerTexts.Get("Default_View"), Command = ResetSortCommand });

                menu.Items.Add(new Separator());
            }

            menu.Items.Add(Column(ColumnTitle(LogColumns.Id, false), LogColumns.Id));
            menu.Items.Add(Column(ColumnTitle(LogColumns.Level, false), LogColumns.Level));
            menu.Items.Add(Column(ColumnTitle(LogColumns.TimeStamp, false), LogColumns.TimeStamp));
            menu.Items.Add(Column(ColumnTitle(LogColumns.Logger, false), LogColumns.Logger));
            menu.Items.Add(Column(ColumnTitle(LogColumns.Thread, false), LogColumns.Thread));
            menu.Items.Add(Column(ColumnTitle(LogColumns.Message, false), LogColumns.Message, enabled: false));

            var shown = keys.Where(key => LogColumns.IsProperty(key, out _)).Select(key => key[LogColumns.PropertyPrefix.Length..]);
            var names = _propertyNames.Union(shown, StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count > 0)
            {
                var properties = new MenuItem { Header = LogViewerTexts.Get("Col_Properties") };
                foreach (var name in names)
                    properties.Items.Add(Column(name, LogColumns.Property(name)));

                menu.Items.Add(new Separator());
                menu.Items.Add(properties);
            }

            if (_columnKeys.GetValueOrDefault(column) == LogColumns.TimeStamp)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(TimeStampFormatItems());
                if (ChangeTimeZoneCommand != null)
                    menu.Items.Add(new MenuItem { Header = LogViewerTexts.Get("Col_TimeZone"), Command = ChangeTimeZoneCommand, CommandParameter = DataContext });
            }

            return menu;
        }

        /// <summary>
        /// The header of the time stamp column says which zones are converted, e.g. <c>TimeStamp (UTC+9 → local)</c>
        /// </summary>
        private void UpdateTimeStampHeader() => UpdateHeaders();

        private MenuItem TimeStampFormatItems()
        {
            var formats = new MenuItem { Header = LogViewerTexts.Get("Col_Format"), IsEnabled = CanChangeTimeStampFormat };

            MenuItem Format(string title, TimeStampFormat format) => new()
            {
                Header = title,
                IsChecked = EffectiveTimeStampFormat == format,
                Command = SetTimeStampFormatCommand,
                CommandParameter = format
            };

            formats.Items.Add(Format(LogViewerTexts.Get("Col_FormatSystem"), TimeStampFormat.System));
            formats.Items.Add(Format("ISO  2025-12-18 15:11:48.385", TimeStampFormat.Iso));
            formats.Items.Add(Format("German  18.12.2025 15:11:48.385", TimeStampFormat.German));
            formats.Items.Add(Format("English  12/18/2025 03:11:48.385 PM", TimeStampFormat.English));
            formats.Items.Add(new Separator());
            formats.Items.Add(new MenuItem { Header = LogViewerTexts.Get("Default_View"), Command = ResetTimeStampFormatCommand });
            return formats;
        }

        /// <summary>
        /// Gives a column the text of a property of the entry
        /// </summary>
        private sealed class PropertyValueConverter : IValueConverter
        {
            public static PropertyValueConverter Instance { get; } = new();

            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                value is LogEntry entry && parameter is string name ? LogColumns.ValueOf(entry, name) : null;

            public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
                throw new NotSupportedException();
        }
    }
}
