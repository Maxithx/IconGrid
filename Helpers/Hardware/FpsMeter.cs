using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Stores and smooths FPS values received from external sources (PresentMon, etc.).
/// Does NOT estimate FPS from GPU usage - that approach was fundamentally incorrect.
/// 
/// Primary source: PresentMonFpsProvider (real Present() event counting via DxgKrnl ETW)
/// Fallback sources: PerformanceCounters, WMI, nvidia-smi (GPU utilization only as last resort)
/// </summary>
internal sealed class FpsMeter : IDisposable
{
    private const int PollIntervalMs = 2; // 500Hz - intentionally aggressive tuning for realtime feel testing
    private const double FpsDecayFactor = 0.85; // 85% decay when no new data (15% drop per tick)
    private const double LiveFpsDecayFactor = 0.55; // live FPS should fall faster when the feed goes stale
    private const double FpsThreshold = 0.5; // below this = "--"
    private const double DefaultResponsiveness = 1.0;

    private bool _disposed;
    private readonly System.Threading.Timer _pollTimer;
    private readonly Action<string>? _log;
    private readonly object _sync = new();
    private readonly double _emaAlpha;
    private bool _initialized;
    private bool _initAttempted; // prevent infinite init retries
    private double _smoothedFps;
    private double _liveFps;
    private DateTime _lastFpsUpdate = DateTime.MinValue;

    // FPS value fed by external providers (PresentMon, ETW, etc.)
    private double _externalFps = -1;
    private DateTime _lastExternalFeed = DateTime.MinValue;
    private static readonly TimeSpan ExternalFeedTimeout = TimeSpan.FromSeconds(2);

    // Fallback: PerformanceCounters (GPU utilization - not FPS, but better than nothing)
    private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new(StringComparer.OrdinalIgnoreCase);

    // Fallback: WMI
    private bool _useWmi;
    private ManagementObjectSearcher? _wmiSearcher;
    private int _wmiFailCount;

    // Fallback: nvidia-smi
    private bool _useNvidiaSmi;
    private string? _nvidiaSmiPath;
    private int _nvidiaFailCount;

    public FpsMeter(double responsiveness = DefaultResponsiveness, Action<string>? log = null)
    {
        _log = log;
        _emaAlpha = Math.Max(0.15, Math.Min(1.0, responsiveness));
        _pollTimer = new System.Threading.Timer(PollTimerCallback, null, 20, PollIntervalMs);
    }

    /// <summary>
    /// Called by HardwareMonitorAgent to set the real FPS value from PresentMon.
    /// This is the PRIMARY data source for FPS.
    /// </summary>
    public void SetFps(double fps)
    {
        lock (_sync)
        {
            _externalFps = fps;
            _lastExternalFeed = DateTime.UtcNow;
            _liveFps = fps;
            _smoothedFps = (_smoothedFps * (1.0 - _emaAlpha)) + (fps * _emaAlpha);
            _lastFpsUpdate = _lastExternalFeed;
        }
    }

    /// <summary>
    /// Legacy - kept for compatibility. Not used for FPS estimation anymore.
    /// GPU utilization cannot estimate FPS reliably.
    /// </summary>
    public void SetGpuUsage(double percent)
    {
        // Not used for FPS estimation - GPU% has no fixed relationship to FPS
    }

    public double GetCurrentFps()
    {
        lock (_sync) { return _smoothedFps; }
    }

    public string GetCurrentFpsFormatted()
    {
        lock (_sync)
        {
            if (_smoothedFps <= FpsThreshold) return "--";
            return Math.Round(_smoothedFps).ToString("F0");
        }
    }

    public FpsSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new FpsSnapshot(
                _liveFps,
                _smoothedFps,
                FormatFps(_liveFps),
                FormatFps(_smoothedFps));
        }
    }

    public bool IsInitialized
    {
        get { lock (_sync) return _initialized; }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _pollTimer.Dispose();

        foreach (var kvp in _gpuCounters)
        {
            try { kvp.Value.Dispose(); } catch { }
        }
        _gpuCounters.Clear();
        _wmiSearcher?.Dispose();
    }

    private bool Initialize()
    {
        try
        {
            _log?.Invoke("[FpsMeter] Initializing (primary source: PresentMon, fallbacks: PerfCounters/WMI/nvidia-smi)...");

            // Initialize fallbacks only
            InitFallbackSources();

            _log?.Invoke($"[FpsMeter] Initialized. Fallbacks: {(_gpuCounters.Count > 0 ? "PerfCounter " : "")}{(_useWmi ? "WMI " : "")}{(_useNvidiaSmi ? "nvidia-smi" : "")}");
            _initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[FpsMeter] Init error: {ex.Message}");
            return false;
        }
    }

    private void InitFallbackSources()
    {
        InitPerformanceCounters();
        InitWmi();
        InitNvidiaSmi();
    }

    private void InitPerformanceCounters()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();

            foreach (var instance in instances)
            {
                if (!instance.Contains("eng_3d", StringComparison.OrdinalIgnoreCase) &&
                    !instance.Contains("3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                    counter.NextValue();
                    _gpuCounters[instance] = counter;
                }
                catch { }
            }
        }
        catch { }
    }

    private void InitWmi()
    {
        try
        {
            var testQuery = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            int count = 0;
            foreach (ManagementObject obj in testQuery.Get()) count++;
            if (count > 0)
            {
                _wmiSearcher = new ManagementObjectSearcher(
                    "SELECT Name, PercentOfTimeDelta FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%3D%'");
                _useWmi = true;
            }
        }
        catch { }
    }

    private void InitNvidiaSmi()
    {
        try
        {
            var paths = new[]
            {
                @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
                @"C:\Windows\System32\nvidia-smi.exe",
                @"nvidia-smi.exe"
            };
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    _nvidiaSmiPath = path;
                    _useNvidiaSmi = true;
                    return;
                }
            }
        }
        catch { }
    }

    private double PollWmiGpuUtilization()
    {
        try
        {
            if (_wmiSearcher == null) return -1;
            double maxUtil = -1;
            foreach (ManagementObject obj in _wmiSearcher.Get())
            {
                try
                {
                    var val = Convert.ToDouble(obj["PercentOfTimeDelta"]);
                    if (val > maxUtil) maxUtil = val;
                }
                catch { }
            }
            return maxUtil;
        }
        catch
        {
            _wmiFailCount++;
            if (_wmiFailCount > 5) _useWmi = false;
            return -1;
        }
    }

    private double PollNvidiaSmi()
    {
        try
        {
            if (_nvidiaSmiPath == null) return -1;
            var psi = new ProcessStartInfo
            {
                FileName = _nvidiaSmiPath,
                Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return -1;
            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(3000)) return -1;
            output = output?.Trim();
            if (string.IsNullOrEmpty(output)) return -1;
            if (double.TryParse(output, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                return val;
            return -1;
        }
        catch
        {
            _nvidiaFailCount++;
            if (_nvidiaFailCount > 3) _useNvidiaSmi = false;
            return -1;
        }
    }

    /// <summary>
    /// Called at 50Hz. Checks if external FPS data is fresh, otherwise decays.
    /// </summary>
    private void PollTimerCallback(object? state)
    {
        if (_disposed) return;
        if (!_initialized && !_initAttempted) { _initAttempted = true; Initialize(); return; }
        if (!_initialized) return; // init failed, don't retry

        try
        {
            lock (_sync)
            {
                // TIER 1: External FPS feed from PresentMon (PRIMARY)
                if (_externalFps >= 0 && (DateTime.UtcNow - _lastExternalFeed) <= ExternalFeedTimeout)
                {
                    return;
                }

                // TIER 2: If external feed is stale, decay current FPS to "--"
                // This handles the case where the game closes
                if (_liveFps > FpsThreshold)
                {
                    _liveFps *= LiveFpsDecayFactor;
                    if (_liveFps < FpsThreshold) _liveFps = 0;
                }

                if (_smoothedFps > FpsThreshold)
                {
                    _smoothedFps *= FpsDecayFactor;
                    if (_smoothedFps < FpsThreshold) _smoothedFps = 0;
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[FpsMeter] Error: {ex.Message}");
        }
    }

    private static string FormatFps(double fps)
    {
        if (fps <= FpsThreshold)
        {
            return "--";
        }

        return Math.Round(fps).ToString("F0");
    }
}

internal readonly record struct FpsSnapshot(
    double LiveFps,
    double SmoothedFps,
    string LiveFpsFormatted,
    string SmoothedFpsFormatted);
