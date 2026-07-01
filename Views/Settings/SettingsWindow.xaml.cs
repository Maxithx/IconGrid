using System;
using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;
using IconGrid.ViewModels.Settings;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfControl = System.Windows.Controls.Control;
using System.Windows.Threading;

namespace IconGrid.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private WpfButton? _selectedNavButton;

        public SettingsWindow(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            LocationChanged += SettingsWindow_LocationChanged;
            Closed += SettingsWindow_Closed;
            Loaded += SettingsWindow_Loaded;
            Unloaded += SettingsWindow_Unloaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Dispatcher.BeginInvoke(new Action(() => ShowPage(new StartsidePage(), StartsideNavButton)),
                                   DispatcherPriority.Loaded);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                if (sender is WpfButton btn)
                {
                    btn.Content = "\xE922";
                }
            }
            else
            {
                WindowState = WindowState.Maximized;
                if (sender is WpfButton btn)
                {
                    btn.Content = "\xE923";
                }
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void SettingsWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
                return;

            DragMove();
        }

        private void SettingsWindow_LocationChanged(object? sender, EventArgs e)
        {
            _viewModel.SaveSettingsWindowPosition(Left, Top);
        }

        private void SettingsWindow_Closed(object? sender, EventArgs e)
        {
            LocationChanged -= SettingsWindow_LocationChanged;
            Closed -= SettingsWindow_Closed;
            Loaded -= SettingsWindow_Loaded;
            Unloaded -= SettingsWindow_Unloaded;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            DetachDevOverlayHandlers();
        }

        private void SettingsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            InitializeDevOverlay();
            UpdateCaptionButtonBrushes();
        }

        private void SettingsWindow_Unloaded(object? sender, RoutedEventArgs e)
        {
            DetachDevOverlayHandlers();
        }

        private void StartsideNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new StartsidePage(), StartsideNavButton);
        }

        private void GenvejsNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new GenvejsIkonerPage(), GenvejsNavButton);
        }

        private void LayoutNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new LayoutPage(), LayoutNavButton);
        }

        private void HardwareNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new HardwarePage(), HardwareNavButton);
        }

        private void AboutNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new AboutPage(), AboutNavButton);
        }

        private void HelpNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(new HjaelpPage(), HelpNavButton);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.ShowDevOverlay), StringComparison.OrdinalIgnoreCase))
            {
                UpdateDevOverlayVisibility();
            }

            if (string.Equals(e.PropertyName, nameof(MainViewModel.TopBarForeground), StringComparison.OrdinalIgnoreCase))
            {
                UpdateCaptionButtonBrushes();
            }
        }

        private void InitializeDevOverlay()
        {
            if (WindowRoot != null)
            {
                WindowRoot.MouseMove += DevOverlay_MouseMove;
                WindowRoot.MouseLeave += DevOverlay_MouseLeave;
            }

            UpdateDevOverlayVisibility();
        }

        private void DetachDevOverlayHandlers()
        {
            if (WindowRoot != null)
            {
                WindowRoot.MouseMove -= DevOverlay_MouseMove;
                WindowRoot.MouseLeave -= DevOverlay_MouseLeave;
            }
        }

        private void DevOverlay_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_viewModel.ShowDevOverlay || DevOverlayPanel == null)
            {
                if (DevOverlayPanel != null)
                {
                    DevOverlayPanel.Visibility = Visibility.Collapsed;
                }

                return;
            }

            var source = e.OriginalSource as DependencyObject;
            if (source == null || IsDescendantOfDevOverlay(source))
            {
                if (DevOverlayPanel != null)
                {
                    DevOverlayPanel.Visibility = Visibility.Collapsed;
                }

                return;
            }

            var element = FindFrameworkElement(source);
            if (element == null)
            {
                if (DevOverlayPanel != null)
                {
                    DevOverlayPanel.Visibility = Visibility.Collapsed;
                }

                return;
            }

            UpdateDevOverlayText(element, e);
        }

        private void DevOverlay_MouseLeave(object? sender, MouseEventArgs e)
        {
            if (DevOverlayPanel != null)
                DevOverlayPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateDevOverlayVisibility()
        {
            if (DevOverlayPanel == null)
                return;

            if (_viewModel.ShowDevOverlay)
            {
                DevOverlayPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DevOverlayPanel.Visibility = Visibility.Collapsed;
                DevOverlayHeader.Text = string.Empty;
                DevOverlayDetails.Text = string.Empty;
            }
        }

        private void UpdateDevOverlayText(FrameworkElement element, MouseEventArgs e)
        {
            if (DevOverlayPanel == null || DevOverlayHeader == null || DevOverlayDetails == null)
                return;

            var metadataElement = FindMetadataElement(element);
            var header = metadataElement != null ? DevInspector.GetMetadata(metadataElement) : null;
            if (string.IsNullOrWhiteSpace(header))
            {
                var namePart = string.IsNullOrWhiteSpace(element.Name) ? string.Empty : $" ({element.Name})";
                header = $"{element.GetType().Name}{namePart}";
            }

            DevOverlayHeader.Text = header;

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

            if (element is WpfButton btn && btn.Command != null)
            {
                builder.AppendLine($"Command: {btn.Command.GetType().Name}");
            }

            if (element is WpfMenuItem menu && menu.Command != null)
            {
                builder.AppendLine($"Command: {menu.Command.GetType().Name}");
            }

            builder.AppendLine($"Size: {element.ActualWidth:F1} × {element.ActualHeight:F1}");
            builder.AppendLine($"Margin: {FormatThickness(element.Margin)}");

            string? padding = element switch
            {
                WpfControl control => FormatThickness(control.Padding),
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

            var backgroundBrush = GetBackgroundBrush(element);
            if (backgroundBrush != null)
            {
                builder.AppendLine($"Background: {FormatBrush(backgroundBrush)}");
            }

            if (element is Border borderElement)
            {
                if (borderElement.BorderBrush != null)
                {
                    builder.AppendLine($"BorderBrush: {FormatBrush(borderElement.BorderBrush)}");
                }
                builder.AppendLine($"BorderThickness: {FormatThickness(borderElement.BorderThickness)}");
                builder.AppendLine($"CornerRadius: {borderElement.CornerRadius}");
            }
            else if (element is WpfControl control && control.BorderBrush != null)
            {
                builder.AppendLine($"BorderBrush: {FormatBrush(control.BorderBrush)}");
                builder.AppendLine($"BorderThickness: {FormatThickness(control.BorderThickness)}");
            }

            DevOverlayDetails.Text = builder.ToString().TrimEnd();
            DevOverlayPanel.Visibility = Visibility.Visible;
            UpdateDevOverlayPosition(e);
        }

        private void ShowPage(System.Windows.FrameworkElement? page, WpfButton? navButton)
        {
            if (page == null || SettingsContentHost == null)
                return;

            page.DataContext = _viewModel;
            SettingsContentHost.Content = page;
            HighlightNavButton(navButton);
        }

        private void HighlightNavButton(WpfButton? navButton)
        {
            if (_selectedNavButton == navButton)
                return;

            if (_selectedNavButton != null)
            {
                _selectedNavButton.Tag = null;
            }

            _selectedNavButton = navButton;
            if (_selectedNavButton != null)
            {
                _selectedNavButton.Tag = "Selected";
            }
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

                current = GetParentSafe(current) as FrameworkElement;
            }

            return null;
        }

        private bool IsDescendantOfDevOverlay(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj == DevOverlayPanel)
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

        private void UpdateDevOverlayPosition(MouseEventArgs e)
        {
            if (DevOverlayCanvas == null || DevOverlayPanel == null)
                return;

            const double offset = 12;
            var canvasWidth = DevOverlayCanvas.ActualWidth;
            var canvasHeight = DevOverlayCanvas.ActualHeight;
            if (canvasWidth <= 0)
            {
                canvasWidth = ActualWidth;
            }
            if (canvasHeight <= 0)
            {
                canvasHeight = ActualHeight;
            }

            var mousePos = e.GetPosition(DevOverlayCanvas);
            var panelWidth = DevOverlayPanel.ActualWidth;
            var panelHeight = DevOverlayPanel.ActualHeight;

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

            Canvas.SetLeft(DevOverlayPanel, left);
            Canvas.SetTop(DevOverlayPanel, top);
        }

        private void UpdateCaptionButtonBrushes()
        {
            if (_viewModel.TopBarForeground is null)
                return;

            MinimizeCaptionButton.Foreground = _viewModel.TopBarForeground;
            MaximizeRestoreButton.Foreground = _viewModel.TopBarForeground;
            CloseCaptionButton.Foreground = _viewModel.TopBarForeground;
        }

        private static string FormatThickness(Thickness thickness) =>
            $"{thickness.Left:F1}, {thickness.Top:F1}, {thickness.Right:F1}, {thickness.Bottom:F1}";

        private static string FormatBrush(System.Windows.Media.Brush brush)
        {
            if (brush is SolidColorBrush solid)
            {
                var color = solid.Color;
                var hex = color.A < 255
                    ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
                    : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                return $"{hex}";
            }

            return brush.ToString() ?? "Unknown";
        }

        private static System.Windows.Media.Brush? GetBackgroundBrush(FrameworkElement element)
        {
            if (element is Border border && border.Background != null)
            {
                return border.Background;
            }

            if (element is System.Windows.Controls.Panel panel && panel.Background != null)
            {
                return panel.Background;
            }

            if (element is WpfControl control && control.Background != null)
            {
                return control.Background;
            }

            if (element is System.Windows.Shapes.Shape shape && shape.Fill != null)
            {
                return shape.Fill;
            }

            return null;
        }

        private static bool IsInteractiveElement(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is System.Windows.Controls.Primitives.ButtonBase
                        or System.Windows.Controls.Slider
                        or System.Windows.Controls.Primitives.Thumb)
                {
                    return true;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        public bool TryApplySavedPosition()
        {
            if (_viewModel.TryGetSavedSettingsWindowPosition(out var left, out var top))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
                return true;
            }

            return false;
        }
    }
}
