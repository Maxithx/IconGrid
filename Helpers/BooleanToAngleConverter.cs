using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    public class BooleanToAngleConverter : IValueConverter
    {
        public double TrueAngle { get; set; } = 180;
        public double FalseAngle { get; set; } = 0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value as bool? ?? false;
            return flag ? TrueAngle : FalseAngle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
