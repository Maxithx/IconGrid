using System;
using System.Globalization;
using System.Windows.Data;

namespace IconGrid.Helpers
{
    [ValueConversion(typeof(double), typeof(double))]
    public class TemplateContentWidthConverter : IValueConverter
    {
        /// <summary>
        /// The widest the hero/certified card column should ever get.
        /// </summary>
        public double MaxWidth { get; set; } = 1280;

        /// <summary>
        /// The smallest usable width for the hero/certified column before wrapping gets awkward.
        /// </summary>
        public double MinWidth { get; set; } = 520;

        /// <summary>
        /// Space reserved for gutters so the cards never touch the viewport edges.
        /// </summary>
        public double HorizontalGutter { get; set; } = 48;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double availableWidth && !double.IsNaN(availableWidth))
            {
                var adjusted = availableWidth - HorizontalGutter;
                if (double.IsNaN(adjusted) || double.IsInfinity(adjusted))
                {
                    return MaxWidth;
                }

                // Clamp the width between the configured min/max.
                return Math.Max(MinWidth, Math.Min(MaxWidth, adjusted));
            }

            return MaxWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
