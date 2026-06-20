using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IconGrid.Helpers;

/// <summary>
/// Visible only when ShowScrollButtons is true and tab count exceeds the threshold (default 6).
/// </summary>
public class ScrollButtonsVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var enabled = values.Length > 0 && values[0] is bool b && b;
            var count = 0;
            if (values.Length > 1)
            {
                if (values[1] is int i) count = i;
                else if (values[1] is double d) count = (int)d;
            }

            var threshold = 6;
            if (parameter != null && int.TryParse(parameter.ToString(), out var parsed))
            {
                threshold = parsed;
            }

            return enabled && count > threshold ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            return Visibility.Collapsed;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
