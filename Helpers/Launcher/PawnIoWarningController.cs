using System;
using System.Windows.Threading;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;
using IconGrid.Views;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the PawnIo-missing warning window lifecycle that previously lived in MainWindow.xaml.cs:
    /// showing/closing the warning, the retry timer that re-checks availability, and the
    /// SystemMonitor.PropertyChanged subscription that triggers re-evaluation.
    /// </summary>
    public sealed class PawnIoWarningController
    {
        private readonly MainViewModel _viewModel;
        private PawnIoWarningWindow? _pawnIoWarningWindow;
        private DispatcherTimer? _pawnIoWarningRetryTimer;

        public PawnIoWarningController(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.SystemMonitor.PropertyChanged += SystemMonitor_PropertyChanged;
        }

        /// <summary>
        /// Re-evaluates whether the warning should be shown and shows/closes it accordingly.
        /// </summary>
        public void Update()
        {
            UpdatePawnIoWarningWindow();
        }

        /// <summary>
        /// Unsubscribes from the monitor and closes the warning window. Call on shutdown.
        /// </summary>
        public void Close()
        {
            _viewModel.SystemMonitor.PropertyChanged -= SystemMonitor_PropertyChanged;
            ClosePawnIoWarningWindow();
        }

        private void SystemMonitor_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(SystemMonitor.IsPawnIoAvailable))
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                    new Action(UpdatePawnIoWarningWindow),
                    DispatcherPriority.Background);
            }
        }

        private void PawnIoWarningWindow_Closed(object? sender, EventArgs e)
        {
            var window = _pawnIoWarningWindow;
            if (window != null && sender == window)
            {
                window.Closed -= PawnIoWarningWindow_Closed;
                _pawnIoWarningWindow = null;
            }
        }

        private void UpdatePawnIoWarningWindow()
        {
            if (_viewModel.SystemMonitor.IsPawnIoAvailable || PawnIoHelper.IsPawnIoInstalled())
            {
                StopPawnIoWarningRetryTimer();
                ClosePawnIoWarningWindow();
                return;
            }

            if (_pawnIoWarningWindow != null)
            {
                return;
            }

            _pawnIoWarningWindow = new PawnIoWarningWindow(_viewModel.PawnIoMissingMessage, _viewModel.PawnIoDownloadLink);
            _pawnIoWarningWindow.Closed += PawnIoWarningWindow_Closed;
            _pawnIoWarningWindow.Show();
            StartPawnIoWarningRetryTimer();
        }

        private void ClosePawnIoWarningWindow()
        {
            if (_pawnIoWarningWindow == null)
            {
                return;
            }

            _pawnIoWarningWindow.Closed -= PawnIoWarningWindow_Closed;
            _pawnIoWarningWindow.Close();
            _pawnIoWarningWindow = null;
        }

        private void StartPawnIoWarningRetryTimer()
        {
            _pawnIoWarningRetryTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _pawnIoWarningRetryTimer.Tick -= PawnIoWarningRetryTimer_Tick;
            _pawnIoWarningRetryTimer.Tick += PawnIoWarningRetryTimer_Tick;
            _pawnIoWarningRetryTimer.Stop();
            _pawnIoWarningRetryTimer.Start();
        }

        private void StopPawnIoWarningRetryTimer()
        {
            if (_pawnIoWarningRetryTimer == null)
            {
                return;
            }

            _pawnIoWarningRetryTimer.Stop();
            _pawnIoWarningRetryTimer.Tick -= PawnIoWarningRetryTimer_Tick;
        }

        private void PawnIoWarningRetryTimer_Tick(object? sender, EventArgs e)
        {
            StopPawnIoWarningRetryTimer();
            UpdatePawnIoWarningWindow();
        }
    }
}