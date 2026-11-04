using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.Helper
{
    /// <summary>
    /// The panel below the list of a viewer (the data context): the tabs <see cref="BottomPanelTab.Results"/>, <see cref="BottomPanelTab.Repeats"/> and
    /// <see cref="BottomPanelTab.Details"/>
    /// </summary>
    public partial class BottomPanel : UserControl
    {
        public BottomPanel()
        {
            InitializeComponent();
        }

        // Only a double click on a row selects the entry, not one on the header or the empty space.
        private void OnHitDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RowClick.Of(HitRows, e) is var (row, _) && row.Content is LogEntry entry && DataContext is LogViewer viewer)
                viewer.GoToHitCommand.Execute(entry);
        }

        private void OnRepeatDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // The first of the entries; the button below goes to the last one.
            if (RowClick.Of(RepeatRows, e) is var (row, _) && row.Content is RepeatGroup group && DataContext is LogViewer viewer)
                viewer.GoToFirstRepeatCommand.Execute(group);
        }
    }

    /// <summary>
    /// The index of a tab and the <see cref="BottomPanelTab"/> that stands for it
    /// </summary>
    internal sealed class EnumIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (int)value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            (int)value < 0 ? Binding.DoNothing : (BottomPanelTab)(int)value;
    }
}
