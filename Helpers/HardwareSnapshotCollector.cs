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

    private (string? cpu, string? gpu) CaptureTemperatures()
    {
        string? cpu = null;
        string? gpu = null;

        try
        {
            _computer.Accept(_visitor);
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    // Tjek for Intel N150 (SoC) arkitektur vs. traditionel Ryzen/CPU
                    if (hardware.Name.Contains("N150", StringComparison.OrdinalIgnoreCase))
                    {
                        // Intel N150: Brug en mere generel tilgang til SoC-sensorer
                        cpu ??= SelectPreferredTemperatureSensor(hardware.Sensors, (name) => 1);
                    }
                    else
                    {
                        // Din eksisterende, optimerede Ryzen-logik (prioriterer Tctl)
                        cpu ??= SelectPreferredTemperatureSensor(hardware.Sensors, GetCpuSensorPriority);
                    }
                }
                else if (IsGpuHardwareType(hardware.HardwareType))
                {
                    // Din eksisterende GPU-logik, som fungerer med Afterburner
                    gpu ??= SelectPreferredTemperatureSensor(hardware.Sensors, GetGpuSensorPriority);
                }
            }
        }
        catch
        {
        }

        return (cpu, gpu);
    }

    private float? CaptureCpuUsage()
    {
        try
        {
            _computer.Accept(_visitor);
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu)
                {
                    continue;
                }

                var usageSensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetCpuUsagePriority(sensor.Name))
                    .FirstOrDefault();

                return usageSensor?.Value;
            }
        }
        catch
        {
        }

        return null;
    }

    private double SmoothCpuUsage(double latestValue)
    {
        const double alpha = 0.35;
        if (!_cpuUsageEma.HasValue)
        {
            _cpuUsageEma = latestValue;
            return latestValue;
        }

        _cpuUsageEma = _cpuUsageEma.Value + alpha * (latestValue - _cpuUsageEma.Value);
        return _cpuUsageEma.Value;
    }

    private static string? SelectPreferredTemperatureSensor(IEnumerable<ISensor> sensors, Func<string?, int> prioritySelector)
    {
        var candidate = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            .OrderByDescending(sensor => prioritySelector(sensor.Name))
            .FirstOrDefault();

        return candidate == null ? null : FormatTemperature(candidate.Value);
    }

    private static int GetCpuUsagePriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("Total", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static int GetCpuSensorPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("Tctl", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static int GetGpuSensorPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("Core", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static string? FormatTemperature(float? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return $"{value.Value:F0}°C";
    }

    private static bool IsGpuHardwareType(HardwareType hardwareType)
    {
        var name = hardwareType.ToString();
        return name.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            _computer.Close();
        }
        catch
        {
        }
    }
}
