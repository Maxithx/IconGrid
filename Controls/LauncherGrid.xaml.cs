using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace IconGrid.Controls
{
    public partial class LauncherGrid : System.Windows.Controls.UserControl
    {
        public LauncherGrid()
        {
            InitializeComponent();
        }

        public event System.Windows.DragEventHandler? ItemsDragOver;
        public event System.Windows.DragEventHandler? ItemsDrop;
        public event ContextMenuEventHandler? ContentAreaContextMenuOpening;
        public event ScrollChangedEventHandler? IconScrollChanged;
        public event System.Windows.Input.MouseButtonEventHandler? LauncherItemPreviewMouseLeftButtonDown;
        public event System.Windows.Input.MouseEventHandler? LauncherItemPreviewMouseMove;
        public event System.Windows.DragEventHandler? LauncherItemDragOver;
        public event System.Windows.DragEventHandler? LauncherItemDrop;
        public event System.Windows.Input.MouseButtonEventHandler? LauncherItemMouseDoubleClick;
        public event RoutedEventHandler? OpenItemClick;
        public event RoutedEventHandler? RunAsAdminClick;
        public event RoutedEventHandler? OpenLocationClick;
        public event RoutedEventHandler? CopyPathClick;
        public event RoutedEventHandler? ChangeIconClick;
        public event RoutedEventHandler? ResetIconClick;
        public event RoutedEventHandler? RenameItemClick;
        public event RoutedEventHandler? RemoveItemClick;

        private void ItemsControl_DragOver(object sender, System.Windows.DragEventArgs e) => ItemsDragOver?.Invoke(sender, e);
        private void ItemsControl_Drop(object sender, System.Windows.DragEventArgs e) => ItemsDrop?.Invoke(sender, e);
        private void ContentArea_ContextMenuOpening(object sender, ContextMenuEventArgs e) => ContentAreaContextMenuOpening?.Invoke(sender, e);
        private void IconScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => IconScrollChanged?.Invoke(sender, e);
        private void LauncherItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => LauncherItemPreviewMouseLeftButtonDown?.Invoke(sender, e);
        private void LauncherItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => LauncherItemPreviewMouseMove?.Invoke(sender, e);
        private void LauncherItem_DragOver(object sender, System.Windows.DragEventArgs e) => LauncherItemDragOver?.Invoke(sender, e);
        private void LauncherItem_Drop(object sender, System.Windows.DragEventArgs e) => LauncherItemDrop?.Invoke(sender, e);
        private void LauncherItemButton_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => LauncherItemMouseDoubleClick?.Invoke(sender, e);
        private void OpenItemMenuItem_Click(object sender, RoutedEventArgs e) => OpenItemClick?.Invoke(sender, e);
        private void RunAsAdminMenuItem_Click(object sender, RoutedEventArgs e) => RunAsAdminClick?.Invoke(sender, e);
        private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e) => OpenLocationClick?.Invoke(sender, e);
        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e) => CopyPathClick?.Invoke(sender, e);
        private void ChangeIconMenuItem_Click(object sender, RoutedEventArgs e) => ChangeIconClick?.Invoke(sender, e);
        private void ResetIconMenuItem_Click(object sender, RoutedEventArgs e) => ResetIconClick?.Invoke(sender, e);
        private void RenameMenuItem_Click(object sender, RoutedEventArgs e) => RenameItemClick?.Invoke(sender, e);
        private void RemoveItemMenuItem_Click(object sender, RoutedEventArgs e) => RemoveItemClick?.Invoke(sender, e);
    }
}
