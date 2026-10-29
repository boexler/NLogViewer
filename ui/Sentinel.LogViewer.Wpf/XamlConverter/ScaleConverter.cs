using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
    /// <summary>
    /// Multiplies a number by the converter parameter, e.g. the size of the mark of a bookmark follows the font size of the list
    /// </summary>
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double number && double.TryParse((string?)parameter, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor)
                ? number * factor
                : DependencyProperty.UnsetValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
