using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IconGrid.Helpers;

/// <summary>
/// Returns Visible when the bound string equals ConverterParameter; otherwise Collapsed.
/// Used to show/hide UI sections based on a selected combo value.
/// </summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value as string;
        var p = parameter as string;
        if (s == null || p == null)
        {
            return Visibility.Collapsed;
        }
        return string.Equals(s, p, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}