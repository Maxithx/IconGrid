using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using IconGrid.Helpers.Hardware.Storage;

namespace IconGrid.Helpers;

/// <summary>
/// Maps a language-neutral disk health code to the status dot colour used on the
/// Hardware page.
/// </summary>
public class DiskHealthToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Healthy = Frozen(34, 197, 94);
    private static readonly SolidColorBrush Warning = Frozen(245, 158, 11);
    private static readonly SolidColorBrush Unhealthy = Frozen(239, 68, 68);
    private static readonly SolidColorBrush Unknown = Frozen(148, 163, 184);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            DiskHealthCodes.Healthy => Healthy,
            DiskHealthCodes.Warning => Warning,
            DiskHealthCodes.Unhealthy => Unhealthy,
            _ => Unknown
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
