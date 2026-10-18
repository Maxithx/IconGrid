using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Phase 0 game-detection telemetry. Writes ONE self-describing line per
/// candidate/target so the classifier can be calibrated from real measurements
/// instead of guessed thresholds. This class is deliberately read-only with
/// respect to detection: it never changes what is classified as a game.
///
/// The line captures the structural signals that later phases will use:
///   - dedicated VRAM (current, peak, recent 95th percentile),
///   - GPU engine distribution (3D / Compute / Copy / VideoDecode / ...),
///   - present evidence reported by the native ETW agent for this PID,
///   - how long the user has been idle (games present input-independently),
///   - the largest visible window and how much of its monitor it covers.
/// </summary>
internal static class GameEvidenceProbe
{
    private const string Marker = "GameEvidenceProbe";

    private static readonly TimeSpan TopVramScanInterval = TimeSpan.FromSeconds(10);
    private static readonly object ScanGate = new();
    private static DateTime _nextTopVramScanUtc = DateTime.MinValue;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private const uint MonitorDefaultToNearest = 2;

    /// <summary>
    /// Builds the full phase-0 evidence line for one PID. Safe to call for any
    /// process: every measurement degrades to "n/a" instead of throwing.
    /// </summary>
    public static string BuildLine(
        int pid,
        NativeFpsAgentState? nativeState,
        DateTime nowUtc,
        long? knownVramBytes = null)
    {
        var name = ResolveProcessName(pid, nativeState);

        var vram = knownVramBytes ?? GpuProcessMemory.TryGetDedicatedUsageBytes(pid);
        GameVramEvidence.RecordSample(pid, vram, nowUtc);
        var peak = GameVramEvidence.GetPeakBytes(pid, nowUtc);
        var recent = GameVramEvidence.GetRecentPercentileBytes(pid, nowUtc);

        var parts = new List<string>
        {
            $"pid={pid}",
            $"name={Sanitize(name)}",
            $"vram={(vram.HasValue ? GpuProcessMemory.Format(vram.Value) : "n/a")}",
            $"peak={(peak.HasValue ? GpuProcessMemory.Format(peak.Value) : "n/a")}",
            $"p95={(recent.HasValue ? GpuProcessMemory.Format(recent.Value) : "n/a")}"
        };

        if (GpuEngineActivity.TryGet(pid, out var engines))
        {
            parts.Add($"3D={GpuEngineActivity.FormatPercent(engines.ThreeDPercent)}%");
            parts.Add($"Compute={GpuEngineActivity.FormatPercent(engines.ComputePercent)}%");
            parts.Add($"Copy={GpuEngineActivity.FormatPercent(engines.CopyPercent)}%");
            parts.Add($"VideoDec={GpuEngineActivity.FormatPercent(engines.VideoDecodePercent)}%");
            parts.Add($"VideoEnc={GpuEngineActivity.FormatPercent(engines.VideoEncodePercent)}%");
            parts.Add($"Other={GpuEngineActivity.FormatPercent(engines.OtherPercent)}%");
            parts.Add($"3Dshare={GpuEngineActivity.FormatPercent(engines.ThreeDShare * 100)}%");
            parts.Add($"ComputeShare={GpuEngineActivity.FormatPercent(engines.ComputeShare * 100)}%");
            parts.Add($"engines={engines.InstanceCount}");
        }
        else
        {
            parts.Add("engines=n/a");
        }

        if (nativeState != null && nativeState.TargetPid == pid)
        {
            parts.Add($"presentFps={GpuEngineActivity.FormatPercent(nativeState.FpsValue ?? 0)}");
            parts.Add($"dxgi={nativeState.MatchedDxgiEventCount}");
            parts.Add($"d3d9={nativeState.MatchedD3D9EventCount}");
            parts.Add($"dxgkrnl={nativeState.MatchedDxgKrnlEventCount}");
        }
        else
        {
            parts.Add("presentFps=--");
        }

        var idle = SystemInputIdle.TryGetSeconds();
        parts.Add($"idle={(idle.HasValue
            ? idle.Value.ToString("F1", CultureInfo.InvariantCulture) + "s"
            : "n/a")}");

        if (TryGetLargestWindowInfo(pid, out var width, out var height, out var coverage))
        {
            parts.Add($"win={width}x{height}");
            parts.Add($"cover={coverage}%");
        }
        else
        {
            parts.Add("win=--");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Compact subset for the existing per-snapshot trace line.
    /// </summary>
    public static string BuildCompact(int pid, NativeFpsAgentState? nativeState, DateTime nowUtc)
    {
        if (pid <= 0)
        {
            return string.Empty;
        }

        var engineText = GpuEngineActivity.TryGet(pid, out var engines)
            ? $"3D={GpuEngineActivity.FormatPercent(engines.ThreeDPercent)}% Compute={GpuEngineActivity.FormatPercent(engines.ComputePercent)}%"
            : "3D=n/a Compute=n/a";

        var idle = SystemInputIdle.TryGetSeconds();
        var idleText = idle.HasValue
            ? idle.Value.ToString("F1", CultureInfo.InvariantCulture) + "s"
            : "n/a";

        var p95 = GameVramEvidence.GetRecentPercentileBytes(pid, nowUtc);
        var p95Text = p95.HasValue ? GpuProcessMemory.Format(p95.Value) : "n/a";

        return $"{engineText} Idle={idleText} VramP95={p95Text}";
    }

    /// <summary>Logs a full evidence line for a candidate the pipeline considered.</summary>
    public static void LogCandidate(
        string source,
        int pid,
        NativeFpsAgentState? nativeState,
        Action<string>? log,
        DateTime? nowUtc = null)
    {
        if (log == null || pid <= 0)
        {
            return;
        }

        try
        {
            log($"{Marker}[{source}] {BuildLine(pid, nativeState, nowUtc ?? DateTime.UtcNow)}");
        }
        catch (Exception ex)
        {
            log($"{Marker}[{source}] pid={pid} probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Throttled background scan of the processes holding the most dedicated VRAM.
    /// This is what captures GPU compute workloads (LLM runtimes, PyTorch/SAM/DINO)
    /// that have no game-sized window and therefore never reach the window paths.
    /// </summary>
    public static void MaybeLogTopVramProcesses(Action<string>? log, DateTime nowUtc, int limit = 4)
    {
        if (log == null)
        {
            return;
        }

        lock (ScanGate)
        {
            if (nowUtc < _nextTopVramScanUtc)
            {
                return;
            }

            _nextTopVramScanUtc = nowUtc.Add(TopVramScanInterval);
        }

        try
        {
            foreach (var (pid, bytes) in GpuProcessMemory.GetTopDedicatedUsage(limit))
            {
                if (bytes < GameVramEvidence.NotGameBytes)
                {
                    continue;
                }

                log($"{Marker}[topvram] {BuildLine(pid, null, nowUtc, bytes)}");
            }
        }
        catch (Exception ex)
        {
            log($"{Marker}[topvram] scan failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string ResolveProcessName(int pid, NativeFpsAgentState? nativeState)
    {
        if (nativeState != null &&
            nativeState.TargetPid == pid &&
            !string.IsNullOrWhiteSpace(nativeState.TargetProcessName))
        {
            return nativeState.TargetProcessName!;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return "?";
        }
    }

    private static string Sanitize(string value)
        => string.IsNullOrWhiteSpace(value) ? "?" : value.Replace(' ', '_');

    private static bool TryGetLargestWindowInfo(int pid, out int width, out int height, out int coveragePercent)
    {
        width = 0;
        height = 0;
        coveragePercent = 0;

        var bestArea = 0L;
        var bestWidth = 0;
        var bestHeight = 0;
        var bestCoverage = 0;

        try
        {
            EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(hwnd, out var windowPid);
                    if ((int)windowPid != pid || !GetWindowRect(hwnd, out var rect))
                    {
                        return true;
                    }

                    var windowWidth = Math.Max(0, rect.Right - rect.Left);
                    var windowHeight = Math.Max(0, rect.Bottom - rect.Top);
                    var area = (long)windowWidth * windowHeight;
                    if (area <= bestArea)
                    {
                        return true;
                    }

                    bestArea = area;
                    bestWidth = windowWidth;
                    bestHeight = windowHeight;
                    bestCoverage = ComputeMonitorCoverage(hwnd, area);
                }
                catch
                {
                    // Never let one window abort the enumeration.
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            return false;
        }

        if (bestArea <= 0)
        {
            return false;
        }

        width = bestWidth;
        height = bestHeight;
        coveragePercent = bestCoverage;
        return true;
    }

    private static int ComputeMonitorCoverage(IntPtr hwnd, long windowArea)
    {
        try
        {
            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return 0;
            }

            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return 0;
            }

            var monitorArea = Math.Max(0L,
                (long)Math.Max(0, info.Monitor.Right - info.Monitor.Left) *
                Math.Max(0, info.Monitor.Bottom - info.Monitor.Top));

            return monitorArea <= 0
                ? 0
                : (int)Math.Round(Math.Clamp(windowArea / (double)monitorArea, 0.0, 1.0) * 100);
        }
        catch
        {
            return 0;
        }
    }
}
