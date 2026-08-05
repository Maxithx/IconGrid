using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace IconGrid.Helpers.Launcher
{
    /// <summary>
    /// Switches the primary display to a target resolution before a game launches,
    /// and restores the original resolution when the game exits.
    /// </summary>
    public sealed class DisplayResolutionService : IDisposable
    {
        // ---- Win32 definitions ----
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        private const int ENUM_CURRENT_SETTINGS = unchecked((int)0xFFFFFFFF);
        private const uint CDS_UPDATEREGISTRY = 0x00000001;
        private const uint CDS_TEST = 0x00000002;
        private const uint DISP_CHANGE_SUCCESSFUL = 0;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string? deviceName, ref DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string? deviceName, IntPtr devMode, IntPtr hwnd, uint flags, IntPtr lParam);

        private const string PrimaryDeviceName = null; // null => primary display

        private readonly object _lock = new();
        private readonly Dictionary<int, DEVMODE> _savedDevModes = new();
        private DEVMODE? _pendingOriginalMode;
        private CancellationTokenSource? _watchdogCts;
        private bool _disposed;

        public event Action? ResolutionRestored;

        public static IReadOnlyList<string> GetSupportedResolutions()
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            var mode = new DEVMODE();
            mode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            var index = 0;
            while (EnumDisplaySettings(PrimaryDeviceName, index, ref mode))
            {
                if (mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0)
                {
                    var key = $"{mode.dmPelsWidth}x{mode.dmPelsHeight}";
                    if (seen.Add(key))
                        result.Add(key);
                }

                mode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                index++;
            }

            return result
                .OrderByDescending(r => ParseResolution(r)?.Width ?? 0)
                .ThenByDescending(r => ParseResolution(r)?.Height ?? 0)
                .ToList();
        }

        public static string? GetCurrentResolution()
        {
            var mode = new DEVMODE();
            mode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(PrimaryDeviceName, ENUM_CURRENT_SETTINGS, ref mode))
                return null;

            return mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0
                ? $"{mode.dmPelsWidth}x{mode.dmPelsHeight}"
                : null;
        }

        public static (int Width, int Height)? ParseResolution(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var parts = value.Split('x', 'X', '×');
            if (parts.Length != 2)
                return null;

            if (int.TryParse(parts[0].Trim(), out var w) &&
                int.TryParse(parts[1].Trim(), out var h) &&
                w > 0 && h > 0)
            {
                return (w, h);
            }

            return null;
        }

        public bool TrySetResolution(string resolution)
        {
            var parsed = ParseResolution(resolution);
            return parsed != null && TrySetResolution(parsed.Value.Width, parsed.Value.Height);
        }

        public bool TrySetResolution(int width, int height)
        {
            lock (_lock)
            {
                var current = new DEVMODE();
                current.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettings(PrimaryDeviceName, ENUM_CURRENT_SETTINGS, ref current))
                    return false;

                if (current.dmPelsWidth == width && current.dmPelsHeight == height)
                    return true;

                var target = new DEVMODE();
                target.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettings(PrimaryDeviceName, ENUM_CURRENT_SETTINGS, ref target))
                    return false;

                target.dmPelsWidth = (uint)width;
                target.dmPelsHeight = (uint)height;

                if (ChangeDisplaySettingsEx(PrimaryDeviceName, ref target, IntPtr.Zero, CDS_TEST, IntPtr.Zero) != DISP_CHANGE_SUCCESSFUL)
                    return false;

                var result = ChangeDisplaySettingsEx(PrimaryDeviceName, ref target, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                if (result == DISP_CHANGE_SUCCESSFUL)
                {
                    _pendingOriginalMode = current;
                    Debug.WriteLine($"[DisplayResolutionService] Switched primary display to {width}x{height}");
                }
                else
                {
                    Debug.WriteLine($"[DisplayResolutionService] Change to {width}x{height} failed: {result}");
                }
                return result == DISP_CHANGE_SUCCESSFUL;
            }
        }

        public void RestoreResolution(int rootProcessId)
        {
            DEVMODE original;
            lock (_lock)
            {
                if (!_savedDevModes.TryGetValue(rootProcessId, out original))
                    return;

                _savedDevModes.Remove(rootProcessId);
            }

            var result = ChangeDisplaySettingsEx(PrimaryDeviceName, ref original, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            Debug.WriteLine(result == DISP_CHANGE_SUCCESSFUL
                ? $"[DisplayResolutionService] Restored original resolution for process {rootProcessId}"
                : $"[DisplayResolutionService] Restore failed for process {rootProcessId}: {result}");

            ResolutionRestored?.Invoke();
        }

        public void WatchProcess(int rootProcessId, TimeSpan? crashTimeout = null)
        {
            var timeout = crashTimeout ?? TimeSpan.FromSeconds(30);

            lock (_lock)
            {
                _watchdogCts?.Cancel();
                _watchdogCts?.Dispose();
                _watchdogCts = new CancellationTokenSource();
            }

            var token = _watchdogCts.Token;

            Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                while (!token.IsCancellationRequested)
                {
                    if (!IsProcessAlive(rootProcessId))
                    {
                        RestoreResolution(rootProcessId);
                        return;
                    }

                    if (sw.Elapsed >= timeout)
                    {
                        Debug.WriteLine($"[DisplayResolutionService] Watchdog timeout ({sw.Elapsed.TotalSeconds:F0}s); restoring resolution as crash fallback.");
                        RestoreResolution(rootProcessId);
                        return;
                    }

                    try
                    {
                        await Task.Delay(1000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }, token);
        }

        /// <summary>
        /// Binds the pending original resolution (captured right before the switch)
        /// to a launched process id so it can be restored when that process exits.
        /// </summary>
        public bool AttachPendingResolution(int rootProcessId)
        {
            lock (_lock)
            {
                if (_pendingOriginalMode == null)
                    return false;

                _savedDevModes[rootProcessId] = _pendingOriginalMode.Value;
                _pendingOriginalMode = null;
                return true;
            }
        }

        private static bool IsProcessAlive(int pid)
        {
            if (pid <= 0)
                return false;

            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
        }
    }
}