using System;
using System.Collections.Generic;
using System.Linq;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;
using LibreHardwareMonitor.Hardware;

namespace IconGrid.Helpers.Hardware;

public sealed class HardwareSnapshotCollector : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private double? _cpuUsageEma;
    private string? _gpuName;

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
        // One sensor update per snapshot. Calling Accept() inside every helper
        // updated all CPU/GPU/motherboard sensors six times per capture, which
        // took ~500 ms and kept a full core busy.
        try
        {
            _computer.Accept(_visitor);
        }
        catch
        {
        }

        var (cpuTemp, gpuTemp, gpuHotspot) = CaptureTemperatures();
        var cpuUsage = CaptureCpuUsage();
        var gpuUsage = CaptureGpuUsage();
        var cpuClock = CaptureCpuClock();
        var gpuClock = CaptureGpuClock();
        var gpuMemoryClock = CaptureGpuMemoryClock();
        var gpuMemoryUsage = CaptureGpuMemoryUsage();
        var gpuPower = CaptureGpuPower();
        var gpuFan = CaptureGpuFan();
        var cpuVoltage = CaptureCpuVoltage();
        var gpuVoltage = CaptureGpuVoltage();
        var smoothedCpuUsage = cpuUsage.HasValue ? SmoothCpuUsage(cpuUsage.Value) : (double?)null;
        var displayCpuUsage = smoothedCpuUsage.HasValue ? Math.Clamp(smoothedCpuUsage.Value, 1d, 100d) : (double?)null;
        var displayGpuUsage = gpuUsage.HasValue ? Math.Clamp(gpuUsage.Value, 0d, 100d) : (double?)null;

        return new HardwareMonitorSnapshot
        {
            CapturedAtUtc = DateTime.UtcNow,
            CpuTemp = cpuTemp,
            GpuTemp = gpuTemp,
            GpuHotspot = gpuHotspot,
            GpuHotspotDeltaC = ComputeTemperatureDelta(gpuTemp, gpuHotspot),
            CpuUsage = displayCpuUsage.HasValue ? $"{displayCpuUsage.Value:F0}%" : null,
            CpuUsagePercent = displayCpuUsage.HasValue ? displayCpuUsage.Value : null,
            GpuUsage = displayGpuUsage.HasValue ? $"{displayGpuUsage.Value:F0}%" : null,
            GpuUsagePercent = displayGpuUsage.HasValue ? displayGpuUsage.Value : null,
            CpuClock = cpuClock,
            GpuClock = gpuClock,
            GpuMemoryClock = gpuMemoryClock,
            GpuMemoryUsage = gpuMemoryUsage,
            GpuPower = gpuPower,
            GpuFan = gpuFan,
            CpuVoltage = cpuVoltage,
            GpuVoltage = gpuVoltage,
            GpuName = _gpuName,
            IsPawnIoAvailable = pawnIoAvailable
        };
    }

    private (string? cpu, string? gpu, string? gpuHotspot) CaptureTemperatures()
    {
        string? cpu = null;
        string? gpu = null;
        string? gpuHotspot = null;

        try
        {
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
                    gpuHotspot ??= SelectHotspotTemperatureSensor(hardware.Sensors);
                }
            }
        }
        catch
        {
        }

        return (cpu, gpu, gpuHotspot);
    }

    /// <summary>
    /// Hotspot minus edge temperature. A large delta (roughly &gt; 15-20 °C) means the
    /// cooler is not making proper contact with the die (thermal paste/pads or
    /// mounting pressure), which caps the boost clock long before the power limit.
    /// </summary>
    private static double? ComputeTemperatureDelta(string? edgeText, string? hotspotText)
    {
        var edge = ParseTemperature(edgeText);
        var hotspot = ParseTemperature(hotspotText);

        if (!edge.HasValue || !hotspot.HasValue || hotspot.Value < edge.Value)
        {
            return null;
        }

        return Math.Round(hotspot.Value - edge.Value, 1);
    }

    private static double? ParseTemperature(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsDigit(character) || character == '.' || character == ',')
            {
                builder.Append(character == ',' ? '.' : character);
            }
        }

        return double.TryParse(
            builder.ToString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Reads the GPU "hot spot" sensor. The hotspot is the temperature the driver
    /// actually throttles on, so it is the number that explains missing boost clocks.
    /// </summary>
    private static string? SelectHotspotTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var candidate = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            .FirstOrDefault(sensor =>
            {
                var name = (sensor.Name ?? string.Empty).Replace(" ", string.Empty);
                return name.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("junction", StringComparison.OrdinalIgnoreCase);
            });

        return candidate == null ? null : FormatTemperature(candidate.Value);
    }

    private float? CaptureCpuUsage()
    {
        try
        {
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

    /// <summary>
    /// Reads the live GPU core voltage (for example "0,9875 V") from the same
    /// LibreHardwareMonitor GPU node used for the GPU clock. The settings page
    /// shows it next to the stock/profile curve voltages so the user can see the
    /// effect of an applied undervolt without opening Afterburner.
    /// </summary>
    private string? CaptureGpuVoltage()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var voltageSensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetGpuVoltagePriority(sensor.Name))
                    .ThenByDescending(sensor => sensor.Value)
                    .FirstOrDefault();

                return FormatVoltage(voltageSensor?.Value);
            }
        }
        catch
        {
        }

        return null;
    }

    private static int GetGpuVoltagePriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("GPU Core Voltage", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Core Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("GPU", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private string? CaptureCpuVoltage()
    {
        try
        {
            string? bestCpuNode = null;
            string? bestBoardNode = null;

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    bestCpuNode ??= SelectCpuVoltageSensor(hardware.Sensors, requireCpuRelevance: false);
                }
                else if (hardware.HardwareType == HardwareType.Motherboard)
                {
                    bestBoardNode ??= SelectCpuVoltageSensor(hardware.Sensors, requireCpuRelevance: true);
                }
            }

            return bestCpuNode ?? bestBoardNode;
        }
        catch
        {
        }

        return null;
    }

    private static string? SelectCpuVoltageSensor(IEnumerable<ISensor> sensors, bool requireCpuRelevance)
    {
        var candidates = sensors.Where(sensor => sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue);
        if (requireCpuRelevance)
        {
            candidates = candidates.Where(sensor => IsCpuVoltageCandidate(sensor.Name));
        }

        var voltageSensor = candidates
            .OrderByDescending(sensor => GetCpuVoltagePriority(sensor.Name))
            .ThenByDescending(sensor => sensor.Value)
            .FirstOrDefault();

        return FormatVoltage(voltageSensor?.Value);
    }

    private static bool IsCpuVoltageCandidate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("VID", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Core Voltage", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("CPU", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetCpuVoltagePriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("VID", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Core Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("Voltage", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private string? CaptureCpuClock()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu)
                {
                    continue;
                }

                var clockSensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetCpuClockPriority(sensor.Name))
                    .ThenByDescending(sensor => sensor.Value)
                    .FirstOrDefault();

                return FormatClock(clockSensor?.Value);
            }
        }
        catch
        {
        }

        return null;
    }

    private float? CaptureGpuUsage()
    {
        _gpuName = null;
        try
        {
            float? nvidiaUsage = null;
            string? nvidiaName = null;
            float? fallbackUsage = null;
            string? fallbackName = null;

            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var usageSensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetGpuUsagePriority(sensor.Name))
                    .FirstOrDefault();

                if (usageSensor == null)
                    continue;

                // Prefer NVIDIA GPU (dGPU) over integrated AMD/Intel GPU
                if (hardware.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    nvidiaUsage = usageSensor.Value;
                    nvidiaName = hardware.Name;
                    break; // NVIDIA found, stop looking
                }

                // Keep fallback in case no NVIDIA is found
                fallbackUsage ??= usageSensor.Value;
                fallbackName ??= hardware.Name;
            }

            if (nvidiaUsage.HasValue)
            {
                _gpuName = nvidiaName;
                return nvidiaUsage.Value;
            }

            _gpuName = fallbackName;
            return fallbackUsage;
        }
        catch
        {
        }

        return null;
    }

    private static int GetGpuUsagePriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("D3D", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
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

    private string? CaptureGpuClock()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var clockSensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetGpuClockPriority(sensor.Name))
                    .ThenByDescending(sensor => sensor.Value)
                    .FirstOrDefault();

                return FormatGpuClock(clockSensor?.Value);
            }
        }
        catch
        {
        }

        return null;
    }

    private string? CaptureGpuMemoryClock()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var sensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Clock &&
                                     sensor.Value.HasValue &&
                                     sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(sensor => sensor.Value)
                    .FirstOrDefault();

                return sensor?.Value is float value && value > 0
                    ? $"{value:F0} MHz"
                    : null;
            }
        }
        catch
        {
        }

        return null;
    }

    private string? CaptureGpuMemoryUsage()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var smallData = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.SmallData && sensor.Value.HasValue)
                    .ToList();

                var used = smallData.FirstOrDefault(sensor =>
                    sensor.Name.Contains("GPU Memory Used", StringComparison.OrdinalIgnoreCase))?.Value ??
                    smallData.FirstOrDefault(sensor =>
                        sensor.Name.Contains("D3D Dedicated Memory Used", StringComparison.OrdinalIgnoreCase))?.Value;
                var total = smallData.FirstOrDefault(sensor =>
                    sensor.Name.Contains("GPU Memory Total", StringComparison.OrdinalIgnoreCase))?.Value;

                if (used.HasValue && total.HasValue && total.Value > 0)
                {
                    return $"{used.Value / 1024f:F1} / {total.Value / 1024f:F1} GB";
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private string? CaptureGpuPower()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var sensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
                    .OrderByDescending(sensor => GetGpuPowerPriority(sensor.Name))
                    .FirstOrDefault();

                return sensor?.Value is float value && value > 0
                    ? $"{value:F0} W"
                    : null;
            }
        }
        catch
        {
        }

        return null;
    }

    private static int GetGpuPowerPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("GPU Package", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("GPU Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("GPU Board Power", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return name.Contains("Power", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private string? CaptureGpuFan()
    {
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (!IsGpuHardwareType(hardware.HardwareType))
                {
                    continue;
                }

                var sensor = hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                    .OrderByDescending(sensor => sensor.Value)
                    .FirstOrDefault();

                return sensor?.Value is float value && value > 0
                    ? $"{value:F0} RPM"
                    : null;
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

    private static int GetCpuClockPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Max Clock", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (name.Contains("Bus Speed", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        if (name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Effective Clock", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("Clock", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
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

    private static int GetGpuClockPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("GPU Clock", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Graphics", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("Clock", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
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

    private static string? FormatVoltage(float? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        return $"{value.Value:F4} V";
    }

    private static string? FormatClock(float? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        if (value.Value >= 1000f)
        {
            return $"{value.Value / 1000f:F2} GHz";
        }

        return $"{value.Value:F0} MHz";
    }

    /// <summary>
    /// GPU core clock is always shown in MHz (never GHz) so undervolt/clock tuning can be read
    /// directly off the gaming overlay, the same way tuning tools display it.
    /// </summary>
    private static string? FormatGpuClock(float? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        return $"{value.Value:F0} MHz";
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
