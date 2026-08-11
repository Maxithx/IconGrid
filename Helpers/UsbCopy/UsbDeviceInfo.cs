using System;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Represents the physical USB connection speed / capability of a port.
    /// </summary>
    public enum UsbPortType
    {
        Unknown = 0,
        Usb2 = 1,
        Usb3 = 2,
        Usb3_2 = 3,
        Usb4 = 4
    }

    /// <summary>
    /// Describes a removable USB drive detected by the system.
    /// </summary>
    public sealed class UsbDeviceInfo
    {
        public required string DeviceId { get; init; }

        public required string Name { get; init; }

        public required string DriveLetter { get; init; }

        public long TotalSizeBytes { get; init; }

        public long FreeSpaceBytes { get; init; }

        public bool IsReady { get; init; }

        public string? VolumeLabel { get; init; }

        public UsbPortType PortType { get; init; }

        public string PortTypeDisplay => PortType switch
        {
            UsbPortType.Usb2 => "USB 2.0",
            UsbPortType.Usb3 => "USB 3.0",
            UsbPortType.Usb3_2 => "USB 3.2",
            UsbPortType.Usb4 => "USB 4.0",
            _ => "Unknown"
        };

        public string CapacityDisplay => FormatBytes(TotalSizeBytes);

        public string FreeSpaceDisplay => FormatBytes(FreeSpaceBytes);

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:0.##} {units[unitIndex]}";
        }
    }
}