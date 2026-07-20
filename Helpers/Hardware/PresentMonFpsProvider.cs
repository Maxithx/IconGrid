using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Measures real FPS by running PresentMon as a subprocess.
/// PresentMon hooks into the DxgKrnl ETW provider (kernel-level) to capture
/// actual frame presents from any DirectX/Vulkan game.
/// 
/// Uses PresentMon v1 metrics format for correct FPS:
/// - v1 metrics = one CSV row per DISPLAYED frame
/// - --exclude_dropped = only count frames actually shown on screen
/// - --terminate_existing_session = force-kill orphan ETW sessions (fixes error 1450)
/// - Unique session name to avoid conflicts with other ETW tools
/// </summary>
internal sealed class PresentMonFpsProvider : IDisposable
{
    private const string PresentMonExe = "PresentMon-2.5.1-x64.exe";
    private const int FpsWindowMs = 1000;

    private readonly Action<string>? _log;
    private readonly string? _presentMonPath;
    private readonly object _sync = new();
    private readonly List<string> _targetProcessNames;
    private readonly Queue<double> _recentFpsSamples = new();

    private Process? _presentMonProcess;
    private Thread? _readerThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _frameDeltaColumnIndex = -1;
    private volatile int _currentFps;
    private volatile bool _isRunning;
    private volatile bool _isDead; // PresentMon exited (error 1450 or other)

    private static readonly HashSet<string> KnownGameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pathofexile_x64.exe", "pathofexile_x64steam.exe", "pathofexile_x64egs.exe",
        "pathofexile.exe", "pathofexilesteam.exe",
        "yuzu.exe", "ryujinx.exe", "rpcs3.exe"
    };

    public PresentMonFpsProvider(IEnumerable<string>? additionalProcessNames = null, Action<string>? log = null)
    {
        _log = log;
        _targetProcessNames = new List<string>(KnownGameProcesses);
        if (additionalProcessNames != null)
        {
            foreach (var name in additionalProcessNames)
            {
                var clean = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : name + ".exe";
                if (!_targetProcessNames.Contains(clean, StringComparer.OrdinalIgnoreCase))
                    _targetProcessNames.Add(clean);
            }
        }

        _presentMonPath = ResolvePresentMonPath();
        if (_presentMonPath == null)
        {
            _log?.Invoke("[PresentMon] executable not found!");
            return;
        }

        _log?.Invoke($"[PresentMon] Found at: {_presentMonPath}");
        _log?.Invoke($"[PresentMon] Tracking: {string.Join(", ", _targetProcessNames)}");
    }

    public bool IsRunning => _isRunning;
    public bool IsDead => _isDead;

    public int GetCurrentFps()
    {
        return _currentFps;
    }

    public string GetCurrentFpsFormatted()
    {
        var fps = _currentFps;
        return fps > 0 ? fps.ToString("F0") : "--";
    }

    public void Start()
    {
        if (_disposed) return;
        if (_presentMonPath == null) return;
        if (_isRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _isDead = false;
            _frameDeltaColumnIndex = -1;
            lock (_sync)
            {
                _recentFpsSamples.Clear();
                _currentFps = 0;
            }

            var sessionName = $"IconGrid_{Environment.ProcessId}_{DateTime.UtcNow.Ticks}";
            var processNames = string.Join("\" --process_name \"", _targetProcessNames);
            var args = $"--output_stdout --no_console_stats --session_name {sessionName} --stop_existing_session --terminate_on_proc_exit --process_name \"{processNames}\" --v1_metrics --exclude_dropped --no_track_gpu --no_track_input";

            var psi = new ProcessStartInfo
            {
                FileName = _presentMonPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _log?.Invoke($"[PresentMon] Starting: {psi.FileName} {psi.Arguments}");

            _presentMonProcess = new Process { StartInfo = psi };
            _presentMonProcess.Start();

            _isRunning = true;

            _readerThread = new Thread(ReadOutputLoop)
            {
                IsBackground = true,
                Name = "PresentMonReader"
            };
            _readerThread.Start();

            Task.Run(() => ReadErrorStream(_presentMonProcess));

            _log?.Invoke("[PresentMon] Started successfully");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[PresentMon] Failed to start: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _cts?.Cancel();

        if (_presentMonProcess != null && !_presentMonProcess.HasExited)
        {
            try
            {
                _presentMonProcess.Kill(entireProcessTree: true);
                _presentMonProcess.WaitForExit(2000);
            }
            catch { }
        }

        _isRunning = false;
        _log?.Invoke("[PresentMon] Stopped");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }

    private void ReadOutputLoop()
    {
        var stream = _presentMonProcess?.StandardOutput;
        if (stream == null) return;

        try
        {
            string? line;
            var lastLogTime = DateTime.UtcNow;

            while (_cts?.IsCancellationRequested == false && (line = stream.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (_frameDeltaColumnIndex < 0)
                {
                    _frameDeltaColumnIndex = ResolveFrameDeltaColumnIndex(line);
                    _log?.Invoke($"[PresentMon] Header parsed. Frame delta column={_frameDeltaColumnIndex}");
                    continue;
                }

                if (!TryRecordFrame(line))
                    continue;

                var now = DateTime.UtcNow;
                if ((now - lastLogTime).TotalSeconds >= 1.0)
                {
                    _log?.Invoke($"[PresentMon] Current FPS={_currentFps}");
                    lastLogTime = now;
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) when (_cts?.IsCancellationRequested == true) { }
        catch (Exception ex)
        {
            if (_cts?.IsCancellationRequested != true)
                _log?.Invoke($"[PresentMon] Read error: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
            _isDead = true;
        }
    }

    private void RecordFrame()
    {
        lock (_sync)
        {
            _recentFpsSamples.Enqueue(_currentFps);
            while (_recentFpsSamples.Count > 20)
                _recentFpsSamples.Dequeue();
        }
    }

    private bool TryRecordFrame(string line)
    {
        if (_frameDeltaColumnIndex < 0)
            return false;

        var columns = SplitCsvLine(line);
        if (_frameDeltaColumnIndex >= columns.Count)
            return false;

        if (!double.TryParse(columns[_frameDeltaColumnIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var frameDeltaMs))
            return false;

        if (frameDeltaMs <= 0.0001d)
            return false;

        var fps = 1000.0 / frameDeltaMs;
        if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0)
            return false;

        lock (_sync)
        {
            _recentFpsSamples.Enqueue(fps);
            while (_recentFpsSamples.Count > 20)
                _recentFpsSamples.Dequeue();
            _currentFps = (int)Math.Round(_recentFpsSamples.Average());
        }

        return true;
    }

    private void ReadErrorStream(Process process)
    {
        try
        {
            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(error) && _cts?.IsCancellationRequested != true)
            {
                _log?.Invoke($"[PresentMon] stderr: {error.Truncate(500)}");
                if (error.Contains("1450", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("failed to start trace session", StringComparison.OrdinalIgnoreCase))
                {
                    _isDead = true;
                }
            }
        }
        catch { }
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

    private string? ResolvePresentMonPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "PresentMon", PresentMonExe),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PresentMonExe),
            Path.Combine(Environment.CurrentDirectory, "Tools", "PresentMon", PresentMonExe),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Tools", "PresentMon", PresentMonExe),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Tools", "PresentMon", PresentMonExe),
        };

        foreach (var path in candidates)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
