using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;
using IconGrid.ViewModels;
using Microsoft.Win32;

namespace IconGrid.Views
{
    public partial class GamingOverlayWindow : Window
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const double BaseOverlayHeight = 44.0;
        private const double PopupRowHeight = 116.0;
        private const double PopupRowSpacing = 8.0;
        private const double DisplayChangeSettleDelayMs = 400.0;
        private readonly MainViewModel _viewModel;
        private bool _isScaleMenuOpen;
        private bool _isDraggingPopupScaleSlider;
        private bool _pendingOverlayMetricsUpdate;
        private System.Windows.Threading.DispatcherTimer? _displayChangeTimer;

        public GamingOverlayWindow(MainViewModel viewModel, GamingOverlayLayout layout)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            UpdateOverlayMetrics();
            Loaded += GamingOverlayWindow_Loaded;
            Closed += GamingOverlayWindow_Closed;
            LocationChanged += GamingOverlayWindow_LocationChanged;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void GamingOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResolutionDefaultScale();
            UpdateOverlayMetrics();
            ApplyPositionPreset();
            ApplyLayout();
        }

        private void GamingOverlayWindow_Closed(object? sender, EventArgs e)
        {
            _displayChangeTimer?.Stop();
            _displayChangeTimer = null;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            Loaded -= GamingOverlayWindow_Loaded;
            Closed -= GamingOverlayWindow_Closed;
            LocationChanged -= GamingOverlayWindow_LocationChanged;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            // DisplaySettingsChanged fires as soon as ChangeDisplaySettingsEx returns,
            // but EnumDisplaySettings(ENUM_CURRENT_SETTINGS) can still report the OLD
            // resolution at that moment because the display mode transition is not
            // complete. Applying the per-resolution scale immediately would therefore
            // read e.g. 3840x2160 (100%) instead of 2560x1440 (150%) and leave the
            // overlay at the wrong size until the user touches the scale slider.
            //
            // Debounce the switch: wait until the mode change has settled (~400ms, the
            // same settling window used for the window-layout snapshot restore), then
            // read the current resolution and apply its default scale.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                _displayChangeTimer?.Stop();
                _displayChangeTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(DisplayChangeSettleDelayMs),
                    System.Windows.Threading.DispatcherPriority.Background,
                    HandleDisplayChangeSettled,
                    dispatcher);
                _displayChangeTimer.Start();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void HandleDisplayChangeSettled(object? sender, EventArgs e)
        {
            _displayChangeTimer?.Stop();
            ApplyResolutionDefaultScale();
            UpdateOverlayMetrics();
            ApplyPositionPreset();
        }

        private void ApplyResolutionDefaultScale()
        {
            var resolution = DisplayResolutionService.GetCurrentResolution();
            var scale = _viewModel.GetGamingOverlayScaleForResolution(resolution);
            _viewModel.GamingOverlayUiScale = scale;
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

            var extraHeight = SettingsMenuButton?.IsChecked == true
                ? (PopupRowSpacing + PopupRowHeight) * scale
                : 0;
            Height = (BaseOverlayHeight * scale) + extraHeight;
            MinHeight = BaseOverlayHeight * scale;

            // Remember the current RIGHT edge before we resize. The overlay content is
            // right-aligned (HorizontalAlignment="Right"), so when the width changes —
            // e.g. opening/closing the inline settings row or sliding the scale — we must
            // keep the window anchored to its existing right edge. Otherwise WPF keeps the
            // LEFT edge fixed, the right edge (and the right-aligned content) moves left,
            // and the whole overlay "jumps" out of its top-right spot.
            var previousRight = double.IsNaN(Left) || double.IsNaN(Width)
                ? double.NaN
                : Left + Width;

            var overlayWidth = MeasureElementWidth(OverlayRoot);
            var popupWidth = PopupPanelsRow?.Visibility == Visibility.Visible
                ? MeasureElementWidth(PopupPanelsRow)
                : 0;
            var targetWidth = Math.Max(overlayWidth, popupWidth);
            if (targetWidth > 0)
            {
                Width = targetWidth;
                MinWidth = targetWidth;
            }

            // Re-anchor to the previous right edge: the window grows/shrinks towards the
            // LEFT so the right side (where the user keeps the overlay) never moves.
            if (!double.IsNaN(previousRight) && !double.IsNaN(Left) && !double.IsNaN(Width))
            {
                var newRight = Left + Width;
                if (Math.Abs(newRight - previousRight) > 0.5)
                {
                    Left = previousRight - Width;
                }
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
            // The overlay scale slider is the REAL physical scale: 100% is the design
            // size (720x44), 150% is 1.5x. No resolution compensation is applied here —
            // the per-resolution defaults (GamingOverlayResolutionScales) already pick
            // the right scale automatically when the display resolution changes.
            return _viewModel.GamingOverlayUiScale;
        }

        private void ApplyPositionPreset()
        {
            // The gaming overlay can snap to a user-chosen standard placement (top/
            // bottom corner or center edge, configured on the Gaming Overlay settings
            // page, mirroring classic overlay tools like FPS Overlay). Whenever the
            // overlay opens or the display resolution changes, it snaps to that
            // preset so it can never end up mid-screen or off-screen after a mode
            // switch. "Custom" keeps the user's manually dragged position untouched.
            // The overlay is never locked: the user can still drag it anywhere and
            // LocationChanged persists that position as usual.
            // NOTE: we use SystemParameters.WorkArea (WPF DIPs) because Window.Left/
            // Top/Width/Height are also in DIPs — the same convention as the
            // coordinator's PositionRelativeToOwner.
            try
            {
                var preset = ParsePositionPreset(_viewModel.GamingOverlayPositionPreset);
                if (preset == GamingOverlayPositionPreset.Custom)
                    return;

                var area = SystemParameters.WorkArea;
                var w = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;
                var h = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
                if (w <= 0 || h <= 0)
                    return;

                // No margin: the overlay sits flush against the screen edge/corner.
                // The user wants it fully in the corner (top of the screen + right
                // edge), not floating 16px in.
                const double gap = 0.0;

                double left;
                switch (preset)
                {
                    case GamingOverlayPositionPreset.TopLeft:
                    case GamingOverlayPositionPreset.BottomLeft:
                        left = area.Left + gap;
                        break;
                    case GamingOverlayPositionPreset.TopCenter:
                    case GamingOverlayPositionPreset.BottomCenter:
                        left = area.Left + ((area.Width - w) / 2.0);
                        break;
                    default: // TopRight / BottomRight
                        left = area.Right - w - gap;
                        break;
                }

                double top;
                switch (preset)
                {
                    case GamingOverlayPositionPreset.BottomLeft:
                    case GamingOverlayPositionPreset.BottomCenter:
                    case GamingOverlayPositionPreset.BottomRight:
                        top = area.Bottom - h - gap;
                        break;
                    default: // Top*
                        top = area.Top + gap;
                        break;
                }

                Left = left;
                Top = top;
            }
            catch
            {
                // never crash the overlay for a placement check
            }
        }

        private static GamingOverlayPositionPreset ParsePositionPreset(string? value)
        {
            if (Enum.TryParse<GamingOverlayPositionPreset>(value, true, out var preset))
                return preset;

            return GamingOverlayPositionPreset.TopRight;
        }

        private static double MeasureElementWidth(FrameworkElement? element)
        {
            if (element == null)
            {
                return 0;
            }

            element.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            return Math.Ceiling(element.DesiredSize.Width);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When the user picks a new standard placement on the Gaming Overlay
            // settings page, move the overlay immediately — no need to close/reopen it.
            if (string.Equals(e.PropertyName, nameof(MainViewModel.GamingOverlayPositionPreset), StringComparison.OrdinalIgnoreCase))
            {
                ApplyPositionPreset();
                return;
            }

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
            if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed)
            {
                return;
            }

            // DragMove() throws on windows with AllowsTransparency="True" (which this
            // window always has). Use the Win32 caption drag instead so the overlay can
            // be moved even while the transparent background is active in-game.
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SendMessage(hwnd, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
                }
            }
            catch
            {
                // ignore drag failures when clicked on controls
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

            // Save the user's chosen scale as that resolution's default going forward.
            var resolution = DisplayResolutionService.GetCurrentResolution();
            _viewModel.SetGamingOverlayScaleForResolution(resolution, _viewModel.GamingOverlayUiScale);
        }
    }

    public enum GamingOverlayLayout
    {
        Horizontal,
        Vertical
    }
}
