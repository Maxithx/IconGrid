using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IconGrid.Helpers.Hardware;

/// <summary>One recorded high GPU hotspot reading.</summary>
public sealed class HotspotHistoryEntry
{
    public DateTime CapturedAtUtc { get; set; }
    public double HotspotC { get; set; }
    public double EdgeC { get; set; }
    public double DeltaC { get; set; }
    public string? GameName { get; set; }
    public string? GpuClock { get; set; }
    public string? GpuMemoryClock { get; set; }
    public string? GpuMemoryUsage { get; set; }
    public string? GpuPower { get; set; }
    public string? GpuFan { get; set; }
    public string? GpuVoltage { get; set; }
}

/// <summary>
/// Persists the five highest hotspot readings, one per minute so the list shows
/// different points in time instead of five samples from the same second.
/// </summary>
public sealed class HotspotHistoryStore
{
    private const int MaxEntries = 5;
    private readonly string _path;
    private readonly List<HotspotHistoryEntry> _entries = new();

    private HotspotHistoryStore(string path)
    {
        _path = path;
    }

    public static HotspotHistoryStore Load(string baseDirectory)
    {
        var store = new HotspotHistoryStore(Path.Combine(baseDirectory, "hotspot-history.json"));
        try
        {
            if (File.Exists(store._path))
            {
                var entries = JsonSerializer.Deserialize<List<HotspotHistoryEntry>>(
                    File.ReadAllText(store._path));
                if (entries != null)
                {
                    store._entries.AddRange(entries
                        .Where(entry => entry.HotspotC > 0)
                        .OrderByDescending(entry => entry.HotspotC)
                        .Take(MaxEntries));

                    // Older history entries stored the process name (PathOfExile.exe)
                    // while newer entries use the FPS target's display name
                    // (PathOfExile). Normalize both so the table stays consistent.
                    foreach (var entry in store._entries)
                    {
                        entry.GameName = NormalizeGameName(entry.GameName);
                    }
                }
            }
        }
        catch
        {
            // A corrupt history file must never stop the monitor.
        }

        return store;
    }

    public IReadOnlyList<HotspotHistoryEntry> Top => _entries;

    /// <summary>
    /// Returns a stable display name for a game regardless of whether the caller
    /// captured a display name or a process name. This keeps mixed history entries
    /// such as "PathOfExile" and "PathOfExile.exe" as one game name.
    /// </summary>
    public static string? NormalizeGameName(string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return null;
        }

        var trimmed = gameName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    /// <summary>Removes every recorded reading and persists the empty list.</summary>
    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    /// <summary>
    /// Keeps at most one entry per minute and only replaces it when the new
    /// reading is hotter, so five different minutes are shown.
    /// </summary>
    public bool Record(
        DateTime capturedAtUtc,
        double hotspotC,
        double edgeC,
        double deltaC,
        string? gameName,
        string? gpuClock = null,
        string? gpuMemoryClock = null,
        string? gpuMemoryUsage = null,
        string? gpuPower = null,
        string? gpuFan = null,
        string? gpuVoltage = null)
    {
        if (hotspotC <= 0)
        {
            return false;
        }

        gameName = NormalizeGameName(gameName);

        var bucket = new DateTime(
            capturedAtUtc.Year,
            capturedAtUtc.Month,
            capturedAtUtc.Day,
            capturedAtUtc.Hour,
            capturedAtUtc.Minute,
            0,
            DateTimeKind.Utc);

        var existing = _entries.FirstOrDefault(entry =>
            new DateTime(
                entry.CapturedAtUtc.Year,
                entry.CapturedAtUtc.Month,
                entry.CapturedAtUtc.Day,
                entry.CapturedAtUtc.Hour,
                entry.CapturedAtUtc.Minute,
                0,
                DateTimeKind.Utc) == bucket);

        if (existing != null)
        {
            if (hotspotC <= existing.HotspotC)
            {
                return false;
            }

            existing.CapturedAtUtc = capturedAtUtc;
            existing.HotspotC = hotspotC;
            existing.EdgeC = edgeC;
            existing.DeltaC = deltaC;
            existing.GameName = gameName;
            existing.GpuClock = gpuClock;
            existing.GpuMemoryClock = gpuMemoryClock;
            existing.GpuMemoryUsage = gpuMemoryUsage;
            existing.GpuPower = gpuPower;
            existing.GpuFan = gpuFan;
            existing.GpuVoltage = gpuVoltage;
        }
        else
        {
            _entries.Add(new HotspotHistoryEntry
            {
                CapturedAtUtc = capturedAtUtc,
                HotspotC = hotspotC,
                EdgeC = edgeC,
                DeltaC = deltaC,
                GameName = gameName,
                GpuClock = gpuClock,
                GpuMemoryClock = gpuMemoryClock,
                GpuMemoryUsage = gpuMemoryUsage,
                GpuPower = gpuPower,
                GpuFan = gpuFan,
                GpuVoltage = gpuVoltage
            });
        }

        _entries.Sort((left, right) => right.HotspotC.CompareTo(left.HotspotC));
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        }

        Save();
        return true;
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _path,
                JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // History is informative only; a failed write must not break the agent.
        }
    }
}
