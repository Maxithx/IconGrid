using System.Windows;

namespace IconGrid.Controls
{
    public partial class LauncherTabsBar : System.Windows.Controls.UserControl
    {
        public LauncherTabsBar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? TabToggleClick;
        public event RoutedEventHandler? RenameTabClick;
        public event RoutedEventHandler? RemoveTabClick;
        public event RoutedEventHandler? AddTabClick;
        public event RoutedEventHandler? MoreClick;

        private void ScrollTabsLeft_Click(object sender, RoutedEventArgs e)
        {
            TabsScrollViewer?.LineLeft();
            TabsScrollViewer?.LineLeft();
            TabsScrollViewer?.LineLeft();
        }

        private void ScrollTabsRight_Click(object sender, RoutedEventArgs e)
        {
            TabsScrollViewer?.LineRight();
            TabsScrollViewer?.LineRight();
            TabsScrollViewer?.LineRight();
        }

        private void TabToggle_Click(object sender, RoutedEventArgs e)
        {
            TabToggleClick?.Invoke(sender, e);
        }

        private void RenameTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RenameTabClick?.Invoke(sender, e);
        }

        private void RemoveTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RemoveTabClick?.Invoke(sender, e);
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddTabClick?.Invoke(sender, e);
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreClick?.Invoke(sender, e);
        }
    }
}
