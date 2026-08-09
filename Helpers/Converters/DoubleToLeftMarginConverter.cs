using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IconGrid.Helpers.Converters
{
    /// <summary>
    /// Converts a double value to a Thickness with Left = value, others = 0.
    /// Used for MonitorRow element spacing.
    /// </summary>
    [ValueConversion(typeof(double), typeof(Thickness))]
    public class DoubleToLeftMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var d = value is double dVal ? dVal : System.Convert.ToDouble(value);
            return new Thickness(d, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Thickness t)
                return t.Left;
            return 0.0;
        }
    }
}