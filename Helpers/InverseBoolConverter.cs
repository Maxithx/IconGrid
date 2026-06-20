using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Simple converter that inverts a boolean value.
    /// Uses WPF types only (System.Windows.Data.IValueConverter) to avoid ambiguity
    /// with System.Windows.Forms.Binding/other WinForms types.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            // If target expects Visibility, return Visible/Collapsed inverted
            if (targetType == typeof(Visibility) && value is bool vb)
                return vb ? Visibility.Collapsed : Visibility.Visible;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;

            // Support converting back from Visibility
            if (value is Visibility vis)
                return vis != Visibility.Visible;

            return DependencyProperty.UnsetValue;
        }
    }
}
