using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;

namespace IconGrid.Helpers
{
    public sealed class FloatingIconController
    {
        public const double FloatingIconSize = 66;
        public const double FloatingIconMargin = 12;

        private System.Windows.Point _dragStartPoint;

        public bool IsDragging { get; private set; }

        public void EnterFloatingMode(
            Window window,
            MainViewModel viewModel,
            DispatcherTimer? autoHideTimer,
            Forms.NotifyIcon? trayIcon,
            Action<bool> setMonitorPollingEnabled)
        {
            viewModel.IsFullWindowVisible = false;
            setMonitorPollingEnabled(false);
            IsDragging = false;

            window.WindowState = WindowState.Normal;

            autoHideTimer?.Stop();
            window.BeginAnimation(Window.TopProperty, null);
            window.BeginAnimation(Window.LeftProperty, null);
            window.BeginAnimation(Window.WidthProperty, null);
            window.BeginAnimation(Window.HeightProperty, null);

            window.ClearValue(Window.WidthProperty);
            window.ClearValue(Window.HeightProperty);
            window.Width = FloatingIconSize;
            window.Height = FloatingIconSize;

            PositionFloatingIcon(window, viewModel);
            window.ShowInTaskbar = false;

            if (trayIcon != null)
            {
                trayIcon.Visible = true;
            }
        }

        public void PositionFloatingIcon(Window window, MainViewModel viewModel, bool preferSaved = true)
        {
            var area = SystemParameters.WorkArea;
            double left = area.Right - FloatingIconSize - FloatingIconMargin;
            double top = area.Bottom - FloatingIconSize - FloatingIconMargin;

            if (preferSaved && viewModel.TryGetSavedFloatingPosition(out var savedLeft, out var savedTop))
            {
                left = savedLeft;
                top = savedTop;
            }

            (left, top) = ClampToWorkArea(left, top, FloatingIconSize, FloatingIconSize);
            window.Left = left;
            window.Top = top;
        }

        public void ClampFloatingIconToWorkArea(Window window)
        {
            var (left, top) = ClampToWorkArea(window.Left, window.Top, FloatingIconSize, FloatingIconSize);
            window.Left = left;
            window.Top = top;
        }

        public void HandleMouseLeftButtonDown(Window window, MainViewModel viewModel, MouseButtonEventArgs e)
        {
            if (viewModel.IsFullWindowVisible)
                return;

            _dragStartPoint = e.GetPosition(window);
            IsDragging = false;
            e.Handled = true;
        }

        public void HandleMouseMove(Window window, MainViewModel viewModel, System.Windows.Input.MouseEventArgs e)
        {
            if (viewModel.IsFullWindowVisible || e.LeftButton != MouseButtonState.Pressed)
                return;

            var current = e.GetPosition(window);
            var delta = current - _dragStartPoint;

            if (IsDragging ||
                (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                 Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance))
            {
                return;
            }

            IsDragging = true;
            try
            {
                window.DragMove();
            }
            catch
            {
            }

            ClampFloatingIconToWorkArea(window);
            viewModel.SaveFloatingIconPosition(window.Left, window.Top);
            e.Handled = true;
        }

        public bool HandleMouseLeftButtonUp(MainViewModel viewModel, MouseButtonEventArgs e)
        {
            if (viewModel.IsFullWindowVisible)
                return false;

            var shouldOpen = !IsDragging;
            IsDragging = false;
            e.Handled = true;
            return shouldOpen;
        }

        public bool HandleClick()
        {
            if (!IsDragging)
                return true;

            IsDragging = false;
            return false;
        }

        public void HandleLocationChanged(Window window, MainViewModel viewModel, bool skipSavingLocation)
        {
            ClampFloatingIconToWorkArea(window);
            if (!skipSavingLocation)
            {
                viewModel.SaveFloatingIconPosition(window.Left, window.Top);
            }
        }

        private static (double left, double top) ClampToWorkArea(double left, double top, double width, double height)
        {
            var area = SystemParameters.WorkArea;

            var newLeft = left;
            var newTop = top;

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
    }
}
