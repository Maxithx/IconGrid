using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IconGrid.ViewModels;
using Forms = System.Windows.Forms;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the launcher window-mode and positioning behavior that previously lived in MainWindow.xaml.cs:
    /// full vs floating mode, full-window sizing/positioning, work-area clamping, and auto-hide sliding.
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
        }

        public bool IsHidden => _isHidden;

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

            _window.Activate();
        }

        public void EnterFloatingMode()
        {
            _autoHideEnabled = false;
            _floatingIconController.EnterFloatingMode(_window, _viewModel, _autoHideTimer, _trayIcon, _setMonitorPollingEnabled);
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

            var area = SystemParameters.WorkArea;
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
            var (left, top) = ClampToWorkArea(_window.Left, _window.Top, width, height);
            _window.Left = left;
            _window.Top = top;
        }

        public void ClampFloatingIconToWorkArea()
        {
            _floatingIconController.ClampFloatingIconToWorkArea(_window);
        }

        public static (double left, double top) ClampToWorkArea(double left, double top, double width, double height)
        {
            var area = SystemParameters.WorkArea;

            var newLeft = left;
            var newTop = top;

            // Clamp to work area boundaries
            if (newLeft + width > area.Right)
                newLeft = area.Right - width;
            if (newTop + height > area.Bottom)
                newTop = area.Bottom - height;
            if (newLeft < area.Left)
                newLeft = area.Left;
            if (newTop < area.Top)
                newTop = area.Top;

            return (newLeft, newTop);
        }

        public void HandleMouseEnter()
        {
            if (!_autoHideEnabled)
                return;

            _autoHideTimer?.Stop();
            if (_isHidden)
            {
                SlideTo(0);
            }
        }

        public void HandleMouseLeave()
        {
            if (!_autoHideEnabled)
                return;

            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }

        public void HandleAutoHideTick()
        {
            _autoHideTimer?.Stop();
            SlideTo(-_window.Height + 8);
        }

        private void SlideTo(double targetTop)
        {
            var animation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };

            _window.BeginAnimation(Window.TopProperty, animation);
            _isHidden = targetTop < 0;
        }
    }
}