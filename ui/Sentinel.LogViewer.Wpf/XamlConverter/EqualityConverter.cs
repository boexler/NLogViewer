using System;
using System.Globalization;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
	/// <summary>
	/// Tells whether the value equals the converter parameter, e.g. to check the menu entry of the current choice
	/// </summary>
	public class EqualityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value, parameter);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
}
