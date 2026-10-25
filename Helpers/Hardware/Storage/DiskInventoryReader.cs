using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

namespace IconGrid.Helpers.Hardware.Storage;

internal sealed class DiskInventoryEntry
{
    public int Index { get; init; }
    public string Model { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public string Firmware { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string BusType { get; init; } = DiskBusTypes.Unknown;
    public string MediaType { get; init; } = DiskMediaTypeCodes.Unknown;
    public string DriveLetters { get; init; } = string.Empty;
    public bool IsHdd { get; init; }
    public bool IsSata { get; init; }
    public int? RotationRpm { get; init; }
    public string Health { get; init; } = DiskHealthCodes.Unknown;
}

/// <summary>
/// Enumerates physical disks through WMI. This path does not require elevation
/// and never issues ATA commands, so it cannot wake a sleeping disk.
/// </summary>
internal static class DiskInventoryReader
{
    public static IReadOnlyList<DiskInventoryEntry> Read()
    {
        try
        {
            var drives = ReadPhysicalDrives();
            var storage = ReadStorageNamespace();
            var driveLetters = ReadDriveLetters();

            if (drives.Count == 0)
            {
                return storage.Select(info => FromStorageInfo(info, driveLetters)).ToList();
            }

            return drives.Select(drive => Merge(drive, storage, driveLetters)).ToList();
        }
        catch
        {
            return Array.Empty<DiskInventoryEntry>();
        }
    }

    private static DiskInventoryEntry Merge(
        Win32DiskInfo drive,
        List<StorageNamespaceInfo> storage,
        IReadOnlyDictionary<int, string> driveLetters)
    {
        var match = FindStorageMatch(drive, storage);
        var mediaType = match == null ? DiskMediaTypeCodes.Unknown : MapMediaType(match.MediaType);
        var rpm = match?.SpindleSpeed > 1 ? match.SpindleSpeed : (int?)null;
        if (mediaType == DiskMediaTypeCodes.Unknown && rpm.HasValue && rpm.Value > 0)
        {
            mediaType = DiskMediaTypeCodes.Hdd;
        }

        var model = string.IsNullOrWhiteSpace(drive.Model)
            ? match?.FriendlyName ?? string.Empty
            : drive.Model.Trim();

        // Last resort when the OS reports "unspecified" and the spindle speed is missing
        // (older SATA drives, USB bridges, drivers that block ATA IDENTIFY).
        if (mediaType == DiskMediaTypeCodes.Unknown)
        {
            mediaType = DiskModelClassifier.Classify(model) ?? DiskMediaTypeCodes.Unknown;
        }

        var busType = match == null ? MapInterfaceType(drive.InterfaceType) : MapBusType(match.BusType);
        var isHdd = string.Equals(mediaType, DiskMediaTypeCodes.Hdd, StringComparison.Ordinal);
        var isSata = string.Equals(busType, DiskBusTypes.Sata, StringComparison.Ordinal) ||
                     string.Equals(busType, DiskBusTypes.Ata, StringComparison.Ordinal);

        return new DiskInventoryEntry
        {
            Index = drive.Index,
            Model = model,
            Serial = string.IsNullOrWhiteSpace(drive.Serial) ? match?.SerialNumber ?? string.Empty : drive.Serial.Trim(),
            Firmware = string.IsNullOrWhiteSpace(drive.Firmware) ? match?.Firmware ?? string.Empty : drive.Firmware.Trim(),
            SizeBytes = drive.Size > 0 ? drive.Size : match?.Size ?? 0,
            BusType = busType,
            MediaType = mediaType,
            DriveLetters = driveLetters.TryGetValue(drive.Index, out var letters) ? letters : string.Empty,
            IsHdd = isHdd,
            IsSata = isSata,
            RotationRpm = rpm,
            Health = match == null ? MapWin32Status(drive.Status) : MapHealthStatus(match.HealthStatus)
        };
    }

    private static DiskInventoryEntry FromStorageInfo(
        StorageNamespaceInfo info,
        IReadOnlyDictionary<int, string> driveLetters)
    {
        var mediaType = MapMediaType(info.MediaType);
        var rpm = info.SpindleSpeed > 1 ? info.SpindleSpeed : (int?)null;
        if (mediaType == DiskMediaTypeCodes.Unknown && rpm.HasValue)
        {
            mediaType = DiskMediaTypeCodes.Hdd;
        }

        if (mediaType == DiskMediaTypeCodes.Unknown)
        {
            mediaType = DiskModelClassifier.Classify(info.FriendlyName) ?? DiskMediaTypeCodes.Unknown;
        }

        var busType = MapBusType(info.BusType);
        return new DiskInventoryEntry
        {
            Index = info.Index,
            Model = info.FriendlyName,
            Serial = info.SerialNumber,
            Firmware = info.Firmware,
            SizeBytes = info.Size,
            BusType = busType,
            MediaType = mediaType,
            DriveLetters = driveLetters.TryGetValue(info.Index, out var letters) ? letters : string.Empty,
            IsHdd = string.Equals(mediaType, DiskMediaTypeCodes.Hdd, StringComparison.Ordinal),
            IsSata = string.Equals(busType, DiskBusTypes.Sata, StringComparison.Ordinal) ||
                     string.Equals(busType, DiskBusTypes.Ata, StringComparison.Ordinal),
            RotationRpm = rpm,
            Health = MapHealthStatus(info.HealthStatus)
        };
    }

    private static StorageNamespaceInfo? FindStorageMatch(Win32DiskInfo drive, List<StorageNamespaceInfo> storage)
    {
        if (storage.Count == 0)
        {
            return null;
        }

        var serial = Normalize(drive.Serial);
        if (serial.Length > 0)
        {
            var bySerial = storage.FirstOrDefault(s =>
                Normalize(s.SerialNumber).Length > 0 &&
                string.Equals(Normalize(s.SerialNumber), serial, StringComparison.OrdinalIgnoreCase));
            if (bySerial != null)
            {
                return bySerial;
            }
        }

        var model = Normalize(drive.Model);
        return storage.FirstOrDefault(s =>
            s.Size == drive.Size &&
            Normalize(s.FriendlyName).Length > 0 &&
            (Normalize(s.FriendlyName).Contains(model, StringComparison.OrdinalIgnoreCase) ||
             model.Contains(Normalize(s.FriendlyName), StringComparison.OrdinalIgnoreCase)));
    }


    /// <summary>
    /// Maps physical disk numbers to their assigned drive letters (MSFT_Partition.DriveLetter
    /// is zero-based: 0 = A).
    /// </summary>
    private static Dictionary<int, string> ReadDriveLetters()
    {
        var map = new Dictionary<int, List<string>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage", "SELECT DriveLetter, DiskNumber FROM MSFT_Partition");

            foreach (ManagementObject partition in searcher.Get())
            {
                try
                {
                    var value = partition["DriveLetter"];
                    if (value == null)
                    {
                        continue;
                    }

                    // MSFT_Partition.DriveLetter is returned as the character code of the
                    // letter (e.g. 68 = 'D'); 0 means "no drive letter".
                    var rawLetter = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
                    if (rawLetter is < (ushort)'A' or > (ushort)'Z')
                    {
                        continue;
                    }

                    var diskNumber = (int)ReadUInt(partition, "DiskNumber");
                    var letter = ((char)rawLetter).ToString() + ":";

                    if (!map.TryGetValue(diskNumber, out var letters))
                    {
                        letters = new List<string>();
                        map[diskNumber] = letters;
                    }

                    if (!letters.Contains(letter))
                    {
                        letters.Add(letter);
                    }
                }
                catch
                {
                }
                finally
                {
                    partition.Dispose();
                }
            }
        }
        catch
        {
        }

        return map.ToDictionary(pair => pair.Key, pair => string.Join(", ", pair.Value));
    }

    private static List<Win32DiskInfo> ReadPhysicalDrives()
    {
        var result = new List<Win32DiskInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Index, Model, SerialNumber, Size, InterfaceType, MediaType, FirmwareRevision, Status FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get())
            {
                try
                {
                    result.Add(new Win32DiskInfo
                    {
                        Index = (int)ReadUInt(disk, "Index"),
                        Model = ReadString(disk, "Model"),
                        Serial = ReadString(disk, "SerialNumber"),
                        Firmware = ReadString(disk, "FirmwareRevision"),
                        Size = (long)ReadULong(disk, "Size"),
                        InterfaceType = ReadString(disk, "InterfaceType"),
                        MediaType = ReadString(disk, "MediaType"),
                        Status = ReadString(disk, "Status")
                    });
                }
                catch
                {
                    // Skip malformed entries; the remaining disks still render.
                }
                finally
                {
                    disk.Dispose();
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static List<StorageNamespaceInfo> ReadStorageNamespace()
    {
        var result = new List<StorageNamespaceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk");

            var fallbackIndex = 0;
            foreach (ManagementObject disk in searcher.Get())
            {
                try
                {
                    var deviceId = ReadString(disk, "DeviceId");
                    var index = int.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : fallbackIndex;

                    result.Add(new StorageNamespaceInfo
                    {
                        Index = index,
                        FriendlyName = ReadString(disk, "FriendlyName"),
                        SerialNumber = ReadString(disk, "SerialNumber"),
                        Firmware = ReadString(disk, "FirmwareVersion"),
                        Size = (long)ReadULong(disk, "Size"),
                        MediaType = ReadUShort(disk, "MediaType"),
                        BusType = ReadUShort(disk, "BusType"),
                        HealthStatus = ReadUShort(disk, "HealthStatus"),
                        SpindleSpeed = ReadUShort(disk, "SpindleSpeed")
                    });
                    fallbackIndex++;
                }
                catch
                {
                }
                finally
                {
                    disk.Dispose();
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private static string MapBusType(int busType) => busType switch
    {
        3 => DiskBusTypes.Ata,
        7 => DiskBusTypes.Usb,
        8 => DiskBusTypes.Raid,
        10 => DiskBusTypes.Sas,
        11 => DiskBusTypes.Sata,
        17 => DiskBusTypes.Nvme,
        _ => DiskBusTypes.Unknown
    };

    private static string MapMediaType(int mediaType) => mediaType switch
    {
        3 => DiskMediaTypeCodes.Hdd,
        4 => DiskMediaTypeCodes.Ssd,
        _ => DiskMediaTypeCodes.Unknown
    };

    private static string MapHealthStatus(int healthStatus) => healthStatus switch
    {
        0 => DiskHealthCodes.Healthy,
        1 => DiskHealthCodes.Warning,
        2 => DiskHealthCodes.Unhealthy,
        _ => DiskHealthCodes.Unknown
    };

    private static string MapWin32Status(string status) =>
        string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)
            ? DiskHealthCodes.Healthy
            : string.IsNullOrWhiteSpace(status) ? DiskHealthCodes.Unknown : DiskHealthCodes.Warning;

    private static string MapInterfaceType(string interfaceType) => interfaceType switch
    {
        "USB" => DiskBusTypes.Usb,
        "IDE" => DiskBusTypes.Ata,
        _ => DiskBusTypes.Unknown
    };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

    private static string ReadString(ManagementBaseObject source, string property)
    {
        try
        {
            return source[property]?.ToString()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static uint ReadUInt(ManagementBaseObject source, string property)
    {
        try
        {
            var value = source[property];
            return value == null ? 0u : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static ulong ReadULong(ManagementBaseObject source, string property)
    {
        try
        {
            var value = source[property];
            return value == null ? 0UL : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0UL;
        }
    }

    private static ushort ReadUShort(ManagementBaseObject source, string property)
    {
        try
        {
            var value = source[property];
            return value == null ? (ushort)0 : Convert.ToUInt16(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private sealed class Win32DiskInfo
    {
        public int Index { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Serial { get; init; } = string.Empty;
        public string Firmware { get; init; } = string.Empty;
        public long Size { get; init; }
        public string InterfaceType { get; init; } = string.Empty;
        public string MediaType { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class StorageNamespaceInfo
    {
        public int Index { get; init; }
        public string FriendlyName { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string Firmware { get; init; } = string.Empty;
        public long Size { get; init; }
        public int MediaType { get; init; }
        public int BusType { get; init; }
        public int HealthStatus { get; init; }
        public int SpindleSpeed { get; init; }
    }
}

