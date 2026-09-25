 using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IconGrid.ViewModels;
using Forms = System.Windows.Forms;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the launcher window-mode and positioning behavior that previously lived in MainWindow.xaml.cs:
    /// full vs floating mode, full-window sizing/positioning, work-area clamping, auto-hide sliding,
    /// and idle hide modes (manual hide via button, auto-hide with proximity detection).
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class LauncherWindowModeController
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly FloatingIconController _floatingIconController;
        private readonly DispatcherTimer? _autoHideTimer;
        private readonly Forms.NotifyIcon? _trayIcon;
        private readonly Action<bool> _setMonitorPollingEnabled;
        private bool _autoHideEnabled;
        private bool _isHidden;

        // Idle hide (no game running): manual / auto-hide modes.
        private readonly DispatcherTimer _idleProximityTimer;
        private readonly DispatcherTimer _idleHideDelayTimer;
        private bool _isManuallyHidden;
        private double _peekHeight = 10;
        private bool _isGameHideActive;
        private bool _isPeekAtBottom; // true when launcher is hidden at the bottom edge
        private double? _originalTop;  // saved position before peek (DIP), used to restore; null = not saved
        private double? _preGameTop;   // window position BEFORE game-hide (DIP). Separate from _originalTop
                                       // because auto-hide may have already saved the peek position there.

        // Win32 interop for getting the actual rendered window position.
        // _window.Left/Top/Width are NOT updated during WPF animations
        // (BeginAnimation), so we use GetWindowRect for proximity checks.
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private bool TryGetWindowRect(out RECT rect)
        {
            try
            {
                var handle = new WindowInteropHelper(_window).Handle;
                return GetWindowRect(handle, out rect);
            }
            catch
            {
                rect = default;
                return false;
            }
        }

        public LauncherWindowModeController(
            Window window,
            MainViewModel viewModel,
            FloatingIconController floatingIconController,
            DispatcherTimer? autoHideTimer,
            Forms.NotifyIcon? trayIcon,
            Action<bool> setMonitorPollingEnabled)
        {
            _window = window;
            _viewModel = viewModel;
            _floatingIconController = floatingIconController;
            _autoHideTimer = autoHideTimer;
            _trayIcon = trayIcon;
            _setMonitorPollingEnabled = setMonitorPollingEnabled;

            _idleProximityTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _idleProximityTimer.Tick += IdleProximityTimer_Tick;

            _idleHideDelayTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(_viewModel.IdleAutoHideDelaySeconds <= 0 ? 2 : _viewModel.IdleAutoHideDelaySeconds)
            };
            _idleHideDelayTimer.Tick += IdleHideDelayTimer_Tick;
        }

        public bool IsHidden => _isHidden;
        public bool IsManuallyHidden => _isManuallyHidden;
        public bool IsIdleHideActive { get; private set; }

        public void EnterFullMode()
        {
            var wasFull = _viewModel.IsFullWindowVisible;
            _viewModel.IsFullWindowVisible = true;
            _setMonitorPollingEnabled(true);
            ApplyFullWindowSizing();
            if (!wasFull)
            {
                PositionFullWindow();

                if (_viewModel.EnableSlideUpAnimation)
                {
                    if (_window.FindName("IconAreaGrid") is FrameworkElement iconAreaGrid)
                    {
                        iconAreaGrid.Opacity = 0;
                        if (_window.FindResource("SlideUpAnimation") is Storyboard slideUpAnimation)
                        {
                            var animation = slideUpAnimation.Clone();
                            animation.Begin(iconAreaGrid);
                        }
                    }
                }
                else
                {
                    if (_window.FindName("IconAreaGrid") is FrameworkElement iconAreaGrid)
                    {
                        iconAreaGrid.Opacity = 1;
                        iconAreaGrid.RenderTransform = new TranslateTransform(0, 0);
                    }
                }
            }
            _window.WindowState = WindowState.Normal;
            _window.ShowInTaskbar = true;

            // Apply idle hide mode when entering full mode (if no game is running).
            if (!_isGameHideActive)
            {
                ApplyIdleHideMode();
            }

            _window.Activate();
        }

        public void EnterFloatingMode()
        {
            _autoHideEnabled = false;
            _floatingIconController.EnterFloatingMode(_window, _viewModel, _autoHideTimer, _trayIcon, _setMonitorPollingEnabled);
            StopIdleHide();
        }

        public void ApplyFullWindowSizing()
        {
            _window.SetBinding(Window.WidthProperty, new System.Windows.Data.Binding(nameof(MainViewModel.WindowDesiredWidth)));
            _window.SetBinding(Window.HeightProperty, new System.Windows.Data.Binding(nameof(MainViewModel.WindowDesiredHeightEffective)));
        }

        public void PositionFullWindow()
        {
            if (_viewModel.TryGetSavedWindowPosition(out var left, out var top))
            {
                _window.Left = left;
                _window.Top = top;
                return;
            }

            var area = WindowWorkAreaHelper.GetWorkAreaDip(_window);
            var width = _viewModel.WindowDesiredWidth;
            var height = _viewModel.WindowDesiredHeight;
            const double defaultTopOffset = 20;
            _window.Left = area.Left + Math.Max(0, (area.Width - width) / 2);
            _window.Top = area.Top + defaultTopOffset;
        }

        public void PositionFloatingIcon(bool preferSaved = true)
        {
            _floatingIconController.PositionFloatingIcon(_window, _viewModel, preferSaved);
        }

        public void ClampWindowToWorkArea()
        {
            var width = double.IsNaN(_window.ActualWidth) || _window.ActualWidth <= 0 ? _window.Width : _window.ActualWidth;
            var height = double.IsNaN(_window.ActualHeight) || _window.ActualHeight <= 0 ? _window.Height : _window.ActualHeight;
            var area = WindowWorkAreaHelper.GetWorkAreaDip(_window);
            var (left, top) = WindowWorkAreaHelper.Clamp(_window.Left, _window.Top, width, height, area);
            _window.Left = left;
            _window.Top = top;
        }

        public void ClampFloatingIconToWorkArea()
        {
            _floatingIconController.ClampFloatingIconToWorkArea(_window);
        }

        // ---- Idle hide (manual / auto, no game running) ----

        /// <summary>
        /// Called when the user clicks the mini-button in the icon area to toggle manual hide.
        /// </summary>
        public void ToggleManualHide()
        {
            if (_isManuallyHidden)
            {
                ShowLauncherFromManualHide();
            }
            else
            {
                HideLauncherManually();
            }
        }

        private void HideLauncherManually()
        {
            if (_isManuallyHidden)
                return;

            _isManuallyHidden = true;
            IsIdleHideActive = true;
            _idleHideDelayTimer.Stop();
            SlideToPeek();
        }

        private void ShowLauncherFromManualHide()
        {
            if (!_isManuallyHidden)
                return;

            _isManuallyHidden = false;
            IsIdleHideActive = IsAutoHideEnabled();
            SlideToVisible();

            if (!IsIdleHideActive)
            {
                _idleProximityTimer.Stop();
                _idleHideDelayTimer.Stop();
            }
        }

        /// <summary>
        /// Called when the peek strip is clicked — shows the launcher.
        /// In manual mode, the hide is released entirely.
        /// In auto mode, just slides visible and proximity tracking resumes.
        /// </summary>
        public void PeekStripClicked()
        {
            if (_isManuallyHidden)
            {
                // Manual mode: release the hide.
                ShowLauncherFromManualHide();
                return;
            }

            // Auto mode: just show. Proximity will re-hide when mouse leaves.
            if (IsIdleHideActive && _isHidden)
            {
                SlideToVisible();
            }
        }

        /// <summary>
        /// Checks whether proximity to the peek strip should trigger a show.
        /// Called every ~200ms by the idle proximity timer when the launcher is auto-hidden.
        /// </summary>
        private void IdleProximityTimer_Tick(object? sender, EventArgs e)
        {
            if (_isGameHideActive || !IsIdleHideActive || !_isHidden)
            {
                _idleProximityTimer.Stop();
                return;
            }

            try
            {
                var cursorPos = Forms.Cursor.Position;
                // SystemParameters.WorkArea returns WPF logical pixels (DIP).
                // GetWindowRect and Cursor.Position return physical pixels.
                // Convert to a common coordinate system: use DIP for all comparisons
                // by applying the window's DPI scale factor.
                var dpiScale = GetWindowDpiScale();
                var area = new Rect(
                    SystemParameters.WorkArea.Left,
                    SystemParameters.WorkArea.Top,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);

                // Peek activation mode: 0 = hover (proximity), 1 = click only.
                if (_viewModel.PeekActivationMode == 1)
                    return;

                // Convert physical cursor to DIP for comparison with DIP values.
                var cursorDipX = cursorPos.X / dpiScale;
                var cursorDipY = cursorPos.Y / dpiScale;

                // Use Win32 GetWindowRect for the ACTUAL rendered position (physical px),
                // then convert to DIP.
                double winDipLeft, winDipWidth;
                if (TryGetWindowRect(out var rect))
                {
                    winDipLeft = rect.Left / dpiScale;
                    winDipWidth = (rect.Right - rect.Left) / dpiScale;
                }
                else
                {
                    winDipLeft = _window.Left;
                    winDipWidth = _window.Width;
                }

                var peekHeightDip = _peekHeight;

                var inZone = _isPeekAtBottom
                    ? cursorDipY >= area.Bottom - peekHeightDip
                       && cursorDipX >= winDipLeft
                       && cursorDipX <= winDipLeft + winDipWidth
                    : cursorDipY <= area.Top + peekHeightDip
                       && cursorDipX >= winDipLeft
                       && cursorDipX <= winDipLeft + winDipWidth;

                TraceProximityTick(cursorPos, area.Top, winDipLeft, winDipLeft + winDipWidth, dpiScale, inZone);

                if (inZone)
                {
                    SlideToVisible();
                    // Don't restart the delay timer here. The mouse is already inside
                    // the window (the peek strip IS the window), so HandleMouseEnter
                    // won't fire. Let HandleMouseLeave restart the timer naturally.
                    _idleHideDelayTimer.Stop();
                }
            }
            catch
            {
                // Cursor position polling failure is non-critical.
            }
        }

        /// <summary>
        /// Returns the DPI scale factor for the monitor the window is on.
        /// PerMonitorV2: each monitor can have a different scale.
        /// </summary>
        private double GetWindowDpiScale()
        {
            try
            {
                var source = PresentationSource.FromVisual(_window);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformToDevice;
                    return matrix.M11; // X-scale; assume uniform scaling
                }
            }
            catch
            {
                // Fall through to fallback.
            }
            return 1.0;
        }

        private void TraceProximityTick(System.Drawing.Point cursor, double topEdgeDip, double zoneLeftDip, double zoneRightDip, double dpiScale, bool inZone)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "trace.log");
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [PeekZone] cursor=({cursor.X:F0},{cursor.Y:F0}) dpiScale={dpiScale:F2} topEdgeDip={topEdgeDip:F0} zoneDip=({zoneLeftDip:F0}-{zoneRightDip:F0}) winDip=({_window.Left:F0},{_window.Width:F0}) peekHeight={_peekHeight:F0} inZone={inZone} isPeekBottom={_isPeekAtBottom} isHidden={_isHidden}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch
            {
                // ignore logging failures
            }
        }

        /// <summary>
        /// After the idle delay expires, auto-hide the launcher by sliding
        /// it up to the peek position and starting proximity tracking for peek-to-show.
        /// </summary>
        private void IdleHideDelayTimer_Tick(object? sender, EventArgs e)
        {
            _idleHideDelayTimer.Stop();
            if (_isGameHideActive || _isHidden || _viewModel.IsOverlayOpen || _viewModel.IsSettingsWindowOpen)
                return;

            SlideToPeek();
            _idleProximityTimer.Start();
        }

        public void ApplyIdleHideMode()
        {
            var mode = _viewModel.LauncherHideMode;
            IsIdleHideActive = mode == 1 || mode == 2;

            if (!IsIdleHideActive)
            {
                StopIdleHide();
                return;
            }

            if (IsAutoHideEnabled())
            {
                if (!_isHidden && !_isManuallyHidden)
                {
                    _idleHideDelayTimer.Stop();
                    _idleHideDelayTimer.Start();
                }
                else
                {
                    _idleProximityTimer.Start();
                }
            }
        }

        private bool IsAutoHideEnabled()
        {
            return _viewModel.LauncherHideMode == 2;
        }

        private void StopIdleHide()
        {
            _idleProximityTimer.Stop();
            _idleHideDelayTimer.Stop();
            IsIdleHideActive = false;
            if (_isHidden && !_isGameHideActive)
            {
                SlideToVisible();
            }
            _isManuallyHidden = false;
        }

        // ---- Mouse enter/leave ----

        public void HandleMouseEnter()
        {
            if (IsAutoHideEnabled())
            {
                _idleHideDelayTimer.Stop();
            }

            if (_autoHideEnabled)
            {
                _autoHideTimer?.Stop();
                if (_isHidden)
                {
                    SlideTo(0);
                }
            }
        }

        public void HandleMouseLeave()
        {
            if (IsAutoHideEnabled() && !_isGameHideActive && !_isHidden && !_isManuallyHidden && !_viewModel.IsOverlayOpen && !_viewModel.IsSettingsWindowOpen)
            {
                _idleHideDelayTimer.Stop();
                _idleHideDelayTimer.Start();
            }

            if (_autoHideEnabled)
            {
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
        }

        public void HandleAutoHideTick()
        {
            if (_autoHideEnabled)
            {
                _autoHideTimer?.Stop();
                SlideTo(-_window.Height + 8);
                return;
            }

            if (IsAutoHideEnabled() && !_isGameHideActive && !_isHidden && !_viewModel.IsOverlayOpen && !_viewModel.IsSettingsWindowOpen)
            {
                _idleHideDelayTimer.Stop();
                _idleHideDelayTimer.Start();
            }
        }

        public void HideForGame(int behavior)
        {
            // Save the VISIBLE window position BEFORE hiding. If the launcher
            // is already hidden by auto-hide, _originalTop contains the PEEK
            // position (e.g. -202.7), not the visible one. Use the CURRENT
            // rendered position as the restore target instead — that's where
            // the user last saw the launcher before the game started.
            //
            // This matters because auto-hide fires SlideToPeek FIRST (saving
            // the visible position in _originalTop), then the idle delay timer
            // slides the window to the peek position. If a game starts while
            // the window is already at its peek position, _originalTop has been
            // overwritten — so we store the pre-game position separately.
            //
            // IMPORTANT: if the launcher is PHYSICALLY hidden (rendered off-screen)
            // even though _isHidden is false (a stale SlideToVisible left it at the
            // peek position), DO NOT use that off-screen position as the restore
            // target. Otherwise the launcher would restore to -205 instead of the
            // visible desktop position when the game exits — the "launcher never
            // comes out of hide mode" bug.
            var dpiScale = GetWindowDpiScale();
            var isPhysicallyOffScreen = IsPhysicallyHidden();
            if (TryGetWindowRect(out var currentRect))
            {
                _preGameTop = _isHidden
                    ? _originalTop
                    : (isPhysicallyOffScreen ? null : currentRect.Top / dpiScale);
            }
            else
            {
                _preGameTop = _isHidden
                    ? _originalTop
                    : (isPhysicallyOffScreen ? null : _window.Top);
            }

            _isGameHideActive = true;
            _autoHideEnabled = false;
            _autoHideTimer?.Stop();
            _idleProximityTimer.Stop();
            _idleHideDelayTimer.Stop();

            if (behavior == 1)
            {
                _isHidden = true;
                SlideTo(-_window.Height + 8);
            }
            else if (behavior == 2)
            {
                if (_window.WindowState != WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Minimized;
                }
            }
        }

        private void SlideToPreGameTop()
        {
            _isHidden = false;
            _isPeekAtBottom = false;
            _isManuallyHidden = false;
            var restoreTop = _preGameTop ?? _originalTop;
            if (!restoreTop.HasValue)
            {
                // No valid pre-game position saved (e.g. the launcher was already
                // off-screen due to a stale peek, or GetWindowRect failed). Fall
                // back to the desktop work-area so we NEVER leave the launcher
                // off-screen.
                restoreTop = SystemParameters.WorkArea.Top;
            }
            TraceSlideToVisible(restoreTop.Value);
            SlideTo(restoreTop.Value);
            _originalTop = null;
            _preGameTop = null;
        }

        public void RestoreLauncherFromGame()
        {
            // If the launcher was not hidden for a game AND it is not hidden at
            // all, there is nothing to do. This covers externally started games
            // (e.g. COD from Battle.net) where the launcher stayed visible —
            // running ApplyIdleHideMode would restart the auto-hide delay timer
            // and hide the launcher for no reason.
            //
            // HOWEVER: the game-hide flag can be cleared EARLY while the window
            // is still hidden in the game position. This happens when the user
            // closes the gaming overlay manually during a game — the OverlayClosed
            // handler calls this method, which resets _isGameHideActive, but the
            // launcher remains in the hidden game-hide position. When the game
            // later exits, the flag is already false, so the launcher would stay
            // hidden forever (and the peek strip would be dead because
            // ApplyIdleHideMode never re-armed it). Detect that stale case by
            // checking _isHidden and restore the window anyway.
            //
            // Additionally detect a PHYSICALLY hidden launcher even when the
            // logical flags say otherwise. A stale SlideToVisible() can set
            // _isHidden=false while the window is still rendered off-screen
            // (e.g. _originalTop contained the peek position -205 instead of
            // the visible one), leaving _isGameHideActive=false and _isHidden=false
            // but the window stuck at Top=-205. The window is then invisible and
            // no further event restores it — the "launcher locked in hide mode"
            // bug the user reports.
            if (!_isGameHideActive && !_isHidden && !IsPhysicallyHidden())
            {
                return;
            }

            // If the user MANUALLY hid the launcher, respect that — do not force
            // it back into view just because a game exited. Only the true
            // game-hide path (or a stale game-hide) auto-restores.
            if (!_isGameHideActive && _isManuallyHidden && !IsPhysicallyHidden())
            {
                return;
            }

            // Restore the launcher even if the "logical" hide flag is false, as
            // long as the window is physically off-screen. This covers the stale
            // case described above where _isHidden was reset but the window never
            // actually slid back into view.
            var restoreBecausePhysicallyHidden = !_isGameHideActive && !_isHidden && IsPhysicallyHidden();

            _isGameHideActive = false;

            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            }

            // Restore the saved pre-game position. If the game never set a valid
            // pre-game position (e.g. the launcher was already off-screen due to a
            // stale peek), fall back to _originalTop and finally to the desktop
            // work-area center so we NEVER leave the launcher off-screen.
            if (_isHidden || restoreBecausePhysicallyHidden)
            {
                // Use the saved pre-game position, NOT _originalTop.
                // _originalTop may have been overwritten by auto-hide's
                // SlideToPeek while the game was starting, so it contains
                // the peek position (-202.7) instead of the visible one.
                SlideToPreGameTop();
            }

            if (_viewModel.IsFullWindowVisible)
            {
                ApplyIdleHideMode();
            }
        }

        /// <summary>
        /// True when the launcher's actual rendered top is fully off the visible
        /// work area (i.e. above the screen top or below the bottom edge).
        /// Uses Win32 GetWindowRect so it reflects the REAL rendered position,
        /// not the logical WPF Top property which can be stale during animations.
        /// </summary>
        private bool IsPhysicallyHidden()
        {
            try
            {
                var dpiScale = GetWindowDpiScale();
                if (TryGetWindowRect(out var rect))
                {
                    var topDip = rect.Top / dpiScale;
                    var area = SystemParameters.WorkArea;
                    // The peek strip keeps ~10px visible; anything fully outside
                    // the work area (more than a few px above/below) is "hidden".
                    return topDip < area.Top - 20 || topDip > area.Bottom + 20;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ---- Sliding ----

        private void SlideToPeek()
        {
            // Cache the current rendered position via Win32 so we can restore it exactly.
            // GetWindowRect returns physical pixels; convert to DIP for _window.TopProperty.
            var dpiScale = GetWindowDpiScale();
            if (TryGetWindowRect(out var currentRect))
            {
                _originalTop = currentRect.Top / dpiScale;
            }
            else
            {
                _originalTop = _window.Top;
            }

            var area = SystemParameters.WorkArea;

            // Determine direction: slide up if launcher is in the top half of the work area,
            // slide down if it's in the bottom half. Uses WorkArea (DIP) consistently.
            double targetTop;
            var areaCenterY = (area.Top + area.Bottom) / 2.0;
            var winCenterY = _originalTop + _window.Height / 2.0;
            _isPeekAtBottom = winCenterY > areaCenterY;
            targetTop = _isPeekAtBottom
                ? area.Bottom - _peekHeight
                : area.Top - _window.Height + _peekHeight;

            _isHidden = true;
            TraceSlideToPeek(_originalTop.Value, targetTop, _isPeekAtBottom);
            SlideTo(targetTop);
        }

        private void SlideToVisible()
        {
            _isHidden = false;
            _isPeekAtBottom = false;
            // Restore to the saved original position from before hide.
            // Use null-check instead of >0 so Top=0 positions work correctly.
            var restoreTop = _originalTop ?? _window.Top;
            TraceSlideToVisible(restoreTop);
            SlideTo(restoreTop);
            _originalTop = null;
        }

        private void SlideTo(double targetTop)
        {
            // Kill any stale animation clock. This is processed asynchronously
            // by WPF's dispatcher — it may not take effect until after we return.
            _window.BeginAnimation(Window.TopProperty, null);

            // Snap the local Top property to the current REAL rendered position
            // (via GetWindowRect → DIP). This sets the correct base value.
            var dpiScale = GetWindowDpiScale();
            var fromDip = TryGetWindowRect(out var currentRect)
                ? currentRect.Top / dpiScale
                : _window.Top;
            _window.Top = fromDip;

            // Defer the new animation by one dispatcher tick so the old clock
            // removal (BeginAnimation(null) above) is fully committed first.
            // Without this, the old animation's interpolated value may leak into
            // the new animation's From, causing a visible "hop" when the window
            // was dragged to a new position between slides.
            _window.Dispatcher.BeginInvoke(() =>
            {
                var animation = new DoubleAnimation
                {
                    From = fromDip,
                    To = targetTop,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase()
                };
                _window.BeginAnimation(Window.TopProperty, animation);
                TraceSlideAnimation(fromDip, targetTop);
            }, DispatcherPriority.Background);
        }

        private void TraceSlideAnimation(double fromDip, double toDip)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "trace.log");
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [SlideAnim] from={fromDip:F1} to={toDip:F1} delta={toDip - fromDip:F1}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch { /* ignore */ }
        }

        private void TraceSlideToPeek(double originalTopDip, double targetTopDip, bool isPeekBottom)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "trace.log");
                var dpi = GetWindowDpiScale();
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [SlidePeek] originalTopDip={originalTopDip:F1} targetTopDip={targetTopDip:F1} isPeekBottom={isPeekBottom} dpiScale={dpi:F2} winHeight={_window.Height:F1} winTop={_window.Top:F1}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch { /* ignore */ }
        }

        private void TraceSlideToVisible(double restoreTopDip)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "trace.log");
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [SlideVisible] restoreTopDip={restoreTopDip:F1}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch { /* ignore */ }
        }
    }
}
