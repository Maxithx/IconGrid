using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using IconGrid.ViewModels;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the periodic hardware-monitor polling that previously lived in MainWindow.xaml.cs:
    /// the 2-second DispatcherTimer, the re-entrancy guard, and the enable/disable callback
    /// used by the window-mode controller when switching full/floating mode.
    /// </summary>
    public sealed class MonitorPollingController
    {
        private readonly MainViewModel _viewModel;
        private readonly DispatcherTimer _monitorTimer;
        private int _monitorUpdateRunning;

        public MonitorPollingController(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
        }

        /// <summary>
        /// Starts periodic polling and performs an immediate refresh. Call once on startup.
        /// </summary>
        public void Start()
        {
            _monitorTimer.Start();
            _viewModel.SystemMonitor.Update();
        }

        /// <summary>
        /// Enables or disables periodic polling. Kept for the window-mode controller so full
        /// mode polls the monitor while floating mode does not.
        /// </summary>
        public void SetPollingEnabled(bool enabled)
        {
            if (enabled)
            {
                if (!_monitorTimer.IsEnabled)
                {
                    _monitorTimer.Start();
                    _viewModel.SystemMonitor.Update();
                }
            }
            else
            {
                _monitorTimer.Stop();
            }
        }

        /// <summary>
        /// Stops periodic polling. Call on shutdown.
        /// </summary>
        public void Stop()
        {
            _monitorTimer.Stop();
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref _monitorUpdateRunning, 1, 0) == 1)
                return;

            Task.Run(() =>
            {
                try
                {
                    _viewModel.SystemMonitor.Update();
                }
                finally
                {
                    Interlocked.Exchange(ref _monitorUpdateRunning, 0);
                }
            });
        }
    }
}