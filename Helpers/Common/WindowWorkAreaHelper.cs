using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Resolves a monitor's work area in the SAME WPF DIP coordinate space as
    /// <see cref="Window.Left"/>/<see cref="Window.Top"/>, and clamps a window rectangle to it.
    /// <para>
    /// Deliberately does NOT use <see cref="SystemParameters.WorkArea"/>: that value is not
    /// per-monitor-DPI aware, so when the process/system DPI differs from the monitor's
    /// effective scale it returns the work area divided by the system DPI (e.g. a 1920px-wide
    /// screen at 125% system scaling reports 1536). Clamping against that value made the
    /// launcher (and the floating icon) stop short of the right edge and spring back when
    /// dragged toward the top-right corner; the left edge worked because the left clamp is 0.
    /// </para>
    /// The monitor is resolved from the window (so multi-monitor placements clamp to the
    /// correct screen), and the DPI scale is read with GetDpiForMonitor, which is never stale
    /// after a resolution/scaling change (unlike SystemParameters / VisualTreeHelper.GetDpi).
    /// </summary>
    public static class WindowWorkAreaHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        private const uint MonitorDefaultToNearest = 2;
        private const int MdtEffectiveDpi = 0;

        /// <summary>
        /// Work area of the monitor the window is currently on, in the same WPF DIP space as
        /// <see cref="Window.Left"/>/<see cref="Window.Top"/>.
        /// </summary>
        public static Rect GetWorkAreaDip(Window window)
        {
            try
            {
                var handle = window != null ? new WindowInteropHelper(window).Handle : IntPtr.Zero;
                var screen = (handle != IntPtr.Zero ? Forms.Screen.FromHandle(handle) : null)
                    ?? Forms.Screen.PrimaryScreen;
                if (screen != null)
                {
                    var scale = GetScreenDpiScale(screen);
                    var wa = screen.WorkingArea;
                    return new Rect(wa.Left / scale, wa.Top / scale, wa.Width / scale, wa.Height / scale);
                }
            }
            catch
            {
                // fall through to the SystemParameters fallback below
            }

            var fallback = SystemParameters.WorkArea;
            return new Rect(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
        }

        /// <summary>
        /// Clamps a window rectangle to the given work area. All values are in WPF DIP, in the
        /// same coordinate space as <see cref="Window.Left"/>/<see cref="Window.Top"/>.
        /// </summary>
        public static (double left, double top) Clamp(double left, double top, double width, double height, Rect area)
        {
            var newLeft = left;
            var newTop = top;

            if (newLeft + width > area.Right)
                newLeft = area.Right - width;
            if (newTop + height > area.Bottom)
                newTop = area.Bottom - height;
            if (newLeft < area.Left)
                newLeft = area.Left;
            if (newTop < area.Top)
                newTop = area.Top;

            return (newLeft, newTop);
        }

        private static double GetScreenDpiScale(Forms.Screen screen)
        {
            try
            {
                var point = new POINT
                {
                    X = screen.Bounds.Left + Math.Max(1, screen.Bounds.Width / 2),
                    Y = screen.Bounds.Top + Math.Max(1, screen.Bounds.Height / 2)
                };

                var hMonitor = MonitorFromPoint(point, MonitorDefaultToNearest);
                if (hMonitor == IntPtr.Zero)
                    return 1.0;

                if (GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _) != 0)
                    return 1.0;

                return Math.Max(1.0, dpiX / 96.0);
            }
            catch
            {
                return 1.0;
            }
        }
    }
}
