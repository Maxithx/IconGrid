using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IconGrid.Models;
using IconGrid.ViewModels;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the launcher drag & drop behavior that previously lived in MainWindow.xaml.cs:
    /// file-drop acceptance, launcher-item reordering drag, and drop-target handling.
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class LauncherDragDropHelper
    {
        private readonly MainViewModel _viewModel;
        private readonly Func<DependencyObject?, bool> _isOverLauncherTile;
        private System.Windows.Point _dragStartPoint;

        public LauncherDragDropHelper(MainViewModel viewModel, Func<DependencyObject?, bool> isOverLauncherTile)
        {
            _viewModel = viewModel;
            _isOverLauncherTile = isOverLauncherTile;
        }

        public bool TryGetSupportedDropFiles(System.Windows.DragEventArgs e, out string[] files)
        {
            files = Array.Empty<string>();

            if (!TryGetRawDropPaths(e, out var raw) || raw.Length == 0)
                return false;

            files = raw.Where(ShortcutHelper.IsSupportedLauncherFile).ToArray();
            return files.Length > 0;
        }

        public static bool TryGetRawDropPaths(System.Windows.DragEventArgs e, out string[] files)
        {
            files = Array.Empty<string>();

            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] fileDrop && fileDrop.Length > 0)
            {
                files = fileDrop;
                return true;
            }

            if (e.Data.GetDataPresent("Shell IDList Array"))
            {
                // Explorer sometimes exposes shell objects without a FileDrop array.
                // We still accept the drag so Windows doesn't show a blocked-drop cursor.
                return true;
            }

            if (e.Data.GetDataPresent("FileNameW") && e.Data.GetData("FileNameW") is string[] fileNamesW && fileNamesW.Length > 0)
            {
                files = fileNamesW;
                return true;
            }

            if (e.Data.GetDataPresent("FileName") && e.Data.GetData("FileName") is string[] fileNames && fileNames.Length > 0)
            {
                files = fileNames;
                return true;
            }

            return false;
        }

        public void HandleWindowDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
                return;

            e.Handled = true;
            if (TryGetRawDropPaths(e, out _))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        public void HandleWindowDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
                return;

            if (!TryGetSupportedDropFiles(e, out var files))
                return;

            e.Handled = true;
            _viewModel.HandleFileDrop(files);
        }

        public void HandleItemsControlDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent(typeof(LauncherItem)) || e.Data.GetDataPresent("LauncherItem"))
            {
                if (_isOverLauncherTile(e.OriginalSource as DependencyObject))
                {
                    return; // tile-level handler will show move effect
                }
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (TryGetRawDropPaths(e, out _))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        public void HandleItemsControlDrop(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent("LauncherItem"))
            {
                // Let tile-level drop handle reordering; ignore drops on empty area for launcher items.
                return;
            }

            if (!TryGetSupportedDropFiles(e, out var files))
                return;

            _viewModel.HandleFileDrop(files);
        }

        public void HandleLauncherItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        public void HandleLauncherItemPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var position = e.GetPosition(null);
            var diff = position - _dragStartPoint;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (sender is not System.Windows.Controls.Button button || button.DataContext is not LauncherItem item)
                return;

            var data = new System.Windows.DataObject();
            data.SetData("LauncherItem", item);
            data.SetData(typeof(LauncherItem), item);
            System.Windows.DragDrop.DoDragDrop(button, data, System.Windows.DragDropEffects.Move);
        }

        public void HandleLauncherItemDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        public void HandleLauncherItemDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(LauncherItem)) && !e.Data.GetDataPresent("LauncherItem"))
            {
                if (TryGetSupportedDropFiles(e, out var files))
                {
                    e.Handled = true;
                    _viewModel.HandleFileDrop(files);
                }

                return;
            }

            var source = e.Data.GetData(typeof(LauncherItem)) as LauncherItem ?? e.Data.GetData("LauncherItem") as LauncherItem;
            if (source == null)
                return;

            var target = (sender as FrameworkElement)?.DataContext as LauncherItem;
            if (target == null || ReferenceEquals(source, target))
                return;

            var fe = sender as FrameworkElement;
            var insertAfter = false;
            if (fe != null)
            {
                var pos = e.GetPosition(fe);
                insertAfter = pos.Y > fe.ActualHeight / 2;
            }

            _viewModel.MoveItemWithinCategory(source, target, insertAfter);
            e.Handled = true;
        }
    }
}