using System;
using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;

namespace IconGrid.Helpers.Hardware;

internal static class NativeFpsSharedMemory
{
    public const string MapName = @"Local\IconGrid.NativeFps.Live";
    private const int BufferSize = 64;
    private const uint Magic = 0x49474650; // "IGFP"
    private const uint Version = 1;
    private const int SequenceStartOffset = 0;
    private const int MagicOffset = 8;
    private const int VersionOffset = 12;
    private const int CapturedTicksOffset = 16;
    private const int FpsValueOffset = 24;
    private const int TargetPidOffset = 32;
    private const int FlagsOffset = 36;
    private const int SequenceEndOffset = 48;
    private const uint FlagHasFps = 0x1;
    private const uint FlagEtwRunning = 0x2;

    public static NativeFpsLiveSnapshot? TryRead()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, BufferSize, MemoryMappedFileAccess.Read);
            var buffer = new byte[BufferSize];

            for (var attempt = 0; attempt < 3; attempt++)
            {
                accessor.ReadArray(0, buffer, 0, buffer.Length);

                var sequenceStart = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(SequenceStartOffset, sizeof(long)));
                var sequenceEnd = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(SequenceEndOffset, sizeof(long)));
                if (sequenceStart <= 0 || sequenceStart != sequenceEnd || (sequenceStart & 1L) != 0)
                {
                    continue;
                }

                var magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(MagicOffset, sizeof(uint)));
                var version = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(VersionOffset, sizeof(uint)));
                if (magic != Magic || version != Version)
                {
                    return null;
                }

                var capturedTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(CapturedTicksOffset, sizeof(long)));
                var fpsBits = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(FpsValueOffset, sizeof(long)));
                var targetPid = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(TargetPidOffset, sizeof(uint)));
                var flags = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(FlagsOffset, sizeof(uint)));
                var capturedAtUtc = new DateTime(capturedTicks, DateTimeKind.Utc);
                var fpsValue = BitConverter.Int64BitsToDouble(fpsBits);

                return new NativeFpsLiveSnapshot(
                    capturedAtUtc,
                    (flags & FlagHasFps) != 0 ? fpsValue : null,
                    unchecked((int)targetPid),
                    (flags & FlagEtwRunning) != 0);
            }
        }
        catch
        {
        }

        return null;
    }
}

internal readonly record struct NativeFpsLiveSnapshot(
    DateTime CapturedAtUtc,
    double? FpsValue,
    int TargetPid,
    bool EtwRunning);
