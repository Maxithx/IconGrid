using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;

namespace IconGrid.Helpers.Hardware.Storage;

internal readonly record struct DiskActivitySample(
    double? ReadBytesPerSec,
    double? WriteBytesPerSec,
    double? ActivityPercent);

/// <summary>
/// Reads live per-disk I/O from the WMI performance class
/// <c>Win32_PerfFormattedData_PerfDisk_PhysicalDisk</c>.
///
/// WMI is used instead of <see cref="System.Diagnostics.PerformanceCounter"/> because
/// performance-counter names are localized on non-English Windows.
/// This never spins a disk up and does not require elevation.
/// </summary>
internal sealed class DiskActivityReader : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);

    private readonly Dictionary<int, DiskActivitySample> _samples = new();
    private DateTime _nextSampleUtc = DateTime.MinValue;
    private bool _unavailable;

    public DiskActivitySample Read(int diskIndex)
    {
        if (_unavailable)
        {
            return default;
        }

        var now = DateTime.UtcNow;
        if (now >= _nextSampleUtc)
        {
            _nextSampleUtc = now + SampleInterval;
            Refresh();
        }

        return _samples.TryGetValue(diskIndex, out var sample) ? sample : default;
    }

    private void Refresh()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DiskReadBytesPerSec, DiskWriteBytesPerSec, PercentIdleTime " +
                "FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk");

            foreach (ManagementObject row in searcher.Get())
            {
                try
                {
                    if (!TryParseDiskIndex(row["Name"]?.ToString(), out var index))
                    {
                        continue;
                    }

                    var read = ReadDouble(row, "DiskReadBytesPerSec");
                    var write = ReadDouble(row, "DiskWriteBytesPerSec");
                    var idle = ReadDouble(row, "PercentIdleTime");
                    double? activity = idle.HasValue ? Math.Clamp(100d - idle.Value, 0d, 100d) : null;

                    _samples[index] = new DiskActivitySample(read, write, activity);
                }
                catch
                {
                }
                finally
                {
                    row.Dispose();
                }
            }
        }
        catch
        {
            _unavailable = true;
        }
    }

    private static double? ReadDouble(ManagementBaseObject row, string property)
    {
        try
        {
            var value = row[property];
            return value == null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseDiskIndex(string? instanceName, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        var space = instanceName.IndexOf(' ');
        var number = space > 0 ? instanceName[..space] : instanceName;
        return int.TryParse(number, out index);
    }

    public void Dispose() => _samples.Clear();
}
