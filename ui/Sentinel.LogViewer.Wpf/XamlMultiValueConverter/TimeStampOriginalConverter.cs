using System;
using System.Globalization;
using System.Windows.Data;
using Sentinel.LogViewer.Core;
using Sentinel.LogViewer.Wpf.Resolver;

namespace Sentinel.LogViewer.Wpf.XamlMultiValueConverter
{
    /// <summary>
    /// The tooltip of a time stamp cell: the time as it is in the entry, with its zone, while the times are converted; nothing otherwise
    /// </summary>
    public class TimeStampOriginalConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length > 1 && values[0] is LogEntry logEntry && values[1] is TimeStampResolver { IsConverting: true } resolver
                ? "Original: " + resolver.Original(logEntry)
                : null;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
