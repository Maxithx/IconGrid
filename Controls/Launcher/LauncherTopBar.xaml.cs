using System.Windows;
using System.Windows.Controls;

namespace IconGrid.Controls
{
    public partial class LauncherTopBar : System.Windows.Controls.UserControl
    {
        public LauncherTopBar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? LogoClick;
        public event RoutedEventHandler? LayoutContextMenuOpened;
        public event RoutedEventHandler? MinimizeClick;
        public event RoutedEventHandler? CloseClick;

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            LogoClick?.Invoke(this, e);
        }

        private void LayoutContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            LayoutContextMenuOpened?.Invoke(sender, e);
        }

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
