using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using IconGrid.ViewModels;
using Forms = System.Windows.Forms;

namespace IconGrid.Views
{
    public partial class GamingOverlayWindow : Window
    {
        private const double ReferenceWidth = 3840.0;
        private const double ReferenceHeight = 2160.0;
        private const double BaseOverlayHeight = 44.0;
        private const double PopupRowHeight = 116.0;
        private const double PopupRowSpacing = 8.0;
        private readonly MainViewModel _viewModel;
        private bool _isScaleMenuOpen;
        private bool _isDraggingPopupScaleSlider;
        private bool _pendingOverlayMetricsUpdate;

        public GamingOverlayWindow(MainViewModel viewModel, GamingOverlayLayout layout)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            UpdateOverlayMetrics();
            Loaded += GamingOverlayWindow_Loaded;
            Closed += GamingOverlayWindow_Closed;
            LocationChanged += GamingOverlayWindow_LocationChanged;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void GamingOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateOverlayMetrics();
            ApplyLayout();
        }

        private void GamingOverlayWindow_Closed(object? sender, EventArgs e)
        {
            Loaded -= GamingOverlayWindow_Loaded;
            Closed -= GamingOverlayWindow_Closed;
            LocationChanged -= GamingOverlayWindow_LocationChanged;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void GamingOverlayWindow_LocationChanged(object? sender, EventArgs e)
        {
            _viewModel.SaveGamingOverlayWindowPosition(Left, Top);
        }

        private void ApplyLayout()
        {
            // Single-row overlay now matches the launcher monitor row directly.
        }

        private void ApplyWindowSize()
        {
            var scale = GetEffectiveScale();
            Width = 720 * scale;
            var extraHeight = SettingsMenuButton?.IsChecked == true
                ? (PopupRowSpacing + PopupRowHeight) * scale
                : 0;
            Height = (BaseOverlayHeight * scale) + extraHeight;
            MinWidth = Width;
            MinHeight = BaseOverlayHeight * scale;

            if (OverlayRoot != null)
            {
                OverlayRoot.LayoutTransform = new ScaleTransform(scale, scale);
            }

            if (PopupPanelsRow != null)
            {
                PopupPanelsRow.LayoutTransform = new ScaleTransform(scale, scale);
                PopupPanelsRow.Margin = new Thickness(0, PopupRowSpacing * scale, 0, 0);
            }

            if (SettingsMenuButton != null)
            {
                SettingsMenuButton.Margin = new Thickness(0, 0, -4 * scale, 0);
            }

            if (CloseButton != null)
            {
                CloseButton.Margin = new Thickness(6 * scale, 0, 2 * scale, 0);
            }
        }

        private void UpdateOverlayMetrics()
        {
            if (_isDraggingPopupScaleSlider)
            {
                _pendingOverlayMetricsUpdate = true;
                return;
            }

            _pendingOverlayMetricsUpdate = false;
            ApplyWindowSize();
        }

        private double GetEffectiveScale()
        {
            var baseScale = _viewModel.GamingOverlayUiScale;
            var screen = TryGetCurrentScreen();
            if (screen == null)
                return baseScale;

            var widthFactor = Math.Min(1.0, screen.Bounds.Width / ReferenceWidth);
            var heightFactor = Math.Min(1.0, screen.Bounds.Height / ReferenceHeight);
            var resolutionFactor = Math.Min(widthFactor, heightFactor);
            return Math.Max(0.5, Math.Min(1.2, baseScale * resolutionFactor));
        }

        private Forms.Screen? TryGetCurrentScreen()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    return Forms.Screen.FromHandle(hwnd);
                }
            }
            catch
            {
                // fall back below
            }

            try
            {
                return Forms.Screen.PrimaryScreen;
            }
            catch
            {
                return null;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayUiScale), StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayWindowWidth), StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayWindowHeight), StringComparison.OrdinalIgnoreCase))
            {
                UpdateOverlayMetrics();
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            UpdateOverlayMetrics();
        }

        private void OverlayRoot_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // ignore drag failures when clicked on controls
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public bool TryApplySavedPosition()
        {
            if (_viewModel.TryGetSavedGamingOverlayWindowPosition(out var left, out var top))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
                return true;
            }

            return false;
        }

        private void SettingsMenuButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdatePopupPanels();
        }

        private void SettingsMenuButton_Unchecked(object sender, RoutedEventArgs e)
        {
            CloseSettingsMenus();
        }

        private void ScaleMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _isScaleMenuOpen = !_isScaleMenuOpen;
            UpdatePopupPanels();
        }

        private void OpenGamingOverlaySettings_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsMenus();

            var settingsWindow = System.Windows.Application.Current?.Windows
                .OfType<SettingsWindow>()
                .FirstOrDefault();

            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow(_viewModel)
                {
                    Owner = Owner ?? System.Windows.Application.Current?.MainWindow
                };

                if (!settingsWindow.TryApplySavedPosition() && settingsWindow.Owner != null)
                {
                    settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }

                settingsWindow.Show();
            }
            else if (!settingsWindow.IsVisible)
            {
                settingsWindow.Show();
            }

            settingsWindow.NavigateToGamingOverlayPage();

            if (settingsWindow.WindowState == WindowState.Minimized)
            {
                settingsWindow.WindowState = WindowState.Normal;
            }

            settingsWindow.Activate();
        }

        private void SettingsPopup_Closed(object sender, EventArgs e)
        {
            _isScaleMenuOpen = false;
            UpdatePopupPanels();
        }

        private void ScalePopup_Closed(object sender, EventArgs e)
        {
        }

        private void CloseSettingsMenus()
        {
            _isScaleMenuOpen = false;

            if (SettingsMenuButton != null)
            {
                SettingsMenuButton.IsChecked = false;
            }

            UpdatePopupPanels();
        }

        private void UpdatePopupPanels()
        {
            var isSettingsOpen = SettingsMenuButton?.IsChecked == true;

            if (PopupPanelsRow != null)
            {
                PopupPanelsRow.Visibility = isSettingsOpen ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ScalePopupPanel != null)
            {
                ScalePopupPanel.Visibility = isSettingsOpen && _isScaleMenuOpen
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            UpdateOverlayMetrics();
        }

        private void PopupScaleSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingPopupScaleSlider = true;
            _pendingOverlayMetricsUpdate = false;
        }

        private void PopupScaleSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FinishPopupScaleSliderDrag();
        }

        private void PopupScaleSlider_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            FinishPopupScaleSliderDrag();
        }

        private void FinishPopupScaleSliderDrag()
        {
            if (!_isDraggingPopupScaleSlider)
            {
                return;
            }

            _isDraggingPopupScaleSlider = false;

            if (_pendingOverlayMetricsUpdate)
            {
                UpdateOverlayMetrics();
            }
        }
    }

    public enum GamingOverlayLayout
    {
        Horizontal,
        Vertical
    }
}
