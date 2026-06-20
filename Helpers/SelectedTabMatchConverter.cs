using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers;

public class SelectedTabMatchConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return false;
        }

        var tab = values[0] as string;
        var selected = values[1] as string;
        return !string.IsNullOrWhiteSpace(tab) && string.Equals(tab, selected, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
