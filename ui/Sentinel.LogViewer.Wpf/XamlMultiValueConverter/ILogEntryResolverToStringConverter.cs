using System;
using System.Globalization;
using System.Windows.Data;
using Sentinel.LogViewer.Wpf.Resolver;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.XamlMultiValueConverter
{
	// ReSharper disable once InconsistentNaming
	public class ILogEntryResolverToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is LogEntry logEntry && values[1] is ILogEntryResolver resolver)
                return resolver.Resolve(logEntry);
            return "####";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
