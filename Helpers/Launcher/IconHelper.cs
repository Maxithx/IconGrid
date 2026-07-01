using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace IconGrid.Helpers;

    public static class IconHelper
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000; // Large icon
        private const uint ShgfiSysIconIndex = 0x00004000;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeDirectory = 0x00000010;
        private const int HilExtraLarge = 2; // 48x48 (or system-defined)
        private const int HilJumbo = 4;      // 256x256 on most systems
        private const int IldTransparent = 0x00000001;

    /// <summary>
    /// Tries to read a desktop.ini in a folder to locate a custom icon (IconResource/IconFile + IconIndex).
    /// Returns an IconLocation if found; otherwise null.
    /// </summary>
    public static IconLocation? TryGetFolderIconFromDesktopIni(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return null;

            var desktopIni = Path.Combine(folderPath, "desktop.ini");
            if (!File.Exists(desktopIni))
                return null;

            string? iconResource = null;
            string? iconFile = null;
            string? iconIndexStr = null;

            foreach (var line in File.ReadLines(desktopIni))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase))
                {
                    iconResource = trimmed.Substring("IconResource=".Length);
                }
                else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                {
                    iconFile = trimmed.Substring("IconFile=".Length);
                }
                else if (trimmed.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    iconIndexStr = trimmed.Substring("IconIndex=".Length);
                }
            }

            string? chosenPath = null;
            int index = 0;
            bool hasIndex = false;

            if (!string.IsNullOrWhiteSpace(iconResource))
            {
                var parts = iconResource.Split(',', 2, StringSplitOptions.TrimEntries);
                chosenPath = parts[0];
                if (parts.Length > 1 && int.TryParse(parts[1], out var parsedIdx))
                {
                    index = parsedIdx;
                    hasIndex = true;
                }
            }
            else if (!string.IsNullOrWhiteSpace(iconFile))
            {
                chosenPath = iconFile;
                if (!string.IsNullOrWhiteSpace(iconIndexStr) && int.TryParse(iconIndexStr, out var parsedIdx))
                {
                    index = parsedIdx;
                    hasIndex = true;
                }
            }

            if (string.IsNullOrWhiteSpace(chosenPath))
                return null;

            var expandedPath = Environment.ExpandEnvironmentVariables(chosenPath);
            return new IconLocation(expandedPath, index, hasIndex);
        }
        catch
        {
            return null;
        }
    }

    public readonly record struct IconLocation(string? Path, int Index, bool HasIndex);

    public static string GetIconBase64(string? iconLocation, string fallbackPath)
    {
        var loc = NormalizeIconLocation(iconLocation);
        var pathCandidate = string.IsNullOrWhiteSpace(loc.Path) ? fallbackPath : loc.Path;
        if (string.IsNullOrWhiteSpace(pathCandidate) || !File.Exists(pathCandidate))
        {
            pathCandidate = fallbackPath;
        }

        return ExtractIconBase64(pathCandidate);
    }

    public static string ExtractIconBase64(string path)
    {
        var hIcon = GetFileIconHandle(path);
        if (hIcon == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static BitmapImage? Base64ToBitmapImage(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64String);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a BitmapSource to a PNG base64 string so we can persist icons even if the source file is deleted.
    /// </summary>
    public static string BitmapSourceToBase64(BitmapSource source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        try
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Trims transparent padding around an icon so the visible glyph fills the area.
    /// </summary>
    public static BitmapSource? TrimTransparentBorder(BitmapSource? source, byte alphaThreshold = 0)
    {
        if (source == null)
        {
            return source;
        }

        try
        {
            // Ensure BGRA32 for predictable stride and alpha access.
            BitmapSource formatted = source;
            if (source.Format != System.Windows.Media.PixelFormats.Bgra32)
            {
                var conv = new FormatConvertedBitmap();
                conv.BeginInit();
                conv.Source = source;
                conv.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                conv.EndInit();
                conv.Freeze();
                formatted = conv;
            }

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[height * stride];
            formatted.CopyPixels(pixels, stride, 0);

            int minX = width, minY = height, maxX = -1, maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[rowStart + x * 4 + 3];
                    if (alpha > alphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // No opaque pixels found; return original.
            if (maxX < minX || maxY < minY)
            {
                return source;
            }

            int cropX = minX;
            int cropY = minY;
            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;

            var cropped = new CroppedBitmap(formatted, new Int32Rect(cropX, cropY, cropW, cropH));
            cropped.Freeze();
            return cropped;
        }
        catch
        {
            return source;
        }
    }

    /// <summary>
    /// Tries to get a high-res icon (JUMBO 256px, falls back to EXTRALARGE) as a BitmapSource.
    /// Returns null if it fails anywhere.
    /// </summary>
    public static BitmapSource? GetHighResIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

        var isDirectory = Directory.Exists(path);
        var isFile = File.Exists(path);
        if (!isDirectory && !isFile)
        {
            return null;
        }

        // First, ask for the system icon index for this file.
        var shinfo = new Shfileinfo();
        var cb = (uint)Marshal.SizeOf(shinfo);
        uint attrs = isDirectory ? FileAttributeDirectory : 0;
        uint flags = ShgfiIcon | ShgfiSysIconIndex;

        // For directories we ask SHGetFileInfo to treat the path as a folder via attributes,
        // which ensures we still get the shell icon even though there is no file extension.
        if (isDirectory)
        {
            flags |= ShgfiUseFileAttributes;
        }

        var res = SHGetFileInfo(path, attrs, ref shinfo, cb, flags);
        if (res == IntPtr.Zero)
        {
            return null;
        }

        var baseIndex = shinfo.iIcon & 0xFFFFFF;
        int iconFlags = IldTransparent;

        // Try jumbo (256) then extra large (48).
        var hIcon = GetShellIcon(baseIndex, HilJumbo, iconFlags);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = GetShellIcon(baseIndex, HilExtraLarge, iconFlags);
        }

        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmpSource.Freeze();
            return bmpSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static IntPtr GetShellIcon(int iconIndex, int imageListSize, int flags)
    {
        var guid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); // IImageList GUID
        var hr = SHGetImageList(imageListSize, ref guid, out var imageList);
        if (hr != 0 || imageList == null)
        {
            return IntPtr.Zero;
        }

        var hIcon = IntPtr.Zero;
        hr = imageList.GetIcon(iconIndex, flags, ref hIcon);
        return hr == 0 ? hIcon : IntPtr.Zero;
    }

    private static IntPtr GetFileIconHandle(string path)
    {
        var isDirectory = Directory.Exists(path);
        uint attrs = isDirectory ? FileAttributeDirectory : 0;
        uint flags = ShgfiIcon | ShgfiLargeIcon;
        if (isDirectory)
        {
            flags |= ShgfiUseFileAttributes;
        }

        var shinfo = new Shfileinfo();
        var result = SHGetFileInfo(path, attrs, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
        return result == IntPtr.Zero ? IntPtr.Zero : shinfo.hIcon;
    }

    /// <summary>
    /// Normalizes icon location strings like "C:\path\file.dll,5" or "%SystemRoot%\System32\shell32.dll,4".
    /// Returns the expanded path, icon index (either parsed or fallback), and whether the string contained an index.
    /// </summary>
    public static IconLocation NormalizeIconLocation(string? iconLocation, int fallbackIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(iconLocation))
        {
            return new IconLocation(null, fallbackIndex, false);
        }

        var expanded = Environment.ExpandEnvironmentVariables(iconLocation.Trim());
        var comma = expanded.LastIndexOf(',');

        if (comma > 0 && int.TryParse(expanded[(comma + 1)..].Trim(), out var parsedIndex))
        {
            var pathPart = expanded[..comma].Trim();
            return new IconLocation(pathPart, parsedIndex, true);
        }

        return new IconLocation(expanded, fallbackIndex, false);
    }

    /// <summary>
    /// Extracts an icon from a DLL/EXE/ICO by index using the shell picker logic.
    /// </summary>
    public static BitmapSource? GetIconFromLibrary(string path, int iconIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(expanded))
        {
            return null;
        }

        var large = new IntPtr[1];
        var small = new IntPtr[1];

        try
        {
            var extracted = ExtractIconEx(expanded, iconIndex, large, small, 1);
            var handle = large[0] != IntPtr.Zero ? large[0] : small[0];

            if (extracted == 0 || handle == IntPtr.Zero)
            {
                return null;
            }

            var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmpSource.Freeze();
            return bmpSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
            if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
        }
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref Shfileinfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("User32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("Shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct Shfileinfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, uint crMask, ref int pi);
        [PreserveSig] int Draw(ref ImageListDrawParams pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        [PreserveSig] int GetImageInfo(int i, ref ImageInfo pImageInfo);
        [PreserveSig] int Copy(int iSrc, IImageList punkSrc, int iDst, int punkDst, int uFlags);
        [PreserveSig] int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, ref IntPtr ppv);
        [PreserveSig] int Clone(ref Guid riid, ref IntPtr ppv);
        [PreserveSig] int GetImageRect(int i, ref RECT prc);
        [PreserveSig] int GetIconSize(ref int cx, ref int cy);
        [PreserveSig] int SetIconSize(int cx, int cy);
        [PreserveSig] int GetImageCount(ref int pi);
        [PreserveSig] int SetImageCount(int uNewCount);
        [PreserveSig] int SetBkColor(int clrBk, ref int pclr);
        [PreserveSig] int GetBkColor(ref int pclr);
        [PreserveSig] int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);
        [PreserveSig] int EndDrag();
        [PreserveSig] int DragEnter(IntPtr hwndLock, int x, int y);
        [PreserveSig] int DragLeave(IntPtr hwndLock);
        [PreserveSig] int DragMove(int x, int y);
        [PreserveSig] int SetDragCursorImage(ref IImageList punk, int iDrag, int dxHotspot, int dyHotspot);
        [PreserveSig] int DragShowNolock(bool fShow);
        [PreserveSig] int GetDragImage(ref Point ppt, ref Point pptHotspot, ref Guid riid, ref IntPtr ppv);
        [PreserveSig] int GetItemFlags(int i, ref int dwFlags);
        [PreserveSig] int GetOverlayImage(int iOverlay, ref int piIndex);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImageInfo
    {
        public IntPtr hbmImage;
        public IntPtr hbmMask;
        public int Unused1;
        public int Unused2;
        public RECT rcImage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImageListDrawParams
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int xBitmap;    // x offset from the upper-left of bitmap
        public int yBitmap;    // y offset from the upper-left of bitmap
        public int rgbBk;
        public int rgbFg;
        public int fStyle;
        public int dwRop;
        public int fState;
        public int Frame;
        public int crEffect;
    }
}
