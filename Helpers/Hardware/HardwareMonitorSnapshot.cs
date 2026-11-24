using System;
using System.Collections.Generic;
using IconGrid.Helpers.Hardware.Storage;

namespace IconGrid.Helpers.Hardware;

public sealed class HardwareMonitorSnapshot
{
    public DateTime CapturedAtUtc { get; set; }
    public string? FpsStatus { get; set; }
    public string? FpsSource { get; set; }
    public string? CpuTemp { get; set; }
    public string? GpuTemp { get; set; }

    /// <summary>GPU hotspot (hottest point on the die); the real thermal limiter.</summary>
    public string? GpuHotspot { get; set; }

    /// <summary>Hotspot minus edge temperature in degrees Celsius (contact quality).</summary>
    public double? GpuHotspotDeltaC { get; set; }

    /// <summary>Five highest hotspot readings with the game that was running.</summary>
    public List<HotspotHistoryEntry>? HotspotHistory { get; set; }
    public string? CpuUsage { get; set; }
    public double? CpuUsagePercent { get; set; }
    public string? CpuClock { get; set; }
    public string? CpuVoltage { get; set; }
    public string? GpuVoltage { get; set; }
    public string? GpuClock { get; set; }
    public string? GpuMemoryClock { get; set; }
    public string? GpuMemoryUsage { get; set; }
    public string? GpuPower { get; set; }
    public string? GpuFan { get; set; }
    public string? GpuUsage { get; set; }
    public double? GpuUsagePercent { get; set; }
    public string? GpuName { get; set; }
    public bool IsPawnIoAvailable { get; set; }

    /// <summary>Rolling hotspot/GPU temperature samples (one every 5 seconds).</summary>
    public List<HotspotTrendPoint>? HotspotTrend { get; set; }

    /// <summary>
    /// Physical disks (identity, health, SMART, power state) for the Hardware page.
    /// Collected by the elevated agent because SMART and ATA pass-through need admin.
    /// </summary>
    public List<PhysicalDiskStatus>? Disks { get; set; }
}

public sealed class HotspotTrendPoint
{
    public DateTime CapturedAtUtc { get; set; }
    public double HotspotC { get; set; }
    public double EdgeC { get; set; }
}
