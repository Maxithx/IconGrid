using RadioButton = System.Windows.Controls.RadioButton;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IconGrid.Models;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;

namespace IconGrid.Controls
{
    public partial class LauncherTabsBar : System.Windows.Controls.UserControl
    {
        private System.Windows.Point _tabDragStartPoint;

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

        // ---------- Drag-and-drop from icon grid to tabs ----------

        private void Tab_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (HasLauncherItem(e) || e.Data.GetDataPresent("TabReorder"))
            {
                if (sender is RadioButton tab)
                {
                    tab.Opacity = 0.7;
                }

                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void Tab_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (HasLauncherItem(e) || e.Data.GetDataPresent("TabReorder"))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void Tab_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is RadioButton tab)
            {
                tab.Opacity = 1.0;
            }
        }

        private void Tab_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is not RadioButton tab)
                return;

            tab.Opacity = 1.0;

            // The DataContext of the RadioButton is the tab name (string).
            if (tab.DataContext is not string targetTab || string.IsNullOrWhiteSpace(targetTab))
                return;

            if (DataContext is not MainViewModel viewModel)
                return;

            // Tab reorder: move one tab before/after another.
            if (TryGetTabReorderSource(e, out var sourceTabName))
            {
                var fe = tab;
                var pos = e.GetPosition(fe);
                var insertAfter = pos.X > fe.ActualWidth / 2;
                viewModel.MoveTab(sourceTabName, targetTab, insertAfter);
                e.Handled = true;
                return;
            }

            // Icon-to-category: move a launcher item to a different tab's category.
            var source = e.Data.GetData(typeof(LauncherItem)) as LauncherItem
                      ?? e.Data.GetData("LauncherItem") as LauncherItem;
            if (source == null)
                return;

            viewModel.MoveItemToCategory(source, targetTab);
            e.Handled = true;
        }

        private static bool HasLauncherItem(System.Windows.DragEventArgs e)
        {
            return e.Data.GetDataPresent(typeof(LauncherItem)) || e.Data.GetDataPresent("LauncherItem");
        }

        private static bool TryGetTabReorderSource(System.Windows.DragEventArgs e, out string tabName)
        {
            if (e.Data.GetDataPresent("TabReorder") && e.Data.GetData("TabReorder") is string name)
            {
                tabName = name;
                return true;
            }

            tabName = string.Empty;
            return false;
        }

        // ---------- Tab reorder drag-and-drop ----------

        private void Tab_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _tabDragStartPoint = e.GetPosition(null);
        }

        private void Tab_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                return;

            var position = e.GetPosition(null);
            var diff = position - _tabDragStartPoint;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (sender is not RadioButton tab || tab.DataContext is not string tabName)
                return;

            var data = new System.Windows.DataObject();
            data.SetData("TabReorder", tabName);
            System.Windows.DragDrop.DoDragDrop(tab, data, System.Windows.DragDropEffects.Move);
        }
    }
}
