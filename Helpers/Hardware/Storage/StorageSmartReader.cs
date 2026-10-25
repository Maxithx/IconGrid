using System;
using System.Collections.Generic;
using System.Linq;
using IconGrid.Helpers.Logging;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;

namespace IconGrid.Helpers.Hardware.Storage;

internal sealed class SmartDiskReading
{
    public int Index { get; init; }
    public double? TemperatureC { get; init; }
    public string Status { get; init; } = DiskSmartStatusCodes.Unavailable;
    public List<SmartAttributeStatus> Attributes { get; init; } = new();
    public int? HealthPercent { get; init; }
    public DateTime CapturedAtUtc { get; set; }
}

/// <summary>
/// Reads SMART attributes through a dedicated LibreHardwareMonitor instance.
///
/// This reader must never be part of the 1 Hz hardware snapshot collector: a
/// SMART read can wake a standby disk, so it is only invoked on a slow cadence
/// (and never for a disk that is known to be sleeping).
/// </summary>
internal sealed class StorageSmartReader : IDisposable
{
    private static readonly HashSet<int> CriticalAttributeIds = new() { 5, 187, 196, 197, 198, 199 };

    private readonly UpdateVisitor _visitor = new();
    private Computer? _computer;
    private bool _unavailable;

    public IReadOnlyList<SmartDiskReading> Read()
    {
        if (_unavailable)
        {
            return Array.Empty<SmartDiskReading>();
        }

        try
        {
            if (_computer == null)
            {
                var computer = new Computer { IsStorageEnabled = true };
                computer.Open();
                _computer = computer;
            }

            _computer.Accept(_visitor);

            var readings = new List<SmartDiskReading>();
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Storage || hardware is not StorageDevice device)
                {
                    continue;
                }

                readings.Add(BuildReading(hardware, device));
            }

            return readings;
        }
        catch
        {
            _unavailable = true;
            Dispose();
            return Array.Empty<SmartDiskReading>();
        }
    }


    private static SmartDiskReading BuildReading(IHardware hardware, StorageDevice device)
    {
        var attributes = new List<SmartAttributeStatus>();
        try
        {
            foreach (var attribute in device.Attributes ?? Array.Empty<SmartAttribute>())
            {
                if (attribute == null)
                {
                    continue;
                }

                attributes.Add(new SmartAttributeStatus
                {
                    Id = attribute.Id,
                    Name = string.IsNullOrWhiteSpace(attribute.Name) ? $"0x{attribute.Id:X2}" : attribute.Name,
                    Value = attribute.Value,
                    Threshold = attribute.Threshold
                });
            }
        }
        catch
        {
            // A controller can reject SMART mid-enumeration; keep whatever was read.
        }

        var status = EvaluateStatus(attributes);
        foreach (var attribute in attributes)
        {
            attribute.Status = IsAttributeWarning(attribute)
                ? SmartAttributeStatusCodes.Warning
                : SmartAttributeStatusCodes.Ok;
        }

        double? temperature = null;
        try
        {
            var sensors = hardware.Sensors?
                .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                .ToList();

            if (sensors is { Count: > 0 })
            {
                // NVMe drives expose several temperature sensors, including
                // "Warning Temperature" and "Critical Temperature" *thresholds*.
                // Use the composite reading (what Windows/vendor tools show); never Max().
                var composite = sensors.FirstOrDefault(s =>
                                    s.Name.Contains("Composite", StringComparison.OrdinalIgnoreCase))
                                ?? sensors.FirstOrDefault(s => !IsThresholdSensor(s.Name))
                                ?? sensors[0];

                temperature = Math.Round(composite.Value!.Value, 0);
                LogTemperatureSensors(hardware.Name, sensors);
            }
        }
        catch
        {
        }

        return new SmartDiskReading
        {
            Index = ParseIndex(hardware.Identifier?.ToString()),
            TemperatureC = temperature,
            Status = status,
            Attributes = attributes,
            HealthPercent = ComputeHealthPercent(attributes)
        };
    }

    private static string EvaluateStatus(List<SmartAttributeStatus> attributes)
    {
        if (attributes.Count == 0)
        {
            return DiskSmartStatusCodes.Unavailable;
        }

        if (attributes.Any(a => a.Threshold > 0 && a.Value > 0 && a.Value <= a.Threshold))
        {
            return DiskSmartStatusCodes.Failing;
        }

        if (attributes.Any(a => a.Threshold > 0 && a.Value > 0 && a.Value < 100))
        {
            return DiskSmartStatusCodes.Warning;
        }

        var spare = attributes.FirstOrDefault(a =>
            a.Name.Contains("Available Spare", StringComparison.OrdinalIgnoreCase) &&
            !a.Name.Contains("Threshold", StringComparison.OrdinalIgnoreCase));
        var spareThreshold = attributes.FirstOrDefault(a =>
            a.Name.Contains("Spare Threshold", StringComparison.OrdinalIgnoreCase));
        if (spare != null && spareThreshold != null && spare.Value <= spareThreshold.Value)
        {
            return DiskSmartStatusCodes.Failing;
        }

        var wear = attributes.FirstOrDefault(a =>
            a.Name.Contains("Percentage Used", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Wear Level", StringComparison.OrdinalIgnoreCase));
        if (wear != null && wear.Value >= 90)
        {
            return DiskSmartStatusCodes.Warning;
        }

        return DiskSmartStatusCodes.Ok;
    }

    private static bool IsAttributeWarning(SmartAttributeStatus attribute)
    {
        if (attribute.Threshold > 0 && attribute.Value > 0 && attribute.Value <= attribute.Threshold)
        {
            return true;
        }

        return CriticalAttributeIds.Contains(attribute.Id) &&
               attribute.Value > 0 &&
               attribute.Value < 100;
    }

    /// <summary>
    /// Estimates a Hard Disk Sentinel style health percentage:
    /// NVMe "Percentage Used" -> 100-used, SSD life indicators -> value,
    /// rotating media -> 100 minus weighted critical SMART counters.
    /// </summary>
    private static int? ComputeHealthPercent(List<SmartAttributeStatus> attributes)
    {
        if (attributes.Count == 0)
        {
            return null;
        }

        var used = attributes.FirstOrDefault(a =>
            a.Name.Contains("Percentage Used", StringComparison.OrdinalIgnoreCase));
        if (used != null)
        {
            return ClampPercent(100 - (int)Math.Round(used.Value));
        }

        // Check each life indicator separately: list order is by SMART id, so a raw
        // counter (e.g. "Wear Leveling Count" = 18667) must not win over "SSD Life Left".
        var lifeLeft = attributes.FirstOrDefault(a =>
            a.Name.Contains("Life Left", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase));
        if (lifeLeft != null)
        {
            return ClampPercent((int)Math.Round(lifeLeft.Value));
        }

        var wearIndicator = attributes.FirstOrDefault(a =>
            a.Name.Contains("Wear Indicator", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Media Wearout", StringComparison.OrdinalIgnoreCase));
        if (wearIndicator != null)
        {
            return ClampPercent((int)Math.Round(wearIndicator.Value));
        }

        var wearLeveling = attributes.FirstOrDefault(a =>
            a.Name.Contains("Wear Leveling", StringComparison.OrdinalIgnoreCase));
        if (wearLeveling != null && wearLeveling.Value is >= 1 and <= 100)
        {
            return ClampPercent((int)Math.Round(wearLeveling.Value));
        }

        // Rotating media. UDMA CRC errors (199) are deliberately ignored: they indicate a
        // cable/signal problem, not media health.
        var score = 100d;
        score -= AttributeValue(attributes, 197) * 0.6; // Current Pending Sector Count
        score -= AttributeValue(attributes, 5) * 1.0;   // Reallocated Sectors Count
        score -= AttributeValue(attributes, 198) * 2.0; // Uncorrectable Sector Count
        score -= AttributeValue(attributes, 187) * 1.0; // Reported Uncorrectable Errors
        score -= AttributeValue(attributes, 10) * 2.0;  // Spin Retry Count
        return ClampPercent((int)Math.Round(score));
    }

    private static double AttributeValue(List<SmartAttributeStatus> attributes, int id) =>
        attributes.FirstOrDefault(a => a.Id == id)?.Value ?? 0d;

    private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);

    private static bool IsThresholdSensor(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Threshold", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> LoggedTemperatureSensors = new();
    private static readonly object TemperatureLogGate = new();

    private static void LogTemperatureSensors(string? deviceName, IReadOnlyList<ISensor> sensors)
    {
        var key = deviceName ?? string.Empty;
        lock (TemperatureLogGate)
        {
            if (!LoggedTemperatureSensors.Add(key))
            {
                return;
            }
        }

        try
        {
            var text = string.Join(", ", sensors.Select(s => $"{s.Name}={s.Value:F0}"));
            AppTrace.Write($"[StorageSmartReader] {key} temperature sensors: {text}");
        }
        catch
        {
        }
    }

    private static int ParseIndex(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return -1;
        }

        var lastSlash = identifier.LastIndexOf('/');
        var tail = lastSlash >= 0 ? identifier[(lastSlash + 1)..] : identifier;
        return int.TryParse(tail, out var index) ? index : -1;
    }

    public void Dispose()
    {
        try
        {
            _computer?.Close();
        }
        catch
        {
        }

        _computer = null;
    }
}
