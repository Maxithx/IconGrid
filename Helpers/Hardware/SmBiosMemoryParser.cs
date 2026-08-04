using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Parses SMBIOS Type 17 (Memory Device) entries via GetSystemFirmwareTable("RSMB")
/// to enumerate every physical RAM slot on the motherboard, including empty ones.
/// SMBIOS is the most accurate source for slot names (DIMM_A1, DIMM_B1, ...),
/// which regular Win32 APIs cannot provide.
/// </summary>
public static class SmBiosMemoryParser
{
    /// <summary>One physical RAM slot reported by the SMBIOS firmware table.</summary>
    public sealed class MemorySlotInfo
    {
        public string DeviceLocator { get; init; } = "--";
        public string BankLocator { get; init; } = "--";
        public ulong SizeBytes { get; init; }
        public bool IsOccupied => SizeBytes > 0;
    }

    private const uint FirmwareTableProviderSignature = 0x52534D42; // 'RSMB'
    private const byte MemoryDeviceType = 17;

    private const int SmBiosHeaderLength = 8; // RawSMBIOSData: 4x BYTE + DWORD Length

    // Type 17 field offsets (from the start of the structure) — SMBIOS 2.1+
    private const int SizeOffset = 0x0C;          // WORD: 0 = empty, bit 15 set = KB, else MB
    private const int DeviceLocatorOffset = 0x10; // BYTE: string reference
    private const int BankLocatorOffset = 0x11;   // BYTE: string reference

    private const short SizeUnknownCapped = 0x7FFF; // 32767 MB sentinel -> use Extended Size field

    /// <summary>
    /// Reads all physical memory slots from the SMBIOS Type 17 structures.
    /// Returns an empty list if the firmware table cannot be read.
    /// </summary>
    public static IReadOnlyList<MemorySlotInfo> ReadMemorySlots()
    {
        var slots = new List<MemorySlotInfo>();

        try
        {
            var tableSize = GetSystemFirmwareTable(FirmwareTableProviderSignature, 0, IntPtr.Zero, 0);
            if (tableSize <= 0)
            {
                return slots;
            }

            var buffer = new byte[tableSize];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var written = GetSystemFirmwareTable(FirmwareTableProviderSignature, 0, handle.AddrOfPinnedObject(), tableSize);
                if (written == 0)
                {
                    return slots;
                }

                ParseRawSmBios(buffer, (int)written, slots);
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            // SMBIOS access is best-effort; callers fall back to other sources.
        }

        return slots;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemFirmwareTable(
        uint firmwareTableProviderSignature,
        uint firmwareTableID,
        IntPtr pFirmwareTableBuffer,
        uint bufferSize);

    private static void ParseRawSmBios(byte[] buffer, int length, List<MemorySlotInfo> slots)
    {
        if (length < SmBiosHeaderLength)
        {
            return;
        }

        var biosLength = (int)BitConverter.ToUInt32(buffer, 4);
        var dataStart = SmBiosHeaderLength;
        var dataEnd = Math.Min(length, dataStart + biosLength);
        var offset = dataStart;

        while (offset + 4 <= dataEnd)
        {
            var type = buffer[offset];
            var structureLength = buffer[offset + 1];
            if (structureLength < 4 || offset + structureLength > dataEnd)
            {
                break;
            }

            if (type == MemoryDeviceType)
            {
                var slot = ReadMemoryDevice(buffer, offset, structureLength, dataEnd);
                if (slot is not null)
                {
                    slots.Add(slot);
                }
            }

            offset += structureLength;
            offset += FindStringBlockLength(buffer, offset, dataEnd);
        }
    }

    private static MemorySlotInfo? ReadMemoryDevice(byte[] buffer, int structureStart, int structureLength, int dataEnd)
    {
        if (structureLength <= BankLocatorOffset)
        {
            return null;
        }

        var deviceLocator = ReadStringReference(buffer, structureStart, structureLength, dataEnd, buffer[structureStart + DeviceLocatorOffset]);
        var bankLocator = ReadStringReference(buffer, structureStart, structureLength, dataEnd, buffer[structureStart + BankLocatorOffset]);
        var sizeBytes = ReadDeviceSizeBytes(buffer, structureStart);

        return new MemorySlotInfo
        {
            DeviceLocator = deviceLocator,
            BankLocator = bankLocator,
            SizeBytes = sizeBytes
        };
    }

    private static ulong ReadDeviceSizeBytes(byte[] buffer, int structureStart)
    {
        var rawSize = BitConverter.ToInt16(buffer, structureStart + SizeOffset);
        if (rawSize <= 0 || rawSize == SizeUnknownCapped)
        {
            return 0;
        }

        // SMBIOS: if bit 15 is set the value is in KB (e.g. removable modules on some boards),
        // otherwise the value is in MB.
        var sizeInKb = (rawSize & 0x8000) != 0
            ? (ulong)(rawSize & 0x7FFF)
            : (ulong)rawSize * 1024UL;

        return sizeInKb * 1024UL;
    }

    private static string ReadStringReference(byte[] buffer, int structureStart, int structureLength, int dataEnd, byte stringIndex)
    {
        if (stringIndex == 0)
        {
            return "--";
        }

        var cursor = structureStart + structureLength;
        var currentIndex = 1;
        while (cursor < dataEnd && currentIndex <= stringIndex)
        {
            if (buffer[cursor] == 0)
            {
                cursor++;
                continue;
            }

            var start = cursor;
            while (cursor < dataEnd && buffer[cursor] != 0)
            {
                cursor++;
            }

            if (currentIndex == stringIndex)
            {
                var text = Encoding.ASCII.GetString(buffer, start, cursor - start).Trim();
                return string.IsNullOrWhiteSpace(text) ? "--" : text;
            }

            currentIndex++;
            cursor++; // skip null terminator
        }

        return "--";
    }

    private static int FindStringBlockLength(byte[] buffer, int offset, int dataEnd)
    {
        // Strings are null-terminated; the whole block ends with an extra null byte (double null).
        var index = offset;
        var previousWasNull = false;
        while (index < dataEnd)
        {
            var isNull = buffer[index] == 0;
            if (previousWasNull && isNull)
            {
                return index + 1 - offset;
            }

            previousWasNull = isNull;
            index++;
        }

        return dataEnd - offset;
    }
}