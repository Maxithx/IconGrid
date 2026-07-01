using System;

namespace IconGrid.Helpers.Hardware;

public sealed class HardwareMonitorSnapshot
{
    public DateTime CapturedAtUtc { get; set; }
    public string? CpuTemp { get; set; }
    public string? GpuTemp { get; set; }
    public string? CpuUsage { get; set; }
    public double? CpuUsagePercent { get; set; }
    public bool IsPawnIoAvailable { get; set; }
}
