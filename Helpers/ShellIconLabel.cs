using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IconGrid.Helpers;

/// <summary>
/// Draws icon text using the same themed rendering Windows Explorer uses for desktop icons.
/// </summary>
public sealed class ShellIconLabel : Control
{
    private const string ListViewClass = "LISTVIEW";

    public ShellIconLabel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        ForeColor = Color.White;
        Font = SystemFonts.IconTitleFont;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(Color.Transparent);
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var hTheme = OpenThemeData(Handle, ListViewClass);
        try
        {
            var hdc = e.Graphics.GetHdc();
            var hFont = Font.ToHfont();
            var oldFont = SelectObject(hdc, hFont);

            try
            {
                var rect = new RECT(0, 0, Width, Height);
                var opts = CreateOptions();
                var flags = DrawTextFlags.DT_CENTER |
                            DrawTextFlags.DT_WORDBREAK |
                            DrawTextFlags.DT_NOPREFIX |
                            DrawTextFlags.DT_EDITCONTROL |
                            DrawTextFlags.DT_END_ELLIPSIS;

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
                    // Fallback if theming is unavailable; still center/wrap text.
                    TextRenderer.DrawText(
                        e.Graphics,
                        Text ?? string.Empty,
                        Font,
                        new Rectangle(0, 0, Width, Height),
                        ForeColor,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.WordBreak |
                        TextFormatFlags.EndEllipsis);
                }
            }
            finally
            {
                SelectObject(hdc, oldFont);
                DeleteObject(hFont);
                e.Graphics.ReleaseHdc(hdc);
            }
        }
        finally
        {
            if (hTheme != IntPtr.Zero)
            {
                CloseThemeData(hTheme);
            }
        }
    }

    private DTTOPTS CreateOptions()
    {
        var opts = new DTTOPTS
        {
            dwSize = (uint)Marshal.SizeOf<DTTOPTS>(),
            dwFlags = DttOptsFlags.DTT_COMPOSITED |
                      DttOptsFlags.DTT_TEXTCOLOR |
                      DttOptsFlags.DTT_SHADOWTYPE |
                      DttOptsFlags.DTT_SHADOWCOLOR |
                      DttOptsFlags.DTT_SHADOWOFFSET,
            crText = unchecked((uint)ColorTranslator.ToWin32(ForeColor)),
            crShadow = unchecked((uint)ColorTranslator.ToWin32(Color.Black)),
            iTextShadowType = (int)TextShadowType.TST_SINGLE,
            ptShadowOffset = new POINT(1, 1),
            iGlowSize = 0
        };

        return opts;
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
    private static extern bool DeleteObject(IntPtr hObject);

    #endregion
}
