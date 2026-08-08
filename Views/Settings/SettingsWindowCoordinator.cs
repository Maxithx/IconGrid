using System;
using System.Windows;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    internal sealed class SettingsWindowCoordinator
    {
        private SettingsWindow? _window;
        private MainViewModel? _viewModel;

        public void Show(Window owner, MainViewModel viewModel, Action<bool> setSkipSavingLocation, Action<string>? logTrace = null)
        {
            if (_window == null)
            {
                _viewModel = viewModel;
                _window = new SettingsWindow(viewModel)
                {
                    Owner = owner
                };
                _window.Closed += (_, _) =>
                {
                    viewModel.IsSettingsWindowOpen = false;
                    _window = null;
                    _viewModel = null;
                    setSkipSavingLocation(false);
                    logTrace?.Invoke($"Settings window closed; returning to normal focus from {owner.Left:F1},{owner.Top:F1}");
                };
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

            viewModel.IsSettingsWindowOpen = true;
            setSkipSavingLocation(true);
            _window.Show();
        }

        public void Close()
        {
            if (_window == null)
            {
                return;
            }

            if (_viewModel != null)
                _viewModel.IsSettingsWindowOpen = false;
            _window.Close();
            _window = null;
            _viewModel = null;
        }

        private static void PositionRelativeToOwner(Window owner, SettingsWindow window)
        {
            var workArea = SystemParameters.WorkArea;
            const double gap = 16;
            var desiredLeft = owner.Left + owner.Width + gap;
            if (desiredLeft + window.Width > workArea.Right)
            {
                desiredLeft = owner.Left - window.Width - gap;
            }

            if (desiredLeft < workArea.Left)
            {
                desiredLeft = workArea.Left;
            }

            var desiredTop = owner.Top;
            if (desiredTop + window.Height > workArea.Bottom)
            {
                desiredTop = workArea.Bottom - window.Height;
            }

            if (desiredTop < workArea.Top)
            {
                desiredTop = workArea.Top;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = desiredLeft;
            window.Top = desiredTop;
        }
    }
}