using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    public class PercentToHeightConverter : IValueConverter
    {
        // Converts a percentage (0-100) into a pixel height based on the provided parameter (container height).
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0.0;

            if (!double.TryParse(value.ToString(), out var percent))
                return 0.0;

            double max = 12.0;
            if (parameter != null && double.TryParse(parameter.ToString(), out var parsedMax))
                max = parsedMax;

            percent = Math.Clamp(percent, 0.0, 100.0);
            return max * (percent / 100.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
