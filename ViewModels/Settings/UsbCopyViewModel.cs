using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using IconGrid.Helpers;
using IconGrid.Helpers.UsbCopy;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace IconGrid.ViewModels.Settings
{
    /// <summary>
    /// Feature view model for the Fast USB Copy settings page. Keeps all USB
    /// copy/benchmark logic out of MainViewModel and owns the UsbCopyState.
    /// </summary>
    public sealed class UsbCopyViewModel
    {
        private readonly UsbDeviceDetector _detector = new();
        private readonly UsbCopyEngine _engine;
        private readonly UsbBenchmarkRunner _benchmark;
        private readonly UsbCopyLogger _logger;
        private readonly List<string> _selectedFiles = new();
        private CancellationTokenSource? _cts;

        private DateTime _lastUiUpdateTime = DateTime.UtcNow;

        public UsbCopyViewModel()
        {
            _logger = new UsbCopyLogger();
            _engine = new UsbCopyEngine(_logger);
            _benchmark = new UsbBenchmarkRunner(_logger);

            _engine.Progress += OnEngineProgress;
            _engine.PipelineEvent += OnPipelineEvent;
            _engine.Error += OnEngineError;
            _benchmark.Progress += OnBenchmarkProgress;
            _benchmark.Error += OnBenchmarkError;

            RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());
            ChooseFilesCommand = new RelayCommand(_ => ChooseFiles());
            StartCopyCommand = new RelayCommand(_ => _ = StartCopyAsync(), _ => CanStartCopy());
            CancelCopyCommand = new RelayCommand(_ => CancelCopy(), _ => State.IsCopying || State.IsBenchmarking);
            ViewLogCommand = new RelayCommand(_ => ViewLog());
            ExportLogCommand = new RelayCommand(_ => ExportLog());
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            RunBenchmarkCommand = new RelayCommand(_ => _ = RunFullBenchmarkAsync(), _ => CanStartCopy());
            PortSpeedTestCommand = new RelayCommand(_ => _ = RunPortSpeedTestAsync(), _ => CanStartCopy());
            ReadTestCommand = new RelayCommand(_ => _ = RunReadTestAsync(), _ => CanStartCopy());
            WriteTestCommand = new RelayCommand(_ => _ = RunWriteTestAsync(), _ => CanStartCopy());
            BufferStressTestCommand = new RelayCommand(_ => _ = RunBufferStressTestAsync(), _ => CanStartCopy());
            StabilityTestCommand = new RelayCommand(_ => _ = RunStabilityTestAsync(), _ => CanStartCopy());
            WindowsBaselineCommand = new RelayCommand(_ => _ = RunWindowsBaselineAsync(), _ => CanStartCopy());
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
        }

        public UsbCopyState State { get; } = new();

        public ICommand RefreshDevicesCommand { get; }

        public ICommand ChooseFilesCommand { get; }

        public ICommand StartCopyCommand { get; }

        public ICommand CancelCopyCommand { get; }

        public ICommand ViewLogCommand { get; }

        public ICommand ExportLogCommand { get; }

        public ICommand ClearLogCommand { get; }

        public ICommand RunBenchmarkCommand { get; }

        public ICommand PortSpeedTestCommand { get; }

        public ICommand ReadTestCommand { get; }

        public ICommand WriteTestCommand { get; }

        public ICommand BufferStressTestCommand { get; }

        public ICommand StabilityTestCommand { get; }

        public ICommand WindowsBaselineCommand { get; }

        public ICommand ExportCsvCommand { get; }

        public void RefreshDevices()
        {
            var devices = _detector.DetectDevices();
            State.Devices = devices;
            if (State.SelectedDevice == null && devices.Count > 0)
            {
                State.SelectedDevice = devices[0];
            }
        }

        private void ChooseFiles()
        {
            var dialog = new WpfOpenFileDialog
            {
                Multiselect = true,
                Title = "Select files"
            };
            if (dialog.ShowDialog() == true)
            {
                _selectedFiles.Clear();
                _selectedFiles.AddRange(dialog.FileNames);
                State.PipelineStatus = $"{_selectedFiles.Count} file(s) selected";
            }
        }

        private bool CanStartCopy()
        {
            var device = State.SelectedDevice;
            return device != null && device.IsReady && !State.IsCopying && !State.IsBenchmarking;
        }

        private async Task StartCopyAsync()
        {
            var device = State.SelectedDevice;
            if (device == null || !device.IsReady || _selectedFiles.Count == 0)
            {
                return;
            }

            State.IsCopying = true;
            State.LastError = string.Empty;
            State.LiveMiBS = 0;
            State.AverageMiBS = 0;
            State.PeakMiBS = 0;
            State.Progress = 0;
            State.PipelineStatus = "starting";
            _cts = new CancellationTokenSource();

            try
            {
                var target = Path.Combine(device.DriveLetter, "IconGridCopy");
                Directory.CreateDirectory(target);
                await _engine.CopyFilesAsync(_selectedFiles, target, State.BufferSize, _cts.Token);
                State.PipelineStatus = "done";
            }
            catch (OperationCanceledException)
            {
                State.PipelineStatus = "cancelled";
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
                State.PipelineStatus = "error";
            }
            finally
            {
                State.IsCopying = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CancelCopy()
        {
            _cts?.Cancel();
            State.PipelineStatus = "cancelling";
        }

        private async Task RunFullBenchmarkAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            State.BenchmarkResults.Clear();
            try
            {
                var write = await _benchmark.DeviceWriteBenchmarkAsync(device.DriveLetter, State.BufferSize, 64 * 1024 * 1024, _cts!.Token);
                State.BenchmarkResults.Add(write);
                var results = await _benchmark.BufferStressTestAsync(device.DriveLetter, 16 * 1024 * 1024, _cts.Token);
                foreach (var r in results)
                {
                    State.BenchmarkResults.Add(r);
                }
            }
            catch (OperationCanceledException)
            {
                State.PipelineStatus = "cancelled";
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunPortSpeedTestAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var result = await _benchmark.PortSpeedTestAsync(device.DriveLetter, State.BufferSize, 64 * 1024 * 1024, _cts!.Token);
                AddBenchmarkResult(result);
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunReadTestAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var probe = Path.Combine(device.DriveLetter, $".icongrid-readprobe-{Guid.NewGuid():N}.tmp");
                await File.WriteAllBytesAsync(probe, new byte[8 * 1024 * 1024]);
                try
                {
                    var result = await _benchmark.DeviceReadBenchmarkAsync(probe, State.BufferSize, _cts!.Token);
                    AddBenchmarkResult(result);
                }
                finally
                {
                    try
                    {
                        File.Delete(probe);
                    }
                    catch (IOException)
                    {
                        // Best effort cleanup.
                    }
                }
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunWriteTestAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var result = await _benchmark.DeviceWriteBenchmarkAsync(device.DriveLetter, State.BufferSize, 64 * 1024 * 1024, _cts!.Token);
                AddBenchmarkResult(result);
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunBufferStressTestAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var results = await _benchmark.BufferStressTestAsync(device.DriveLetter, 16 * 1024 * 1024, _cts!.Token);
                foreach (var r in results)
                {
                    AddBenchmarkResult(r);
                }
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunStabilityTestAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var result = await _benchmark.StabilityTestAsync(device.DriveLetter, State.BufferSize, TimeSpan.FromSeconds(30), _cts!.Token);
                AddBenchmarkResult(result);
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task RunWindowsBaselineAsync()
        {
            if (!TryPrepareBenchmark(out var device))
            {
                return;
            }

            State.IsBenchmarking = true;
            try
            {
                var result = await _benchmark.WindowsBaselineTestAsync(device.DriveLetter, 64 * 1024 * 1024, _cts!.Token);
                AddBenchmarkResult(result);
            }
            catch (Exception ex)
            {
                State.LastError = ex.Message;
            }
            finally
            {
                State.IsBenchmarking = false;
                _cts?.Dispose();
                _cts = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ExportCsv()
        {
            if (State.BenchmarkResults.Count == 0)
            {
                State.LastError = "No benchmark results to export.";
                return;
            }

            var dialog = new WpfSaveFileDialog
            {
                Title = "Export benchmark results (CSV)",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"benchmark-results-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                var csvPath = _logger.ExportBenchmarkCsv(State.BenchmarkResults);
                File.Copy(csvPath, dialog.FileName, overwrite: true);
                State.PipelineStatus = $"CSV exported: {dialog.FileName}";
            }
        }

        private bool TryPrepareBenchmark(out UsbDeviceInfo device)
        {
            device = State.SelectedDevice!;
            if (device == null || !device.IsReady)
            {
                State.LastError = "No ready USB device selected.";
                return false;
            }

            State.LastError = string.Empty;
            State.PipelineStatus = "benchmark";
            _cts = new CancellationTokenSource();
            return true;
        }

        private void AddBenchmarkResult(UsbBenchmarkResult result)
        {
            RunOnUi(() =>
            {
                State.BenchmarkResults.Add(result);
                State.PipelineStatus = $"benchmark done: {result.TestName} {result.AverageMiBS:0.00} MiB/s";
            });
        }

        private void ViewLog()
        {
            var files = _logger.GetLogFiles();
            if (files.Count == 0)
            {
                State.LogContent = "Log is empty.";
                return;
            }

            var latest = files[^1];
            State.LogContent = _logger.ReadLogFile(latest) ?? string.Empty;
        }

        private void ExportLog()
        {
            var dialog = new WpfSaveFileDialog
            {
                Title = "Export log",
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt",
                FileName = $"fastusbcopy-{DateTime.Now:yyyyMMdd-HHmmss}.log"
            };
            if (dialog.ShowDialog() == true)
            {
                var files = _logger.GetLogFiles();
                if (files.Count > 0)
                {
                    var content = _logger.ReadLogFile(files[^1]) ?? string.Empty;
                    File.WriteAllText(dialog.FileName, content);
                }
            }
        }

        private void ClearLog()
        {
            _logger.ClearAll();
            State.LogContent = string.Empty;
        }

        private void OnEngineProgress(object? sender, UsbCopyProgressArgs e)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastUiUpdateTime).TotalMilliseconds < 100)
            {
                return;
            }

            _lastUiUpdateTime = now;
            RunOnUi(() =>
            {
                State.LiveMiBS = e.BytesPerSecond / (1024.0 * 1024.0);
                State.AverageMiBS = e.AverageBytesPerSecond / (1024.0 * 1024.0);
                State.PeakMiBS = e.PeakBytesPerSecond / (1024.0 * 1024.0);
                if (e.TotalBytes > 0)
                {
                    State.Progress = (double)e.TotalBytesCopied / e.TotalBytes * 100.0;
                }

                var remaining = e.TotalBytes - e.TotalBytesCopied;
                if (e.AverageBytesPerSecond > 1)
                {
                    var eta = TimeSpan.FromSeconds(remaining / e.AverageBytesPerSecond);
                    State.EtaText = eta.ToString(@"hh\:mm\:ss");
                }
            });
        }

        private void OnPipelineEvent(object? sender, string stage)
        {
            RunOnUi(() => State.PipelineStatus = stage);
        }

        private void OnEngineError(object? sender, string message)
        {
            RunOnUi(() => State.LastError = message);
        }

        private void OnBenchmarkProgress(object? sender, UsbBenchmarkProgressArgs e)
        {
            RunOnUi(() => State.LiveMiBS = e.CurrentMiBS);
        }

        private void OnBenchmarkError(object? sender, string message)
        {
            RunOnUi(() => State.LastError = message);
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = WpfApplication.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.BeginInvoke(action);
        }
    }
}