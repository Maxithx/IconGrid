using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WMedia = System.Windows.Media;

namespace IconGrid.Helpers;

public class BoolToBrushConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty TrueBrushProperty =
        DependencyProperty.Register(nameof(TrueBrush), typeof(WMedia.Brush), typeof(BoolToBrushConverter), new PropertyMetadata(WMedia.Brushes.Transparent));

    public static readonly DependencyProperty FalseBrushProperty =
        DependencyProperty.Register(nameof(FalseBrush), typeof(WMedia.Brush), typeof(BoolToBrushConverter), new PropertyMetadata(WMedia.Brushes.Transparent));

    public WMedia.Brush TrueBrush
    {
        get => (WMedia.Brush)GetValue(TrueBrushProperty);
        set => SetValue(TrueBrushProperty, value);
    }

    public WMedia.Brush FalseBrush
    {
        get => (WMedia.Brush)GetValue(FalseBrushProperty);
        set => SetValue(FalseBrushProperty, value);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TrueBrush : FalseBrush;
        }

        return FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
