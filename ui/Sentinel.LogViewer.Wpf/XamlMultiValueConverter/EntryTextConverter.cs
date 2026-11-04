using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.XamlMultiValueConverter
{
    /// <summary>
    /// A text of an entry for the lists of the panel, written like the main list writes it. The values are the entry (or its level) and the viewer;
    /// the parameter says which text: <c>Id</c>, <c>Time</c>, <c>Level</c> or <c>Message</c> (the first line).
    /// </summary>
    public class EntryTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[1] is not LogViewer viewer)
                return DependencyProperty.UnsetValue;

            return (string?)parameter switch
            {
                "Time" when values[0] is LogEntry entry => viewer.TimeText(entry),
                "Id" when values[0] is LogEntry entry => viewer.IdResolver?.Resolve(entry) ?? string.Empty,
                "Level" when values[0] is LogEntry entry => viewer.LevelText(entry.Level),
                "Level" when values[0] is RepeatGroup group => viewer.LevelText(group.Level),
                "Message" when values[0] is LogEntry entry => FirstLine(entry.Message),
                _ => DependencyProperty.UnsetValue
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();

        private static string FirstLine(string message) => message.Split('\n', 2)[0].TrimEnd('\r');
    }
}
