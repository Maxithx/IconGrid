using System;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingFont = System.Drawing.Font;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSystemFonts = System.Drawing.SystemFonts;
using MediaBrush = System.Windows.Media.Brush;

namespace IconGrid.Helpers;

/// <summary>
/// Renders icon text with the Windows theme (DrawThemeTextEx) directly onto a bitmap so it works in transparent WPF windows.
/// </summary>
public class ShellIconTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(ShellIconTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(MediaBrush),
            typeof(ShellIconTextBlock),
            new FrameworkPropertyMetadata(System.Windows.Media.Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeDeltaProperty =
        DependencyProperty.Register(
            nameof(FontSizeDelta),
            typeof(double),
            typeof(ShellIconTextBlock),
            new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MediaBrush Foreground
    {
        get => (MediaBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSizeDelta
    {
        get => (double)GetValue(FontSizeDeltaProperty);
        set => SetValue(FontSizeDeltaProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var renderSize = RenderSize;
        if (renderSize.Width <= 0 || renderSize.Height <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(renderSize.Width * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(renderSize.Height * dpi.DpiScaleY));

        using var bmp = new DrawingBitmap(pixelWidth, pixelHeight, DrawingPixelFormat.Format32bppPArgb);
        using var g = DrawingGraphics.FromImage(bmp);
        g.Clear(DrawingColor.Transparent);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var hTheme = OpenThemeData(IntPtr.Zero, "LISTVIEW");
        try
        {
            // Minimal padding; we're drawing plain white text with no effects.
            const int padding = 1;
            var rect = new RECT(padding, 0, pixelWidth - padding, pixelHeight);
            var opts = CreateOptions(GetTextColor());
            var flags = DrawTextFlags.DT_CENTER |
                        DrawTextFlags.DT_WORDBREAK |
                        DrawTextFlags.DT_NOPREFIX |
                        DrawTextFlags.DT_EDITCONTROL |
                        DrawTextFlags.DT_END_ELLIPSIS;

            var hdc = g.GetHdc();
            using var renderFont = CreateRenderFont();
            var hFont = renderFont.ToHfont();
            var oldFont = SelectObject(hdc, hFont);

            try
            {
                if (hTheme != IntPtr.Zero)
                {
                    DrawThemeTextEx(
                        hTheme,
                        hdc,
                        (int)ListViewParts.LVP_LISTITEM,
                        (int)ListViewItemStates.LISS_NORMAL,
                        Text ?? string.Empty,
                        -1,
                        (uint)flags,
                        ref rect,
                        ref opts);
                }
                else
                {
                        System.Windows.Forms.TextRenderer.DrawText(
                        g,
                        Text ?? string.Empty,
                        renderFont,
                        new DrawingRectangle(0, 0, pixelWidth, pixelHeight),
                        DrawingColor.FromArgb(unchecked((int)opts.crText)),
                        System.Windows.Forms.TextFormatFlags.HorizontalCenter |
                        System.Windows.Forms.TextFormatFlags.WordBreak |
                        System.Windows.Forms.TextFormatFlags.NoPrefix |
                        System.Windows.Forms.TextFormatFlags.EndEllipsis);
                }
            }
            finally
            {
                SelectObject(hdc, oldFont);
                DeleteObject(hFont);
                g.ReleaseHdc(hdc);
            }
        }
        finally
        {
            if (hTheme != IntPtr.Zero)
            {
                CloseThemeData(hTheme);
            }
        }

        var hBitmap = bmp.GetHbitmap(DrawingColor.FromArgb(0));
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(pixelWidth, pixelHeight));

            drawingContext.DrawImage(source, new Rect(0, 0, renderSize.Width, renderSize.Height));
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private uint GetTextColor()
    {
        if (Foreground is SolidColorBrush scb)
        {
            return unchecked((uint)DrawingColor.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B).ToArgb());
        }

        return unchecked((uint)DrawingColor.White.ToArgb());
    }

    private static DTTOPTS CreateOptions(uint textColor)
    {
        var opts = new DTTOPTS
        {
            dwSize = (uint)Marshal.SizeOf<DTTOPTS>(),
            dwFlags = DttOptsFlags.DTT_COMPOSITED |
                      DttOptsFlags.DTT_TEXTCOLOR,
            crText = textColor,
            crShadow = 0,
            crBorder = 0,
            iTextShadowType = (int)TextShadowType.TST_NONE,
            ptShadowOffset = new POINT(0, 0),
            iBorderSize = 0,
            iGlowSize = 0
        };

        return opts;
    }

    private DrawingFont CreateRenderFont()
    {
        var baseFont = DrawingSystemFonts.IconTitleFont ?? DrawingSystemFonts.DefaultFont;
        var size = Math.Max(1f, baseFont.Size + (float)FontSizeDelta);
        var style = baseFont.Style;
        var unit = baseFont.Unit;

        string?[] preferredNames =
        {
            "Segoe UI Variable Display",
            "Segoe UI Variable Text",
            "Segoe UI",
            baseFont.FontFamily?.Name
        };

        foreach (var name in preferredNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            try
            {
                return new DrawingFont(name, size, style, unit);
            }
            catch
            {
                // try next candidate
            }
        }

        var fallbackFamily = baseFont.FontFamily ?? DrawingSystemFonts.DefaultFont.FontFamily;
        return new DrawingFont(fallbackFamily, size, style, unit);
    }

    #region Win32 interop

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;

        public POINT(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DTTOPTS
    {
        public uint dwSize;
        public DttOptsFlags dwFlags;
        public uint crText;
        public uint crBorder;
        public uint crShadow;
        public int iTextShadowType;
        public POINT ptShadowOffset;
        public int iBorderSize;
        public int iFontPropId;
        public int iColorPropId;
        public int iStateId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fApplyOverlay;
        public int iGlowSize;
        public IntPtr pfnDrawTextCallback;
        public IntPtr lParam;
    }

    [Flags]
    private enum DttOptsFlags : uint
    {
        DTT_TEXTCOLOR = 0x00000001,
        DTT_BORDERCOLOR = 0x00000002,
        DTT_SHADOWCOLOR = 0x00000004,
        DTT_SHADOWTYPE = 0x00000008,
        DTT_SHADOWOFFSET = 0x00000010,
        DTT_BORDERSIZE = 0x00000020,
        DTT_FONTPROP = 0x00000040,
        DTT_COLORPROP = 0x00000080,
        DTT_STATEID = 0x00000100,
        DTT_CALCRECT = 0x00000200,
        DTT_APPLYOVERLAY = 0x00000400,
        DTT_GLOWSIZE = 0x00000800,
        DTT_CALLBACK = 0x00001000,
        DTT_COMPOSITED = 0x00002000,
        DTT_VALIDBITS = 0x00003FFF
    }

    private enum TextShadowType
    {
        TST_NONE = 0,
        TST_SINGLE = 1,
        TST_CONTINUOUS = 2
    }

    [Flags]
    private enum DrawTextFlags : uint
    {
        DT_TOP = 0x00000000,
        DT_LEFT = 0x00000000,
        DT_CENTER = 0x00000001,
        DT_RIGHT = 0x00000002,
        DT_VCENTER = 0x00000004,
        DT_BOTTOM = 0x00000008,
        DT_WORDBREAK = 0x00000010,
        DT_SINGLELINE = 0x00000020,
        DT_EXPANDTABS = 0x00000040,
        DT_TABSTOP = 0x00000080,
        DT_NOCLIP = 0x00000100,
        DT_EXTERNALLEADING = 0x00000200,
        DT_CALCRECT = 0x00000400,
        DT_NOPREFIX = 0x00000800,
        DT_INTERNAL = 0x00001000,
        DT_EDITCONTROL = 0x00002000,
        DT_PATH_ELLIPSIS = 0x00004000,
        DT_END_ELLIPSIS = 0x00008000,
        DT_MODIFYSTRING = 0x00010000,
        DT_RTLREADING = 0x00020000,
        DT_WORD_ELLIPSIS = 0x00040000
    }

    private enum ListViewParts
    {
        LVP_LISTITEM = 1
    }

    private enum ListViewItemStates
    {
        LISS_NORMAL = 1
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenThemeData(IntPtr hwnd, string pszClassList);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int CloseThemeData(IntPtr hTheme);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DrawThemeTextEx(
        IntPtr hTheme,
        IntPtr hdc,
        int iPartId,
        int iStateId,
        string text,
        int iCharCount,
        uint dwTextFlags,
        ref RECT pRect,
        ref DTTOPTS pOptions);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    #endregion
}
