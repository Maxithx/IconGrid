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
    public string? CpuUsage { get; set; }
    public double? CpuUsagePercent { get; set; }
    public string? CpuClock { get; set; }
    public string? CpuVoltage { get; set; }
    public string? GpuClock { get; set; }
    public string? GpuUsage { get; set; }
    public double? GpuUsagePercent { get; set; }
    public string? GpuName { get; set; }
    public bool IsPawnIoAvailable { get; set; }

    /// <summary>
    /// Physical disks (identity, health, SMART, power state) for the Hardware page.
    /// Collected by the elevated agent because SMART and ATA pass-through need admin.
    /// </summary>
    public List<PhysicalDiskStatus>? Disks { get; set; }
}
