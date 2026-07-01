using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGrid.Helpers;

internal static class IconResourceUpdater
{
    private const int RT_ICON = 3;
    private const int RT_GROUP_ICON = 14;
    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExisting);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static void TryUpdateApplicationIcon(BitmapSource? source)
    {
        if (source == null)
            return;

        using var icon = DynamicIconHelper.CreateIcon(source);
        if (icon == null)
            return;

        try
        {
            var iconData = IconToBytes(icon);
            if (iconData == null || iconData.Length == 0)
                return;

            UpdateIconResources(iconData);
        }
        finally
        {
            icon.Dispose();
        }
    }

    private static byte[]? IconToBytes(System.Drawing.Icon icon)
    {
        try
        {
            using var ms = new MemoryStream();
            icon.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static void UpdateIconResources(byte[] iconData)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        var images = ParseIconFile(iconData);
        if (images == null || images.Count == 0)
            return;

        var hUpdate = BeginUpdateResource(exePath, false);
        if (hUpdate == IntPtr.Zero)
            return;

        try
        {
            for (int i = 0; i < images.Count; i++)
            {
                var result = UpdateResource(hUpdate, new IntPtr(RT_ICON), new IntPtr(i + 1), 0, images[i].Data, (uint)images[i].Data.Length);
                if (!result)
                    return;
            }

            var groupBytes = BuildGroupIconData(images);
            UpdateResource(hUpdate, new IntPtr(RT_GROUP_ICON), new IntPtr(1), 0, groupBytes, (uint)groupBytes.Length);
        }
        finally
        {
            EndUpdateResource(hUpdate, false);
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static byte[] BuildGroupIconData(IReadOnlyList<IconImage> images)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type
        writer.Write((ushort)images.Count); // count

        for (int i = 0; i < images.Count; i++)
        {
            var entry = images[i];
            writer.Write(entry.Width);
            writer.Write(entry.Height);
            writer.Write(entry.ColorCount);
            writer.Write(entry.Reserved);
            writer.Write(entry.Planes);
            writer.Write(entry.BitCount);
            writer.Write(entry.BytesInRes);
            writer.Write((ushort)(i + 1));
        }

        return ms.ToArray();
    }

    private static IReadOnlyList<IconImage>? ParseIconFile(byte[] data)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            reader.ReadUInt16(); // reserved
            reader.ReadUInt16(); // type
            var count = reader.ReadUInt16();
            if (count == 0)
                return null;

            var entries = new List<IconDirEntry>(count);
            for (int i = 0; i < count; i++)
            {
                entries.Add(new IconDirEntry
                {
                    Width = reader.ReadByte(),
                    Height = reader.ReadByte(),
                    ColorCount = reader.ReadByte(),
                    Reserved = reader.ReadByte(),
                    Planes = reader.ReadUInt16(),
                    BitCount = reader.ReadUInt16(),
                    BytesInRes = reader.ReadUInt32(),
                    ImageOffset = reader.ReadUInt32()
                });
            }

            var images = new List<IconImage>(count);
            foreach (var entry in entries)
            {
                reader.BaseStream.Seek(entry.ImageOffset, SeekOrigin.Begin);
                var raw = reader.ReadBytes((int)entry.BytesInRes);
                images.Add(new IconImage(entry, raw));
            }

            return images;
        }
        catch
        {
            return null;
        }
    }

    private sealed record IconImage(byte Width, byte Height, byte ColorCount, byte Reserved, ushort Planes, ushort BitCount, uint BytesInRes, byte[] Data)
    {
        public IconImage(IconDirEntry entry, byte[] data) : this(entry.Width, entry.Height, entry.ColorCount, entry.Reserved, entry.Planes, entry.BitCount, entry.BytesInRes, data)
        {
        }
    }

    private record struct IconDirEntry
    {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint BytesInRes;
        public uint ImageOffset;
    }
}
