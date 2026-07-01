using System;
using System.Globalization;
using System.Windows.Data;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers;

public class TabNameLocalizationConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return values?.Length > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;
        }

        var tabName = values[0]?.ToString() ?? string.Empty;
        var language = values[1]?.ToString() ?? "en";

        var translationKey = tabName switch
        {
            "Games" => "TabGames",
            "Software" => "TabSoftware",
            "Develop" => "TabDevelop",
            _ => null
        };

        if (translationKey == null)
        {
            return tabName;
        }

        return LocalizationHelper.Get(language, translationKey);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
