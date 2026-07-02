using RadioButton = System.Windows.Controls.RadioButton;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;

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

            if (sender is System.Windows.Controls.RadioButton radioButton)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() => EnsureTabVisible(radioButton)));
            }
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

        private void EnsureTabVisible(RadioButton tabButton)
        {
            if (TabsScrollViewer == null || !tabButton.IsVisible)
                return;

            if (VisualTreeHelper.GetParent(tabButton) is not Visual)
                return;

            var transform = tabButton.TransformToAncestor(TabsScrollViewer);
            var bounds = transform.TransformBounds(new Rect(0, 0, tabButton.ActualWidth, tabButton.ActualHeight));
            var leftEdge = bounds.Left;
            var rightEdge = bounds.Right;
            var viewportWidth = TabsScrollViewer.ViewportWidth;

            if (viewportWidth <= 0)
                return;

            if (leftEdge < 0)
            {
                TabsScrollViewer.ScrollToHorizontalOffset(TabsScrollViewer.HorizontalOffset + leftEdge);
                return;
            }

            if (rightEdge > viewportWidth)
            {
                var delta = rightEdge - viewportWidth;
                TabsScrollViewer.ScrollToHorizontalOffset(TabsScrollViewer.HorizontalOffset + delta);
            }
        }
    }
}
