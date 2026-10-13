using System;
using System.Globalization;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
	public class ContextMenuHeaderConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string s)
			{
				// Trim to 100 characters and add ellipsis if longer
				if (s.Length > 100)
				{
					return s.Substring(0, 97) + "...";
				}
				return s;
			}

			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
