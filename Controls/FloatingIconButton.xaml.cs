using System.Windows;

namespace IconGrid.Controls
{
    public partial class FloatingIconButton : System.Windows.Controls.UserControl
    {
        public FloatingIconButton()
        {
            InitializeComponent();
        }

        public System.Windows.Media.Brush AccentBrush
        {
            get => (System.Windows.Media.Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(System.Windows.Media.Brush), typeof(FloatingIconButton),
                new PropertyMetadata(System.Windows.Media.Brushes.SteelBlue));

        public string ToolTipText
        {
            get => (string)GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(FloatingIconButton),
                new PropertyMetadata("Open IconGrid"));

        public event System.Windows.Input.MouseButtonEventHandler? FloatingIconMouseLeftButtonDown;
        public event System.Windows.Input.MouseEventHandler? FloatingIconMouseMove;
        public event System.Windows.Input.MouseButtonEventHandler? FloatingIconMouseLeftButtonUp;
        public event RoutedEventHandler? OpenRequested;
        public event RoutedEventHandler? ExitRequested;

        private void InnerButton_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FloatingIconMouseLeftButtonDown?.Invoke(this, e);
        }

        private void InnerButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            FloatingIconMouseMove?.Invoke(this, e);
        }

        private void InnerButton_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FloatingIconMouseLeftButtonUp?.Invoke(this, e);
        }

        private void InnerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenRequested?.Invoke(this, e);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, e);
        }
    }
}
