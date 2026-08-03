using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;
using Forms = System.Windows.Forms;

namespace IconGrid.Helpers
{
    /// <summary>
    /// Owns the Win32 / window-interop behavior that previously lived in MainWindow.xaml.cs:
    /// the window hook (WndProc), DWM attribute application, UIPI drag-drop filters,
    /// dynamic taskbar/tray icon refresh, and the tray icon lifecycle.
    /// Keeps the launcher shell focused on window lifetime and UI composition.
    /// </summary>
    public sealed class LauncherWindowInterop
    {
        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly Action<string> _log;
        private HwndSource? _hwndSource;
        private Forms.NotifyIcon? _trayIcon;
        private string _baseTitle;
        private bool _titleClearToggle;

        public LauncherWindowInterop(Window window, MainViewModel viewModel, string baseTitle, Action<string> log)
        {
            _window = window;
            _viewModel = viewModel;
            _baseTitle = baseTitle;
            _log = log;
        }

        public IntPtr Handle => _hwndSource?.Handle ?? IntPtr.Zero;

        public Forms.NotifyIcon? TrayIcon => _trayIcon;

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const int GWL_STYLE = -16;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_CHILD = 0x40000000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMNCRP_DISABLED = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_NOERASE = 0x0020;
        private const uint RDW_NOINTERNALPAINT = 0x0008;
        private const uint RDW_NOFRAME = 0x0800;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        /// <summary>
        /// Attaches the window hook, applies minimal DWM chrome, and registers UIPI
        /// drag-drop message filters. Call once from OnSourceInitialized.
        /// </summary>
        public void Initialize()
        {
            _hwndSource = PresentationSource.FromVisual(_window) as HwndSource;
            _hwndSource?.AddHook(WndProc);

            var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                ApplyDwmNoClientRendering(hwnd);
            }

            RegisterDragDropFilters();

            // Re-apply icon once the window handle exists to ensure the taskbar uses the dynamic variant in Release builds too.
            ApplyDynamicIcon();
        }

        /// <summary>
        /// Disposes the tray icon and detaches the window hook. Call from window teardown.
        /// </summary>
        public void Cleanup()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }

        public void ApplyDynamicIcon()
        {
            bool restoreTaskbarVisibility = !_window.ShowInTaskbar;
            if (restoreTaskbarVisibility)
            {
                _window.ShowInTaskbar = true;
            }

            bool restoreInFinally = restoreTaskbarVisibility;
            try
            {
                var theme = ThemeHelper.GetTheme();
                var dynamicIcon = DynamicIconHelper.CreateAccentIconImageSource(System.Windows.Media.Color.FromArgb(theme.AccentColor.A, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 256);
                if (dynamicIcon is BitmapSource bitmapSource)
                {
                    var dynamicFrame = CreateCacheBustingIcon(bitmapSource);
                    _window.Icon = dynamicFrame;
                    if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
                    {
                        TryApplyWin32Icons(dynamicFrame);
                        NotifyShellIconRefresh();
                    }
                    else
                    {
                        restoreInFinally = false;
                        _window.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            TryApplyWin32Icons(dynamicFrame);
                            NotifyShellIconRefresh();
                            if (restoreTaskbarVisibility)
                            {
                                _window.ShowInTaskbar = false;
                            }
                        }), DispatcherPriority.ApplicationIdle);
                    }

                    UpdateTrayIconFromSource(dynamicFrame);
                    _log($"Applied dynamic icon using accent #{theme.AccentColor.A:X2}{theme.AccentColor.R:X2}{theme.AccentColor.G:X2}{theme.AccentColor.B:X2}.");
                }
                else
                {
                    _log("Dynamic icon generation returned null; keeping existing icon.");
                }
            }
            catch (Exception ex)
            {
                _log($"ApplyDynamicIcon failed: {ex}");
            }
            finally
            {
                if (restoreInFinally && restoreTaskbarVisibility)
                {
                    _window.ShowInTaskbar = false;
                }
            }
        }

        public async Task RefreshTaskbarIconAsync()
        {
            await Task.Delay(120);
            ApplyDynamicIcon();
            ForceTitleRefresh();
        }

        public void InitializeTrayIcon(Action enterFullMode, Action exitApplication)
        {
            try
            {
                if (_trayIcon != null)
                    return;

                _trayIcon = new Forms.NotifyIcon
                {
                    Visible = true,
                    Text = "IconGrid"
                };

                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "dlb-icon.ico");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath, 32, 32);
                }
                else
                {
                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exe))
                    {
                        _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                    }
                }

                var menu = new Forms.ContextMenuStrip();
                menu.Items.Add(_viewModel.OpenLabel, null, (_, __) => _window.Dispatcher.Invoke(enterFullMode));
                menu.Items.Add(_viewModel.ExitLabel, null, (_, __) => _window.Dispatcher.Invoke(exitApplication));
                _trayIcon.ContextMenuStrip = menu;
                _trayIcon.DoubleClick += (_, __) => _window.Dispatcher.Invoke(enterFullMode);
            }
            catch
            {
                _trayIcon = null;
            }
        }

        public void RefreshTrayIconMenuLabels()
        {
            var menu = _trayIcon?.ContextMenuStrip;
            if (menu == null || menu.Items.Count < 2)
            {
                return;
            }

            menu.Items[0].Text = _viewModel.OpenLabel;
            menu.Items[1].Text = _viewModel.ExitLabel;
        }

        // Dynamic theme icon refresh that updates WM_SETICON / tray icon
        // while briefly toggling ShowInTaskbar and forcing SHChangeNotify so the
        // shell never reuses the frozen blue icon. This flow is fragile and
        // must remain synced with the steps in README; do not rework unless the
        // README instructions are updated accordingly.

        private void ApplyDwmNoClientRendering(IntPtr hwnd)
        {
            try
            {
                // Apply safe DWM attributes without touching window style
                var policy = DWMNCRP_DISABLED;
                DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref policy, Marshal.SizeOf<int>());

                var borderColor = 0;
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, Marshal.SizeOf<int>());

                // Disable rounded corners (Win11)
                const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                const int DWMWCP_DONOTROUND = 1;
                var cornerPref = DWMWCP_DONOTROUND;
                try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, Marshal.SizeOf<int>()); } catch { }

                // Force no transitions
                const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
                var transDisabled = 1;
                try { DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref transDisabled, Marshal.SizeOf<int>()); } catch { }

                _log("ApplyDwmNoClientRendering: applied DWM attributes for minimal chrome");
            }
            catch (Exception ex)
            {
                _log($"ApplyDwmNoClientRendering failed: {ex.Message}");
            }
        }

        private void TryApplyWin32Icons(BitmapSource? source)
        {
            if (source == null || _hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
                return;

            try
            {
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                int pixelFormatBits = source.Format.BitsPerPixel;
                int stride = (width * pixelFormatBits + 7) / 8;
                var pixelData = new byte[height * stride];
                source.CopyPixels(pixelData, stride, 0);

                using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                var rect = new System.Drawing.Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    Marshal.Copy(pixelData, 0, data.Scan0, pixelData.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                IntPtr hIcon = bmp.GetHicon();
                if (hIcon != IntPtr.Zero)
                {
                    const int WM_SETICON = 0x0080;
                    const int ICON_SMALL = 0;
                    const int ICON_BIG = 1;

                    SendMessage(_hwndSource.Handle, WM_SETICON, new IntPtr(ICON_SMALL), hIcon);
                    SendMessage(_hwndSource.Handle, WM_SETICON, new IntPtr(ICON_BIG), hIcon);
                    _log("WM_SETICON applied (small+big) with dynamic accent icon.");
                }
                else
                {
                    _log("GetHicon returned 0; unable to set WM_SETICON.");
                }
            }
            catch (Exception ex)
            {
                _log("TryApplyWin32Icons failed: " + ex);
            }
        }

        private void UpdateTrayIconFromSource(BitmapSource? source)
        {
            if (_trayIcon == null || source == null)
                return;

            var icon = CreateIconFromBitmapSource(source);
            if (icon != null)
            {
                var oldIcon = _trayIcon.Icon;
                _trayIcon.Icon = icon;
                oldIcon?.Dispose();
            }
        }

        private static BitmapFrame CreateCacheBustingIcon(BitmapSource source)
        {
            using var buffer = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(buffer);
            buffer.Seek(0, SeekOrigin.Begin);

            var decoder = new PngBitmapDecoder(buffer, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame == null)
            {
                frame = BitmapFrame.Create(source);
            }

            frame.Freeze();
            return frame;
        }

        // Forces Windows shell to re-read the cached icon metadata after WM_SETICON succeeds.
        // The delayed SHChangeNotify is intentionally required to avoid races.
        private static void NotifyShellIconRefresh()
        {
            Task.Delay(100).ContinueWith(_ => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero));
        }

        private static System.Drawing.Icon? CreateIconFromBitmapSource(BitmapSource source)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                using var ms = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);

                using var bitmap = new System.Drawing.Bitmap(ms);
                handle = bitmap.GetHicon();
                if (handle == IntPtr.Zero)
                    return null;

                using var icon = System.Drawing.Icon.FromHandle(handle);
                return (System.Drawing.Icon)icon.Clone();
            }
            catch
            {
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    DestroyIcon(handle);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOVING = 0x0216;
            const int WM_EXITSIZEMOVE = 0x0232;
            const int WM_DPICHANGED = 0x02E0;
            const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
            const int WM_NCPAINT = 0x0085;
            const int WM_ERASEBKGND = 0x0014;

            if (msg == WM_MOVING)
            {
                var rect = Marshal.PtrToStructure<RECT>(lParam);
                _log($"WM_MOVING: {rect.Left},{rect.Top}-{rect.Right},{rect.Bottom}");
            }
            else if (msg == WM_EXITSIZEMOVE)
            {
                var rect = GetWindowRect(hwnd);
                _log($"WM_EXITSIZEMOVE: window {_window.Left},{_window.Top} size {_window.Width}x{_window.Height}");
            }
            else if (msg == WM_DPICHANGED)
            {
                var dpiX = wParam.ToInt32() & 0xFFFF;
                var dpiY = (wParam.ToInt32() >> 16) & 0xFFFF;
                _log($"WM_DPICHANGED: {dpiX}x{dpiY}");
            }
            else if (msg == WM_DWMCOLORIZATIONCOLORCHANGED)
            {
                _log("WM_DWMCOLORIZATIONCOLORCHANGED received; forcing theme refresh.");
                ThemeHelper.ForceRefresh();
            }
            else if (msg == WM_NCPAINT)
            {
                // Suppress non-client paint to prevent ghost box redraw
                handled = true;
                return IntPtr.Zero;
            }
            else if (msg == WM_ERASEBKGND)
            {
                // Suppress background erase
                handled = true;
                return new IntPtr(1);
            }

            return IntPtr.Zero;
        }

        private const uint WM_DROPFILES = 0x0233;
        private const uint WM_COPYDATA = 0x004A;
        private const uint WM_COPYGLOBALDATA = 0x0049;
        private const uint MSGFLT_ALLOW = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct CHANGEFILTERSTRUCT
        {
            public uint cbSize;
            public uint ExtStatus;
        }

        [DllImport("user32.dll")]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);

        private void RegisterDragDropFilters()
        {
            try
            {
                var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                var cfs = new CHANGEFILTERSTRUCT { cbSize = (uint)Marshal.SizeOf<CHANGEFILTERSTRUCT>() };
                var dropOk = ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, ref cfs);
                var copyOk = ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, ref cfs);
                var copyGlobalOk = ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, ref cfs);

                _log($"Registered UIPI drop filters (Drop={dropOk}, Copy={copyOk}, CopyGlobal={copyGlobalOk}).");
            }
            catch (Exception ex)
            {
                _log($"RegisterDragDropFilters failed: {ex}");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool PickIconDlg(IntPtr hwndOwner, StringBuilder pszFilename, int cchFilename, ref int piIconIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr SetClassLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetClassLongW", SetLastError = true)]
        private static extern IntPtr SetClassLongW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetClassIcon(IntPtr hWnd, int index, IntPtr hIcon)
        {
            return IntPtr.Size == 8
                ? SetClassLongPtrW(hWnd, index, hIcon)
                : SetClassLongW(hWnd, index, hIcon);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public RECT GetWindowRect(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out var rect);
            return rect;
        }

        private void ForceTitleRefresh()
        {
            if (string.IsNullOrEmpty(_baseTitle))
                return;

            _titleClearToggle = !_titleClearToggle;
            _window.Title = _titleClearToggle ? $"{_baseTitle}\u200B" : _baseTitle;
        }
    }
}