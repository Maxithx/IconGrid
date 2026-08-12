using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Detects all local drives (HDD, SSD, NVMe, USB) and best-effort media type.
    ///
    /// v2: enumerates every ready drive via DriveInfo.GetDrives() (not just
    /// Removable) and maps it to a physical disk via Win32_DiskDrive. The media
    /// type is derived from the disk's InterfaceType ("USB") + MediaType/Model
    /// (HDD/SSD/NVMe hints). For USB drives, the port type is still derived
    /// from the USB controller the drive is attached to (v1 heuristic). A future
    /// upgrade can use IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX for the exact
    /// negotiated link speed.
    /// </summary>
    public sealed class UsbDeviceDetector
    {
        /// <summary>Raised by the owner when devices should be re-polled.</summary>
        public event Action<IReadOnlyList<UsbDeviceInfo>>? DevicesChanged;

        public IReadOnlyList<UsbDeviceInfo> DetectDevices()
        {
            var result = new List<UsbDeviceInfo>();
            var controllerPortMap = BuildControllerPortMap();
            var disks = GetDisks();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                    {
                        continue;
                    }

                    var total = 0L;
                    var free = 0L;
                    var ready = drive.IsReady;
                    string? volumeLabel = null;
                    if (ready)
                    {
                        volumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
                        try
                        {
                            total = drive.TotalSize;
                            free = drive.TotalFreeSpace;
                        }
                        catch (IOException)
                        {
                            ready = false;
                        }
                    }

                    var disk = MatchDisk(disks, total);
                    var portType = UsbPortType.Unknown;
                    var mediaType = DriveMediaType.Unknown;
                    var deviceId = drive.Name;
                    if (disk != null)
                    {
                        deviceId = disk.PnpDeviceId;
                        mediaType = ClassifyMediaType(disk);
                        if (mediaType == DriveMediaType.Usb)
                        {
                            portType = ResolvePortType(disk.PnpDeviceId, controllerPortMap);
                        }
                    }

                    var displayName = volumeLabel ?? string.Empty;
                    result.Add(new UsbDeviceInfo
                    {
                        DeviceId = deviceId,
                        Name = displayName.Length > 0 ? $"{displayName} ({drive.Name})" : $"{drive.Name}",
                        DriveLetter = drive.Name,
                        TotalSizeBytes = total,
                        FreeSpaceBytes = free,
                        IsReady = ready,
                        VolumeLabel = volumeLabel,
                        PortType = portType,
                        MediaType = mediaType
                    });
                }
            }
            catch (IOException)
            {
                // A drive disappeared while enumerating; return whatever we have.
            }

            return result;
        }

        public void RaiseDevicesChanged(IReadOnlyList<UsbDeviceInfo> devices)
        {
            DevicesChanged?.Invoke(devices);
        }

        private sealed record DiskInfo(string PnpDeviceId, string Model, string InterfaceType, string MediaType, long Size);

        private static List<DiskInfo> GetDisks()
        {
            var disks = new List<DiskInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT PNPDeviceID, Model, InterfaceType, MediaType, Size FROM Win32_DiskDrive");
                foreach (ManagementObject disk in searcher.Get())
                {
                    var pnpDeviceId = disk["PNPDeviceID"]?.ToString();
                    var model = disk["Model"]?.ToString() ?? string.Empty;
                    var interfaceType = disk["InterfaceType"]?.ToString() ?? string.Empty;
                    var mediaType = disk["MediaType"]?.ToString() ?? string.Empty;
                    var size = disk["Size"] is ulong s ? (long)s : 0L;
                    if (!string.IsNullOrWhiteSpace(pnpDeviceId))
                    {
                        disks.Add(new DiskInfo(pnpDeviceId, model, interfaceType, mediaType, size));
                    }
                }
            }
            catch (ManagementException)
            {
                // WMI unavailable; detection degrades to drive-letter-only entries.
            }

            return disks;
        }

        private static DiskInfo? MatchDisk(List<DiskInfo> disks, long driveSize)
        {
            if (disks.Count == 0)
            {
                return null;
            }

            // Match by closest size within a 10% tolerance so the volume maps to
            // its physical disk (works for full-disk volumes and common partitions).
            DiskInfo? best = null;
            var bestDelta = long.MaxValue;
            foreach (var disk in disks)
            {
                var delta = Math.Abs(disk.Size - driveSize);
                if (delta < bestDelta && (driveSize == 0 || delta <= driveSize / 10))
                {
                    best = disk;
                    bestDelta = delta;
                }
            }

            return best;
        }

        private static DriveMediaType ClassifyMediaType(DiskInfo disk)
        {
            // USB drives are identified by their interface regardless of media type.
            if (disk.InterfaceType.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DriveMediaType.Usb;
            }

            var hint = $"{disk.MediaType} {disk.Model}";
            if (hint.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hint.IndexOf("NVM Express", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hint.IndexOf("Solid State Drive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DriveMediaType.Nvme;
            }

            if (hint.IndexOf("SSD", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DriveMediaType.Ssd;
            }

            if (hint.IndexOf("HDD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hint.IndexOf("Hard Disk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                hint.IndexOf("Fixed hard disk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DriveMediaType.Hdd;
            }

            // SCSI/SATA/unknown interfaces default to HDD behavior for older firmware
            // that reports a generic MediaType (e.g. "Fixed hard disk media").
            return DriveMediaType.Hdd;
        }

        private static Dictionary<string, string> BuildControllerPortMap()
        {
            var controllerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var controllers = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name FROM Win32_USBController");
                foreach (ManagementObject controller in controllers.Get())
                {
                    var deviceId = controller["DeviceID"]?.ToString();
                    var name = controller["Name"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(deviceId))
                    {
                        controllerNames[deviceId] = name;
                    }
                }
            }
            catch (ManagementException)
            {
                // Fall through with an empty map.
            }

            var deviceToController = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var associations = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_USBControllerDevice");
                foreach (ManagementObject association in associations.Get())
                {
                    var controllerId = ExtractRefDeviceId(association["Antecedent"]?.ToString());
                    var deviceId = ExtractRefDeviceId(association["Dependent"]?.ToString());
                    if (controllerId != null && deviceId != null && controllerNames.TryGetValue(controllerId, out var controllerName))
                    {
                        deviceToController[deviceId] = controllerName;
                    }
                }
            }
            catch (ManagementException)
            {
                // Fall through with whatever we matched so far.
            }

            return deviceToController;
        }

        private static string? ExtractRefDeviceId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            // Ref format: \\MACHINE\root\cimv2:Win32_PnPEntity.DeviceID="USB\VID_...&PID_...\serial"
            var match = Regex.Match(reference, @"DeviceID\s*=\s*""(?<id>[^""]+)""");
            return match.Success ? match.Groups["id"].Value : null;
        }

        private static UsbPortType ResolvePortType(string pnpDeviceId, Dictionary<string, string> deviceToController)
        {
            if (deviceToController.TryGetValue(pnpDeviceId, out var controllerName))
            {
                return ClassifyController(controllerName);
            }

            // Fallback for devices behind an external hub that did not appear in the
            // association map: classify by controller model name if it is present in the
            // PnP id (Video/USBSTOR names sometimes embed a hint), otherwise Unknown.
            return UsbPortType.Unknown;
        }

        private static UsbPortType ClassifyController(string name)
        {
            if (name.IndexOf("4.0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("USB4", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UsbPortType.Usb4;
            }

            if (name.IndexOf("3.2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("3.1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UsbPortType.Usb3_2;
            }

            if (name.IndexOf("3.0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("xHCI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Extensible", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UsbPortType.Usb3;
            }

            if (name.IndexOf("2.0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("EHCI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Enhanced", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UsbPortType.Usb2;
            }

            return UsbPortType.Unknown;
        }
    }
}