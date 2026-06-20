using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace IconGrid.Helpers;

public sealed class HardwareSnapshotCollector : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private double? _cpuUsageEma;

    public HardwareSnapshotCollector()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true
        };

        try
        {
            _computer.Open();
        }
        catch
        {
        }
    }

    public HardwareMonitorSnapshot Capture()
    {
        var pawnIoAvailable = false;
        try
        {
            pawnIoAvailable = PawnIoHelper.IsPawnIoInstalled();
        }
        catch
        {
        }

        var (cpuTemp, gpuTemp) = CaptureTemperatures();
        var cpuUsage = CaptureCpuUsage();
        var smoothedUsage = cpuUsage.HasValue ? SmoothCpuUsage(cpuUsage.Value) : (double?)null;
        var displayUsage = smoothedUsage.HasValue ? Math.Clamp(smoothedUsage.Value, 1d, 100d) : (double?)null;

        return new HardwareMonitorSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow,
            CpuTemp = cpuTemp,
            GpuTemp = gpuTemp,
            CpuUsage = displayUsage.HasValue ? $"{displayUsage.Value:F0}%" : null,
            CpuUsagePercent = displayUsage.HasValue ? displayUsage.Value : null,
            IsPawnIoAvailable = pawnIoAvailable
        };
    }

    private (string? CpuTemp, string? GpuTemp) CaptureTemperatures()
    {
        _computer.Accept(_visitor);

        var cpuTemps = new List<float>();
        var gpuTemps = new List<float>();

        foreach (var hardware in _computer.Hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    // Dynamisk opsamling af alle temperatursensorer
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        cpuTemps.Add(sensor.Value ?? 0);
                    }
                    else if (IsGpuHardwareType(hardware.HardwareType))
                    {
                        gpuTemps.Add(sensor.Value ?? 0);
                    }
                }
            }
        }

        // Returnerer den højeste temperatur fundet for CPU og GPU
        return (
            FormatTemperature(cpuTemps.Any() ? cpuTemps.Max() : null),
            FormatTemperature(gpuTemps.Any() ? gpuTemps.Max() : null)
        );
    }

    private double? CaptureCpuUsage()
    {
        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType == HardwareType.Cpu)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                    {
                        return sensor.Value;
                    }
                }
            }
        }
        return null;
    }

    private double SmoothCpuUsage(double usage)
    {
        _cpuUsageEma ??= usage;
        _cpuUsageEma = (_cpuUsageEma * 0.9) + (usage * 0.1);
        return _cpuUsageEma.Value;
    }

    private static string? FormatTemperature(float? value)
    {
        if (!value.HasValue || value.Value <= 0) return null;
        return $"{value.Value:F0}°C";
    }

    private static bool IsGpuHardwareType(HardwareType hardwareType)
    {
        return hardwareType.ToString().StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
    }
}