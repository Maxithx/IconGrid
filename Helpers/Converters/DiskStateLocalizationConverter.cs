using System;
using System.Globalization;
using System.Windows.Data;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers;

/// <summary>
/// Localizes a language-neutral disk code (health, media type, power state, SMART status).
/// Values: [0] = code, [1] = current UI language. ConverterParameter = localization key prefix.
/// </summary>
public class DiskStateLocalizationConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var code = values.Length > 0 ? values[0] as string : null;
        var language = values.Length > 1 ? values[1] as string : null;
        var prefix = parameter as string;

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        return LocalizationHelper.Get(
            string.IsNullOrWhiteSpace(language) ? "en" : language,
            prefix + code);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
