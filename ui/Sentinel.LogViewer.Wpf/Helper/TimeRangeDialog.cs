using System;
using System.Windows;
using System.Windows.Controls;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The dialog for the time range of a viewer: the start and the end as the times that the viewer shows, either of them may be empty
    /// </summary>
    internal sealed class TimeRangeDialog : Window
    {
        private readonly TextBox _from = new() { MinWidth = 190 };
        private readonly TextBox _to = new() { MinWidth = 190 };
        private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
        private readonly TimeZoneInfo? _zone;

        public TimeRangeDialog(TimeZoneInfo? zone, DateTime? from, DateTime? to)
        {
            _zone = zone;
            Title = "Time range";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _from.Text = TimeRangeInput.Format(from, zone);
            _to.Text = TimeRangeInput.Format(to, zone);

            var grid = new Grid { Margin = new Thickness(14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var i = 0; i < 4; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Place(grid, new TextBlock { Text = "From", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) }, 0, 0);
            Place(grid, _from, 0, 1);
            _from.Margin = new Thickness(0, 0, 0, 8);
            Place(grid, new TextBlock { Text = "Until", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) }, 1, 0);
            Place(grid, _to, 1, 1);
            _to.Margin = new Thickness(0, 0, 0, 8);

            var hint = new TextBlock
            {
                Text = $"Times of the list ({EntryTime.Label(zone)}), e.g. 2025-12-18 14:30. An empty box has no limit; an end without a time includes the whole day.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 340,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetColumnSpan(hint, 2);
            Place(grid, hint, 2, 0);
            Grid.SetColumnSpan(_error, 2);
            _error.Margin = new Thickness(0, 0, 0, 8);
            Place(grid, _error, 3, 0);

            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(14, 4, 14, 4) };
            ok.Click += (_, _) => Accept();
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(14, 4, 14, 4) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };

            var root = new StackPanel { Children = { grid, buttons }, Margin = new Thickness(0, 0, 14, 14) };
            Content = root;
        }

        /// <summary>The start that was chosen, a moment in UTC; null for none</summary>
        public DateTime? From { get; private set; }

        /// <summary>The end that was chosen, a moment in UTC; null for none</summary>
        public DateTime? To { get; private set; }

        private static void Place(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private void Accept()
        {
            if (!TimeRangeInput.TryParse(_from.Text, _zone, false, out var from))
            {
                _error.Text = "The start is not a date and time.";
                _from.Focus();
                return;
            }

            if (!TimeRangeInput.TryParse(_to.Text, _zone, true, out var to))
            {
                _error.Text = "The end is not a date and time.";
                _to.Focus();
                return;
            }

            if (from > to)
            {
                _error.Text = "The start is after the end.";
                return;
            }

            From = from;
            To = to;
            DialogResult = true;
        }
    }
}
