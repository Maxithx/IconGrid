using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IconGrid.Helpers.Hardware.Storage;

/// <summary>
/// Collects the physical disk list for the Hardware page.
///
/// Cadence matters: CHECK POWER MODE (power state) is cheap and never spins a disk
/// up, activity comes from performance counters, while SMART is refreshed rarely and
/// never for a disk that is already in standby. This keeps a sleeping hard drive asleep.
/// </summary>
public sealed class DiskStatusCollector : IDisposable
{
    private static readonly TimeSpan InventoryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PowerPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ActivityPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SmartRefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SmartRetryWhenNothingToRead = TimeSpan.FromMinutes(1);

    private readonly DiskActivityReader _activity = new();
    private readonly StorageSmartReader _smart = new();
    private readonly object _smartGate = new();
    private readonly List<PhysicalDiskStatus> _disks = new();
    private readonly Dictionary<int, SmartDiskReading> _smartByIndex = new();
    private readonly Dictionary<int, int> _rotationRateByIndex = new();

    private IReadOnlyList<DiskInventoryEntry> _inventory = Array.Empty<DiskInventoryEntry>();
    private DateTime _nextInventoryUtc = DateTime.MinValue;
    private DateTime _nextPowerUtc = DateTime.MinValue;
    private DateTime _nextActivityUtc = DateTime.MinValue;
    private DateTime _nextSmartUtc = DateTime.MinValue;
    private Task? _smartTask;
    private bool _disposed;

    public IReadOnlyList<PhysicalDiskStatus> GetSnapshot()
    {
        if (_disposed)
        {
            return _disks;
        }

        try
        {
            var now = DateTime.UtcNow;

            if (now >= _nextInventoryUtc)
            {
                _inventory = DiskInventoryReader.Read();
                _nextInventoryUtc = now + InventoryInterval;
                RebuildDisks();
                ClassifyRotatingMedia();
            }

            if (now >= _nextActivityUtc)
            {
                ApplyActivity();
                _nextActivityUtc = now + ActivityPollInterval;
            }

            if (now >= _nextPowerUtc)
            {
                ApplyPowerStates();
                _nextPowerUtc = now + PowerPollInterval;
            }

            StartSmartRefreshIfDue(now);
            ApplyCompletedSmartRefresh();

            return _disks;
        }
        catch
        {
            return _disks;
        }
    }

    private void RebuildDisks()
    {
        _disks.Clear();

        foreach (var entry in _inventory.OrderBy(e => e.Index))
        {
            var disk = new PhysicalDiskStatus
            {
                Index = entry.Index,
                Model = entry.Model,
                Serial = entry.Serial,
                Firmware = entry.Firmware,
                SizeBytes = entry.SizeBytes,
                BusType = entry.BusType,
                MediaType = entry.MediaType,
                DriveLetters = entry.DriveLetters,
                IsHdd = entry.IsHdd,
                IsSata = entry.IsSata,
                RotationRpm = entry.RotationRpm,
                Health = entry.Health,
                PowerState = DiskPowerStateCodes.Unknown
            };

            if (_smartByIndex.TryGetValue(entry.Index, out var reading))
            {
                ApplySmartReading(disk, reading);
            }

            _disks.Add(disk);
        }
    }

    private void ApplyActivity()
    {
        foreach (var disk in _disks)
        {
            var sample = _activity.Read(disk.Index);
            disk.ReadBytesPerSec = sample.ReadBytesPerSec;
            disk.WriteBytesPerSec = sample.WriteBytesPerSec;
            disk.ActivityPercent = sample.ActivityPercent;
        }
    }

    private void ApplyPowerStates()
    {
        foreach (var disk in _disks)
        {
            if (!disk.IsHdd || !disk.IsSata)
            {
                disk.PowerState = DiskPowerStateCodes.Unknown;
                continue;
            }

            disk.PowerState = AtaDriveReader.ReadPowerState(disk.Index);
        }
    }

    /// <summary>
    /// Some drives (typically older SATA units) report an unspecified MediaType and no
    /// spindle speed through WMI. ATA IDENTIFY word 217 fills that gap; it wakes the disk,
    /// so the result is cached for the lifetime of the agent.
    /// </summary>
    private void ClassifyRotatingMedia()
    {
        foreach (var disk in _disks)
        {
            if (!disk.IsSata ||
                string.Equals(disk.MediaType, DiskMediaTypeCodes.Hdd, StringComparison.Ordinal) ||
                string.Equals(disk.MediaType, DiskMediaTypeCodes.Ssd, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_rotationRateByIndex.TryGetValue(disk.Index, out var rate))
            {
                rate = AtaDriveReader.ReadNominalRotationRate(disk.Index) ?? 0;
                _rotationRateByIndex[disk.Index] = rate;
            }

            if (rate == 1)
            {
                disk.MediaType = DiskMediaTypeCodes.Ssd;
                disk.IsHdd = false;
            }
            else if (rate > 1)
            {
                disk.MediaType = DiskMediaTypeCodes.Hdd;
                disk.IsHdd = true;
                disk.RotationRpm = rate;
            }
        }
    }

    private void StartSmartRefreshIfDue(DateTime now)
    {
        if (_smartTask is { IsCompleted: false })
        {
            return;
        }

        if (now < _nextSmartUtc)
        {
            return;
        }

        // A disk that has never been read is refreshed once (SMART is needed for
        // health); after that sleeping disks are left alone so we never wake them.
        var indexes = _disks
            .Where(d => d.SmartCapturedAtUtc == null ||
                        !string.Equals(d.PowerState, DiskPowerStateCodes.Standby, StringComparison.Ordinal))
            .Select(d => d.Index)
            .ToList();

        if (indexes.Count == 0)
        {
            _nextSmartUtc = now + SmartRetryWhenNothingToRead;
            return;
        }

        _nextSmartUtc = now + SmartRefreshInterval;
        _smartTask = Task.Run(() => RunSmartRefresh(indexes));
    }

    private void RunSmartRefresh(List<int> indexes)
    {
        try
        {
            var capturedAtUtc = DateTime.UtcNow;
            var readings = _smart.Read();
            if (readings.Count == 0)
            {
                return;
            }

            lock (_smartGate)
            {
                var unresolved = readings.Where(r => r.Index < 0).ToList();
                foreach (var reading in readings.Where(r => r.Index >= 0))
                {
                    reading.CapturedAtUtc = capturedAtUtc;
                    _smartByIndex[reading.Index] = reading;
                }

                // Some controllers do not expose a device index in the identifier.
                // Fall back to the inventory order when the counts line up.
                if (unresolved.Count > 0 && unresolved.Count == indexes.Count)
                {
                    for (var i = 0; i < unresolved.Count; i++)
                    {
                        unresolved[i].CapturedAtUtc = capturedAtUtc;
                        _smartByIndex[indexes[i]] = unresolved[i];
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void ApplyCompletedSmartRefresh()
    {
        if (_smartTask is not { IsCompleted: true })
        {
            return;
        }

        _smartTask = null;

        Dictionary<int, SmartDiskReading> snapshot;
        lock (_smartGate)
        {
            snapshot = new Dictionary<int, SmartDiskReading>(_smartByIndex);
        }

        foreach (var disk in _disks)
        {
            if (snapshot.TryGetValue(disk.Index, out var reading))
            {
                ApplySmartReading(disk, reading);
            }
        }
    }

    private static void ApplySmartReading(PhysicalDiskStatus disk, SmartDiskReading reading)
    {
        disk.SmartStatus = reading.Status;
        disk.SmartAttributes = reading.Attributes;
        disk.SmartCapturedAtUtc = reading.CapturedAtUtc;
        disk.HealthPercent = reading.HealthPercent;
        if (reading.TemperatureC.HasValue)
        {
            disk.TemperatureC = reading.TemperatureC;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activity.Dispose();
        _smart.Dispose();
    }
}

