using System.Windows;
using IconGrid.ViewModels;

namespace IconGrid.Controls
{
    public partial class LauncherTabsBar : System.Windows.Controls.UserControl
    {
        public LauncherTabsBar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? TabToggleClick;
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
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not string tabName)
                return;

            if (DataContext is not MainViewModel viewModel)
                return;

            var newName = ShowInputBox("Enter a new name for the tab:", "Rename Tab", tabName);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                viewModel.RenameTab(tabName, newName);
            }
        }

        private void RemoveTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not string tabName)
                return;

            if (DataContext is not MainViewModel viewModel)
                return;

            viewModel.RemoveTab(tabName);
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel)
                return;

            var name = ShowInputBox("Enter new tab name:", "Add Category", "Video");
            if (!string.IsNullOrWhiteSpace(name))
            {
                viewModel.AddTab(name);
            }
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreClick?.Invoke(sender, e);
        }

        private static string ShowInputBox(string prompt, string title, string defaultValue)
        {
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);
        }
    }
}
