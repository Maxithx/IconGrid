using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IconGrid.Helpers.Logging;
using Microsoft.Win32.SafeHandles;

namespace IconGrid.Helpers.Hardware.Storage;

/// <summary>
/// Issues ATA pass-through commands to a SATA disk.
///
/// CHECK POWER MODE (0xE5) does not spin a disk up, so it is safe to poll.
/// IDENTIFY DEVICE (0xEC) does wake a standby disk, so callers must cache its result.
/// Both require administrator rights; <c>Unknown</c>/<c>null</c> is returned when the
/// handle cannot be opened or the controller/USB bridge blocks ATA pass-through.
/// </summary>
public static class AtaDriveReader
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    // CTL_CODE(IOCTL_SCSI_BASE (0x4), 0x040C/0x040B, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS)
    private const uint IoctlAtaPassThroughDirect = 0x0004D030;
    private const uint IoctlAtaPassThrough = 0x0004D02C;

    private const ushort AtaFlagDrdyRequired = 0x01;
    private const ushort AtaFlagDataIn = 0x02;
    private const byte AtaCheckPowerModeCommand = 0xE5;
    private const byte AtaIdentifyDeviceCommand = 0xEC;
    private const byte DeviceHeadMaster = 0xA0;
    private const int IdentifyBufferSize = 512;

    private static readonly object FailureLogGate = new();
    private static readonly HashSet<string> LoggedFailures = new();
    private static bool _volumePrivilegeEnabled;

    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const uint ErrorNotAllAssigned = 1306;

    private static void EnsureVolumePrivilege()
    {
        if (_volumePrivilegeEnabled)
        {
            return;
        }

        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            {
                LogOnce($"OpenProcessToken failed (win32={Marshal.GetLastWin32Error()})");
                return;
            }

            try
            {
                if (!LookupPrivilegeValue(null, "SeManageVolumePrivilege", out var luid))
                {
                    LogOnce($"LookupPrivilegeValue failed (win32={Marshal.GetLastWin32Error()})");
                    return;
                }

                var privileges = new TokenPrivileges
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SePrivilegeEnabled
                };

                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    LogOnce($"AdjustTokenPrivileges failed (win32={Marshal.GetLastWin32Error()})");
                    return;
                }

                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNotAllAssigned)
                {
                    LogOnce("SeManageVolumePrivilege was not assigned to this process");
                    return;
                }

                _volumePrivilegeEnabled = true;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        catch
        {
        }
    }

    public static string ReadPowerState(int diskIndex)
    {
        if (!TryExecute(diskIndex, AtaCheckPowerModeCommand, 0, AtaFlagDrdyRequired, out var taskFile, out _))
        {
            return DiskPowerStateCodes.Unknown;
        }

        return taskFile[1] switch
        {
            0xFF => DiskPowerStateCodes.Active,
            0x80 => DiskPowerStateCodes.Idle,
            0x40 => DiskPowerStateCodes.Idle, // Legacy "active or idle"
            0x00 => DiskPowerStateCodes.Standby,
            _ => DiskPowerStateCodes.Unknown
        };
    }

    /// <summary>
    /// Reads ATA IDENTIFY DEVICE word 217 (nominal media rotation rate).
    /// Returns 1 for non-rotating media, the RPM value for rotating media, or null when
    /// the drive does not report it. This command can wake a sleeping disk.
    /// </summary>
    public static int? ReadNominalRotationRate(int diskIndex)
    {
        if (!TryExecute(diskIndex, AtaIdentifyDeviceCommand, IdentifyBufferSize, AtaFlagDrdyRequired | AtaFlagDataIn, out _, out var data))
        {
            return null;
        }

        if (data == null || data.Length < IdentifyBufferSize)
        {
            return null;
        }

        var word217 = data[434] | (data[435] << 8);
        if (word217 == 1)
        {
            return 1;
        }

        return word217 is >= 0x0401 and <= 0xFFFE ? word217 : null;
    }

    private static bool TryExecute(
        int diskIndex,
        byte command,
        uint dataTransferLength,
        ushort ataFlags,
        out byte[] taskFile,
        out byte[]? dataOut)
    {
        if (TryExecuteDirect(diskIndex, command, dataTransferLength, ataFlags, out taskFile, out dataOut))
        {
            return true;
        }

        return TryExecuteBuffered(diskIndex, command, dataTransferLength, ataFlags, out taskFile, out dataOut);
    }

    private static SafeFileHandle OpenDrive(int diskIndex)
    {
        EnsureVolumePrivilege();

        var path = $@"\\.\PhysicalDrive{diskIndex}";
        var handle = CreateFile(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            LogOnce($"CreateFile {path} failed (win32={error})");
        }

        return handle;
    }

    private static bool TryExecuteDirect(
        int diskIndex,
        byte command,
        uint dataTransferLength,
        ushort ataFlags,
        out byte[] taskFile,
        out byte[]? dataOut)
    {
        taskFile = new byte[8];
        dataOut = null;

        var handle = OpenDrive(diskIndex);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        IntPtr dataBuffer = IntPtr.Zero;
        try
        {
            var size = Marshal.SizeOf<AtaPassThroughDirect>();
            var request = new AtaPassThroughDirect
            {
                Length = (ushort)size,
                AtaFlags = ataFlags,
                DataTransferLength = dataTransferLength,
                TimeOutValue = 5,
                DataBuffer = IntPtr.Zero,
                PreviousTaskFile = new byte[8],
                CurrentTaskFile = new byte[8]
            };

            request.CurrentTaskFile[5] = DeviceHeadMaster;
            request.CurrentTaskFile[6] = command;

            if (dataTransferLength > 0)
            {
                dataBuffer = AllocateZeroed((int)dataTransferLength);
                request.DataBuffer = dataBuffer;
            }

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(request, buffer, false);

                if (!DeviceIoControl(handle, IoctlAtaPassThroughDirect, buffer, (uint)size, buffer, (uint)size, out _, IntPtr.Zero))
                {
                    LogOnce($"DIRECT cmd=0x{command:X2} disk={diskIndex} win32={Marshal.GetLastWin32Error()}");
                    return false;
                }

                var response = Marshal.PtrToStructure<AtaPassThroughDirect>(buffer);
                taskFile = response.CurrentTaskFile ?? new byte[8];

                if (dataTransferLength > 0 && dataBuffer != IntPtr.Zero)
                {
                    dataOut = new byte[dataTransferLength];
                    Marshal.Copy(dataBuffer, dataOut, 0, (int)dataTransferLength);
                }

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (dataBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataBuffer);
            }

            handle.Dispose();
        }
    }


    /// <summary>
    /// Buffered variant. For METHOD_BUFFERED ATA pass-through the data buffer must be part
    /// of the same allocation as the ATA_PASS_THROUGH_EX structure.
    /// </summary>
    private static bool TryExecuteBuffered(
        int diskIndex,
        byte command,
        uint dataTransferLength,
        ushort ataFlags,
        out byte[] taskFile,
        out byte[]? dataOut)
    {
        taskFile = new byte[8];
        dataOut = null;

        var handle = OpenDrive(diskIndex);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        try
        {
            var structSize = Marshal.SizeOf<AtaPassThroughEx>();
            var totalSize = structSize + (int)dataTransferLength;
            var buffer = Marshal.AllocHGlobal(totalSize);
            try
            {
                for (var i = 0; i < totalSize; i++)
                {
                    Marshal.WriteByte(buffer, i, 0);
                }

                var request = new AtaPassThroughEx
                {
                    AtaFlags = ataFlags,
                    DataTransferLength = dataTransferLength,
                    TimeOutValue = 5,
                    DataBuffer = dataTransferLength > 0 ? IntPtr.Add(buffer, structSize) : IntPtr.Zero,
                    PreviousTaskFile = new byte[8],
                    CurrentTaskFile = new byte[8]
                };

                request.CurrentTaskFile[5] = DeviceHeadMaster;
                request.CurrentTaskFile[6] = command;

                Marshal.StructureToPtr(request, buffer, false);

                if (!DeviceIoControl(handle, IoctlAtaPassThrough, buffer, (uint)totalSize, buffer, (uint)totalSize, out _, IntPtr.Zero))
                {
                    LogOnce($"BUFFERED cmd=0x{command:X2} disk={diskIndex} win32={Marshal.GetLastWin32Error()}");
                    return false;
                }

                var response = Marshal.PtrToStructure<AtaPassThroughEx>(buffer);
                taskFile = response.CurrentTaskFile ?? new byte[8];

                if (dataTransferLength > 0)
                {
                    dataOut = new byte[dataTransferLength];
                    Marshal.Copy(IntPtr.Add(buffer, structSize), dataOut, 0, (int)dataTransferLength);
                }

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            handle.Dispose();
        }
    }

    private static IntPtr AllocateZeroed(int size)
    {
        var buffer = Marshal.AllocHGlobal(size);
        for (var i = 0; i < size; i++)
        {
            Marshal.WriteByte(buffer, i, 0);
        }

        return buffer;
    }

    private static void LogOnce(string message)
    {
        lock (FailureLogGate)
        {
            if (!LoggedFailures.Add(message))
            {
                return;
            }
        }

        try
        {
            AppTrace.Write("[AtaDriveReader] " + message);
        }
        catch
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AtaPassThroughDirect
    {
        public ushort Length;
        public ushort AtaFlags;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte ReservedAsUShort;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public IntPtr DataBuffer;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] PreviousTaskFile;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] CurrentTaskFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AtaPassThroughEx
    {
        public ushort AtaFlags;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte ReservedAsUShort;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public IntPtr DataBuffer;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] PreviousTaskFile;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] CurrentTaskFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TokenPrivileges newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        IntPtr inBuffer,
        uint inBufferSize,
        IntPtr outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}

