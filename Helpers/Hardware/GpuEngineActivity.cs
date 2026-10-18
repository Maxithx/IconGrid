using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Per-process GPU engine utilization split by engine class, read from the
/// Windows "GPU Engine" performance-counter category.
///
/// This is the structural signal that separates a renderer from other GPU work
/// without any process-name list:
///   - a game renders on the 3D engine,
///   - an AI/ML workload (CUDA, DirectML, ONNX Runtime) runs on the Compute
///     engine and does not present frames,
///   - video playback runs on VideoDecode,
///   - desktop composition presents through DWM, not through the app's 3D engine.
///
/// Phase 0: telemetry only. The values are logged for calibration; nothing in
/// the game classification reads them yet.
/// </summary>
internal static class GpuEngineActivity
{
    private const string CategoryName = "GPU Engine";
    private const string CounterName = "Utilization Percentage";

    /// <summary>
    /// The counter instances change as processes start and exit, so the list is
    /// re-enumerated on this interval. Values are read from cached counters in
    /// between, which is also required because a performance counter needs two
    /// samples to produce a rate.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, PerformanceCounter> Counters =
        new(StringComparer.OrdinalIgnoreCase);

    private static DateTime _nextRefreshUtc = DateTime.MinValue;
    private static bool _categoryUnavailable;

    /// <summary>
    /// Why the counter category is currently unavailable, or null when it works.
    /// Surfaced in trace output so a broken provider is visible instead of
    /// silently turning every measurement into "unknown".
    /// </summary>
    public static string? LastUnavailableReason { get; private set; }

    /// <summary>
    /// Utilization percentages for one process. Percentages are summed per engine
    /// class across adapters/instances, so a value can exceed 100 when a process
    /// uses several adapters. The shares are computed from those sums and are
    /// therefore always in the 0-1 range.
    /// </summary>
    public readonly record struct Sample(
        double ThreeDPercent,
        double ComputePercent,
        double CopyPercent,
        double VideoDecodePercent,
        double VideoEncodePercent,
        double SecurityPercent,
        double OtherPercent,
        int InstanceCount)
    {
        public double TotalPercent =>
            ThreeDPercent + ComputePercent + CopyPercent + VideoDecodePercent +
            VideoEncodePercent + SecurityPercent + OtherPercent;

        public double ThreeDShare => Share(ThreeDPercent);

        public double ComputeShare => Share(ComputePercent);

        public double VideoDecodeShare => Share(VideoDecodePercent);

        private double Share(double value)
        {
            var total = TotalPercent;
            return total <= 0 ? 0 : value / total;
        }
    }

    /// <summary>
    /// Reads the current engine distribution for a process. Returns false when the
    /// counter category is unavailable or the process has no GPU engine instance
    /// at all (which is meaningful: it is not doing any GPU work).
    /// </summary>
    public static bool TryGet(int pid, out Sample sample)
    {
        sample = default;
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            lock (Gate)
            {
                RefreshCountersIfDue();

                if (_categoryUnavailable)
                {
                    return false;
                }

                var prefix = $"pid_{pid}_";
                double threeD = 0, compute = 0, copy = 0, videoDecode = 0, videoEncode = 0;
                double security = 0, other = 0;
                var instances = 0;

                foreach (var entry in Counters)
                {
                    if (!entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    double value;
                    try
                    {
                        value = entry.Value.NextValue();
                    }
                    catch
                    {
                        // Instance vanished between enumeration and read.
                        continue;
                    }

                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    {
                        value = 0;
                    }

                    instances++;
                    switch (GetEngineClass(entry.Key))
                    {
                        case EngineClass.ThreeD:
                            threeD += value;
                            break;
                        case EngineClass.Compute:
                            compute += value;
                            break;
                        case EngineClass.Copy:
                            copy += value;
                            break;
                        case EngineClass.VideoDecode:
                            videoDecode += value;
                            break;
                        case EngineClass.VideoEncode:
                            videoEncode += value;
                            break;
                        case EngineClass.Security:
                            security += value;
                            break;
                        default:
                            other += value;
                            break;
                    }
                }

                if (instances == 0)
                {
                    return false;
                }

                sample = new Sample(
                    threeD,
                    compute,
                    copy,
                    videoDecode,
                    videoEncode,
                    security,
                    other,
                    instances);
                return true;
            }
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex.Message);
            return false;
        }
    }

    private enum EngineClass
    {
        Other,
        ThreeD,
        Compute,
        Copy,
        VideoDecode,
        VideoEncode,
        Security
    }

    private static EngineClass GetEngineClass(string instanceName)
    {
        const string marker = "engtype_";
        var index = instanceName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return EngineClass.Other;
        }

        var type = instanceName.Substring(index + marker.Length);
        if (type.StartsWith("3D", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.ThreeD;
        }

        if (type.StartsWith("Compute", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.Compute;
        }

        if (type.StartsWith("Copy", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.Copy;
        }

        if (type.StartsWith("VideoDecode", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.VideoDecode;
        }

        if (type.StartsWith("VideoEncode", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.VideoEncode;
        }

        if (type.StartsWith("Security", StringComparison.OrdinalIgnoreCase))
        {
            return EngineClass.Security;
        }

        return EngineClass.Other;
    }

    private static void RefreshCountersIfDue()
    {
        var now = DateTime.UtcNow;
        if (now < _nextRefreshUtc)
        {
            return;
        }

        _nextRefreshUtc = now.Add(RefreshInterval);

        try
        {
            if (!PerformanceCounterCategory.Exists(CategoryName))
            {
                MarkUnavailable($"counter category '{CategoryName}' is not present");
                return;
            }

            var category = new PerformanceCounterCategory(CategoryName);
            var instances = category.GetInstanceNames();
            var seen = new HashSet<string>(instances, StringComparer.OrdinalIgnoreCase);

            foreach (var instance in instances)
            {
                if (Counters.ContainsKey(instance))
                {
                    continue;
                }

                try
                {
                    var counter = new PerformanceCounter(CategoryName, CounterName, instance, readOnly: true);
                    // Prime the counter: the first NextValue call always returns 0.
                    counter.NextValue();
                    Counters[instance] = counter;
                }
                catch
                {
                    // Skip instances that cannot be opened.
                }
            }

            foreach (var stale in new List<string>(Counters.Keys))
            {
                if (seen.Contains(stale))
                {
                    continue;
                }

                try
                {
                    Counters[stale].Dispose();
                }
                catch
                {
                    // ignore
                }

                Counters.Remove(stale);
            }

            _categoryUnavailable = false;
            LastUnavailableReason = null;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex.Message);
        }
    }

    private static void MarkUnavailable(string reason)
    {
        _categoryUnavailable = true;
        LastUnavailableReason = reason;
    }

    /// <summary>Formats a percentage for the single-line trace probe.</summary>
    public static string FormatPercent(double value)
        => value.ToString("F1", CultureInfo.InvariantCulture);
}

