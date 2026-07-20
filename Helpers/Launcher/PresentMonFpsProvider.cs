using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IconGrid.Helpers
{
    internal sealed class PresentMonFpsProvider : IDisposable
    {
        private const int PollIntervalMs = 1000;
        private const int SampleWindowSize = 20;
        private const string IgnoredProcessName = "IconGrid";
        private readonly System.Threading.Timer _pollTimer;
        private readonly object _sync = new();
        private readonly Queue<double> _recentFpsSamples = new();
        private readonly int _currentProcessId = Environment.ProcessId;
        private readonly Action<string>? _log;

        private Process? _presentMonProcess;
        private string? _presentMonPath;
        private string? _currentTargetProcessName;
        private int? _currentTargetProcessId;
        private int _frameDeltaColumnIndex = -1;
        private string _currentFpsStatus = "--";
        private bool _disposed;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public PresentMonFpsProvider(Action<string>? log = null)
        {
            _log = log;
            _pollTimer = new System.Threading.Timer(PollActiveProcess, null, PollIntervalMs, PollIntervalMs);
        }

        public string GetCurrentFpsStatus()
        {
            lock (_sync)
            {
                return _currentFpsStatus;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _pollTimer.Dispose();
            StopPresentMonProcess();
        }

        private void PollActiveProcess(object? state)
        {
            try
            {
                if (_disposed)
                {
                    return;
                }

                var target = TryGetForegroundTarget();
                if (target == null)
                {
                    return;
                }

                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (_currentTargetProcessId == target.Value.ProcessId)
                    {
                        return;
                    }

                    EnsurePresentMonPath();
                    if (string.IsNullOrWhiteSpace(_presentMonPath))
                    {
                        Trace("PresentMon path could not be resolved.");
                        SetFpsStatus("--");
                        return;
                    }

                    StartPresentMonProcess(target.Value.ProcessId, target.Value.ProcessName);
                }
            }
            catch
            {
                // Passive telemetry only; ignore FPS provider failures.
            }
        }

        private (int ProcessId, string ProcessName)? TryGetForegroundTarget()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return null;
                }

                GetWindowThreadProcessId(hwnd, out var processId);
                if (processId == 0 || processId == _currentProcessId)
                {
                    return null;
                }

                using var process = Process.GetProcessById((int)processId);
                if (process.HasExited ||
                    string.IsNullOrWhiteSpace(process.ProcessName) ||
                    string.Equals(process.ProcessName, IgnoredProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return ((int)processId, process.ProcessName);
            }
            catch
            {
                return null;
            }
        }

        private void EnsurePresentMonPath()
        {
            if (!string.IsNullOrWhiteSpace(_presentMonPath) && File.Exists(_presentMonPath))
            {
                return;
            }

            _presentMonPath = TryFindPresentMonExecutable();
        }

        private static string? TryFindPresentMonExecutable()
        {
            var localToolPath = TryFindBundledPresentMon();
            if (!string.IsNullOrWhiteSpace(localToolPath))
            {
                return localToolPath;
            }

            foreach (var candidate in new[]
                     {
                         "PresentMon.exe",
                         "PresentMon-2.5.1-x64.exe",
                         "PresentMon-2.5.0-x64.exe",
                         "PresentMon-2.4.0-x64.exe",
                         "PresentMon-2.3.1-x64.exe"
                     })
            {
                var resolved = TryResolveExecutableFromPath(candidate);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            var downloadsCandidate = TryFindPresentMonInDownloads();
            if (!string.IsNullOrWhiteSpace(downloadsCandidate))
            {
                return downloadsCandidate;
            }

            return null;
        }

        private static string? TryFindBundledPresentMon()
        {
            try
            {
                var baseDirectory = AppContext.BaseDirectory;
                if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
                {
                    return null;
                }

                return Directory.EnumerateFiles(
                        Path.Combine(baseDirectory, "Tools", "PresentMon"),
                        "PresentMon-*-x64.exe",
                        SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string? TryFindPresentMonInDownloads()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfile))
                {
                    return null;
                }

                var downloadsPath = Path.Combine(userProfile, "Downloads");
                if (!Directory.Exists(downloadsPath))
                {
                    return null;
                }

                return Directory.EnumerateFiles(downloadsPath, "PresentMon-*-x64.exe", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string? TryResolveExecutableFromPath(string executableName)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = executableName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                var path = output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(File.Exists);

                return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
            }
            catch
            {
                return null;
            }
        }

        private void StartPresentMonProcess(int processId, string processName)
        {
            StopPresentMonProcess();

            _currentTargetProcessId = processId;
            _currentTargetProcessName = processName;
            _frameDeltaColumnIndex = -1;
            _recentFpsSamples.Clear();
            SetFpsStatus("--");

            var sessionName = $"IconGrid_{_currentProcessId}_{processId}";
            Trace($"Starting PresentMon for PID={processId} Name={processName} Path={_presentMonPath} Session={sessionName}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _presentMonPath!,
                    Arguments = $"--process_id {processId} --output_stdout --no_console_stats --terminate_on_proc_exit --v1_metrics --session_name {sessionName} --stop_existing_session",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            process.Exited += PresentMonProcess_Exited;
            process.OutputDataReceived += PresentMonProcess_OutputDataReceived;
            process.ErrorDataReceived += PresentMonProcess_ErrorDataReceived;

            if (!process.Start())
            {
                Trace($"PresentMon failed to start for PID={processId}.");
                SetFpsStatus("--");
                return;
            }

            _presentMonProcess = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private void PresentMonProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            try
            {
                if (_frameDeltaColumnIndex < 0)
                {
                    _frameDeltaColumnIndex = ResolveFrameDeltaColumnIndex(e.Data);
                    Trace($"PresentMon header: {e.Data}");
                    Trace($"Resolved PresentMon frame delta column index: {_frameDeltaColumnIndex}");
                    return;
                }

                var columns = SplitCsvLine(e.Data);
                if (_frameDeltaColumnIndex >= columns.Count)
                {
                    return;
                }

                if (!double.TryParse(columns[_frameDeltaColumnIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var frameDeltaMs))
                {
                    return;
                }

                if (frameDeltaMs <= 0.0001)
                {
                    return;
                }

                var fps = 1000.0 / frameDeltaMs;
                if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0)
                {
                    return;
                }

                lock (_sync)
                {
                    _recentFpsSamples.Enqueue(fps);
                    while (_recentFpsSamples.Count > SampleWindowSize)
                    {
                        _recentFpsSamples.Dequeue();
                    }

                    var smoothedFps = _recentFpsSamples.Average();
                    _currentFpsStatus = $"{Math.Round(smoothedFps):F0}";
                }
            }
            catch
            {
                // Passive telemetry only; ignore malformed rows.
            }
        }

        private void PresentMonProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            Trace($"PresentMon stderr: {e.Data}");

            if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                SetFpsStatus("--");
            }
        }

        private void PresentMonProcess_Exited(object? sender, EventArgs e)
        {
            Trace($"PresentMon exited for PID={_currentTargetProcessId?.ToString() ?? "null"} Name={_currentTargetProcessName ?? "null"}");
            lock (_sync)
            {
                _currentTargetProcessId = null;
                _currentTargetProcessName = null;
                _frameDeltaColumnIndex = -1;
                _recentFpsSamples.Clear();
                _currentFpsStatus = "--";
            }
        }

        private void StopPresentMonProcess()
        {
            Process? process;

            lock (_sync)
            {
                process = _presentMonProcess;
                _presentMonProcess = null;
                _currentTargetProcessId = null;
                _currentTargetProcessName = null;
                _frameDeltaColumnIndex = -1;
                _recentFpsSamples.Clear();
                _currentFpsStatus = "--";
            }

            if (process == null)
            {
                return;
            }

            try
            {
                Trace("Stopping PresentMon process.");
                process.OutputDataReceived -= PresentMonProcess_OutputDataReceived;
                process.ErrorDataReceived -= PresentMonProcess_ErrorDataReceived;
                process.Exited -= PresentMonProcess_Exited;

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1000);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        private static int ResolveFrameDeltaColumnIndex(string headerLine)
        {
            var columns = SplitCsvLine(headerLine);
            for (var i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i], "MsBetweenDisplayChange", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(columns[i], "MsBetweenPresents", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            foreach (var ch in line)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            values.Add(current.ToString());
            return values;
        }

        private void SetFpsStatus(string status)
        {
            lock (_sync)
            {
                _currentFpsStatus = status;
            }
        }

        private void Trace(string message)
        {
            _log?.Invoke($"[PresentMonFpsProvider] {message}");
        }
    }
}
