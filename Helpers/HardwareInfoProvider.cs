using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace IconGrid.Helpers;

public static class HardwareInfoProvider
{
    public static HardwareOverview LoadOverview()
    {
        return new HardwareOverview
        {
            Board = LoadBoard(),
            Cpu = LoadCpu(),
            Gpu = LoadGpu(),
            Memory = LoadMemory()
        };
    }

    private static HardwareBoardInfo LoadBoard()
    {
        var manufacturer = "--";
        var model = "--";
        var biosVersion = "--";

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject board in searcher.Get())
            {
                manufacturer = ReadString(board, "Manufacturer");
                model = ReadString(board, "Product");
                break;
            }
        }
        catch
        {
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            foreach (ManagementObject bios in searcher.Get())
            {
                biosVersion = ReadString(bios, "SMBIOSBIOSVersion");
                break;
            }
        }
        catch
        {
        }

        return new HardwareBoardInfo
        {
            Manufacturer = manufacturer,
            Model = model,
            BiosVersion = biosVersion
        };
    }

    private static HardwareProcessorInfo LoadCpu()
    {
        var name = "--";
        var vendor = "CPU";
        var cores = "--";
        var threads = "--";
        var boostClock = "--";

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject cpu in searcher.Get())
            {
                name = NormalizeCpuName(ReadString(cpu, "Name"));
                vendor = DetectVendor(ReadString(cpu, "Manufacturer"), name, "CPU");
                cores = ReadUInt(cpu, "NumberOfCores").ToString(CultureInfo.InvariantCulture);
                threads = ReadUInt(cpu, "NumberOfLogicalProcessors").ToString(CultureInfo.InvariantCulture);
                var mhz = ReadUInt(cpu, "MaxClockSpeed");
                boostClock = mhz > 0 ? $"{mhz / 1000d:F1} GHz" : "--";
                break;
            }
        }
        catch
        {
        }

        return new HardwareProcessorInfo
        {
            Name = name,
            Vendor = vendor,
            Cores = cores,
            Threads = threads,
            BoostClock = boostClock
        };
    }

    private static HardwareGraphicsInfo LoadGpu()
    {
        var candidates = new List<HardwareGraphicsInfo>();
        var lhmMemoryCandidates = LoadGpuMemoryCandidatesFromLibreHardwareMonitor();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility, AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject gpu in searcher.Get())
            {
                var name = NormalizeWhitespace(ReadString(gpu, "Name"));
                if (string.IsNullOrWhiteSpace(name) || name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var manufacturer = ReadString(gpu, "AdapterCompatibility");
                var vendor = DetectVendor(manufacturer, name, "GPU");
                var wmiVramBytes = ReadULong(gpu, "AdapterRAM");
                var lhmVramBytes = MatchLibreHardwareMonitorMemory(lhmMemoryCandidates, name, vendor);
                var vramBytes = lhmVramBytes > 0 ? lhmVramBytes : wmiVramBytes;
                candidates.Add(new HardwareGraphicsInfo
                {
                    Name = name,
                    Vendor = vendor,
                    Vram = vramBytes > 0 ? FormatBytes(vramBytes) : "--",
                    SortValue = vramBytes
                });
            }
        }
        catch
        {
        }

        var best = candidates
            .OrderByDescending(candidate => candidate.SortValue)
            .FirstOrDefault();

        return best ?? new HardwareGraphicsInfo
        {
            Name = "--",
            Vendor = "GPU",
            Vram = "--"
        };
    }

    private static List<HardwareGraphicsInfo> LoadGpuMemoryCandidatesFromLibreHardwareMonitor()
    {
        var candidates = new List<HardwareGraphicsInfo>();

        try
        {
            var computer = new Computer
            {
                IsGpuEnabled = true
            };
            try
            {
                computer.Open();
                computer.Accept(new UpdateVisitor());

                foreach (var hardware in computer.Hardware)
                {
                    if (!IsGpuHardwareType(hardware.HardwareType))
                    {
                        continue;
                    }

                    var name = NormalizeWhitespace(hardware.Name);
                    var vendor = DetectVendor(string.Empty, name, "GPU");
                    var vramBytes = TryReadGpuMemoryFromSensors(hardware.Sensors);
                    if (vramBytes <= 0)
                    {
                        continue;
                    }

                    candidates.Add(new HardwareGraphicsInfo
                    {
                        Name = name,
                        Vendor = vendor,
                        Vram = FormatBytes(vramBytes),
                        SortValue = vramBytes
                    });
                }
            }
            finally
            {
                try
                {
                    computer.Close();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return candidates;
    }

    private static ulong MatchLibreHardwareMonitorMemory(IEnumerable<HardwareGraphicsInfo> candidates, string name, string vendor)
    {
        var normalizedName = NormalizeWhitespace(name);

        var exactMatch = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch.SortValue;
        }

        var vendorMatch = candidates
            .Where(candidate => string.Equals(candidate.Vendor, vendor, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => NameSimilarityScore(candidate.Name, normalizedName))
            .ThenByDescending(candidate => candidate.SortValue)
            .FirstOrDefault();

        return vendorMatch?.SortValue ?? 0;
    }

    private static int NameSimilarityScore(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
    }

    private static ulong TryReadGpuMemoryFromSensors(IEnumerable<ISensor> sensors)
    {
        var memorySensors = sensors
            .Where(sensor => sensor.Value.HasValue && IsGpuMemorySensorName(sensor.Name))
            .ToList();

        var totalSensor = memorySensors
            .Where(sensor => sensor.Name.Contains("total", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("size", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sensor => GetGpuMemorySensorPriority(sensor.Name))
            .FirstOrDefault();

        if (totalSensor is not null)
        {
            return ConvertGpuMemorySensorToBytes(totalSensor);
        }

        var usedSensor = memorySensors
            .Where(sensor => sensor.Name.Contains("used", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sensor => GetGpuMemorySensorPriority(sensor.Name))
            .FirstOrDefault();

        var freeSensor = memorySensors
            .Where(sensor => sensor.Name.Contains("free", StringComparison.OrdinalIgnoreCase) ||
                             sensor.Name.Contains("available", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(sensor => GetGpuMemorySensorPriority(sensor.Name))
            .FirstOrDefault();

        if (usedSensor is not null && freeSensor is not null)
        {
            return ConvertGpuMemorySensorToBytes(usedSensor) + ConvertGpuMemorySensorToBytes(freeSensor);
        }

        return 0;
    }

    private static bool IsGpuMemorySensorName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("vram", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetGpuMemorySensorPriority(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("dedicated", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (name.Contains("gpu memory", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (name.Contains("d3d", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static ulong ConvertGpuMemorySensorToBytes(ISensor sensor)
    {
        if (!sensor.Value.HasValue)
        {
            return 0;
        }

        const double mib = 1024d * 1024d;
        const double gib = 1024d * mib;

        return sensor.SensorType switch
        {
            SensorType.Data => (ulong)Math.Round(sensor.Value.Value * gib),
            SensorType.SmallData => (ulong)Math.Round(sensor.Value.Value * mib),
            _ => 0
        };
    }

    private static HardwareMemoryInfo LoadMemory()
    {
        ulong totalBytes = 0;
        var modules = 0;
        var effectiveSpeed = 0u;
        var type = "--";
        var casLatency = 0u;
        var configuredVoltageMv = 0u;
        var timing = "--";
        var capacities = new List<ulong>();
        var smBiosMemory = LoadMemoryFromLibreHardwareMonitor();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject system in searcher.Get())
            {
                totalBytes = ReadULong(system, "TotalPhysicalMemory");
                break;
            }
        }
        catch
        {
        }

        if (smBiosMemory.ModuleCount > 0)
        {
            modules = smBiosMemory.ModuleCount;
        }

        if (smBiosMemory.Capacities.Count > 0)
        {
            capacities = smBiosMemory.Capacities;
        }

        if (smBiosMemory.ConfiguredSpeed > 0)
        {
            effectiveSpeed = smBiosMemory.ConfiguredSpeed;
        }

        if (!string.IsNullOrWhiteSpace(smBiosMemory.Type) && smBiosMemory.Type != "--")
        {
            type = smBiosMemory.Type;
        }

        if (smBiosMemory.ConfiguredVoltageMv > 0)
        {
            configuredVoltageMv = smBiosMemory.ConfiguredVoltageMv;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, ConfiguredVoltage, CASLatency, PartNumber, Manufacturer FROM Win32_PhysicalMemory");
            foreach (ManagementObject module in searcher.Get())
            {
                if (smBiosMemory.ModuleCount == 0)
                {
                    modules++;
                }
                var configuredClockSpeed = ReadUInt(module, "ConfiguredClockSpeed");
                var moduleSpeed = configuredClockSpeed > 0 ? configuredClockSpeed : ReadUInt(module, "Speed");
                if (effectiveSpeed == 0)
                {
                    effectiveSpeed = Math.Max(effectiveSpeed, moduleSpeed);
                }
                casLatency = Math.Max(casLatency, ReadUInt(module, "CASLatency"));
                if (configuredVoltageMv == 0)
                {
                    configuredVoltageMv = Math.Max(configuredVoltageMv, ReadUInt(module, "ConfiguredVoltage"));
                }

                var partNumber = ReadString(module, "PartNumber");
                if (partNumber != "--")
                {
                    smBiosMemory.IdentityHints.Add(partNumber);
                }

                var manufacturer = ReadString(module, "Manufacturer");
                if (manufacturer != "--")
                {
                    smBiosMemory.IdentityHints.Add(manufacturer);
                }

                var capacity = ReadULong(module, "Capacity");
                if (smBiosMemory.Capacities.Count == 0 && capacity > 0)
                {
                    capacities.Add(capacity);
                }

                var memoryType = ReadUInt(module, "SMBIOSMemoryType");
                if (type == "--")
                {
                    type = MapMemoryType(memoryType);
                }
            }
        }
        catch
        {
        }

        if (casLatency > 0)
        {
            timing = $"CL{casLatency}";
        }
        else
        {
            timing = DetectMemoryTiming(smBiosMemory.IdentityHints);
        }

        return new HardwareMemoryInfo
        {
            Total = totalBytes > 0 ? FormatBytes(totalBytes) : "--",
            ModuleCount = modules > 0 ? modules.ToString(CultureInfo.InvariantCulture) : "--",
            Layout = FormatMemoryLayout(capacities),
            Type = type,
            Speed = effectiveSpeed > 0 ? $"{effectiveSpeed} MT/s" : "--",
            Timing = timing,
            Voltage = configuredVoltageMv > 0 ? $"{configuredVoltageMv / 1000d:F2} V" : "--"
        };
    }

    private static MemorySummary LoadMemoryFromLibreHardwareMonitor()
    {
        var result = new MemorySummary();

        try
        {
            var smBios = new SMBios();
            foreach (var device in smBios.MemoryDevices)
            {
                var size = ConvertMemoryDeviceSizeToBytes((int)device.Size);
                if (size == 0)
                {
                    continue;
                }

                result.Capacities.Add(size);
                result.ModuleCount++;
                result.ConfiguredSpeed = Math.Max(result.ConfiguredSpeed, Math.Max((uint)device.ConfiguredSpeed, (uint)device.Speed));
                result.ConfiguredVoltageMv = Math.Max(result.ConfiguredVoltageMv, (uint)device.ConfiguredVoltage);

                var deviceType = NormalizeWhitespace(device.Type.ToString());
                if (result.Type == "--" && !string.IsNullOrWhiteSpace(deviceType) && !deviceType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    result.Type = deviceType;
                }

                var partNumber = ReadLibrePropertyString(device, "PartNumber", "PartNumber2");
                if (partNumber != "--")
                {
                    result.IdentityHints.Add(partNumber);
                }

                var manufacturer = ReadLibrePropertyString(device, "Manufacturer", "ManufacturerName");
                if (manufacturer != "--")
                {
                    result.IdentityHints.Add(manufacturer);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static ulong ConvertMemoryDeviceSizeToBytes(int size)
    {
        if (size <= 0 || size == 0xFFFF)
        {
            return 0;
        }

        const ulong mib = 1024UL * 1024UL;
        return (ulong)size * mib;
    }

    private static string DetectVendor(string manufacturer, string name, string fallback)
    {
        var combined = $"{manufacturer} {name}";
        if (combined.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return "NVIDIA";
        }

        if (combined.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("ATI", StringComparison.OrdinalIgnoreCase))
        {
            return "AMD";
        }

        if (combined.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        return fallback;
    }

    private static string NormalizeCpuName(string value)
    {
        value = NormalizeWhitespace(value);
        value = value.Replace("(R)", string.Empty, StringComparison.OrdinalIgnoreCase);
        value = value.Replace("(TM)", string.Empty, StringComparison.OrdinalIgnoreCase);
        value = value.Replace("CPU", string.Empty, StringComparison.OrdinalIgnoreCase);
        return NormalizeWhitespace(value);
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "--";
        }

        return string.Join(" ", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ReadLibrePropertyString(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var property = source.GetType().GetProperty(propertyName);
                var rawValue = property?.GetValue(source)?.ToString();
                var normalized = NormalizeWhitespace(rawValue ?? "--");
                if (normalized != "--")
                {
                    return normalized;
                }
            }
            catch
            {
            }
        }

        return "--";
    }

    private static string DetectMemoryTiming(IEnumerable<string> identityHints)
    {
        foreach (var hint in identityHints.Where(value => !string.IsNullOrWhiteSpace(value) && value != "--"))
        {
            var clMatch = Regex.Match(hint, @"(?:CL|C)(\d{1,2})(?!\d)", RegexOptions.IgnoreCase);
            if (clMatch.Success && uint.TryParse(clMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var casLatency))
            {
                return $"CL{casLatency}";
            }

            var timingMatch = Regex.Match(hint, @"(\d{2})-(\d{2})-(\d{2,2})(?:-(\d{2,2}))?");
            if (timingMatch.Success && uint.TryParse(timingMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out casLatency))
            {
                return $"CL{casLatency}";
            }
        }

        return "--";
    }

    private static string ReadString(ManagementObject obj, string propertyName)
    {
        try
        {
            return NormalizeWhitespace(obj[propertyName]?.ToString() ?? "--");
        }
        catch
        {
            return "--";
        }
    }

    private static uint ReadUInt(ManagementObject obj, string propertyName)
    {
        try
        {
            return Convert.ToUInt32(obj[propertyName] ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static ulong ReadULong(ManagementObject obj, string propertyName)
    {
        try
        {
            return Convert.ToUInt64(obj[propertyName] ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gib = 1024d * 1024d * 1024d;
        return $"{bytes / gib:F0} GB";
    }

    private static string FormatMemoryLayout(IEnumerable<ulong> capacities)
    {
        var moduleGroups = capacities
            .Where(capacity => capacity > 0)
            .GroupBy(capacity => capacity)
            .OrderByDescending(group => group.Key)
            .ToList();

        if (moduleGroups.Count == 0)
        {
            return "--";
        }

        var parts = moduleGroups.Select(group => $"{group.Count()} x {FormatBytes(group.Key)}");
        return string.Join(" + ", parts);
    }

    private static bool IsGpuHardwareType(HardwareType hardwareType)
    {
        var name = hardwareType.ToString();
        return name.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapMemoryType(uint value)
    {
        return value switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            34 => "DDR5",
            _ => "--"
        };
    }

    private sealed class MemorySummary
    {
        public int ModuleCount { get; set; }
        public List<ulong> Capacities { get; } = new();
        public uint ConfiguredSpeed { get; set; }
        public uint ConfiguredVoltageMv { get; set; }
        public string Type { get; set; } = "--";
        public HashSet<string> IdentityHints { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class HardwareOverview
{
    public HardwareBoardInfo Board { get; init; } = new();
    public HardwareProcessorInfo Cpu { get; init; } = new();
    public HardwareGraphicsInfo Gpu { get; init; } = new();
    public HardwareMemoryInfo Memory { get; init; } = new();
}

public sealed class HardwareBoardInfo
{
    public string Manufacturer { get; init; } = "--";
    public string Model { get; init; } = "--";
    public string BiosVersion { get; init; } = "--";
}

public sealed class HardwareProcessorInfo
{
    public string Name { get; init; } = "--";
    public string Vendor { get; init; } = "CPU";
    public string Cores { get; init; } = "--";
    public string Threads { get; init; } = "--";
    public string BoostClock { get; init; } = "--";
}

public sealed class HardwareGraphicsInfo
{
    public string Name { get; init; } = "--";
    public string Vendor { get; init; } = "GPU";
    public string Vram { get; init; } = "--";
    public ulong SortValue { get; init; }
}

public sealed class HardwareMemoryInfo
{
    public string Total { get; init; } = "--";
    public string ModuleCount { get; init; } = "--";
    public string Layout { get; init; } = "--";
    public string Type { get; init; } = "--";
    public string Speed { get; init; } = "--";
    public string Timing { get; init; } = "--";
    public string Voltage { get; init; } = "--";
}
