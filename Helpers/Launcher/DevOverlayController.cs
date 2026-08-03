using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using IconGrid.ViewModels;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the dev-inspector overlay behavior that previously lived in MainWindow.xaml.cs:
    /// attaching mouse tracking to the root layout grid, showing/hiding the overlay panel,
    /// and rendering hover metadata (DataContext, Content, Command, size, margin, etc.).
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class DevOverlayController
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;

        private FrameworkElement? _rootLayoutGrid;
        private System.Windows.Controls.Panel? _devOverlayPanel;
        private TextBlock? _devOverlayHeader;
        private TextBlock? _devOverlayDetails;
        private Canvas? _devOverlayCanvas;

        public DevOverlayController(Window window, MainViewModel viewModel)
        {
            _window = window;
            _viewModel = viewModel;
        }

        /// <summary>
        /// Resolves the XAML-named elements, attaches mouse tracking, and applies initial visibility.
        /// Call once from the window constructor.
        /// </summary>
        public void Initialize()
        {
            _rootLayoutGrid = _window.FindName("RootLayoutGrid") as FrameworkElement;
            _devOverlayPanel = _window.FindName("DevOverlayPanel") as System.Windows.Controls.Panel;
            _devOverlayHeader = _window.FindName("DevOverlayHeader") as TextBlock;
            _devOverlayDetails = _window.FindName("DevOverlayDetails") as TextBlock;
            _devOverlayCanvas = _window.FindName("DevOverlayCanvas") as Canvas;

            if (_rootLayoutGrid != null)
            {
                _rootLayoutGrid.PreviewMouseMove += DevOverlay_MouseMove;
                _rootLayoutGrid.MouseLeave += DevOverlay_MouseLeave;
            }

            UpdateVisibility();
        }

        public void UpdateVisibility()
        {
            if (_devOverlayPanel == null)
                return;

            if (_viewModel.ShowDevOverlay)
            {
                _devOverlayPanel.Visibility = Visibility.Visible;
            }
            else
            {
                _devOverlayPanel.Visibility = Visibility.Collapsed;
                if (_devOverlayHeader != null)
                    _devOverlayHeader.Text = string.Empty;
                if (_devOverlayDetails != null)
                    _devOverlayDetails.Text = string.Empty;
            }
        }

        private void DevOverlay_MouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_viewModel.ShowDevOverlay || _devOverlayPanel == null)
            {
                if (_devOverlayPanel != null)
                    _devOverlayPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var source = e.OriginalSource as DependencyObject ?? e.Source as DependencyObject;
            if (source == null || IsDescendantOfDevOverlay(source))
            {
                _devOverlayPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var element = FindFrameworkElement(source);
            if (element == null)
            {
                _devOverlayPanel.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateText(element, e);
        }

        private void DevOverlay_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_devOverlayPanel != null)
                _devOverlayPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateText(FrameworkElement element, System.Windows.Input.MouseEventArgs e)
        {
            if (_devOverlayPanel == null || _devOverlayHeader == null || _devOverlayDetails == null)
                return;

            var metadataElement = FindMetadataElement(element);
            var header = metadataElement != null ? DevInspector.GetMetadata(metadataElement) : null;
            if (string.IsNullOrWhiteSpace(header))
            {
                var namePart = string.IsNullOrWhiteSpace(element.Name) ? string.Empty : $" ({element.Name})";
                header = $"{element.GetType().Name}{namePart}";
            }

            _devOverlayHeader.Text = header;

            var builder = new StringBuilder();
            if (metadataElement != null && metadataElement != element)
            {
                var metadataNamePart = string.IsNullOrWhiteSpace(metadataElement.Name) ? string.Empty : $" ({metadataElement.Name})";
                builder.AppendLine($"Metadata source: {metadataElement.GetType().Name}{metadataNamePart}");
            }
            if (element.DataContext != null)
            {
                builder.AppendLine($"DataContext: {element.DataContext.GetType().Name}");
            }

            if (element is ContentControl cc && cc.Content != null)
            {
                builder.AppendLine($"Content: {cc.Content}");
            }

            if (element is System.Windows.Controls.Button btn && btn.Command != null)
            {
                builder.AppendLine($"Command: {btn.Command.GetType().Name}");
            }

            if (element is System.Windows.Controls.MenuItem menu && menu.Command != null)
            {
                builder.AppendLine($"Command: {menu.Command.GetType().Name}");
            }

            builder.AppendLine($"Size: {element.ActualWidth:F1} × {element.ActualHeight:F1}");
            builder.AppendLine($"Margin: {FormatThickness(element.Margin)}");

            string? padding = element switch
            {
                System.Windows.Controls.Control control => FormatThickness(control.Padding),
                Border border => FormatThickness(border.Padding),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(padding))
            {
                builder.AppendLine($"Padding: {padding}");
            }

            if (element.Tag != null)
            {
                builder.AppendLine($"Tag: {element.Tag}");
            }

            _devOverlayDetails.Text = builder.ToString().TrimEnd();
            _devOverlayPanel.Visibility = Visibility.Visible;
            UpdatePosition(e);
        }

        private static FrameworkElement? FindFrameworkElement(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement fe)
                    return fe;
                source = GetParentSafe(source);
            }

            return null;
        }

        private static FrameworkElement? FindMetadataElement(FrameworkElement element)
        {
            var current = element;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(DevInspector.GetMetadata(current)))
                {
                    return current;
                }

                var parent = GetParentSafe(current);
                current = parent as FrameworkElement;
            }

            return null;
        }

        private bool IsDescendantOfDevOverlay(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (_devOverlayPanel != null && obj == _devOverlayPanel)
                    return true;
                obj = GetParentSafe(obj);
            }

            return false;
        }

        private static DependencyObject? GetParentSafe(DependencyObject obj)
        {
            if (obj is Visual or Visual3D)
            {
                return VisualTreeHelper.GetParent(obj);
            }

            return LogicalTreeHelper.GetParent(obj);
        }

        private void UpdatePosition(System.Windows.Input.MouseEventArgs e)
        {
            if (_devOverlayCanvas == null || _devOverlayPanel == null)
                return;

            const double offset = 12;
            var canvasWidth = _devOverlayCanvas.ActualWidth;
            var canvasHeight = _devOverlayCanvas.ActualHeight;
            if (canvasWidth <= 0)
            {
                canvasWidth = _window.ActualWidth;
            }
            if (canvasHeight <= 0)
            {
                canvasHeight = _window.ActualHeight;
            }

            var mousePos = e.GetPosition(_devOverlayCanvas);
            var panelWidth = _devOverlayPanel.ActualWidth;
            var panelHeight = _devOverlayPanel.ActualHeight;

            var left = mousePos.X + offset;
            var top = mousePos.Y + offset;

            var maxLeft = Math.Max(0, canvasWidth - panelWidth - offset);
            var maxTop = Math.Max(0, canvasHeight - panelHeight - offset);

            if (left > maxLeft)
            {
                left = Math.Max(offset, maxLeft);
            }
            if (top > maxTop)
            {
                top = Math.Max(offset, maxTop);
            }

            left = Math.Max(offset, left);
            top = Math.Max(offset, top);

            Canvas.SetLeft(_devOverlayPanel, left);
            Canvas.SetTop(_devOverlayPanel, top);
        }

        private static string FormatThickness(Thickness thickness) =>
            $"{thickness.Left:F1}, {thickness.Top:F1}, {thickness.Right:F1}, {thickness.Bottom:F1}";
    }
}