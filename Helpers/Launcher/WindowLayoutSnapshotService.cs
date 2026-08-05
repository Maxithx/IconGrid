using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace IconGrid.Helpers.Launcher
{
    public sealed class WindowLayoutSnapshotService
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private readonly object _lock = new();
        private readonly HashSet<IntPtr> _iconGridWindows = new();
        private List<WindowSnapshot>? _snapshot;

        private sealed record WindowSnapshot(IntPtr Hwnd, bool WasIconic, RECT Rect);

        public bool HasSnapshot { get { lock (_lock) { return _snapshot != null; } } }

        public void Capture()
        {
            // Re-register IconGrid's own windows that participate in the snapshot:
            // the launcher and settings windows. The gaming overlay is deliberately
            // excluded — it manages its own position persistence (TryApplySavedPosition /
            // SaveGamingOverlayWindowPosition), so snapshot/restore would overwrite
            // its saved position and move it to a stale rectangle when the game
            // exits and the resolution is restored.
            //
            // NOTE: we filter by window type name, not by WS_EX_TOOLWINDOW / ShowInTaskbar:
            // every IconGrid window (launcher, settings, overlay) is a tool window, so an
            // extended-style check would exclude everything and make the snapshot useless.
            lock (_lock)
            {
                _iconGridWindows.Clear();

                if (System.Windows.Application.Current != null)
                {
                    foreach (var window in System.Windows.Application.Current.Windows)
                    {
                        if (window is not Window w)
                            continue;

                        var handle = new WindowInteropHelper(w).Handle;
                        if (handle == IntPtr.Zero)
                            continue;

                        // The gaming overlay persists its own position — never capture it.
                        if (string.Equals(w.GetType().Name, "GamingOverlayWindow", StringComparison.Ordinal))
                            continue;

                        _iconGridWindows.Add(handle);
                    }
                }
            }

            var captured = new List<WindowSnapshot>();
            EnumWindows((hwnd, _) =>
            {
                if (ShouldCapture(hwnd) && GetWindowRect(hwnd, out var rect))
                    captured.Add(new WindowSnapshot(hwnd, IsIconic(hwnd), rect));
                return true;
            }, IntPtr.Zero);

            lock (_lock)
            {
                _snapshot = captured;
            }
        }

        public void Restore()
        {
            List<WindowSnapshot>? snapshot;
            lock (_lock)
            {
                snapshot = _snapshot;
                _snapshot = null;
            }

            if (snapshot == null || snapshot.Count == 0)
                return;

            foreach (var entry in snapshot)
            {
                if (!IsWindow(entry.Hwnd))
                    continue;
                if (!IsWindowVisible(entry.Hwnd) && !entry.WasIconic)
                    continue;

                if (entry.WasIconic)
                    ShowWindow(entry.Hwnd, SW_RESTORE);

                var width = Math.Max(100, entry.Rect.Right - entry.Rect.Left);
                var height = Math.Max(100, entry.Rect.Bottom - entry.Rect.Top);
                SetWindowPos(entry.Hwnd, IntPtr.Zero, entry.Rect.Left, entry.Rect.Top, width, height,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private bool ShouldCapture(IntPtr hwnd)
        {
            try
            {
                // Capture IconGrid's own registered windows (launcher, settings).
                // The gaming overlay is never registered (it persists its own
                // position). For other applications, skip invisible windows and
                // tool windows (e.g. flyouts, popups) — only real desktop windows
                // participate in the snapshot.
                lock (_lock)
                {
                    if (_iconGridWindows.Contains(hwnd))
                        return true;
                }

                if (!IsWindowVisible(hwnd))
                    return false;

                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                    return false;

                var className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                var name = className.ToString();
                if (name.Contains("ExploreWClass") || name.Contains("CabinetWClass"))
                    return false;
                if (name.Contains("Shell_TrayWnd") || name.Contains("Progman"))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}