using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sentinel.LogViewer.Wpf.XamlConverter
{
    /// <summary>
    /// Converter that converts a boolean value to Visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to Visibility
        /// </summary>
        /// <param name="value">The boolean value to convert</param>
        /// <param name="targetType">The target type (not used)</param>
        /// <param name="parameter">Optional parameter to invert the conversion</param>
        /// <param name="culture">The culture (not used)</param>
        /// <returns>Visibility.Visible if true, Visibility.Collapsed if false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if parameter is "Invert" to reverse the logic
                bool invert = parameter?.ToString()?.Contains("i", StringComparison.OrdinalIgnoreCase) == true;
                
                if (invert)
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        /// <summary>
        /// Converts Visibility back to boolean
        /// </summary>
        /// <param name="value">The Visibility value to convert</param>
        /// <param name="targetType">The target type (not used)</param>
        /// <param name="parameter">Optional parameter to invert the conversion</param>
        /// <param name="culture">The culture (not used)</param>
        /// <returns>true if Visible, false if Collapsed</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                // Check if parameter is "Invert" to reverse the logic
                bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
                
                if (invert)
                {
                    return visibility == Visibility.Collapsed;
                }
                
                return visibility == Visibility.Visible;
            }
            
            return false;
        }
    }
}
