using System.Globalization;
using System.Windows;

namespace IconGrid.Controls
{
    public partial class SliderRow : System.Windows.Controls.UserControl
    {
        public SliderRow()
        {
            InitializeComponent();
            UpdateFormattedValue();
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderRow), new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderRow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow), new PropertyMetadata(0.0));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow), new PropertyMetadata(100.0));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(SliderRow), new PropertyMetadata(0.02));

        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(SliderRow), new PropertyMetadata(0.05));

        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register(nameof(TickFrequency), typeof(double), typeof(SliderRow), new PropertyMetadata(0.02));

        public double TickFrequency
        {
            get => (double)GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        public static readonly DependencyProperty IsSnapToTickEnabledProperty =
            DependencyProperty.Register(nameof(IsSnapToTickEnabled), typeof(bool), typeof(SliderRow), new PropertyMetadata(false));

        public bool IsSnapToTickEnabled
        {
            get => (bool)GetValue(IsSnapToTickEnabledProperty);
            set => SetValue(IsSnapToTickEnabledProperty, value);
        }

        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(SliderRow), new PropertyMetadata("{0:F0}", OnValueFormatChanged));

        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        public static readonly DependencyProperty LabelForegroundProperty =
            DependencyProperty.Register(nameof(LabelForeground), typeof(System.Windows.Media.Brush), typeof(SliderRow), new PropertyMetadata(System.Windows.Media.Brushes.Gainsboro));

        public System.Windows.Media.Brush LabelForeground
        {
            get => (System.Windows.Media.Brush)GetValue(LabelForegroundProperty);
            set => SetValue(LabelForegroundProperty, value);
        }

        private static readonly DependencyPropertyKey FormattedValuePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(FormattedValue), typeof(string), typeof(SliderRow), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty FormattedValueProperty = FormattedValuePropertyKey.DependencyProperty;

        public string FormattedValue
        {
            get => (string)GetValue(FormattedValueProperty);
            private set => SetValue(FormattedValuePropertyKey, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SliderRow)d).UpdateFormattedValue();
        }

        private static void OnValueFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SliderRow)d).UpdateFormattedValue();
        }

        private void UpdateFormattedValue()
        {
            var format = string.IsNullOrEmpty(ValueFormat) ? "{0:F0}" : ValueFormat;
            try
            {
                FormattedValue = string.Format(CultureInfo.CurrentCulture, format, Value);
            }
            catch
            {
                FormattedValue = Value.ToString(CultureInfo.CurrentCulture);
            }
        }
    }
}
