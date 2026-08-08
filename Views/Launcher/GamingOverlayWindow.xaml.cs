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
using Forms = System.Windows.Forms;

namespace IconGrid.Views
{
    public partial class GamingOverlayWindow : Window
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoSendChanging = 0x0400;

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        private const uint MonitorDefaultToNearest = 2;
        private const int MdtEffectiveDpi = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const double BaseOverlayHeight = 44.0;
        private const double PopupRowHeight = 116.0;
        private const double PopupRowSpacing = 8.0;
        private const double DisplayChangeSettleDelayMs = 400.0;
        private readonly MainViewModel _viewModel;
        private bool _isScaleMenuOpen;
        private bool _isDraggingPopupScaleSlider;
        private bool _pendingOverlayMetricsUpdate;
        private System.Windows.Threading.DispatcherTimer? _displayChangeTimer;
        private HwndSource? _hwndSource;
        private bool _suppressLocationSave;
        private const int WmDpiChanged = 0x02E0;
        private const int WmDisplayChange = 0x007E;
        private const int PositionReapplyAttempts = 3;
        private const int PositionReapplyDelayMs = 150;
        private int _reapplyAttempt;
        private System.Windows.Threading.DispatcherTimer? _positionReapplyTimer;
        private System.Windows.Threading.DispatcherTimer? _scaleDebounceTimer;
        private const int ScaleDebounceDelayMs = 150;

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
            _positionReapplyTimer?.Stop();
            _positionReapplyTimer = null;
            _scaleDebounceTimer?.Stop();
            _scaleDebounceTimer = null;
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            Loaded -= GamingOverlayWindow_Loaded;
            Closed -= GamingOverlayWindow_Closed;
            LocationChanged -= GamingOverlayWindow_LocationChanged;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // A display mode change re-positions every top-level window. WM_DISPLAYCHANGE
            // is broadcast to each window when the screen resolution changes (more
            // reliable than SystemEvents.DisplaySettingsChanged, which we have seen not
            // fire in practice). WM_DPICHANGED follows and triggers WPF scaling/clamping
            // that re-positions the overlay to an intermediate location.
            //
            // Strategy: on either message, suppress position saving and re-apply the
            // preset a few times (with a short delay) so the final placement is always
            // the user's chosen "Default position" in the NEW resolution — never a stale
            // mid-transition spot.
            if ((msg == WmDisplayChange || msg == WmDpiChanged) && !_suppressLocationSave)
            {
                _suppressLocationSave = true;
                SchedulePositionReapply();
            }

            return IntPtr.Zero;
        }

        private void SchedulePositionReapply()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            _reapplyAttempt = 0;
            _positionReapplyTimer?.Stop();
            _positionReapplyTimer = new System.Windows.Threading.DispatcherTimer(
                TimeSpan.FromMilliseconds(PositionReapplyDelayMs),
                System.Windows.Threading.DispatcherPriority.Background,
                (_, _) => OnPositionReapplyTick(dispatcher),
                dispatcher);
            _positionReapplyTimer.Start();
        }

        private void OnPositionReapplyTick(System.Windows.Threading.Dispatcher dispatcher)
        {
            // Read the live bounds/DPI and place the overlay. Repeat a few times because
            // Forms.Screen.Bounds / GetDpiForMonitor can settle shortly after the mode
            // change; the last attempt is the authoritative placement.
            ApplyResolutionDefaultScale();
            UpdateOverlayMetrics();
            ApplyPositionPreset();

            _reapplyAttempt++;
            if (_reapplyAttempt >= PositionReapplyAttempts)
            {
                _positionReapplyTimer?.Stop();
                _positionReapplyTimer = null;
                _suppressLocationSave = false;
                return;
            }

            _positionReapplyTimer?.Start();
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
                // Windows re-positions windows during the mode transition; do not persist
                // any intermediate positions until the preset has been re-applied.
                _suppressLocationSave = true;
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
            LogTrace($"[GamingOverlay] HandleDisplayChangeSettled fired. CurrentResolution={DisplayResolutionService.GetCurrentResolution()}, Left={Left:F1}, Top={Top:F1}, Width={Width:F1}, Height={Height:F1}, UiScale={_viewModel.GamingOverlayUiScale:F3}");
            ApplyResolutionDefaultScale();
            UpdateOverlayMetrics();
            ApplyPositionPreset();
            LogTrace($"[GamingOverlay] HandleDisplayChangeSettled done. Left={Left:F1}, Top={Top:F1}, Width={Width:F1}");

            // Resume saving positions now that the preset has been re-applied. If
            // WM_DPICHANGED arrives later, the WndProc hook re-arms suppression and
            // re-applies the preset a final time so the last position is the preset's.
            _suppressLocationSave = false;
        }

        private void ApplyResolutionDefaultScale()
        {
            var resolution = DisplayResolutionService.GetCurrentResolution();
            var scale = _viewModel.GetGamingOverlayScaleForResolution(resolution);
            LogTrace($"[GamingOverlay] ApplyResolutionDefaultScale: resolution={resolution}, scale={scale:F3}");
            _viewModel.GamingOverlayUiScale = scale;
        }

        private void GamingOverlayWindow_LocationChanged(object? sender, EventArgs e)
        {
            LogTrace($"[GamingOverlay] LocationChanged: Left={Left:F1} Top={Top:F1} Width={Width:F1} suppress={_suppressLocationSave}");
            // During a display mode change Windows re-positions the overlay and the
            // intermediate positions are NOT the user's choice. Suppress saving them so
            // a stale mid-transition position cannot overwrite the saved preset/custom
            // placement (same pattern as MainWindow._skipSavingLocation).
            if (_suppressLocationSave)
                return;

            _viewModel.SaveGamingOverlayWindowPosition(Left, Top);
        }

        private void ApplyLayout()
        {
            // Single-row overlay now matches the launcher monitor row directly.
        }

        private void ApplyWindowSize()
        {
            var scale = GetEffectiveScale();
            LogTrace($"[GamingOverlay] ApplyWindowSize START: scale={scale:F3} Left={Left:F1} Top={Top:F1} Width={Width:F1} Height={Height:F1} preset={_viewModel.GamingOverlayPositionPreset}");

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

            // The window frame must match the VISUALLY SCALED content. LayoutTransform
            // scales the content but does not change Window.Width by itself — if Width
            // stays at the base value, the drag outline / window frame no longer matches
            // the visible overlay (it looks "too big" toward the left) and preset
            // anchoring drifts. The shared GetOverlayScaledWidth() (max of root and popup,
            // times scale) is used by BOTH ApplyWindowSize and ApplyPositionPreset so the
            // preset never fights the window size — that mismatch is what made the scale
            // slider jump between 150% and 100%.
            var scaledWidth = GetOverlayScaledWidth();
            if (scaledWidth > 0)
            {
                Width = scaledWidth;
                MinWidth = scaledWidth;
            }

            // Position is owned by ApplyPositionPreset() in preset mode and by the user's
            // drag in Custom mode. No right-edge re-anchoring is needed here anymore:
            // Width now always equals the scaled content, so presets and manual drags
            // stay consistent. (See ApplyPositionPreset for the suppress-save guard.)
            LogTrace($"[GamingOverlay] ApplyWindowSize END: Width={Width:F1} MinWidth={MinWidth:F1} Left={Left:F1} Top={Top:F1}");
        }

        /// <summary>
        /// Updates only the visual scaling transforms (LayoutTransform + margins)
        /// without changing the window frame size or position. Called during slider
        /// drag so the content scales smoothly while the window stays put — preventing
        /// the shaking that occurs when SetWindowPos is called on every slider tick.
        /// </summary>
        private void ApplyVisualScaleOnly()
        {
            var scale = GetEffectiveScale();

            // Only update LayoutTransform during drag — NEVER change Width/Height.
            // Changing the window frame on every slider tick forces DWM to redraw
            // the non-client area on AllowsTransparency windows, causing violent
            // shaking. The full size+position update happens once via SetWindowPos
            // in ApplyScaleSettled() when the user stops dragging.
            if (OverlayRoot != null)
                OverlayRoot.LayoutTransform = new ScaleTransform(scale, scale);

            if (PopupPanelsRow != null)
            {
                PopupPanelsRow.LayoutTransform = new ScaleTransform(scale, scale);
                PopupPanelsRow.Margin = new Thickness(0, PopupRowSpacing * scale, 0, 0);
            }

            if (SettingsMenuButton != null)
                SettingsMenuButton.Margin = new Thickness(0, 0, -4 * scale, 0);

            if (CloseButton != null)
                CloseButton.Margin = new Thickness(6 * scale, 0, 2 * scale, 0);
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

        /// <summary>
        /// The overlay's ACTUAL scaled width. Single source of truth for BOTH the window
        /// sizing (ApplyWindowSize) and the preset anchoring (ApplyPositionPreset):
        /// max(OverlayRoot, PopupPanelsRow) measured at base scale, times the chosen scale.
        /// Using one shared value prevents the preset from fighting the resize — which
        /// otherwise makes the scale slider jump violently between 150% and 100%.
        /// </summary>
        private double GetOverlayScaledWidth()
        {
            var overlayWidth = MeasureElementWidth(OverlayRoot);
            var popupWidth = PopupPanelsRow?.Visibility == Visibility.Visible
                ? MeasureElementWidth(PopupPanelsRow)
                : 0;
            var targetWidth = Math.Max(overlayWidth, popupWidth);
            return targetWidth > 0 ? Math.Ceiling(targetWidth * GetEffectiveScale()) : 0;
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
            // NOTE: we compute the placement from the CURRENT physical screen bounds
            // (Forms.Screen.Bounds) converted to WPF DIPs via VisualTreeHelper.GetDpi,
            // because SystemParameters.WorkArea can be stale right after a runtime
            // resolution switch.
            try
            {
                var preset = ParsePositionPreset(_viewModel.GamingOverlayPositionPreset);
                if (preset == GamingOverlayPositionPreset.Custom)
                    return;

                // Anchor by the overlay's ACTUAL window width (GetOverlayScaledWidth),
                // which is the same value ApplyWindowSize uses for Window.Width — max of
                // the overlay bar and the settings popup row, times the scale. The content
                // is right-aligned within the window, so placing Left = areaRight - width
                // keeps the bar flush to the right edge at ALL times: with the popup open
                // (wider window) the bar still sits top-right, and there is no moment where
                // the right edge sticks out of the viewport while scaling down.
                var w = GetOverlayScaledWidth();
                var h = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
                if (w <= 0 || h <= 0)
                    return;

                // Use the CURRENT physical screen bounds + DPI converted to WPF DIPs.
                // SystemParameters.WorkArea can be stale right after a runtime resolution
                // switch (e.g. 4K -> 1440p via the Game Resolution feature): WPF still
                // reports the OLD work area for a moment, so the overlay would be
                // positioned against the old screen size and fall off the right edge.
                // Forms.Screen.Bounds always reflects the live display mode immediately.
                //
                // The same staleness applies to the DPI scale factor: VisualTreeHelper.GetDpi
                // reports the WINDOW's current DPI, which can still be the OLD monitor DPI
                // right after a mode change (WM_DPICHANGED has not been delivered yet). On a
                // 4K display at 150% scaling the old 100% factor would make areaRight =
                // 3840/1.0 = 3840 instead of 2560, pushing the overlay off the right edge.
                // GetDpiForMonitor(MDT_EFFECTIVE_DPI) on the primary display returns the
                // screen's ACTUAL scaling (144 at 150%) independent of the window, so it is
                // never stale here.
                var screen = Forms.Screen.PrimaryScreen;
                if (screen == null)
                    return;

                var dpiScale = GetPrimaryScreenDpiScale();
                var scaleX = dpiScale;
                var scaleY = dpiScale;

                var areaLeft = screen.Bounds.Left / scaleX;
                var areaTop = screen.Bounds.Top / scaleY;
                var areaRight = screen.Bounds.Right / scaleX;
                var areaBottom = screen.Bounds.Bottom / scaleY;

                // No margin: the overlay sits flush against the screen edge/corner.
                // The user wants it fully in the corner (top of the screen + right
                // edge), not floating 16px in.
                const double gap = 0.0;

                double left;
                switch (preset)
                {
                    case GamingOverlayPositionPreset.TopLeft:
                    case GamingOverlayPositionPreset.BottomLeft:
                        left = areaLeft + gap;
                        break;
                    case GamingOverlayPositionPreset.TopCenter:
                    case GamingOverlayPositionPreset.BottomCenter:
                        left = areaLeft + ((areaRight - areaLeft - w) / 2.0);
                        break;
                    default: // TopRight / BottomRight
                        left = areaRight - w - gap;
                        break;
                }

                double top;
                switch (preset)
                {
                    case GamingOverlayPositionPreset.BottomLeft:
                    case GamingOverlayPositionPreset.BottomCenter:
                    case GamingOverlayPositionPreset.BottomRight:
                        top = areaBottom - h - gap;
                        break;
                    default: // Top*
                        top = areaTop + gap;
                        break;
                }

                // Clamp: ensure the overlay never lands outside the virtual screen bounds,
                // even if Width and Left are applied in separate WPF layout passes.
                // Without this, scaling down can produce one frame where the new (smaller) Width
                // is combined with the old (larger) Left, pushing the right edge off-screen.
                if (left + w > areaRight)
                    left = areaRight - w;
                if (left < areaLeft)
                    left = areaLeft;
                if (top + h > areaBottom)
                    top = areaBottom - h;
                if (top < areaTop)
                    top = areaTop;

                // Never persist the position we set ourselves — only the user's manual
                // drag should update the saved overlay position. Guard must live INSIDE
                // this method so every call site (Load, resolution switch, scale change,
                // preset change) is protected, not just a few.
                _suppressLocationSave = true;
                Left = left;
                Top = top;
                _suppressLocationSave = false;

                LogTrace($"[GamingOverlay] ApplyPositionPreset: preset={preset} screenBounds={screen.Bounds} dpiScale={dpiScale:F3} areaLeft={areaLeft:F1} areaTop={areaTop:F1} areaRight={areaRight:F1} areaBottom={areaBottom:F1} w={w:F1} h={h:F1} -> left={left:F1} top={top:F1}");
            }
            catch (Exception ex)
            {
                LogTrace($"[GamingOverlay] ApplyPositionPreset failed: {ex.Message}");
                // never crash the overlay for a placement check
            }
        }

        private static double GetPrimaryScreenDpiScale()
        {
            var screen = Forms.Screen.PrimaryScreen;
            if (screen == null)
                return 1.0;

            try
            {
                // Probe the center of the primary display so the monitor handle is always
                // the active primary monitor after a resolution switch.
                var point = new POINT
                {
                    X = screen.Bounds.Left + Math.Max(1, screen.Bounds.Width / 2),
                    Y = screen.Bounds.Top + Math.Max(1, screen.Bounds.Height / 2)
                };

                var hMonitor = MonitorFromPoint(point, MonitorDefaultToNearest);
                if (hMonitor == IntPtr.Zero)
                    return 1.0;

                if (GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out var dpiY) != 0)
                    return 1.0;

                return Math.Max(1.0, dpiX / 96.0);
            }
            catch
            {
                return 1.0;
            }
        }

        private void LogTrace(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "trace.log"), $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
            }
            catch
            {
                // logging must never break overlay positioning
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
                LogTrace($"[GamingOverlay] PropertyChanged {e.PropertyName}: scale={_viewModel.GamingOverlayUiScale:F3} Left={Left:F1} Top={Top:F1} Width={Width:F1}");

                // Popup slider drag: skip all scale updates during drag. The final
                // value is applied once in FinishPopupScaleSliderDrag() when the
                // mouse is released, matching the settings-page slider behavior.
                if (_isDraggingPopupScaleSlider)
                    return;

                // During slider drag (settings page): only update LayoutTransform for
                // smooth visual scaling. Do NOT change the window frame size or position.
                ApplyVisualScaleOnly();

                // Debounce: once the user pauses or lets go (~150ms without a tick),
                // apply the full resize+reposition in one atomic SetWindowPos call.
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                _scaleDebounceTimer?.Stop();
                _scaleDebounceTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(ScaleDebounceDelayMs),
                    System.Windows.Threading.DispatcherPriority.Background,
                    (_, _) => ApplyScaleSettled(),
                    dispatcher);
                _scaleDebounceTimer.Start();
            }
        }

        private void ApplyScaleSettled()
        {
            _scaleDebounceTimer?.Stop();
            _scaleDebounceTimer = null;
            LogTrace($"[GamingOverlay] ApplyScaleSettled: scale={_viewModel.GamingOverlayUiScale:F3} Left={Left:F1} Top={Top:F1} Width={Width:F1} preset={_viewModel.GamingOverlayPositionPreset}");

            // Full resize + atomic reposition via SetWindowPos so DWM never shows
            // an intermediate frame with new size + old position. The _suppressLocationSave
            // guard prevents the LocationChanged feedback loop that caused shaking in 05:02.
            _suppressLocationSave = true;
            ApplyWindowSize();
            ApplyPresetGeometryAtomic();
            _suppressLocationSave = false;
        }

        /// <summary>
        /// Computes and applies Left/Top for the selected preset using the NEW scaled width
        /// (the same value ApplyWindowSize will set for Width). Called BEFORE the resize so
        /// WPF never shows a frame with the new Width and the old Left.
        /// </summary>
        private void ApplyPresetPositionAgainstNewSize()
        {
            var scaledWidth = GetOverlayScaledWidth();
            if (scaledWidth <= 0)
                return;

            var screen = Forms.Screen.PrimaryScreen;
            if (screen == null)
                return;

            var dpiScale = GetPrimaryScreenDpiScale();
            var areaLeft = screen.Bounds.Left / dpiScale;
            var areaTop = screen.Bounds.Top / dpiScale;
            var areaRight = screen.Bounds.Right / dpiScale;
            var areaBottom = screen.Bounds.Bottom / dpiScale;

            var preset = ParsePositionPreset(_viewModel.GamingOverlayPositionPreset);
            var gap = 0.0;
            double left;
            switch (preset)
            {
                case GamingOverlayPositionPreset.TopLeft:
                case GamingOverlayPositionPreset.BottomLeft:
                    left = areaLeft + gap;
                    break;
                case GamingOverlayPositionPreset.TopCenter:
                case GamingOverlayPositionPreset.BottomCenter:
                    left = areaLeft + ((areaRight - areaLeft - scaledWidth) / 2.0);
                    break;
                default:
                    left = areaRight - scaledWidth - gap;
                    break;
            }

            var h = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
            if (h <= 0)
                return;

            double top;
            switch (preset)
            {
                case GamingOverlayPositionPreset.BottomLeft:
                case GamingOverlayPositionPreset.BottomCenter:
                case GamingOverlayPositionPreset.BottomRight:
                    top = areaBottom - h - gap;
                    break;
                default:
                    top = areaTop + gap;
                    break;
            }

            Left = left;
            Top = top;
        }

        /// <summary>
        /// Sets the overlay's size AND position in a single native call. WPF updates
        /// Width and Left in separate layout passes, which produces a visible frame where
        /// the new Width is drawn with the old Left — the "drift" seen when scaling down.
        /// SetWindowPos moves and sizes the window atomically, which is what RTSS/Overwolf
        /// /Afterburner-style overlays use for their anchored geometry.
        /// </summary>
        private void ApplyPresetGeometryAtomic()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                ApplyPositionPreset();
                return;
            }

            var scaledWidth = GetOverlayScaledWidth();
            if (scaledWidth <= 0)
                return;

            var screen = Forms.Screen.PrimaryScreen;
            if (screen == null)
                return;

            var dpiScale = GetPrimaryScreenDpiScale();
            var areaLeft = screen.Bounds.Left / dpiScale;
            var areaTop = screen.Bounds.Top / dpiScale;
            var areaRight = screen.Bounds.Right / dpiScale;
            var areaBottom = screen.Bounds.Bottom / dpiScale;

            // Calculate Left/Top exactly like ApplyPositionPreset.
            var preset = ParsePositionPreset(_viewModel.GamingOverlayPositionPreset);
            var gap = 0.0;
            double left;
            switch (preset)
            {
                case GamingOverlayPositionPreset.TopLeft:
                case GamingOverlayPositionPreset.BottomLeft:
                    left = areaLeft + gap;
                    break;
                case GamingOverlayPositionPreset.TopCenter:
                case GamingOverlayPositionPreset.BottomCenter:
                    left = areaLeft + ((areaRight - areaLeft - scaledWidth) / 2.0);
                    break;
                default:
                    left = areaRight - scaledWidth - gap;
                    break;
            }

            var h = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
            if (h <= 0)
                return;

            double top;
            switch (preset)
            {
                case GamingOverlayPositionPreset.BottomLeft:
                case GamingOverlayPositionPreset.BottomCenter:
                case GamingOverlayPositionPreset.BottomRight:
                    top = areaBottom - h - gap;
                    break;
                default:
                    top = areaTop + gap;
                    break;
            }

            var x = (int)Math.Round(left * dpiScale);
            var y = (int)Math.Round(top * dpiScale);
            var cx = (int)Math.Round(scaledWidth * dpiScale);
            var cy = (int)Math.Round(h * dpiScale);

            LogTrace($"[GamingOverlay] SetWindowPos (atomic): x={x} y={y} w={cx} h={cy} preset={preset} dpiScale={dpiScale:F3}");
            SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SwpNoZOrder | SwpNoActivate | SwpNoSendChanging);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            // Break the resize/reposition feedback loop: SetWindowPos (and ApplyPositionPreset)
            // raise LocationChanged, which would otherwise call UpdateOverlayMetrics -> ApplyWindowSize
            // -> Width change -> ViewModel_PropertyChanged -> debounce -> SetWindowPos -> ... which
            // makes the overlay shake while scaling down. When WE move the window (suppress flag active),
            // skip re-measuring; the layout is already settled.
            if (_suppressLocationSave)
                return;

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
                // Open directly on the Gaming Overlay settings page. The SettingsWindow
                // constructor already decides Owner based on IsInGame: during a game it
                // sets Owner = null so WPF does not bring the hidden launcher to the
                // foreground. Do NOT override Owner here (an explicit Owner would undo that).
                settingsWindow = new SettingsWindow(_viewModel, openGamingOverlay: true);

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

            // Apply the scale chosen during drag in one atomic operation.
            ApplyScaleSettled();

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