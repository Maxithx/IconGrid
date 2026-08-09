using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers.Converters
{
    /// <summary>
    /// Converts a double pixel width to itself, or Double.NaN (auto) when the value is 0.
    /// Used for optional fixed-width monitor row value slots.
    /// </summary>
    public class ZeroToAutoWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 0)
                return d;

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && !double.IsNaN(d))
                return d;

            return 0.0;
        }
    }
}