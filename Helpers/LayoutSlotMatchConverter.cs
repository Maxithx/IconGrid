using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    public sealed class LayoutSlotMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return false;
            }

            if (values[0] is int layoutSlot && TryParseSlot(values[1], out var buttonSlot))
            {
                return layoutSlot == buttonSlot;
            }

            return false;
        }

        private static bool TryParseSlot(object value, out int slot)
        {
            slot = 0;
            if (value == null)
            {
                return false;
            }

            if (value is int intValue)
            {
                slot = intValue;
                return true;
            }

            return int.TryParse(value.ToString(), out slot);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
