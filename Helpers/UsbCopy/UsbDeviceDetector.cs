using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Detects removable USB drives and best-effort port type (USB 2.0 / 3.0 / 3.2 / 4.0).
    ///
    /// v1 heuristic: the port type is derived from the USB controller the drive is
    /// attached to (via the Win32_USBControllerDevice association). This reports the
    /// controller capability, not the negotiated link speed. A future upgrade can use
    /// IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX for the exact negotiated speed.
    /// </summary>
    public sealed class UsbDeviceDetector
    {
        /// <summary>Raised by the owner when devices should be re-polled.</summary>
        public event Action<IReadOnlyList<UsbDeviceInfo>>? DevicesChanged;

        public IReadOnlyList<UsbDeviceInfo> DetectDevices()
        {
            var result = new List<UsbDeviceInfo>();
            var controllerPortMap = BuildControllerPortMap();
            var usbDisks = GetUsbDisks();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Removable)
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

                    var disk = MatchUsbDisk(usbDisks, total);
                    var portType = UsbPortType.Unknown;
                    var deviceId = drive.Name;
                    if (disk != null)
                    {
                        deviceId = disk.PnpDeviceId;
                        portType = ResolvePortType(disk.PnpDeviceId, controllerPortMap);
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
                        PortType = portType
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

        private sealed record UsbDiskInfo(string PnpDeviceId, string Model, long Size);

        private static List<UsbDiskInfo> GetUsbDisks()
        {
            var disks = new List<UsbDiskInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT PNPDeviceID, Model, Size FROM Win32_DiskDrive WHERE InterfaceType = 'USB'");
                foreach (ManagementObject disk in searcher.Get())
                {
                    var pnpDeviceId = disk["PNPDeviceID"]?.ToString();
                    var model = disk["Model"]?.ToString() ?? string.Empty;
                    var size = disk["Size"] is ulong s ? (long)s : 0L;
                    if (!string.IsNullOrWhiteSpace(pnpDeviceId))
                    {
                        disks.Add(new UsbDiskInfo(pnpDeviceId, model, size));
                    }
                }
            }
            catch (ManagementException)
            {
                // WMI unavailable; detection degrades to drive-letter-only entries.
            }

            return disks;
        }

        private static UsbDiskInfo? MatchUsbDisk(List<UsbDiskInfo> disks, long driveSize)
        {
            if (disks.Count == 0)
            {
                return null;
            }

            // USB sticks usually expose almost the whole disk as a single volume.
            // Match by closest size within a 10% tolerance.
            UsbDiskInfo? best = null;
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