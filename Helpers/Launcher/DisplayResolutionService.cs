using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private WindowTrackingService? _trackingService;

        public event Action? ResolutionRestored;

        /// <summary>
        /// Injects the window tracking service so it can be notified of resolution
        /// changes. This keeps the tracking store's current-resolution key in sync
        /// with the actual display state.
        /// </summary>
        public void SetTrackingService(WindowTrackingService? service) => _trackingService = service;

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
                    var resKey = $"{width}x{height}";
                    Debug.WriteLine($"[DisplayResolutionService] Switched primary display to {resKey}");
                    WriteTrace($"[DisplayResolutionService] Switched primary display to {resKey}");
                    _trackingService?.NotifyResolutionChange(resKey);
                }
                else
                {
                    Debug.WriteLine($"[DisplayResolutionService] Change to {width}x{height} failed: {result}");
                    WriteTrace($"[DisplayResolutionService] Change to {width}x{height} failed: {result}");
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
            WriteTrace(result == DISP_CHANGE_SUCCESSFUL
                ? $"[DisplayResolutionService] Restored original resolution for process {rootProcessId}"
                : $"[DisplayResolutionService] Restore failed for process {rootProcessId}: {result}");

            _trackingService?.NotifyResolutionChange(GetCurrentResolution() ?? "3840x2160");
            ResolutionRestored?.Invoke();
        }

        /// <summary>
        /// Holds the resolution lock for the lifetime of the launched game, mirroring the
        /// sticky-target pattern of the FPS/ETW pipeline: like an entry in Task Manager,
        /// the lock is only released when the game process actually exits. Foreground
        /// changes (alt-tab, Windows key, other windows) never release it.
        /// </summary>
        public void WatchProcess(int rootProcessId)
        {
            var identity = ResolveProcessIdentity(rootProcessId);

            lock (_lock)
            {
                _watchdogCts?.Cancel();
                _watchdogCts?.Dispose();
                _watchdogCts = new CancellationTokenSource();
            }

            var token = _watchdogCts.Token;

            WriteTrace(identity != null
                ? $"[DisplayResolutionService] WatchProcess started for PID {identity.Value.Pid} (root {rootProcessId}, StartTime {identity.Value.StartFileTimeUtc}). Resolution lock held until the game process exits."
                : $"[DisplayResolutionService] WatchProcess started for PID {rootProcessId} but the process could not be resolved; holding lock until the process exits.");

            Task.Run(async () =>
            {
                var watchPid = identity?.Pid ?? rootProcessId;
                var watchStartFileTimeUtc = identity?.StartFileTimeUtc ?? 0L;
                var executableName = identity?.ExecutableName;

                while (!token.IsCancellationRequested)
                {
                    if (!IsProcessAlive(watchPid, watchStartFileTimeUtc))
                    {
                        // The watched process exited. If it was a short-lived root (e.g. a
                        // launcher) that handed off to the real game process with the same
                        // executable name, keep the lock and follow the handoff instead of
                        // restoring the original resolution.
                        var handoffPid = FindHandoffProcess(watchPid, executableName);
                        if (handoffPid > 0)
                        {
                            WriteTrace($"[DisplayResolutionService] Root process {watchPid} exited but a game process with the same executable is still running (PID {handoffPid}); keeping the resolution locked.");
                            var handoffIdentity = ResolveProcessIdentity(handoffPid);
                            if (handoffIdentity != null)
                            {
                                watchPid = handoffIdentity.Value.Pid;
                                watchStartFileTimeUtc = handoffIdentity.Value.StartFileTimeUtc;
                                executableName = handoffIdentity.Value.ExecutableName;
                            }
                            continue;
                        }

                        WriteTrace($"[DisplayResolutionService] Process {watchPid} exited; restoring resolution.");
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

        private static bool IsProcessAlive(int pid, long startFileTimeUtc)
        {
            if (pid <= 0)
                return false;

            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return false;

                // PID-reuse protection: if the PID was recycled since we locked it, treat it
                // as gone so a completely different process with the same PID cannot hold the
                // resolution lock indefinitely.
                if (startFileTimeUtc > 0)
                {
                    var currentStartFileTimeUtc = process.StartTime.ToUniversalTime().ToFileTimeUtc();
                    if (currentStartFileTimeUtc != startFileTimeUtc)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static (int Pid, long StartFileTimeUtc, string? ExecutableName)? ResolveProcessIdentity(int pid)
        {
            if (pid <= 0)
                return null;

            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return null;

                string? executableName = null;
                try
                {
                    var fileName = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(fileName))
                        executableName = Path.GetFileName(fileName);
                }
                catch
                {
                    // access denied or process exited between calls — executable name stays null
                }

                return (process.Id, process.StartTime.ToUniversalTime().ToFileTimeUtc(), executableName);
            }
            catch
            {
                return null;
            }
        }

        private static int FindHandoffProcess(int deadPid, string? executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
                return 0;

            var bestPid = 0;
            var bestStartTime = DateTime.MinValue;

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.Id == deadPid || process.HasExited)
                            continue;

                        var fileName = process.MainModule?.FileName;
                        if (fileName == null ||
                            !string.Equals(Path.GetFileName(fileName), executableName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Prefer the newest matching instance — that is the actual game process.
                        var startTime = process.StartTime;
                        if (startTime > bestStartTime)
                        {
                            bestStartTime = startTime;
                            bestPid = process.Id;
                        }
                    }
                    catch
                    {
                        // skip inaccessible or transient processes
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                return 0;
            }

            return bestPid;
        }

        private static void WriteTrace(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "IconGrid");
                System.IO.Directory.CreateDirectory(folder);
                var logPath = System.IO.Path.Combine(folder, "trace.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
            }
            catch
            {
                // logging must never break resolution handling
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