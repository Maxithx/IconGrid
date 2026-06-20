using System.Windows;

namespace IconGrid.Views
{
    public partial class TemplatePage : System.Windows.Controls.UserControl
    {
        public TemplatePage()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty HeroContentProperty =
            DependencyProperty.Register(nameof(HeroContent), typeof(object), typeof(TemplatePage), new PropertyMetadata(null));

        public object? HeroContent
        {
            get => GetValue(HeroContentProperty);
            set => SetValue(HeroContentProperty, value);
        }

        public static readonly DependencyProperty CardsContentProperty =
            DependencyProperty.Register(nameof(CardsContent), typeof(object), typeof(TemplatePage), new PropertyMetadata(null));

        public object? CardsContent
        {
            get => GetValue(CardsContentProperty);
            set => SetValue(CardsContentProperty, value);
        }
    }
}
