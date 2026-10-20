using System;
using System.Globalization;
using System.Windows.Data;
using Sentinel.LogViewer.Core;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
	/// <summary>
	/// Converts between an optional level and the index of a list that starts with "any level" and then has the levels in their order
	/// </summary>
	public class LevelIndexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value is LogEntryLevel level ? (int)level + 1 : 0;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			value is int index && index > 0 ? (LogEntryLevel)(index - 1) : null!;
	}
}
