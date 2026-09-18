using System;
using System.Globalization;
using System.Windows.Data;
using Sentinel.LogViewer.Wpf.Resolver;
using NLog;

namespace Sentinel.LogViewer.Wpf.XamlMultiValueConverter
{
	// ReSharper disable once InconsistentNaming
	public class ILogEventResolverToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is LogEventInfo logEventInfo && values[1] is ILogEventInfoResolver resolver)
                return resolver.Resolve(logEventInfo);
            return "####";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
