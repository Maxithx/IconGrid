using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Maps a 0-100 percentage to a pixel length (meter bar heights, segment widths).
    /// Two shapes are supported:
    ///  - single binding plus ConverterParameter = maximum length (classic XAML usage),
    ///  - MultiBinding of (percent, maximum length) so one module can expose its own meter size.
    /// </summary>
    public class PercentToLengthConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var max = 12.0;
            if (parameter != null &&
                double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                max = parsed;
            }

            return ToLength(value, max);
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var max = values.Length > 1 ? ToDouble(values[1], 12.0) : 12.0;
            return ToLength(values.Length > 0 ? values[0] : null, max);
        }

        private static double ToLength(object? value, double max)
        {
            var percent = Math.Clamp(ToDouble(value, 0.0), 0.0, 100.0);
            return max * (percent / 100.0);
        }

        private static double ToDouble(object? value, double fallback) =>
            value != null &&
            double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
