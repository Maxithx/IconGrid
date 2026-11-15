using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Returns the first value of a MultiBinding that is actually set — null, unset and NaN are
    /// skipped. Used so an optional override (e.g. a layer group's own bar height) can fall back
    /// to the skin/blueprint value without duplicating the surrounding markup.
    /// </summary>
    public class FirstValueConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var value in values)
            {
                if (value == null || ReferenceEquals(value, System.Windows.DependencyProperty.UnsetValue))
                {
                    continue;
                }

                if (value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                {
                    continue;
                }

                return value;
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
