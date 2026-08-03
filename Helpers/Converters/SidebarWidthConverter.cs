using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    [ValueConversion(typeof(bool), typeof(double))]
    public class SidebarWidthConverter : IValueConverter
    {
        public double CollapsedWidth { get; set; } = 52;
        public double ExpandedWidth { get; set; } = 220;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCollapsed)
            {
                return isCollapsed ? CollapsedWidth : ExpandedWidth;
            }
            return ExpandedWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}