using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    public sealed class LayoutSlotLabelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var reserveEnabled = TryGetBool(values, 0, out var reserve) ? reserve : false;
            var layoutSlot = TryGetInt(values, 1, out var slot) ? slot : 0;
            var buttonSlot = TryGetInt(values, 2, out var button) ? button : 0;

            if (reserveEnabled && layoutSlot == buttonSlot)
            {
                return "IG";
            }

            return (buttonSlot + 1).ToString(culture);
        }

        private static bool TryGetBool(object[] values, int index, out bool result)
        {
            result = false;
            if (values.Length <= index)
            {
                return false;
            }

            if (values[index] is bool boolValue)
            {
                result = boolValue;
                return true;
            }

            if (bool.TryParse(values[index]?.ToString(), out boolValue))
            {
                result = boolValue;
                return true;
            }

            return false;
        }

        private static bool TryGetInt(object[] values, int index, out int result)
        {
            result = 0;
            if (values.Length <= index)
            {
                return false;
            }

            if (values[index] is int intValue)
            {
                result = intValue;
                return true;
            }

            if (int.TryParse(values[index]?.ToString(), out intValue))
            {
                result = intValue;
                return true;
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
