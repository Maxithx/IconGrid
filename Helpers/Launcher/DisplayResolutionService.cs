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

        /// <summary>
        /// Absolute safety valve (NOT a grace period): if a launch session never
        /// produces a real game window (update check, launcher chain, aborted
        /// launch), hold the resolution lock for at most this long before restoring
        /// it, so the user is never stuck on the wrong resolution forever. Any path
        /// where the game actually ran and exited restores immediately.
        /// </summary>
        private static readonly TimeSpan AbsoluteResolutionHoldLimit = TimeSpan.FromMinutes(30);

        private readonly object _lock = new();
        private readonly Dictionary<int, DEVMODE> _savedDevModes = new();
        private DEVMODE? _pendingOriginalMode;
        private CancellationTokenSource? _watchdogCts;
        private bool _disposed;
        private WindowTrackingService? _trackingService;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // Shell/system processes that must NEVER be treated as a game window or
        // as a handoff target. Without this filter, SystemSettings,
        // XboxGameBarWidgets, SearchApp etc. get marked "game confirmed" by the
        // watchdog, which restores the resolution too early when the shell
        // window closes while the real game is still starting.
        private static readonly string[] NonGameProcessNames =
        {
            "explorer",
            "ApplicationFrameHost",
            "SearchApp",
            "StartMenuExperienceHost",
            "SystemSettings",
            "mscopilot",
            "WmiPrvSE",
            "XboxGameBar",
            "XboxGameBarWidgets",
            "GameBar",
            "GameBarPresenceWriter",
            "TextInputHost",
            "ShellExperienceHost",
            "Widgets",
            "Code",
            "brave",
            "chrome",
            "msedge",
            "firefox",
            "notepad",
            "mspaint",
            "Battle.net",
            "steam",
            "steamwebhelper",
            "upc",
            "EADesktop",
            "EpicGamesLauncher",
            "launcher"
        };

        // Per launch-session resolution-lock state. The lock follows the launch
        // session, not a single PID: as long as a process with the game's
        // executable name is alive (or is expected to appear, e.g. during an
        // update check), the resolution stays locked.
        // Both fields are only accessed from the watchdog task (single-threaded)
        // and reset under _lock before the task starts, so no volatile is needed.
        private bool _hasSeenGameRunning;
        private DateTime _sessionStartedAtUtc;

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

        /// <summary>
        /// Returns the current display resolution and refresh rate, e.g. "3840x2160 @ 144Hz".
        /// </summary>
        public static string GetCurrentResolutionWithHz()
        {
            var mode = new DEVMODE();
            mode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(PrimaryDeviceName, ENUM_CURRENT_SETTINGS, ref mode))
                return "Unknown";

            var hz = mode.dmDisplayFrequency > 0 ? $"{mode.dmDisplayFrequency}Hz" : "?Hz";
            return $"{mode.dmPelsWidth}x{mode.dmPelsHeight} @ {hz}";
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
            var executableName = identity?.ExecutableName;
            var startFileTimeUtc = identity?.StartFileTimeUtc ?? 0L;
            WatchProcess(rootProcessId, executableName, startFileTimeUtc);
        }

        /// <summary>
        /// Holds the resolution lock for the lifetime of a launch session, not a
        /// single PID. The lock follows any process with the game's executable
        /// name: while such a process is alive (or is expected to appear — e.g.
        /// during an update check where the game exits and restarts itself), the
        /// resolution stays locked.
        ///
        /// The resolution is restored in exactly two cases:
        ///   1. The game was observed running (a visible game-sized window) and
        ///      then all processes with its executable name exited — a real game
        ///      session ended. This restores IMMEDIATELY.
        ///   2. The game was NEVER observed running (update check / launcher
        ///      chain / aborted launch) and the absolute safety limit expires.
        ///      This is a safety valve only, not a fixed grace period, so
        ///      arbitrarily long update checks never restore too early.
        /// </summary>
        public void WatchProcess(int rootProcessId, string? executableName, long startFileTimeUtc = 0L)
        {
            var identity = rootProcessId > 0 ? ResolveProcessIdentity(rootProcessId) : null;
            if (identity != null)
            {
                executableName ??= identity.Value.ExecutableName;
                startFileTimeUtc = identity.Value.StartFileTimeUtc;
            }

            lock (_lock)
            {
                _watchdogCts?.Cancel();
                _watchdogCts?.Dispose();
                _watchdogCts = new CancellationTokenSource();
                _hasSeenGameRunning = false;
                _sessionStartedAtUtc = DateTime.UtcNow;
            }

            var token = _watchdogCts.Token;
            var watchPid = identity?.Pid ?? rootProcessId;
            var watchStartFileTimeUtc = identity?.StartFileTimeUtc ?? startFileTimeUtc;

            WriteTrace($"[DisplayResolutionService] WatchProcess started for PID {watchPid} (root {rootProcessId}, exe {executableName ?? "unknown"}). Resolution lock held until the launch session ends.");

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (watchPid > 0 && IsProcessAlive(watchPid, watchStartFileTimeUtc))
                    {
                        // The watched process is alive. When we see it with a real
                        // game-sized visible window, the game is confirmed running —
                        // a later exit of all processes with this exe is a REAL game
                        // close, and the resolution must be restored immediately.
                        if (!_hasSeenGameRunning && HasVisibleGameWindow(watchPid))
                        {
                            _hasSeenGameRunning = true;
                            WriteTrace($"[DisplayResolutionService] Game window confirmed for PID {watchPid}; marking session as game-running.");
                        }

                        try
                        {
                            await Task.Delay(1000, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        continue;
                    }

                    // The watched process is gone. Re-target to any process with the
                    // same executable name — the game may be an updater/launcher
                    // chain that restarts itself (PathOfExile, Division 2, COD).
                    var sessionPid = FindHandoffProcess(watchPid, executableName);
                    if (sessionPid > 0)
                    {
                        var sessionIdentity = ResolveProcessIdentity(sessionPid);
                        if (sessionIdentity != null)
                        {
                            watchPid = sessionIdentity.Value.Pid;
                            watchStartFileTimeUtc = sessionIdentity.Value.StartFileTimeUtc;
                            WriteTrace($"[DisplayResolutionService] Launch session continued by process PID {watchPid} (same executable); keeping the resolution locked.");
                            continue;
                        }
                    }

                    // No process with the game's executable name is alive.
                    if (_hasSeenGameRunning)
                    {
                        // The game actually ran and has now fully exited — restore
                        // the original resolution immediately (normal close).
                        WriteTrace($"[DisplayResolutionService] Game session confirmed running and exited; restoring resolution.");
                        RestoreResolution(rootProcessId);
                        return;
                    }

                    // The game was never observed running (update check / launcher
                    // chain / aborted launch). Keep the lock until the game appears
                    // or the absolute safety limit expires. This is NOT a grace
                    // period — a real game start + exit restores immediately.
                    if (DateTime.UtcNow - _sessionStartedAtUtc > AbsoluteResolutionHoldLimit)
                    {
                        WriteTrace($"[DisplayResolutionService] No game window appeared within {AbsoluteResolutionHoldLimit.TotalMinutes:F0} min of launch; restoring resolution.");
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

        /// <summary>
        /// Re-targets an existing resolution lock from one process to another.
        /// Used when a launcher (e.g. EACLaunch) hands off to the real game process
        /// with a different executable name. The saved original resolution is moved
        /// to the new process id and a fresh watchdog is started on it, so the
        /// resolution is restored when the REAL game process exits — not when the
        /// launcher (which often stays alive in the background) exits.
        /// </summary>
        public bool RetargetResolutionLock(int oldRootProcessId, int newRootProcessId)
        {
            if (oldRootProcessId == newRootProcessId)
                return false;

            lock (_lock)
            {
                if (!_savedDevModes.TryGetValue(oldRootProcessId, out var original))
                    return false;

                _savedDevModes.Remove(oldRootProcessId);
                _savedDevModes[newRootProcessId] = original;
            }

            WriteTrace($"[DisplayResolutionService] Re-targeted resolution lock from PID {oldRootProcessId} to PID {newRootProcessId}.");
            WatchProcess(newRootProcessId);
            return true;
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

        private static bool HasVisibleGameWindow(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (IsNonGameProcess(process.ProcessName))
                    return false;

                var hwnd = process.MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                    return false;

                if (!GetWindowRect(hwnd, out var rect))
                    return false;

                var width = Math.Max(0, rect.Right - rect.Left);
                var height = Math.Max(0, rect.Bottom - rect.Top);
                return width >= 960 && height >= 540;
            }
            catch
            {
                return false;
            }
        }

        private static int FindHandoffProcess(int deadPid, string? executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName))
                return 0;

            var expectedProcessName = Path.GetFileNameWithoutExtension(executableName);

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

                        // Never treat shell/system processes as the game handoff.
                        if (IsNonGameProcess(process.ProcessName))
                            continue;

                        var fileName = process.MainModule?.FileName;
                        var matches = false;
                        if (fileName != null)
                        {
                            matches = string.Equals(Path.GetFileName(fileName), executableName, StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            // MainModule is unavailable for anti-cheat protected processes
                            // (Path of Exile, The Division 2, COD...) — fall back to the
                            // process name so the handoff/restart can still be detected.
                            matches = string.Equals(process.ProcessName, expectedProcessName, StringComparison.OrdinalIgnoreCase);
                        }

                        if (!matches)
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

        private static bool IsNonGameProcess(string processName)
        {
            foreach (var ignored in NonGameProcessNames)
            {
                if (string.Equals(processName, ignored, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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