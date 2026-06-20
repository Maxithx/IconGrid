using System.Windows;
using System.Windows.Media;

namespace IconGrid.Controls
{
    public partial class LauncherLogo : System.Windows.Controls.UserControl
    {
        public LauncherLogo()
        {
            InitializeComponent();
        }

        public System.Windows.Media.Brush AccentBrush
        {
            get => (System.Windows.Media.Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(System.Windows.Media.Brush), typeof(LauncherLogo),
                new PropertyMetadata(System.Windows.Media.Brushes.SteelBlue));
    }
}
