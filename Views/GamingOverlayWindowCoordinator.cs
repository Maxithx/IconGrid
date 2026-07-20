using System;
using System.Windows;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    internal sealed class GamingOverlayWindowCoordinator
    {
        private GamingOverlayWindow? _window;

        public void Show(Window owner, MainViewModel viewModel, GamingOverlayLayout layout)
        {
            if (_window == null || _window.IsLoaded == false)
            {
                _window = new GamingOverlayWindow(viewModel, layout);
                _window.Closed += (_, _) => _window = null;
            }
            else
            {
                _window.Close();
                _window = new GamingOverlayWindow(viewModel, layout);
                _window.Closed += (_, _) => _window = null;
            }

            if (!_window.TryApplySavedPosition())
            {
                PositionRelativeToOwner(owner, _window);
            }

            if (_window.IsVisible)
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.Activate();
                return;
            }

            _window.Show();
        }

        public void Close()
        {
            if (_window == null)
                return;

            _window.Close();
            _window = null;
        }

        private static void PositionRelativeToOwner(Window owner, Window window)
        {
            var workArea = SystemParameters.WorkArea;
            const double gap = 16;
            var desiredLeft = owner.Left + Math.Max(0, owner.Width - window.Width);
            var desiredTop = owner.Top + owner.Height + gap;

            if (desiredLeft + window.Width > workArea.Right)
            {
                desiredLeft = workArea.Right - window.Width;
            }

            if (desiredLeft < workArea.Left)
            {
                desiredLeft = workArea.Left;
            }

            if (desiredTop + window.Height > workArea.Bottom)
            {
                desiredTop = Math.Max(workArea.Top, owner.Top - window.Height - gap);
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = desiredLeft;
            window.Top = desiredTop;
        }
    }
}
