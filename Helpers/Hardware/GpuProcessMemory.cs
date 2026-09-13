using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Reads PER-PROCESS dedicated GPU memory (VRAM) usage from the Windows
/// "GPU Process Memory" performance-counter category.
///
/// This is a strong, evidence-based game signal. A shell/desktop process
/// (explorer, Command Palette, PowerToys) holds only a few tens to a couple of
/// hundred MB of dedicated VRAM, while a real game holds hundreds of MB up to
/// several GB. It therefore corroborates (or replaces) the weak DxgKrnl kernel
/// present signal that previously allowed explorer.exe to be classified as a
/// game — without needing an app-name blocklist.
///
/// The category is only present when a WDDM GPU driver is installed. When it is
/// unavailable every call returns null so callers can fall back.
/// </summary>
internal static class GpuProcessMemory
{
    private const string CategoryName = "GPU Process Memory";
    private const string CounterName = "Dedicated Usage";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, PerformanceCounter> Counters =
        new(StringComparer.OrdinalIgnoreCase);

    private static DateTime _nextRefreshUtc = DateTime.MinValue;
    private static bool _categoryUnavailable;

    /// <summary>
    /// Total dedicated VRAM (bytes) currently held by the process across all of
    /// its GPU instances. Returns 0 when the process holds none, and null when the
    /// counter category is unavailable (so callers can fall back to other signals).
    /// </summary>
    public static long? TryGetDedicatedUsageBytes(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            lock (Gate)
            {
                RefreshCountersIfDue();

                if (_categoryUnavailable)
                {
                    return null;
                }

                var prefix = $"pid_{pid}_";
                var found = false;
                long total = 0;

                foreach (var entry in Counters)
                {
                    if (!entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var value = entry.Value.NextValue();
                        if (!float.IsNaN(value) && !float.IsInfinity(value) && value > 0)
                        {
                            total += (long)value;
                            found = true;
                        }
                        else
                        {
                            found = true;
                        }
                    }
                    catch
                    {
                        // Instance vanished between enumeration and read.
                    }
                }

                return found ? total : 0L;
            }
        }
        catch
        {
            return null;
        }
    }

    private static void RefreshCountersIfDue()
    {
        var now = DateTime.UtcNow;
        if (now < _nextRefreshUtc || _categoryUnavailable)
        {
            return;
        }

        _nextRefreshUtc = now.Add(RefreshInterval);

        try
        {
            if (!PerformanceCounterCategory.Exists(CategoryName))
            {
                _categoryUnavailable = true;
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
                    counter.NextValue();
                    Counters[instance] = counter;
                }
                catch
                {
                    // Skip instances that cannot be opened.
                }
            }

            foreach (var stale in Counters.Keys.Where(key => !seen.Contains(key)).ToList())
            {
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
        }
        catch
        {
            _categoryUnavailable = true;
        }
    }

    /// <summary>Formats a byte count for trace output (e.g. "812MB").</summary>
    public static string Format(long bytes)
    {
        const double mb = 1024.0 * 1024.0;
        const double gb = 1024.0 * 1024.0 * 1024.0;

        if (bytes >= gb)
        {
            return $"{bytes / gb:F2}GB";
        }

        return $"{bytes / mb:F0}MB";
    }
}
