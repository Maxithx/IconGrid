using System.Windows;
using System.Windows.Controls;

namespace IconGrid.Controls
{
    public partial class LauncherLogoArea : System.Windows.Controls.UserControl
    {
        public LauncherLogoArea()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? LogoClick;
        public event RoutedEventHandler? LayoutContextMenuOpened;

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            LogoClick?.Invoke(this, e);
        }

        private void LayoutContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            LayoutContextMenuOpened?.Invoke(sender, e);
        }
    }
}
