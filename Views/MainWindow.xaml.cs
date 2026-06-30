using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Threading;
using IconGrid.Helpers;
using IconGrid.Models;
using IconGrid.ViewModels;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using RadioButton = System.Windows.Controls.RadioButton;
using Forms = System.Windows.Forms;

namespace IconGrid.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private string _baseTitle = string.Empty;
        private bool _titleClearToggle;
        private bool _isAnimatingHeight = true;
        private Forms.NotifyIcon? _trayIcon = null; // initialized to suppress CS0649 warning
        private readonly DispatcherTimer? _autoHideTimer;
        private bool _autoHideEnabled = false;
        private readonly DispatcherTimer _monitorTimer;
        private bool _isHidden = false;
        private int _monitorUpdateRunning;
        private HwndSource? _hwndSource;
        private System.Windows.Point _dragStartPoint;
        private static readonly string PowerShellPath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe");
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_RESTORE = 9;
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
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

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

        private readonly List<(string Label, string Arguments, string? IconName)> _powerShellPresets = new()
        {
            ("PowerShell console", "", "WindowsPowerShell.png")
        };
        private List<WindowsShortcutTemplate> _windowsShortcuts = new();
        private readonly Dictionary<string, string> _windowsShortcutTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "kontrolpanel", "Control Panel" },
            { "papirkurv", "Recycle Bin" },
            { "stifinder", "File Explorer" },
            { "skrivebord", "Desktop" },
            { "dokumenter", "Documents" },
            { "billeder", "Pictures" },
            { "videoer", "Videos" },
            { "musik", "Music" },
            { "overf?rsler", "Downloads" },
            { "galleri", "Gallery" },
            { "onedrive", "OneDrive" },
            { "taskmanager", "Task Manager" },
            { "windows powershell", "Windows PowerShell" },
            { "windows update", "Windows Update" },
            { "windows security", "Windows Security" },
            { "this pc", "This PC" },
            { "linux", "Linux" }
        };
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        private bool _skipSavingLocation;

        private void SetMonitorPollingEnabled(bool enabled)
        {
            if (_monitorTimer == null)
                return;

            if (enabled)
            {
                if (!_monitorTimer.IsEnabled)
                {
                    _monitorTimer.Start();
                    _viewModel.SystemMonitor.Update();
                }
            }
            else
            {
                _monitorTimer.Stop();
            }
        }

        private PawnIoWarningWindow? _pawnIoWarningWindow;
        private DispatcherTimer? _pawnIoWarningRetryTimer;
        private SettingsWindow? _settingsWindow;
        private double _lastLoggedLeft = double.NaN;
        private double _lastLoggedTop = double.NaN;
        private readonly FloatingIconController _floatingIconController = new();

        private record WindowsShortcutTemplate(string DisplayName, string FullPath);

        public MainWindow()
        {
            InitializeComponent();
            _baseTitle = Title ?? _baseTitle;

            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.SystemMonitor.PropertyChanged += SystemMonitor_PropertyChanged;
            ThemeHelper.ThemeChanged += ThemeHelper_ThemeChanged;
            SizeChanged += (_, _) => Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Background);
            LoadWindowsShortcuts();

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
            _viewModel.SystemMonitor.Update();
            UpdatePawnIoWarningWindow();

            // Start in floating icon mode; final position is set on load
            Left = 0;
            Top = 0;
            ResizeMode = ResizeMode.NoResize;

            InitializeTrayIcon();
            ApplyDynamicIcon();
            InitializeDevOverlay();
        }

        private bool IsOverLauncherTile(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is System.Windows.Controls.Button btn && btn.DataContext is LauncherItem)
                {
                    return true;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }

            return false;
        }

        private void ThemeHelper_ThemeChanged(object? sender, ThemeSnapshot e)
        {
            // Ensure icon updates when Windows accent/theme changes while the app is running.
            Dispatcher.BeginInvoke(() => _ = RefreshTaskbarIconAsync(), DispatcherPriority.Background);
        }

        /// <summary>
        /// Dynamic theme icon refresh that updates WM_SETICON / tray icon
        /// while briefly toggling ShowInTaskbar and forcing SHChangeNotify so the
        /// shell never reuses the frozen blue icon. This flow is fragile and
        /// must remain synced with the steps in README; do not rework unless the
        /// README instructions are updated accordingly.
        /// </summary>
        private void ApplyDynamicIcon()
        {
            bool restoreTaskbarVisibility = !ShowInTaskbar;
            if (restoreTaskbarVisibility)
            {
                ShowInTaskbar = true;
            }

            bool restoreInFinally = restoreTaskbarVisibility;
            try
            {
                var theme = ThemeHelper.GetTheme();
                var dynamicIcon = DynamicIconHelper.CreateAccentIconImageSource(System.Windows.Media.Color.FromArgb(theme.AccentColor.A, theme.AccentColor.R, theme.AccentColor.G, theme.AccentColor.B), 256);
                if (dynamicIcon is BitmapSource bitmapSource)
                {
                    var dynamicFrame = CreateCacheBustingIcon(bitmapSource);
                    Icon = dynamicFrame;
                    if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
                    {
                        TryApplyWin32Icons(dynamicFrame);
                        NotifyShellIconRefresh();
                    }
                    else
                    {
                        restoreInFinally = false;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            TryApplyWin32Icons(dynamicFrame);
                            NotifyShellIconRefresh();
                            if (restoreTaskbarVisibility)
                            {
                                ShowInTaskbar = false;
                            }
                        }), DispatcherPriority.ApplicationIdle);
                    }

                    UpdateTrayIconFromSource(dynamicFrame);
                     LogTrace($"Applied dynamic icon using accent #{theme.AccentColor.A:X2}{theme.AccentColor.R:X2}{theme.AccentColor.G:X2}{theme.AccentColor.B:X2}.");
                }
                else
                {
                    LogTrace("Dynamic icon generation returned null; keeping existing icon.");
                }
            }
            catch (Exception ex)
            {
                LogTrace($"ApplyDynamicIcon failed: {ex}");
            }
            finally
            {
                if (restoreInFinally && restoreTaskbarVisibility)
                {
                    ShowInTaskbar = false;
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);

            var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                ApplyDwmNoClientRendering(hwnd);
            }

            // Capture measured header height once layout is available.
            Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Loaded);

            RegisterDragDropFilters();

            // Re-apply icon once the window handle exists to ensure the taskbar uses the dynamic variant in Release builds too.
            ApplyDynamicIcon();
        }

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

                LogTrace("ApplyDwmNoClientRendering: applied DWM attributes for minimal chrome");
            }
            catch (Exception ex)
            {
                LogTrace($"ApplyDwmNoClientRendering failed: {ex.Message}");
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

                using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
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
                    LogTrace("WM_SETICON applied (small+big) with dynamic accent icon.");
                }
                else
                {
                    LogTrace("GetHicon returned 0; unable to set WM_SETICON.");
                }
            }
            catch (Exception ex)
            {
                LogTrace("TryApplyWin32Icons failed: " + ex);
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

                using var bitmap = new Bitmap(ms);
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

        private void EnterFullMode()
        {
            var wasFull = _viewModel.IsFullWindowVisible;
            _viewModel.IsFullWindowVisible = true;
            SetMonitorPollingEnabled(true);
            ApplyFullWindowSizing();
            if (!wasFull)
            {
                PositionFullWindow();

                if (_viewModel.EnableSlideUpAnimation)
                {
                    IconAreaGrid.Opacity = 0;
                    if (FindResource("SlideUpAnimation") is Storyboard slideUpAnimation)
                    {
                        var animation = slideUpAnimation.Clone();
                        animation.Begin(IconAreaGrid);
                    }
                }
                else
                {
                    IconAreaGrid.Opacity = 1;
                    IconAreaGrid.RenderTransform = new TranslateTransform(0, 0);
                }
            }
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;

            Activate();
        }

        private void EnterFloatingMode(bool showTray)
        {
            _autoHideEnabled = false;
            _floatingIconController.EnterFloatingMode(this, _viewModel, _autoHideTimer, _trayIcon, SetMonitorPollingEnabled);
        }

        private void ApplyFullWindowSizing()
        {
            SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(MainViewModel.WindowDesiredWidth)));
            SetBinding(HeightProperty, new System.Windows.Data.Binding(nameof(MainViewModel.WindowDesiredHeightEffective)));
        }



        private void PositionFullWindow()
        {
            if (_viewModel.TryGetSavedWindowPosition(out var left, out var top))
            {
                Left = left;
                Top = top;
                return;
            }

            var area = SystemParameters.WorkArea;
            var width = _viewModel.WindowDesiredWidth;
            var height = _viewModel.WindowDesiredHeight;
            const double defaultTopOffset = 20;
            Left = area.Left + Math.Max(0, (area.Width - width) / 2);
            Top = area.Top + defaultTopOffset;
        }

        private void PositionFloatingIcon(bool preferSaved = true)
        {
            _floatingIconController.PositionFloatingIcon(this, _viewModel, preferSaved);
        }

        private void ClampWindowToWorkArea()
        {
            var width = double.IsNaN(ActualWidth) || ActualWidth <= 0 ? Width : ActualWidth;
            var height = double.IsNaN(ActualHeight) || ActualHeight <= 0 ? Height : ActualHeight;
            var (left, top) = ClampToWorkArea(Left, Top, width, height);
            Left = left;
            Top = top;
        }

        private void ClampFloatingIconToWorkArea()
        {
            _floatingIconController.ClampFloatingIconToWorkArea(this);
        }

        private (double left, double top) ClampToWorkArea(double left, double top, double width, double height)
        {
            var area = SystemParameters.WorkArea;

            var newLeft = left;
            var newTop = top;

            // Clamp to work area boundaries
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

        private void AutoDetectAndEnableDynamicLayout()
        {
            try
            {
                var myHandle = _hwndSource?.Handle ?? IntPtr.Zero;
                if (myHandle == IntPtr.Zero)
                    return;

                var myRect = GetWindowRect(myHandle);
                var windowsBelow = new List<IntPtr>();

                // Enumerate all windows to find ones on the same monitor below or near IconGrid
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd) || hWnd == myHandle)
                        return true;

                    if (IsExcludedWindow(hWnd))
                        return true;

                    var rect = GetWindowRect(hWnd);
                    
                    // Check if window is below IconGrid (Top is close to or below IconGrid's Bottom)
                    // Also check if they're on the same horizontal area (within 200px)
                    var isBelow = rect.Top >= myRect.Bottom - 50; // 50px tolerance for detection
                    var isSameHorizontalArea = !(rect.Right < myRect.Left || rect.Left > myRect.Right);
                    
                    // Or check if window is just lower on screen (y-position greater than IconGrid)
                    var isLowerOnScreen = rect.Top > myRect.Top;

                    if ((isBelow || isSameHorizontalArea) && isLowerOnScreen)
                    {
                        windowsBelow.Add(hWnd);
                        LogTrace($"AutoDetect: Found window at ({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}), IconGrid at ({myRect.Left},{myRect.Top})-({myRect.Right},{myRect.Bottom})");
                    }

                    return true;
                }, IntPtr.Zero);

                // If windows found below/near IconGrid, enable Dynamic Layout
                if (windowsBelow.Count > 0)
                {
                    LogTrace($"AutoDetect: Found {windowsBelow.Count} windows, enabling Dynamic Layout");
                    _viewModel.LayoutPreset = "Dynamic";
                }
            }
            catch (Exception ex)
            {
                LogTrace("AutoDetectAndEnableDynamicLayout failed: " + ex);
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
                LogTrace($"WM_MOVING: {rect.Left},{rect.Top}-{rect.Right},{rect.Bottom}");
            }
            else if (msg == WM_EXITSIZEMOVE)
            {
                var rect = GetWindowRect(hwnd);
                LogTrace($"WM_EXITSIZEMOVE: window {Left},{Top} size {Width}x{Height}");
            }
            else if (msg == WM_DPICHANGED)
            {
                var dpiX = wParam.ToInt32() & 0xFFFF;
                var dpiY = (wParam.ToInt32() >> 16) & 0xFFFF;
                LogTrace($"WM_DPICHANGED: {dpiX}x{dpiY}");
            }
            else if (msg == WM_DWMCOLORIZATIONCOLORCHANGED)
            {
                LogTrace("WM_DWMCOLORIZATIONCOLORCHANGED received; forcing theme refresh.");
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

                LogTrace($"Registered UIPI drop filters (Drop={dropOk}, Copy={copyOk}, CopyGlobal={copyGlobalOk}).");
            }
            catch (Exception ex)
            {
                LogTrace($"RegisterDragDropFilters failed: {ex}");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool PickIconDlg(IntPtr hwndOwner, StringBuilder pszFilename, int cchFilename, ref int piIconIndex);

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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private RECT GetWindowRect(IntPtr hWnd)
        {
            GetWindowRect(hWnd, out var rect);
            return rect;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private void ForceTitleRefresh()
        {
            if (string.IsNullOrEmpty(_baseTitle))
                return;

            _titleClearToggle = !_titleClearToggle;
            Title = _titleClearToggle ? $"{_baseTitle}\u200B" : _baseTitle;
        }

        private async Task RefreshTaskbarIconAsync()
        {
            await Task.Delay(120);
            ApplyDynamicIcon();
            ForceTitleRefresh();
        }

        private void LogTrace(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "IconGrid");
                Directory.CreateDirectory(folder);
                var logPath = Path.Combine(folder, "trace.log");
                var line = $"[{DateTime.Now:O}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // ignore logging issues
            }
        }

        private void SystemMonitor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(SystemMonitor.IsPawnIoAvailable))
            {
                Dispatcher.BeginInvoke(new Action(UpdatePawnIoWarningWindow), DispatcherPriority.Background);
            }
        }

        private void PawnIoWarningWindow_Closed(object? sender, EventArgs e)
        {
            var window = _pawnIoWarningWindow;
            if (window != null && sender == window)
            {
                window.Closed -= PawnIoWarningWindow_Closed;
                _pawnIoWarningWindow = null;
            }
        }

        private void UpdatePawnIoWarningWindow()
        {
            if (_viewModel.SystemMonitor.IsPawnIoAvailable || PawnIoHelper.IsPawnIoInstalled())
            {
                StopPawnIoWarningRetryTimer();
                ClosePawnIoWarningWindow();
                return;
            }

            if (_pawnIoWarningWindow != null)
            {
                return;
            }

            _pawnIoWarningWindow = new PawnIoWarningWindow(_viewModel.PawnIoMissingMessage, _viewModel.PawnIoDownloadLink);
            _pawnIoWarningWindow.Closed += PawnIoWarningWindow_Closed;
            _pawnIoWarningWindow.Show();
            StartPawnIoWarningRetryTimer();
        }

        private void ClosePawnIoWarningWindow()
        {
            if (_pawnIoWarningWindow == null)
            {
                return;
            }

            _pawnIoWarningWindow.Closed -= PawnIoWarningWindow_Closed;
            _pawnIoWarningWindow.Close();
            _pawnIoWarningWindow = null;
        }

        private void StartPawnIoWarningRetryTimer()
        {
            _pawnIoWarningRetryTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _pawnIoWarningRetryTimer.Tick -= PawnIoWarningRetryTimer_Tick;
            _pawnIoWarningRetryTimer.Tick += PawnIoWarningRetryTimer_Tick;
            _pawnIoWarningRetryTimer.Stop();
            _pawnIoWarningRetryTimer.Start();
        }

        private void StopPawnIoWarningRetryTimer()
        {
            if (_pawnIoWarningRetryTimer == null)
            {
                return;
            }

            _pawnIoWarningRetryTimer.Stop();
            _pawnIoWarningRetryTimer.Tick -= PawnIoWarningRetryTimer_Tick;
        }

        private void PawnIoWarningRetryTimer_Tick(object? sender, EventArgs e)
        {
            StopPawnIoWarningRetryTimer();
            UpdatePawnIoWarningWindow();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EnterFloatingMode(showTray: false);
            PositionFloatingIcon();
            Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Background);

        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _skipSavingLocation = false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            _skipSavingLocation = true;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // no-op; we no longer need to reset positions after WinKey
        }

        private void InitializeTrayIcon()
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
                    var exe = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(exe))
                    {
                        _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                    }
                }

                var menu = new Forms.ContextMenuStrip();
                menu.Items.Add("Open", null, (_, __) => Dispatcher.Invoke(EnterFullMode));
                menu.Items.Add("Exit", null, (_, __) => Dispatcher.Invoke(ExitApplication));
                _trayIcon.ContextMenuStrip = menu;
                _trayIcon.DoubleClick += (_, __) => Dispatcher.Invoke(EnterFullMode);
            }
            catch
            {
                _trayIcon = null;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsOverlayOpen), StringComparison.OrdinalIgnoreCase))
            {
                // If overlay is closing, we want to suppress the window height animation that would normally fire.
                if (!_viewModel.IsOverlayOpen)
                {
                    _isAnimatingHeight = false;
                }
            }

            if (string.Equals(e.PropertyName, nameof(MainViewModel.Language), StringComparison.OrdinalIgnoreCase))
            {
                LoadWindowsShortcuts();
            }

            // Handle Dynamic Layout when icon panel expands/collapses
            if (_viewModel.IsFullWindowVisible &&
                (string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredHeightEffective), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredWidth), StringComparison.OrdinalIgnoreCase)))
            {
                var durationMs = (_viewModel.EnableSlideUpAnimation && _isAnimatingHeight) ? _viewModel.WindowAnimationDurationMs : 0;
                var duration = TimeSpan.FromMilliseconds(durationMs);
                var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

                if (string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredHeightEffective), StringComparison.OrdinalIgnoreCase))
                {
                    var heightAnim = new DoubleAnimation(_viewModel.WindowDesiredHeightEffective, duration) { EasingFunction = ease };
                    this.BeginAnimation(HeightProperty, heightAnim);
                }
                else // Width
                {
                    // Animate width change with a shorter duration
                    var widthAnim = new DoubleAnimation(_viewModel.WindowDesiredWidth, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
                    this.BeginAnimation(WidthProperty, widthAnim);
                }

                // Reset the flag so subsequent height changes are animated again.
                _isAnimatingHeight = true;
            }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.LayoutIconGridSlot), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.LayoutPreset), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.AccentBrush), StringComparison.OrdinalIgnoreCase))
        {
            // Keep the layout card highlighting in sync when preset or slot changes.
            Dispatcher.BeginInvoke(RefreshLayoutCardSelection, DispatcherPriority.Input);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.LayoutReserveIconGridSlot), StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(RefreshLayoutCardSelection, DispatcherPriority.Input);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.ShowDevOverlay), StringComparison.OrdinalIgnoreCase))
        {
            UpdateDevOverlayVisibility();
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.AccentBrush), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.IsLightTheme), StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(() => _ = RefreshTaskbarIconAsync(), DispatcherPriority.Background);
        }
    }

    private void InitializeDevOverlay()
    {
        if (RootLayoutGrid != null)
        {
            RootLayoutGrid.PreviewMouseMove += DevOverlay_MouseMove;
            RootLayoutGrid.MouseLeave += DevOverlay_MouseLeave;
        }

        UpdateDevOverlayVisibility();
    }

    private void UpdateHeaderHeightFromVisuals()
    {
        try
        {
            var top = TopBarGrid?.ActualHeight ?? 0;
            var tabs = TabsBorder?.ActualHeight ?? 0;
            var measured = top + tabs;
            _viewModel.SetHeaderHeight(measured);
        }
        catch (Exception ex)
        {
            LogTrace($"UpdateHeaderHeightFromVisuals failed: {ex}");
        }
    }

    private void DevOverlay_MouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewModel.ShowDevOverlay || DevOverlayPanel == null)
        {
            if (DevOverlayPanel != null)
                DevOverlayPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var source = e.OriginalSource as DependencyObject ?? e.Source as DependencyObject;
        if (source == null || IsDescendantOfDevOverlay(source))
        {
            DevOverlayPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var element = FindFrameworkElement(source);
        if (element == null)
        {
            DevOverlayPanel.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateDevOverlayText(element, e);
    }

    private void DevOverlay_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DevOverlayPanel != null)
            DevOverlayPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateDevOverlayVisibility()
    {
        if (DevOverlayPanel == null)
            return;

        if (_viewModel.ShowDevOverlay)
        {
            DevOverlayPanel.Visibility = Visibility.Visible;
        }
        else
        {
            DevOverlayPanel.Visibility = Visibility.Collapsed;
            DevOverlayHeader.Text = string.Empty;
            DevOverlayDetails.Text = string.Empty;
        }
    }

    private void UpdateDevOverlayText(FrameworkElement element, System.Windows.Input.MouseEventArgs e)
    {
        if (DevOverlayPanel == null || DevOverlayHeader == null || DevOverlayDetails == null)
            return;

        var metadataElement = FindMetadataElement(element);
        var header = metadataElement != null ? DevInspector.GetMetadata(metadataElement) : null;
        if (string.IsNullOrWhiteSpace(header))
        {
            var namePart = string.IsNullOrWhiteSpace(element.Name) ? string.Empty : $" ({element.Name})";
            header = $"{element.GetType().Name}{namePart}";
        }

        DevOverlayHeader.Text = header;

        var builder = new StringBuilder();
        if (metadataElement != null && metadataElement != element)
        {
            var metadataNamePart = string.IsNullOrWhiteSpace(metadataElement.Name) ? string.Empty : $" ({metadataElement.Name})";
            builder.AppendLine($"Metadata source: {metadataElement.GetType().Name}{metadataNamePart}");
        }
        if (element.DataContext != null)
        {
            builder.AppendLine($"DataContext: {element.DataContext.GetType().Name}");
        }

        if (element is ContentControl cc && cc.Content != null)
        {
            builder.AppendLine($"Content: {cc.Content}");
        }

        if (element is System.Windows.Controls.Button btn && btn.Command != null)
        {
            builder.AppendLine($"Command: {btn.Command.GetType().Name}");
        }

        if (element is System.Windows.Controls.MenuItem menu && menu.Command != null)
        {
            builder.AppendLine($"Command: {menu.Command.GetType().Name}");
        }

        builder.AppendLine($"Size: {element.ActualWidth:F1} × {element.ActualHeight:F1}");
        builder.AppendLine($"Margin: {FormatThickness(element.Margin)}");

        string? padding = element switch
        {
            System.Windows.Controls.Control control => FormatThickness(control.Padding),
            Border border => FormatThickness(border.Padding),
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(padding))
        {
            builder.AppendLine($"Padding: {padding}");
        }

        if (element.Tag != null)
        {
            builder.AppendLine($"Tag: {element.Tag}");
        }

        DevOverlayDetails.Text = builder.ToString().TrimEnd();
        DevOverlayPanel.Visibility = Visibility.Visible;
        UpdateDevOverlayPosition(e);
    }

    private static FrameworkElement? FindFrameworkElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement fe)
                return fe;
            source = GetParentSafe(source);
        }

        return null;
    }

    private static FrameworkElement? FindMetadataElement(FrameworkElement element)
    {
        var current = element;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(DevInspector.GetMetadata(current)))
            {
                return current;
            }

            var parent = GetParentSafe(current);
            current = parent as FrameworkElement;
        }

        return null;
    }

    private bool IsDescendantOfDevOverlay(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj == DevOverlayPanel)
                return true;
            obj = GetParentSafe(obj);
        }

        return false;
    }

    private static DependencyObject? GetParentSafe(DependencyObject obj)
    {
        if (obj is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(obj);
        }

        return LogicalTreeHelper.GetParent(obj);
    }

    private void UpdateDevOverlayPosition(System.Windows.Input.MouseEventArgs e)
    {
        if (DevOverlayCanvas == null || DevOverlayPanel == null)
            return;

        const double offset = 12;
        var canvasWidth = DevOverlayCanvas.ActualWidth;
        var canvasHeight = DevOverlayCanvas.ActualHeight;
        if (canvasWidth <= 0)
        {
            canvasWidth = ActualWidth;
        }
        if (canvasHeight <= 0)
        {
            canvasHeight = ActualHeight;
        }

        var mousePos = e.GetPosition(DevOverlayCanvas);
        var panelWidth = DevOverlayPanel.ActualWidth;
        var panelHeight = DevOverlayPanel.ActualHeight;

        var left = mousePos.X + offset;
        var top = mousePos.Y + offset;

        var maxLeft = Math.Max(0, canvasWidth - panelWidth - offset);
        var maxTop = Math.Max(0, canvasHeight - panelHeight - offset);

        if (left > maxLeft)
        {
            left = Math.Max(offset, maxLeft);
        }
        if (top > maxTop)
        {
            top = Math.Max(offset, maxTop);
        }

        left = Math.Max(offset, left);
        top = Math.Max(offset, top);

        Canvas.SetLeft(DevOverlayPanel, left);
        Canvas.SetTop(DevOverlayPanel, top);
    }

    private static string FormatThickness(Thickness thickness) =>
        $"{thickness.Left:F1}, {thickness.Top:F1}, {thickness.Right:F1}, {thickness.Bottom:F1}";

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsFullWindowVisible)
                return;

            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            EnterFloatingMode(showTray: true);
        }

        private void FloatingIconButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _floatingIconController.HandleMouseLeftButtonDown(this, _viewModel, e);
        }

        private void FloatingIconButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _floatingIconController.HandleMouseMove(this, _viewModel, e);
        }

        private void FloatingIconButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_floatingIconController.HandleMouseLeftButtonUp(_viewModel, e))
            {
                EnterFullMode();
            }
        }

        private void FloatingIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (_floatingIconController.HandleClick())
                EnterFullMode();
        }

        private void FloatingIconExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void IconScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Ensure ScrollableHeight updates promptly when content size changes.
            if (sender is ScrollViewer sv)
            {
                sv.InvalidateMeasure();
            }
        }

        private bool TryGetSupportedDropFiles(System.Windows.DragEventArgs e, out string[] files)
        {
            files = Array.Empty<string>();

            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] raw || raw.Length == 0)
                return false;

            files = raw.Where(ShortcutHelper.IsSupportedLauncherFile).ToArray();
            return files.Length > 0;
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
                return;

            e.Handled = true;
            if (TryGetSupportedDropFiles(e, out _))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
                return;

            if (!TryGetSupportedDropFiles(e, out var files))
                return;

            e.Handled = true;
            _viewModel.HandleFileDrop(files);
        }

        // WPF DragEventArgs (fully-qualified to avoid ambiguity with WinForms)
        private void ItemsControl_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent(typeof(LauncherItem)) || e.Data.GetDataPresent("LauncherItem"))
            {
                if (IsOverLauncherTile(e.OriginalSource as DependencyObject))
                {
                    return; // tile-level handler will show move effect
                }
                e.Effects = System.Windows.DragDropEffects.Move;
                return;
            }

            if (TryGetSupportedDropFiles(e, out _))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void ItemsControl_Drop(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent("LauncherItem"))
            {
                // Let tile-level drop handle reordering; ignore drops on empty area for launcher items.
                return;
            }

            if (!TryGetSupportedDropFiles(e, out var files))
                return;

            _viewModel.HandleFileDrop(files);
        }

        private void LauncherItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void LauncherItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var position = e.GetPosition(null);
            var diff = position - _dragStartPoint;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (sender is not System.Windows.Controls.Button button || button.DataContext is not LauncherItem item)
                return;

            var data = new System.Windows.DataObject();
            data.SetData("LauncherItem", item);
            data.SetData(typeof(LauncherItem), item);
            System.Windows.DragDrop.DoDragDrop(button, data, System.Windows.DragDropEffects.Move);
        }

        private void LauncherItem_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("LauncherItem"))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void LauncherItem_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(LauncherItem)) && !e.Data.GetDataPresent("LauncherItem"))
                return;

            var source = e.Data.GetData(typeof(LauncherItem)) as LauncherItem ?? e.Data.GetData("LauncherItem") as LauncherItem;
            if (source == null)
                return;

            var target = (sender as FrameworkElement)?.DataContext as LauncherItem;
            if (target == null || ReferenceEquals(source, target))
                return;

            var fe = sender as FrameworkElement;
            var insertAfter = false;
            if (fe != null)
            {
                var pos = e.GetPosition(fe);
                insertAfter = pos.Y > fe.ActualHeight / 2;
            }

            _viewModel.MoveItemWithinCategory(source, target, insertAfter);
            e.Handled = true;
        }

        private void RenameMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            var newName = ShowInputBox("Enter a new display name:", "Rename Launcher", item.DisplayName);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _viewModel.RenameItem(item, newName);
            }
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_autoHideEnabled)
                return;

            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_autoHideEnabled)
                return;

            _autoHideTimer?.Stop();
            if (_isHidden)
            {
                SlideTo(0);
            }
        }

        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            _autoHideTimer?.Stop();
            SlideTo(-Height + 8);
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (Interlocked.CompareExchange(ref _monitorUpdateRunning, 1, 0) == 1)
                return;

            Task.Run(() =>
            {
                try
                {
                    _viewModel.SystemMonitor.Update();
                }
                finally
                {
                    Interlocked.Exchange(ref _monitorUpdateRunning, 0);
                }
            });
        }

        private void SlideTo(double targetTop)
        {
            var animation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };

            BeginAnimation(TopProperty, animation);
            _isHidden = targetTop < 0;
        }

        private string ShowInputBox(string prompt, string title, string defaultValue)
        {
            // Midlertidig ? kan senere erstattes af en rigtig WPF-dialog
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);
        }

        private void RemoveItemMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _viewModel.RemoveItem(item);
        }

        private void LauncherItemButton_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.DataContext is not LauncherItem item)
                return;

            e.Handled = true;
            _viewModel.LaunchItem(item);
        }

        private void OpenItemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _viewModel.LaunchItem(item);
        }

        private void RunAsAdminMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                var psi = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                var workingDirectory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not run as administrator:\n{ex.Message}", "Run as admin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                if (File.Exists(item.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{item.Path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(item.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{item.Path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open file location:\n{ex.Message}", "Open location", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                System.Windows.Clipboard.SetText(item.Path);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not copy path:\n{ex.Message}", "Copy path", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeIconMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            var initialPath = ResolveIconPickerPath(menuItem, item);
            var sb = new StringBuilder(512);
            sb.Append(initialPath);
            var iconIndex = item.IconIndex;
            var hwnd = new WindowInteropHelper(this).Handle;

            if (PickIconDlg(hwnd, sb, sb.Capacity, ref iconIndex))
            {
                var raw = sb.ToString();
                var cleanPath = raw.Split('\0')[0].Trim();
                if (string.IsNullOrWhiteSpace(cleanPath))
                {
                    return;
                }

                var selectedPath = NormalizeToSystemRootToken(cleanPath);
                var sanitizedIndex = Math.Max(0, iconIndex);
                _viewModel.UpdateItemIcon(item, selectedPath, sanitizedIndex);
            }
        }

        private static string ResolveIconPickerPath(System.Windows.Controls.MenuItem menuItem, LauncherItem item)
        {
            var taggedPath = menuItem.Tag as string;
            var candidate = !string.IsNullOrWhiteSpace(taggedPath)
                ? taggedPath
                : item.IconPath;

            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = @"%SystemRoot%\System32\shell32.dll";
            }

            return Environment.ExpandEnvironmentVariables(candidate);
        }

        private static string NormalizeToSystemRootToken(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            if (!string.IsNullOrWhiteSpace(systemRoot) &&
                path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = path.Length > systemRoot.Length
                    ? path[systemRoot.Length..].TrimStart('\\')
                    : string.Empty;

                return string.IsNullOrEmpty(remainder)
                    ? "%SystemRoot%"
                    : $"%SystemRoot%\\{remainder}";
            }

            return path;
        }

        private void ResetIconMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                System.Windows.MessageBox.Show("Cannot reset icon because the target path is empty.", "Reset icon", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.UpdateItemIcon(item, item.Path, 0);
        }

        private void RefreshNewShortcutIcon(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            _viewModel.UpdateItemIcon(item, item.Path, 0);
        }

        private void AddShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose file or shortcut",
                Filter = "Applications and shortcuts (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var name = ShowInputBox("Enter display name", "New shortcut", System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var item = _viewModel.CreateCustomShortcut(name, dialog.FileName, _viewModel.SelectedTab);
                RefreshNewShortcutIcon(item);
            }
        }

        private void AddPowerShellCustomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var command = ShowInputBox("Enter PowerShell command (e.g. Start-Process ms-settings:windowsupdate)", "New PowerShell shortcut", "Start-Process ms-settings:windowsupdate");
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            var name = ShowInputBox("Enter display name", "New PowerShell shortcut", "PowerShell");
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var item = _viewModel.CreateCustomShortcut(name, PowerShellPath, _viewModel.SelectedTab, $"-NoExit -Command \"{command}\"");
            ApplyDefaultIconFromPack(item, "WindowsPowerShell.png", PowerShellPath, 0);
        }

        private void AddWindowsShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not WindowsShortcutTemplate template)
                return;

            var targetCategory = _viewModel.Tabs.Contains("Windows") ? "Windows" : _viewModel.SelectedTab;
            AddWindowsShortcut(template, targetCategory);
        }

        private void AddPowerShellPresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not string args)
                return;

            var label = mi.Header?.ToString() ?? "PowerShell";
            var item = _viewModel.CreateCustomShortcut(label, PowerShellPath, _viewModel.SelectedTab, args);

            // Prefer executable icon (index 0); fall back to icon pack if available.
            _viewModel.SetIcon(item, PowerShellPath, 0);
            ApplyDefaultIconFromPack(item, "WindowsPowerShell.png", PowerShellPath, 0);
        }

        private void AddAllWindowsShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var targetCategory = _viewModel.Tabs.Contains("Windows") ? "Windows" : _viewModel.SelectedTab;
            foreach (var shortcut in _windowsShortcuts)
            {
                AddWindowsShortcut(shortcut, targetCategory);
            }
        }

        private string GetIconBase64ForItem(LauncherItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.IconBase64))
            {
                return item.IconBase64;
            }

            if (!string.IsNullOrWhiteSpace(item.IconPath))
            {
                var loc = IconHelper.NormalizeIconLocation(item.IconPath, item.IconIndex);
                if (!string.IsNullOrWhiteSpace(loc.Path))
                {
                    var extracted = IconHelper.ExtractIconBase64(loc.Path);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }
            }

            if (item.IconImage is BitmapSource bmp)
            {
                return IconHelper.BitmapSourceToBase64(bmp);
            }

            return string.Empty;
        }

        private void ApplyEmbeddedIcon(LauncherItem item, string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return;
            }

            var img = IconHelper.Base64ToBitmapImage(base64);
            if (img != null)
            {
                var trimmed = IconHelper.TrimTransparentBorder(img) ?? img;
                item.IconBase64 = IconHelper.BitmapSourceToBase64(trimmed);
                item.Icon = trimmed;
            }
        }

        private void ApplyDefaultIconFromPack(LauncherItem item, string fileName, string fallbackIconPath, int fallbackIndex = 0)
        {
            var candidate = System.IO.Path.Combine(_viewModel.IconPackFolder, fileName);
            if (File.Exists(candidate))
            {
                _viewModel.SetIcon(item, candidate, 0);
                return;
            }

            _viewModel.SetIcon(item, fallbackIconPath, fallbackIndex);
        }

        private string TranslateShortcutName(string name)
        {
            var key = name.ToLowerInvariant();
            var lang = _viewModel.Language?.ToLowerInvariant() ?? "en";

            // Default filenames are Danish; when UI is English, map them to English equivalents.
            if (!string.Equals(lang, "da", StringComparison.OrdinalIgnoreCase))
            {
                return _windowsShortcutTranslations.TryGetValue(key, out var translated) ? translated : name;
            }

            // Danish UI: keep original or map known English names back to Danish.
            if (string.Equals(key, "this pc", StringComparison.OrdinalIgnoreCase))
                return "Denne computer";

            // If the file name is already Danish, leave it.
            return name;
        }

        private void AddWindowsShortcut(WindowsShortcutTemplate template, string targetCategory)
        {
            var exists = _viewModel.Items.Any(i =>
                string.Equals(i.Path, template.FullPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.Category, targetCategory, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return;
            }

            var item = _viewModel.CreateCustomShortcut(template.DisplayName, template.FullPath, targetCategory, arguments: null, iconPath: template.FullPath, iconIndex: 0);
            _viewModel.UpdateItemIcon(item, template.FullPath, 0);
        }

        private void LoadWindowsShortcuts()
        {
            try
            {
                var list = new List<WindowsShortcutTemplate>();

                var potentialRoots = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Assets", "WindowsShortcuts"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "WindowsShortcuts"), // dev path
                    Path.Combine(Environment.CurrentDirectory, "Assets", "WindowsShortcuts"),
                    Path.Combine(_viewModel.IconPackFolder, "..", "WindowsShortcuts")
                };

                foreach (var dir in potentialRoots.Distinct())
                {
                    var full = Path.GetFullPath(dir);
                    if (!Directory.Exists(full))
                        continue;

                    foreach (var file in Directory.GetFiles(full, "*.lnk", SearchOption.TopDirectoryOnly))
                    {
                        var rawName = Path.GetFileNameWithoutExtension(file);
                        var displayName = TranslateShortcutName(rawName);
                        if (list.All(l => !string.Equals(l.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(new WindowsShortcutTemplate(displayName, file));
                        }
                    }
                }

                _windowsShortcuts = list.OrderBy(s => s.DisplayName).ToList();
            }
            catch
            {
                _windowsShortcuts = new List<WindowsShortcutTemplate>();
            }
        }

        private void ContentArea_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (IsOverLauncherTile(e.OriginalSource as DependencyObject))
            {
                return;
            }

            var menu = new System.Windows.Controls.ContextMenu();
            DevInspector.SetMetadata(menu, "Icon grid context menu → Views/MainWindow.xaml (ContentArea_ContextMenuOpening)");

            string AddShortcutText() => LocalizationHelper.Get(_viewModel.Language, "AddShortcut");
            string AddPowerShellText() => LocalizationHelper.Get(_viewModel.Language, "AddPowerShellShortcut");
            string CustomText() => LocalizationHelper.Get(_viewModel.Language, "Custom");
            string AddWindowsText() => LocalizationHelper.Get(_viewModel.Language, "AddWindowsShortcut");
            string AddAllText() => LocalizationHelper.Get(_viewModel.Language, "AddAll");
            string ClearCategoryText() => LocalizationHelper.Get(_viewModel.Language, "ClearCategory");
            string ClearCategoryConfirmText() => LocalizationHelper.Get(_viewModel.Language, "ClearCategoryConfirm");

            var addShortcut = new System.Windows.Controls.MenuItem { Header = AddShortcutText() };
            DevInspector.SetMetadata(addShortcut, "Add shortcut menu item → Views/MainWindow.xaml (AddShortcutMenuItem_Click)");
            addShortcut.Click += AddShortcutMenuItem_Click;
            menu.Items.Add(addShortcut);

            var psMenu = new System.Windows.Controls.MenuItem { Header = AddPowerShellText() };
            DevInspector.SetMetadata(psMenu, "Add PowerShell submenu → Views/MainWindow.xaml (AddPowerShellCustomMenuItem_Click)");
            var customPs = new System.Windows.Controls.MenuItem { Header = CustomText() };
            DevInspector.SetMetadata(customPs, "Custom PowerShell menu item → Views/MainWindow.xaml (AddPowerShellCustomMenuItem_Click)");
            customPs.Click += AddPowerShellCustomMenuItem_Click;
            psMenu.Items.Add(customPs);

            menu.Items.Add(psMenu);
            menu.Items.Add(new Separator());

            if (_windowsShortcuts.Any())
            {
                var winMenu = new System.Windows.Controls.MenuItem { Header = AddWindowsText() };
                DevInspector.SetMetadata(winMenu, "Add Windows shortcuts submenu → Views/MainWindow.xaml (AddWindowsShortcutMenuItem_Click)");
                var addAll = new System.Windows.Controls.MenuItem { Header = AddAllText() };
                DevInspector.SetMetadata(addAll, "Add all Windows shortcuts menu item → Views/MainWindow.xaml (AddAllWindowsShortcutsMenuItem_Click)");
                addAll.Click += AddAllWindowsShortcutsMenuItem_Click;
                winMenu.Items.Add(addAll);
                winMenu.Items.Add(new Separator());

                foreach (var shortcut in _windowsShortcuts)
                {
                    var item = new System.Windows.Controls.MenuItem
                    {
                        Header = shortcut.DisplayName,
                        Tag = shortcut
                    };
                    DevInspector.SetMetadata(item, $"Add Windows shortcut '{shortcut.DisplayName}' → Views/MainWindow.xaml (AddWindowsShortcutMenuItem_Click)");
                    item.Click += AddWindowsShortcutMenuItem_Click;
                    winMenu.Items.Add(item);
                }

                menu.Items.Add(winMenu);
            }

            menu.Items.Add(new Separator());

            var clearCategory = new System.Windows.Controls.MenuItem { Header = ClearCategoryText() };
            DevInspector.SetMetadata(clearCategory, "Clear category menu item → Views/MainWindow.xaml (Clear current category)");
            clearCategory.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_viewModel.SelectedTab))
                    return;

                var result = System.Windows.MessageBox.Show(
                    ClearCategoryConfirmText(),
                    ClearCategoryText(),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.ClearCurrentCategory();
                }
            };
            menu.Items.Add(clearCategory);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void ChangeIconFromPackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose icon from custom pack",
                Filter = "Icon and image files (*.ico;*.png;*.jpg)|*.ico;*.png;*.jpg|All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true,
                InitialDirectory = Directory.Exists(_viewModel.IconPackFolder)
                    ? _viewModel.IconPackFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dialog.ShowDialog(this) == true)
            {
                _viewModel.UpdateItemIcon(item, dialog.FileName, 0);
            }
        }

        private void MoreButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ShowSettingsWindow();
        }

        private void ShowSettingsWindow()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_viewModel)
                {
                    Owner = this
                };
                _settingsWindow.Closed += SettingsWindow_Closed;
            }

            if (!_settingsWindow.TryApplySavedPosition())
            {
                PositionSettingsWindowRelativeToMain(_settingsWindow);
            }

            if (_settingsWindow.IsVisible)
            {
                if (_settingsWindow.WindowState == WindowState.Minimized)
                {
                    _settingsWindow.WindowState = WindowState.Normal;
                }

                _settingsWindow.Activate();
            }
            else
            {
                _skipSavingLocation = true;
                _settingsWindow.Show();
            }
        }

        private void SettingsWindow_Closed(object? sender, EventArgs e)
        {
            _settingsWindow = null;
            _skipSavingLocation = false;
            LogTrace($"Settings window closed; returning to normal focus from {Left:F1},{Top:F1}");
        }

        private void PositionSettingsWindowRelativeToMain(SettingsWindow window)
        {
            var workArea = SystemParameters.WorkArea;
            const double gap = 16;
            var desiredLeft = Left + Width + gap;
            if (desiredLeft + window.Width > workArea.Right)
            {
                desiredLeft = Left - window.Width - gap;
            }

            if (desiredLeft < workArea.Left)
            {
                desiredLeft = workArea.Left;
            }

            var desiredTop = Top;
            if (desiredTop + window.Height > workArea.Bottom)
            {
                desiredTop = workArea.Bottom - window.Height;
            }

            if (desiredTop < workArea.Top)
            {
                desiredTop = workArea.Top;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = desiredLeft;
            window.Top = desiredTop;
        }

        private void ArrangeNowButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ArrangeWindowsFromPreset(_viewModel.LayoutPreset);
        }

        private void SaveLayoutAsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PromptAndSaveLayout();
        }

        private void SaveLayoutAsMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PromptAndSaveLayout();
        }

        internal void PromptAndSaveLayout()
        {
            var suggested = string.Equals(_viewModel.LayoutPreset, "Auto", StringComparison.OrdinalIgnoreCase)
                ? "Mit layout"
                : _viewModel.LayoutPreset;

            var name = Interaction.InputBox("Navngiv layoutet", "Gem layout som", suggested ?? "Mit layout").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (string.Equals(name, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show("Navnet kan ikke være 'Auto'. Vælg et andet navn.", "Ugyldigt navn", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (TrySaveLayout(name))
            {
                _viewModel.LayoutPreset = name;
                UpdateLayoutMenuChecks(LayoutPresetButton.ContextMenu.Items);
                RefreshLayoutCardSelection();
            }
        }

        private bool TrySaveLayout(string layoutName)
        {
            layoutName = layoutName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(layoutName))
                return false;

            try
            {
                var targetMonitor = _viewModel.LayoutCurrentMonitorOnly && _hwndSource?.Handle != null
                    ? MonitorFromWindow(_hwndSource.Handle, MONITOR_DEFAULTTONEAREST)
                    : IntPtr.Zero;

                var workArea = GetWorkArea(targetMonitor);
                var windows = CollectCandidateWindows(targetMonitor, _viewModel.LayoutReserveIconGridSlot);
                if (windows.Count == 0)
                {
                    LogTrace($"Layout '{layoutName}' not saved: no candidate windows were found.");
                    return false;
                }

                var iconHandle = _hwndSource?.Handle ?? IntPtr.Zero;
                var orderedWindows = windows
                    .OrderBy(w => w.Rect.Top)
                    .ThenBy(w => w.Rect.Left)
                    .Take(4)
                    .ToList();

                var normalized = new List<CustomLayoutSlot>();
                var iconSlotIndex = -1;

                for (int i = 0; i < orderedWindows.Count; i++)
                {
                    var slot = NormalizeRectToWorkArea(orderedWindows[i].Rect, workArea);
                    if (slot.Width <= 0 || slot.Height <= 0)
                    {
                        continue;
                    }

                    if (iconHandle != IntPtr.Zero && orderedWindows[i].Hwnd == iconHandle)
                    {
                        iconSlotIndex = normalized.Count;
                    }

                    normalized.Add(slot);
                }

                var distinct = new List<CustomLayoutSlot>();
                foreach (var slot in normalized)
                {
                    if (!distinct.Any(existing => SlotsClose(existing, slot, 0.02)))
                    {
                        distinct.Add(slot);
                    }
                }

                normalized = distinct.Take(4).ToList();

                if (normalized.Count == 0)
                {
                    LogTrace($"Layout '{layoutName}' not saved: normalization produced no usable slots.");
                    return false;
                }

                _viewModel.SaveLayout(layoutName, normalized);
                LogTrace($"Layout '{layoutName}' distinct slots saved: {string.Join(", ", normalized.Select(s => $"({s.X:F3},{s.Y:F3},{s.Width:F3},{s.Height:F3})"))}");
                if (iconSlotIndex >= 0)
                {
                    _viewModel.LayoutIconGridSlot = iconSlotIndex;
                }

                LogTrace($"Saved layout '{layoutName}' with {normalized.Count} slots.");
                return true;
            }
            catch (Exception ex)
            {
                LogTrace("SaveLayout failed: " + ex);
                return false;
            }
        }

        private void CloseLayoutsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsLayoutsOpen = false;
        }

        private void CloseHelpButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsHelpOpen = false;
        }

        private void LayoutPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu == null) return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        private void LogoButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsSettingsOpen = false;
            _viewModel.IsLayoutsOpen = false;
            ArrangeWindowsFromPreset(_viewModel.LayoutPreset);
        }

        private void LayoutPresetMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not string preset)
                return;

            _viewModel.LayoutPreset = preset;
            ArrangeWindowsFromPreset(_viewModel.LayoutPreset);
            UpdateLayoutMenuChecks(LayoutPresetButton.ContextMenu.Items);
            RefreshLayoutCardSelection();
        }

        private void LayoutSlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is null)
                return;

            if (!_viewModel.LayoutReserveIconGridSlot)
                return;

            if (!int.TryParse(btn.Tag.ToString(), out var slot))
                return;

            _viewModel.LayoutIconGridSlot = slot;
            RefreshLayoutCardSelection();
            UpdateLayoutMenuChecks(LayoutPresetButton.ContextMenu.Items);
        }

        private void LayoutLinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
                return;

            var parts = tag.Split('|');
            if (parts.Length != 4)
                return;

            var preset = parts[0];
            if (!int.TryParse(parts[2], out var a) || !int.TryParse(parts[3], out var b))
                return;

            if (_viewModel.LayoutLinks.TryGetValue(preset, out var existing) && existing.Length == 2 && existing[0] == a && existing[1] == b)
            {
                _viewModel.SetLayoutLink(preset, Array.Empty<int>());
            }
            else
            {
                _viewModel.SetLayoutLink(preset, new[] { a, b });
            }

            RefreshLayoutCardSelection();
        }

        private void LayoutContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu menu) return;
            PopulateLayoutMenu(menu.Items);
            UpdateLayoutMenuChecks(menu.Items);
        }

        private void LayoutsPageMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu menu) return;
            PopulateLayoutMenu(menu.Items);
            UpdateLayoutMenuChecks(menu.Items);
        }

        private void PopulateLayoutMenu(ItemCollection menuItems)
        {
            menuItems.Clear();

            // Add the static "Auto" option
            var autoItem = new System.Windows.Controls.MenuItem
            {
                Header = "Auto",
                Tag = "Auto",
                IsCheckable = true
            };
            autoItem.Click += LayoutPresetMenuItem_Click;
            menuItems.Add(autoItem);



            if (_viewModel.SavedLayoutNames.Any())
            {
                 menuItems.Add(new Separator());
            }

            // Add each saved layout with its own context menu
            foreach (var name in _viewModel.SavedLayoutNames)
            {
                var containerItem = new System.Windows.Controls.MenuItem
                {
                    Header = name,
                    Tag = name, // Tag for UpdateLayoutMenuChecks to find and potentially style the container
                };

                var selectItem = new System.Windows.Controls.MenuItem
                {
                    Header = "Vælg",
                    Tag = name, // Tag for the click handler
                    IsCheckable = true
                };
                selectItem.Click += LayoutPresetMenuItem_Click;
                containerItem.Items.Add(selectItem);

                containerItem.Items.Add(new Separator());

                var renameItem = new System.Windows.Controls.MenuItem { Header = "Omdøb...", Tag = name };
                renameItem.Click += RenameLayoutMenuItem_Click;
                containerItem.Items.Add(renameItem);

                var deleteItem = new System.Windows.Controls.MenuItem { Header = "Slet", Tag = name };
                deleteItem.Click += DeleteLayoutMenuItem_Click;
                containerItem.Items.Add(deleteItem);
                
                menuItems.Add(containerItem);
            }

            if (_viewModel.SavedLayoutNames.Any())
            {
                menuItems.Add(new Separator());
            }
            
            var saveItem = new System.Windows.Controls.MenuItem { Header = "Gem layout som..." };
            saveItem.Click += SaveLayoutAsMenuItem_Click;
            menuItems.Add(saveItem);
        }

        private void UpdateLayoutMenuChecks(ItemCollection menuItems)
        {
            if (menuItems == null) return;

            // This function will recursively search for checkable items and update them.
            void UpdateChecks(ItemCollection items)
            {
                foreach (var item in items.OfType<System.Windows.Controls.MenuItem>())
                {
                    // Update check state for items that are checkable (e.g. "Auto", "Vælg")
                    if (item.Tag is string tag && item.IsCheckable)
                    {
                        item.IsChecked = string.Equals(tag, _viewModel.LayoutPreset, StringComparison.OrdinalIgnoreCase);
                    }
                    
                    // Additionally, we can make the top-level container bold if it's the selected one.
                    if (item.Tag is string containerTag && !item.IsCheckable && item.HasItems)
                    {
                        item.FontWeight = string.Equals(containerTag, _viewModel.LayoutPreset, StringComparison.OrdinalIgnoreCase)
                            ? FontWeights.Bold
                            : FontWeights.Normal;
                    }

                    // Recurse into sub-items if they exist
                    if (item.HasItems)
                    {
                        UpdateChecks(item.Items);
                    }
                }
            }
            
            UpdateChecks(menuItems);

            RefreshLayoutCardSelection();
        }

        private void RenameLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string current)
                return;

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            var newName = Interaction.InputBox("Omdøb layoutet", "Omdøb layout", current).Trim();
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            if (!_viewModel.RenameLayout(current, newName))
            {
                System.Windows.MessageBox.Show("Kunne ikke omdøbe layoutet. Navnet kan være i brug eller ugyldigt.", "Omdøb layout", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.LayoutPreset = newName;
            PopulateLayoutMenu(LayoutPresetButton.ContextMenu.Items);
            UpdateLayoutMenuChecks(LayoutPresetButton.ContextMenu.Items);
            RefreshLayoutCardSelection();
        }

        private void DeleteLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string current)
                return;

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current, "Auto", StringComparison.OrdinalIgnoreCase))
                return;

            var confirm = System.Windows.MessageBox.Show($"Slet layoutet '{current}'?", "Slet layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            if (_viewModel.DeleteLayout(current))
            {
                PopulateLayoutMenu(LayoutPresetButton.ContextMenu.Items);
                UpdateLayoutMenuChecks(LayoutPresetButton.ContextMenu.Items);
                RefreshLayoutCardSelection();
            }
        }

        private void RefreshLayoutCardSelection()
        {
            try
            {
                var accent = _viewModel.AccentBrush;
                var normal = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));
                var dim = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));

                if (LayoutCardsHost == null)
                    return;

                foreach (var child in LayoutCardsHost.Children)
                {
                    if (child is Border card)
                    {
                        foreach (var button in FindVisualChildren<System.Windows.Controls.Button>(card))
                        {
                            if (button.Tag is not string tag)
                                continue;

                            if (tag.Contains("Link"))
                            {
                                var linkParts = tag.Split('|');
                                if (linkParts.Length == 4 && int.TryParse(linkParts[2], out var la) && int.TryParse(linkParts[3], out var lb))
                                {
                                    var active = _viewModel.LayoutLinks.TryGetValue(_viewModel.LayoutPreset, out var link) && link.Length == 2 && link[0] == la && link[1] == lb;
                                    button.Background = active ? accent : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                                    button.BorderBrush = active ? accent : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
                                    if (button.Content is TextBlock tb)
                                    {
                                        tb.Foreground = active ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
                                    }
                                }
                            }
                                else
                                {
                                    if (!int.TryParse(tag, out var slot))
                                        continue;

                                    var slotEnabled = _viewModel.LayoutReserveIconGridSlot;
                                    button.IsEnabled = slotEnabled;
                                    button.Opacity = slotEnabled ? 1 : 0.6;

                                    // Highlight purely by selected slot so user clicks always show.
                                    var isMatch = slotEnabled && slot == _viewModel.LayoutIconGridSlot;

                                    // Use app accent for selected so it matches the logo color.
                                    var selectedBg = _viewModel.AccentBrush as System.Windows.Media.SolidColorBrush
                                                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
                                    var unselectedBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55)); // dark tile
                                    var unselectedBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 32, 48));

                                    button.Background = isMatch ? selectedBg : unselectedBg;
                                button.BorderBrush = isMatch ? selectedBg : unselectedBorder;
                                button.BorderThickness = isMatch ? new System.Windows.Thickness(2) : new System.Windows.Thickness(0);
                                button.Foreground = System.Windows.Media.Brushes.White;

                                        if (button.Content is TextBlock tb)
                                        {
                                            var showIg = slotEnabled && isMatch;
                                            tb.Text = showIg ? "IG" : $"{slot + 1}";
                                        }
                                    }
                                }
                            }
                }
            }
            catch
            {
                // best effort
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private enum LayoutChoice
        {
            Auto,
            Favorite,
            TwoUp,
            ThreePane,
            ThreePaneMirror,
            Grid2x2
        }

        private record LayoutResolution(LayoutChoice Choice, string? SavedLayoutName);

        internal void ArrangeWindowsFromPreset(string presetName)
        {
            try
            {
                var resolution = ResolvePreset(presetName);
                var savedSlots = resolution.SavedLayoutName != null
                    ? _viewModel.GetSavedLayoutSlots(resolution.SavedLayoutName).ToList()
                    : new List<CustomLayoutSlot>();

                var preset = resolution.Choice;
                var hasSaved = preset == LayoutChoice.Favorite && savedSlots.Any();

                var targetMonitor = _viewModel.LayoutCurrentMonitorOnly && _hwndSource?.Handle != null
                    ? MonitorFromWindow(_hwndSource.Handle, MONITOR_DEFAULTTONEAREST)
                    : IntPtr.Zero;

                var workArea = GetWorkArea(targetMonitor);
                var windows = CollectCandidateWindows(targetMonitor, _viewModel.LayoutReserveIconGridSlot);
                if (preset == LayoutChoice.Favorite)
                {
                    LogTrace($"Arrange(saved '{resolution.SavedLayoutName ?? _viewModel.LayoutPreset}'): candidates={windows.Count}, workArea=({workArea.Left},{workArea.Top},{workArea.Right},{workArea.Bottom})");
                }
                
                if (windows.Count == 0)
                {
                    return;
                }

                var resolvedPreset = preset == LayoutChoice.Auto
                    ? SuggestPresetForCount(windows.Count)
                    : preset;

                if (preset == LayoutChoice.Favorite && !hasSaved)
                {
                    LogTrace("Saved preset selected without stored slots; falling back to auto grid.");
                    preset = SuggestPresetForCount(windows.Count);
                }

                var effectivePreset = preset == LayoutChoice.Auto ? resolvedPreset : preset;
                var slots = effectivePreset == LayoutChoice.Favorite
                    ? BuildSavedSlots(savedSlots, workArea)
                    : BuildSlots(effectivePreset, workArea, windows.Count);

                var selectedSlot = Math.Max(0, Math.Min(_viewModel.LayoutIconGridSlot, Math.Max(0, slots.Count - 1)));
                if (selectedSlot != _viewModel.LayoutIconGridSlot)
                {
                    _viewModel.LayoutIconGridSlot = selectedSlot;
                    RefreshLayoutCardSelection();
                }

                var preferredSlot = _viewModel.LayoutReserveIconGridSlot ? selectedSlot : -1;

                List<(IntPtr Hwnd, RECT Rect)> ordered;
                if (effectivePreset == LayoutChoice.Favorite)
                {
                    ordered = BuildFavoriteAssignments(windows, slots, _hwndSource?.Handle ?? IntPtr.Zero, preferredSlot, _viewModel.LayoutReserveIconGridSlot);
                }
                else
                {
                    ordered = BuildOrderedAssignments(windows, slots, _hwndSource?.Handle, preferredSlot, _viewModel.LayoutReserveIconGridSlot);
                }

                if (effectivePreset == LayoutChoice.Favorite)
                {
                    try
                    {
                        LogTrace($"Saved slots resolved: {slots.Count}; assignments={ordered.Count}; name={resolution.SavedLayoutName ?? presetName}");
                        LogTrace("Saved assignments detail: " + string.Join("; ", ordered.Select((a, idx) => $"slot{idx}:{DescribeHandle(a.Item1)}->{DescribeRect(a.Item2)}")));
                    }
                    catch
                    {
                        // best effort logging
                    }
                }

                var reserveIconSlot = _viewModel.LayoutReserveIconGridSlot;
                var myHandle = _hwndSource?.Handle ?? IntPtr.Zero;
                foreach (var assignment in ordered)
                {
                    var hwnd = assignment.Hwnd;
                    if (hwnd == IntPtr.Zero)
                        continue;

                    var slot = assignment.Rect;

                    if (IsIconic(hwnd))
                    {
                        ShowWindow(hwnd, SW_RESTORE);
                    }

                    var width = Math.Max(100, slot.Right - slot.Left);
                    var height = Math.Max(100, slot.Bottom - slot.Top);

                    if (!reserveIconSlot && hwnd == myHandle)
                    {
                        continue;
                    }

                    if (myHandle != IntPtr.Zero && hwnd == myHandle)
                    {
                        // This is our own window. Use physical pixels (disable DPI scaling for layout test).
                        // By using physical coordinates here, vi afprøver om DPI transform er årsagen til ghost-box.
                        SetWindowPos(hwnd, IntPtr.Zero, slot.Left, slot.Top, width, height,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    }
                    else
                    {
                        // External windows already use physical pixels.
                        SetWindowPos(hwnd, IntPtr.Zero, slot.Left, slot.Top, width, height,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTrace("ArrangeWindows failed: " + ex);
            }
        }



        private bool IsExcludedWindow(IntPtr hwnd)
        {
            try
            {
                // Get window class name
                var className = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                var classNameStr = className.ToString();

                // Exclude Explorer/File Manager windows
                if (classNameStr.Contains("ExploreWClass") || classNameStr.Contains("CabinetWClass"))
                    return true;

                // Exclude other system windows
                if (classNameStr.Contains("Shell_TrayWnd") || classNameStr.Contains("Progman"))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }



        private List<(IntPtr Hwnd, RECT Rect)> BuildOrderedAssignments(List<(IntPtr Hwnd, RECT Rect)> windows, List<RECT> slots, IntPtr? iconGridHandle, int preferredIconSlot, bool reserveIconGridSlot)
        {
            var slotCount = slots.Count;
            var ordered = new (IntPtr Hwnd, RECT Rect)[slotCount];

            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var openSlots = Enumerable.Range(0, slotCount).ToList();

            // Reserve IconGrid slot if available.
            if (reserveIconGridSlot && iconGridHandle.HasValue && iconGridHandle.Value != IntPtr.Zero && preferredIconSlot >= 0 && preferredIconSlot < slotCount)
            {
                var idx = remainingWindows.FindIndex(w => w.Hwnd == iconGridHandle.Value);
                if (idx >= 0)
                {
                    ordered[preferredIconSlot] = (remainingWindows[idx].Hwnd, slots[preferredIconSlot]);
                    remainingWindows.RemoveAt(idx);
                    openSlots.Remove(preferredIconSlot);
                }
            }

            // If a link pair is defined for this preset, try to place the best matching windows into the link slots.
            if (_viewModel.LayoutLinks.TryGetValue(_viewModel.LayoutPreset, out var linkSlots) && linkSlots != null && linkSlots.Length == 2)
            {
                var linkA = Math.Max(0, Math.Min(linkSlots[0], slotCount - 1));
                var linkB = Math.Max(0, Math.Min(linkSlots[1], slotCount - 1));

                var targetLinkSlots = new[] { linkA, linkB }.Distinct().Where(openSlots.Contains).ToList();
                if (targetLinkSlots.Count == 2 && remainingWindows.Count > 0)
                {
                    var targetRects = targetLinkSlots.Select(i => slots[i]).ToList();
                    var matched = MatchWindowsToSlotsByCloseness(remainingWindows, targetRects);
                    for (int i = 0; i < matched.Count && i < targetLinkSlots.Count; i++)
                    {
                        var slotIndex = targetLinkSlots[i];
                        ordered[slotIndex] = (matched[i].Hwnd, slots[slotIndex]);
                        openSlots.Remove(slotIndex);
                        remainingWindows.Remove(matched[i]);
                    }
                }
            }

            // Match remaining windows to remaining slots
            var matchSlots = openSlots.Select(i => slots[i]).ToList();
            var assignments = MatchWindowsToSlotsWithSlots(remainingWindows, openSlots, slots);

            foreach (var (slotIndex, window) in assignments)
            {
                if (slotIndex >= 0 && slotIndex < ordered.Length)
                {
                    ordered[slotIndex] = (window.Hwnd, slots[slotIndex]);
                }
            }

            return ordered.ToList();
        }

        private List<(int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)> MatchWindowsToSlotsWithSlots(List<(IntPtr Hwnd, RECT Rect)> windows, List<int> slotIndices, List<RECT> allSlots)
        {
            var needed = Math.Min(windows.Count, slotIndices.Count);
            var best = new List<(int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)>();
            double bestCost = double.MaxValue;
            var current = new (int SlotIndex, (IntPtr Hwnd, RECT Rect) Window)[needed];
            var usedSlots = new HashSet<int>();

            void Backtrack(int depth, double cost)
            {
                if (cost >= bestCost)
                    return;
                if (depth == needed)
                {
                    bestCost = cost;
                    best = current.ToList();
                    return;
                }

                for (int i = 0; i < slotIndices.Count; i++)
                {
                    var slotIndex = slotIndices[i];
                    if (usedSlots.Contains(slotIndex))
                        continue;

                    usedSlots.Add(slotIndex);
                    var slotRect = allSlots[slotIndex];
                    var added = DistanceCost(windows[depth].Rect, slotRect);
                    current[depth] = (slotIndex, windows[depth]);
                    Backtrack(depth + 1, cost + added);
                    usedSlots.Remove(slotIndex);
                }
            }

            Backtrack(0, 0);
            return best;
        }

        private List<(IntPtr Hwnd, RECT Rect)> MatchWindowsToSlotsByCloseness(List<(IntPtr Hwnd, RECT Rect)> windows, List<RECT> slots)
        {
            var windowCount = windows.Count;
            var slotCount = Math.Min(slots.Count, windowCount);
            if (slotCount == 0)
                return new List<(IntPtr, RECT)>();

            if (slotCount <= 6)
            {
                var used = new bool[windowCount];
                var best = new (IntPtr Hwnd, RECT Rect)[slotCount];
                double bestCost = double.MaxValue;
                var current = new (IntPtr Hwnd, RECT Rect)[slotCount];

                void Backtrack(int depth, double cost)
                {
                    if (cost >= bestCost)
                        return;
                    if (depth == slotCount)
                    {
                        bestCost = cost;
                        Array.Copy(current, best, slotCount);
                        return;
                    }

                    for (int i = 0; i < windowCount; i++)
                    {
                        if (used[i]) continue;
                        used[i] = true;
                        current[depth] = windows[i];
                        var added = DistanceCost(windows[i].Rect, slots[depth]);
                        Backtrack(depth + 1, cost + added);
                        used[i] = false;
                    }
                }

                Backtrack(0, 0);
                var bestList = best.ToList();
                var bestHandles = new HashSet<IntPtr>(bestList.Select(b => b.Hwnd));
                bestList.AddRange(windows.Where(w => !bestHandles.Contains(w.Hwnd)));
                return bestList;
            }

            // Greedy fallback
            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var ordered = new List<(IntPtr Hwnd, RECT Rect)>();
            foreach (var slot in slots)
            {
                if (remainingWindows.Count == 0)
                    break;
                var nearest = remainingWindows.OrderBy(w => DistanceCost(w.Rect, slot)).First();
                ordered.Add(nearest);
                remainingWindows.Remove(nearest);
            }
            ordered.AddRange(remainingWindows);
            return ordered;
        }

        private static double DistanceCost(RECT rect, RECT slot)
        {
            var cx = rect.Left + (rect.Right - rect.Left) / 2.0;
            var cy = rect.Top + (rect.Bottom - rect.Top) / 2.0;
            var sx = slot.Left + (slot.Right - slot.Left) / 2.0;
            var sy = slot.Top + (slot.Bottom - slot.Top) / 2.0;
            var dx = cx - sx;
            var dy = cy - sy;
            return (dx * dx) + (dy * dy);
        }

        private LayoutResolution ResolvePreset(string presetName)
        {
            if (_viewModel.TryGetSavedLayout(presetName, out _, out var canonical))
                return new LayoutResolution(LayoutChoice.Favorite, canonical);

            if (string.IsNullOrWhiteSpace(presetName))
                return new LayoutResolution(LayoutChoice.Auto, null);

            var key = presetName.Trim().ToLowerInvariant();
            return key switch
            {
                "twoup" => new LayoutResolution(LayoutChoice.TwoUp, null),
                "two-up" => new LayoutResolution(LayoutChoice.TwoUp, null),
                "favorite" => _viewModel.TryGetSavedLayout("Favorit", out _, out var fallbackName)
                    ? new LayoutResolution(LayoutChoice.Favorite, fallbackName)
                    : new LayoutResolution(LayoutChoice.Auto, null),
                "threepane" => new LayoutResolution(LayoutChoice.ThreePane, null),
                "three-up" => new LayoutResolution(LayoutChoice.ThreePane, null),
                "threepanemirror" => new LayoutResolution(LayoutChoice.ThreePaneMirror, null),
                "3-up (mirror)" => new LayoutResolution(LayoutChoice.ThreePaneMirror, null),
                "grid2x2" => new LayoutResolution(LayoutChoice.Grid2x2, null),
                "grid" => new LayoutResolution(LayoutChoice.Grid2x2, null),
                _ => new LayoutResolution(LayoutChoice.Auto, null)
            };
        }

        private LayoutChoice SuggestPresetForCount(int windowCount)
        {
            // Auto-suggest layout based on number of windows
            return windowCount switch
            {
                1 => LayoutChoice.Auto,  // Single window, leave as is
                2 => LayoutChoice.TwoUp,  // Two windows side-by-side
                3 => LayoutChoice.ThreePane,  // Three windows: one large on left, two stacked on right
                _ => LayoutChoice.Grid2x2  // Four or more windows: 2x2 grid
            };
        }

        private RECT GetWorkArea(IntPtr targetMonitor)
        {
            if (targetMonitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(targetMonitor, ref info))
                {
                    return info.rcWork;
                }
            }

            var work = SystemParameters.WorkArea;
            return new RECT
            {
                Left = (int)work.Left,
                Top = (int)work.Top,
                Right = (int)work.Right,
                Bottom = (int)work.Bottom
            };
        }

        private List<(IntPtr Hwnd, RECT Rect)> CollectCandidateWindows(IntPtr targetMonitor, bool includeIconGridWindow)
        {
            var result = new List<(IntPtr, RECT)>();
            var currentPid = Process.GetCurrentProcess().Id;
            var iconGridHandle = _hwndSource?.Handle ?? IntPtr.Zero;

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                var styles = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((styles & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW)
                    return true;

                if (_viewModel.LayoutSkipMinimized && IsIconic(hwnd))
                    return true;

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == (uint)currentPid && !_viewModel.IsFullWindowVisible)
                    return true;

                if (targetMonitor != IntPtr.Zero && _viewModel.LayoutCurrentMonitorOnly)
                {
                    var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    if (mon != targetMonitor)
                        return true;
                }

                if (!GetWindowRect(hwnd, out var rect))
                    return true;
                if (!includeIconGridWindow && iconGridHandle != IntPtr.Zero && hwnd == iconGridHandle)
                    return true;

                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (w < 120 || h < 120)
                    return true;

                var className = string.Empty;
                try
                {
                    var sbCls = new StringBuilder(256);
                    if (GetClassName(hwnd, sbCls, sbCls.Capacity) > 0)
                    {
                        className = sbCls.ToString().Trim();
                    }
                }
                catch
                {
                    // ignore class fetch errors
                }

                if (!string.IsNullOrEmpty(className) &&
                    className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var titleLen = GetWindowTextLength(hwnd);
                if (titleLen <= 0 || titleLen > 512)
                    return true;

                var sb = new StringBuilder(Math.Min(512, titleLen + 10));
                GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                result.Add((hwnd, rect));
                return true;
            }, IntPtr.Zero);

            try
            {
                LogTrace($"CollectCandidateWindows: count={result.Count}, skipMin={_viewModel.LayoutSkipMinimized}, currentMonitorOnly={_viewModel.LayoutCurrentMonitorOnly}, target={targetMonitor}");
                LogTrace("Candidates: " + string.Join("; ", result.Select(DescribeWindow)));
            }
            catch
            {
                // best effort logging
            }

            return result;
        }

        private List<RECT> BuildSlots(LayoutChoice preset, RECT work, int windowCount)
        {
            var width = work.Right - work.Left;
            var height = work.Bottom - work.Top;
            var slots = new List<RECT>();

            RECT Make(int x, int y, int w, int h) => new RECT
            {
                Left = x,
                Top = y,
                Right = x + w,
                Bottom = y + h
            };

            switch (preset)
            {
                case LayoutChoice.Favorite:
                {
                    var favorite = BuildSavedSlots(_viewModel.GetSavedLayoutSlots(_viewModel.LayoutPreset), work);
                    if (favorite.Count > 0)
                    {
                        slots.AddRange(favorite);
                        break;
                    }
                    goto case LayoutChoice.Grid2x2;
                }
                case LayoutChoice.Grid2x2:
                case LayoutChoice.Auto:
                {
                    var halfW = width / 2;
                    var halfH = height / 2;
                    slots.Add(Make(work.Left, work.Top, halfW, halfH));
                    slots.Add(Make(work.Left + halfW, work.Top, width - halfW, halfH));
                    slots.Add(Make(work.Left, work.Top + halfH, halfW, height - halfH));
                    slots.Add(Make(work.Left + halfW, work.Top + halfH, width - halfW, height - halfH));
                    break;
                }
                case LayoutChoice.TwoUp:
                {
                    var half = width / 2;
                    slots.Add(Make(work.Left, work.Top, half, height));
                    slots.Add(Make(work.Left + half, work.Top, width - half, height));
                    break;
                }
                case LayoutChoice.ThreePane:
                {
                    var leftWidth = (int)(width * 0.5);
                    var rightWidth = width - leftWidth;
                    var halfHeight = height / 2;
                    slots.Add(Make(work.Left, work.Top, leftWidth, height));
                    slots.Add(Make(work.Left + leftWidth, work.Top, rightWidth, halfHeight));
                    slots.Add(Make(work.Left + leftWidth, work.Top + halfHeight, rightWidth, height - halfHeight));
                    break;
                }
                case LayoutChoice.ThreePaneMirror:
                {
                    var rightWidth = (int)(width * 0.5);
                    var leftWidth = width - rightWidth;
                    var halfHeight = height / 2;
                    slots.Add(Make(work.Left, work.Top, leftWidth, halfHeight));
                    slots.Add(Make(work.Left, work.Top + halfHeight, leftWidth, height - halfHeight));
                    slots.Add(Make(work.Left + leftWidth, work.Top, rightWidth, height));
                    break;
                }
            }

            return slots;
        }

        private CustomLayoutSlot NormalizeRectToWorkArea(RECT rect, RECT workArea)
        {
            var workWidth = Math.Max(1, workArea.Right - workArea.Left);
            var workHeight = Math.Max(1, workArea.Bottom - workArea.Top);

            double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

            var x = Clamp01((rect.Left - workArea.Left) / (double)workWidth);
            var y = Clamp01((rect.Top - workArea.Top) / (double)workHeight);
            var width = Clamp01((rect.Right - rect.Left) / (double)workWidth);
            var height = Clamp01((rect.Bottom - rect.Top) / (double)workHeight);

            width = Math.Min(width, 1 - x);
            height = Math.Min(height, 1 - y);

            return new CustomLayoutSlot
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
        }

        private bool SlotsClose(CustomLayoutSlot a, CustomLayoutSlot b, double tol)
        {
            return Math.Abs(a.X - b.X) <= tol
                   && Math.Abs(a.Y - b.Y) <= tol
                   && Math.Abs(a.Width - b.Width) <= tol
                   && Math.Abs(a.Height - b.Height) <= tol;
        }

        private List<RECT> BuildSavedSlots(IReadOnlyList<CustomLayoutSlot> savedSlots, RECT work)
        {
            var result = new List<RECT>();
            var workWidth = Math.Max(1, work.Right - work.Left);
            var workHeight = Math.Max(1, work.Bottom - work.Top);

            foreach (var slot in savedSlots)
            {
                var left = work.Left + (int)Math.Round(slot.X * workWidth);
                var top = work.Top + (int)Math.Round(slot.Y * workHeight);
                var width = (int)Math.Round(slot.Width * workWidth);
                var height = (int)Math.Round(slot.Height * workHeight);

                width = Math.Max(120, width);
                height = Math.Max(120, height);

                left = Math.Max(work.Left, Math.Min(left, work.Right - width));
                top = Math.Max(work.Top, Math.Min(top, work.Bottom - height));

                var right = Math.Min(work.Right, left + width);
                var bottom = Math.Min(work.Bottom, top + height);

                result.Add(new RECT
                {
                    Left = left,
                    Top = top,
                    Right = right,
                    Bottom = bottom
                });
            }

            try
            {
                LogTrace("Saved normalized slots: " + string.Join("; ", savedSlots.Select(s => $"({s.X:F3},{s.Y:F3},{s.Width:F3},{s.Height:F3})")));
                LogTrace("Saved realized slots: " + string.Join("; ", result.Select(DescribeRect)));
            }
            catch
            {
                // best effort logging
            }

            return result;
        }

        private List<(IntPtr Hwnd, RECT Rect)> BuildFavoriteAssignments(List<(IntPtr Hwnd, RECT Rect)> windows, List<RECT> slots, IntPtr iconGridHandle, int preferredIconSlot, bool reserveIconGridSlot)
        {
            var slotCount = slots.Count;
            var assignments = Enumerable.Repeat((IntPtr.Zero, new RECT()), slotCount).ToArray();

            var remainingWindows = new List<(IntPtr Hwnd, RECT Rect)>(windows);
            var availableSlots = Enumerable.Range(0, slotCount).ToList();

            // Pin IconGrid to its saved slot if present.
            if (reserveIconGridSlot && iconGridHandle != IntPtr.Zero && preferredIconSlot >= 0 && preferredIconSlot < slotCount)
            {
                var idx = remainingWindows.FindIndex(w => w.Hwnd == iconGridHandle);
                if (idx >= 0)
                {
                    assignments[preferredIconSlot] = (iconGridHandle, slots[preferredIconSlot]);
                    remainingWindows.RemoveAt(idx);
                    availableSlots.Remove(preferredIconSlot);
                }
            }

            if (remainingWindows.Count == 0 || availableSlots.Count == 0)
            {
                return assignments.ToList();
            }

            // Match remaining windows to remaining slots by closeness.
            var matches = MatchWindowsToSlotsWithSlots(remainingWindows, availableSlots, slots);
            foreach (var (slotIndex, window) in matches)
            {
                if (slotIndex >= 0 && slotIndex < assignments.Length)
                {
                    assignments[slotIndex] = (window.Hwnd, slots[slotIndex]);
                }
            }

            var finalAssignments = assignments.ToList();

            try
            {
                LogTrace("Favorite assignment mapping: " + string.Join("; ", finalAssignments.Select((a, idx) => $"slot{idx}:{DescribeHandle(a.Item1)}->{DescribeRect(a.Item2)}")));
            }
            catch
            {
                // ignore log issues
            }

            return finalAssignments;
        }

        private string DescribeHandle(IntPtr hwnd) => hwnd == IntPtr.Zero ? "null" : $"0x{hwnd.ToInt64():X}";

        private string DescribeWindow((IntPtr hwnd, RECT rect) w)
        {
            string title = string.Empty;
            try
            {
                var len = GetWindowTextLength(w.hwnd);
                if (len > 0 && len < 512)
                {
                    var sb = new StringBuilder(len + 5);
                    GetWindowText(w.hwnd, sb, sb.Capacity);
                    title = sb.ToString().Trim();
                }
            }
            catch
            {
                // ignore title fetch errors
            }

            string cls = string.Empty;
            try
            {
                var sbCls = new StringBuilder(256);
                if (GetClassName(w.hwnd, sbCls, sbCls.Capacity) > 0)
                {
                    cls = sbCls.ToString().Trim();
                }
            }
            catch
            {
                // ignore class fetch errors
            }

            GetWindowThreadProcessId(w.hwnd, out var pid);
            var titlePart = string.IsNullOrWhiteSpace(title) ? "" : $" \"{title}\"";
            var classPart = string.IsNullOrWhiteSpace(cls) ? "" : $" [{cls}]";
            return $"{DescribeHandle(w.hwnd)}:{DescribeRect(w.rect)} pid={pid}{titlePart}{classPart}";
        }

        private string DescribeRect(RECT rect) => $"({rect.Left},{rect.Top},{rect.Right - rect.Left}x{rect.Bottom - rect.Top})";

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _monitorTimer?.Stop();
            _autoHideTimer?.Stop();
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.SystemMonitor.PropertyChanged -= SystemMonitor_PropertyChanged;
                _viewModel.SystemMonitor.Dispose();
            }
            ClosePawnIoWarningWindow();
            if (_settingsWindow != null)
            {
                _settingsWindow.Close();
                _settingsWindow = null;
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            ThemeHelper.ThemeChanged -= ThemeHelper_ThemeChanged;
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }

        private void ExitApplication()
        {
            _monitorTimer?.Stop();
            _autoHideTimer?.Stop();
            ClosePawnIoWarningWindow();
            if (_settingsWindow != null)
            {
                _settingsWindow.Close();
                _settingsWindow = null;
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            var application = System.Windows.Application.Current;
            var windows = application?.Windows;
            if (windows != null)
            {
                foreach (var window in windows.OfType<System.Windows.Window>().ToList())
                {
                    if (window != this)
                    {
                        window.Close();
                    }
                }
            }
            application?.Shutdown();
            System.Environment.Exit(0);
        }

        // Ensure clicking a tab sets SelectedTab on the VM and closes overlays.
        private void TabToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.DataContext is not string tabName)
                return;

            _viewModel.IsSettingsOpen = false;

            // If the animation feature is enabled, handle the toggle/expand logic.
            if (_viewModel.EnableSlideUpAnimation)
            {
                // If the same tab is clicked, toggle the panel's visibility.
                if (string.Equals(_viewModel.SelectedTab, tabName, StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.IsIconPanelExpanded = !_viewModel.IsIconPanelExpanded;
                }
                else
                {
                    // If a different tab is clicked, always expand the panel and switch tabs.
                    _viewModel.IsIconPanelExpanded = true;
                    _viewModel.SelectedTab = tabName;
                }
            }
            else
            {
                // Default behavior when animation is disabled: just switch tabs.
                _viewModel.IsIconPanelExpanded = true; // Ensure it's always expanded
                if (!string.Equals(_viewModel.SelectedTab, tabName, StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.SelectedTab = tabName;
                }
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            var moved = double.IsNaN(_lastLoggedLeft)
                        || Math.Abs(Left - _lastLoggedLeft) > 0.5
                        || Math.Abs(Top - _lastLoggedTop) > 0.5;
            if (moved)
            {
                LogTrace($"OnLocationChanged skip={_skipSavingLocation}, pos={Left:F1},{Top:F1}, size={Width:F1}x{Height:F1}, state={WindowState}");
                _lastLoggedLeft = Left;
                _lastLoggedTop = Top;
            }

            if (_viewModel.IsFullWindowVisible && WindowState == WindowState.Normal)
            {
                if (!_skipSavingLocation)
                {
                    _viewModel.SaveWindowPosition(Left, Top);
                }
            }
            else if (!_viewModel.IsFullWindowVisible && WindowState == WindowState.Normal)
            {
                _floatingIconController.HandleLocationChanged(this, _viewModel, _skipSavingLocation);
            }
        }
    }
}















