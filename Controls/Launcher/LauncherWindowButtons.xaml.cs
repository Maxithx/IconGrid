using System.Windows;
using System.Windows.Controls;

namespace IconGrid.Controls
{
    public partial class LauncherWindowButtons : System.Windows.Controls.UserControl
    {
        public LauncherWindowButtons()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? MinimizeClick;
        public event RoutedEventHandler? CloseClick;

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            MinimizeClick?.Invoke(this, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseClick?.Invoke(this, e);
        }
    }
}
