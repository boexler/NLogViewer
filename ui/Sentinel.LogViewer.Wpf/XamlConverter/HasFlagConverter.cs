using System;
using System.Globalization;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
	/// <summary>
	/// Tells whether a flags value contains the flag of the converter parameter, e.g. to check the menu entry of a field
	/// </summary>
	public class HasFlagConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value is Enum flags && parameter is Enum flag && flags.HasFlag(flag);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
