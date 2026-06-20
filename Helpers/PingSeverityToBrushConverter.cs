using System;
using System.Globalization;
using System.Windows.Data;
using WMedia = System.Windows.Media;

namespace IconGrid.Helpers;

public class PingSeverityToBrushConverter : IValueConverter
{
    public WMedia.Brush GoodBrush { get; set; } = WMedia.Brushes.LimeGreen;
    public WMedia.Brush WarningBrush { get; set; } = WMedia.Brushes.Orange;
    public WMedia.Brush CriticalBrush { get; set; } = WMedia.Brushes.Red;
    public WMedia.Brush UnknownBrush { get; set; } = WMedia.Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PingSeverity severity)
        {
            return severity switch
            {
                PingSeverity.Good => GoodBrush,
                PingSeverity.Warning => WarningBrush,
                PingSeverity.Critical => CriticalBrush,
                _ => UnknownBrush
            };
        }

        return UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
