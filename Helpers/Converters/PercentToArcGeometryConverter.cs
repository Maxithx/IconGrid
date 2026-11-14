using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Builds an arc geometry for a ring gauge: 0-100 percent maps to a clockwise sweep that starts
    /// at 12 o'clock. ConverterParameter is the box size in DIPs; the stroke is centred on a radius
    /// of size/2 - 1 so a 2 DIP stroke stays inside the box.
    /// </summary>
    public class PercentToArcGeometryConverter : IValueConverter
    {
        private const double StrokeThickness = 2.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var percent = ToDouble(value);
            percent = Math.Clamp(percent, 0.0, 100.0);
            if (percent <= 0.05)
            {
                return Geometry.Empty;
            }

            var size = ToDouble(parameter, 16.0);
            var center = size / 2.0;
            var radius = Math.Max(1.0, center - StrokeThickness / 2.0);
            var start = new Point(center, center - radius);

            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };

            if (percent >= 99.95)
            {
                figure.Segments.Add(new ArcSegment(new Point(center, center + radius),
                    new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
                figure.Segments.Add(new ArcSegment(start,
                    new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            }
            else
            {
                var angle = Math.PI * 2.0 * (percent / 100.0);
                var end = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
                figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0,
                    percent > 50.0, SweepDirection.Clockwise, true));
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        private static double ToDouble(object? value, double fallback = 0.0) =>
            value != null &&
            double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
