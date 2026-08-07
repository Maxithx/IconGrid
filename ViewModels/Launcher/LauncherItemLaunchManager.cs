using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemLaunchManager
    {
        // Win32 for FindAnyGameProcess — enumerates visible windows to find
        // a game's real process regardless of launcher/anti-cheat wrapper name.
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }


        private readonly Action<LauncherItem, FpsTargetConfig>? _onLaunching;
        private readonly DisplayResolutionService? _displayResolutionService;
        private readonly Func<bool>? _shouldRestoreResolution;
        private WindowLayoutSnapshotService? _windowSnapshot;
        private WindowTrackingService? _trackingService;

        public void SetWindowLayoutSnapshotService(WindowLayoutSnapshotService? service) => _windowSnapshot = service;

        /// <summary>
        /// Injects the window tracking service so SafetyPass can be called after
        /// resolution restore to ensure no windows remain off-screen.
        /// </summary>
        public void SetTrackingService(WindowTrackingService? service) => _trackingService = service;

        public LauncherItemLaunchManager(
            Action<LauncherItem, FpsTargetConfig>? onLaunching = null,
            DisplayResolutionService? displayResolutionService = null,
            Func<bool>? shouldRestoreResolution = null)
        {
            _onLaunching = onLaunching;
            _displayResolutionService = displayResolutionService;
            _shouldRestoreResolution = shouldRestoreResolution;

            if (_displayResolutionService != null)
            {
                _displayResolutionService.ResolutionRestored += OnResolutionRestored;
            }
        }

        private void OnResolutionRestored()
        {
            _ = RestoreAfterResolutionSettlesAsync();
        }

        private async Task RestoreAfterResolutionSettlesAsync()
        {
            // The 400ms delay gives the display mode transition time to settle
            // before any downstream code reads the current resolution. We do NOT
            // automatically reposition windows here — the user's saved layout
            // system handles window positioning correctly, and the tracking
            // service provides LastSeenOpenRect data for any edge cases (e.g.
            // minimized windows that the layout engine needs to place).
            try
            {
                await Task.Delay(400).ConfigureAwait(false);
            }
            catch
            {
            }

            // No automatic window restore. The tracking service continues to
            // run in the background and the layout engine can query it when
            // the user applies a saved layout.
        }

        public bool LaunchItem(LauncherItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return false;

            _windowSnapshot?.Capture();

            var resolutionChanged = TrySwitchResolution(item);

            try
            {
                var psi = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true
                };

                var workingDirectory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                if (!string.IsNullOrWhiteSpace(item.Arguments))
                {
                    psi.Arguments = item.Arguments;
                }

                using var launchedProcess = Process.Start(psi);

                var rootProcessId = TryGetRootProcessId(launchedProcess);
                if (resolutionChanged && _displayResolutionService != null &&
                    (_shouldRestoreResolution?.Invoke() ?? true))
                {
                    if (rootProcessId > 0)
                    {
                        _displayResolutionService.AttachPendingResolution(rootProcessId);
                        _displayResolutionService.WatchProcess(rootProcessId);
                    }

                    // Always scan for the real game process. Launcher chains (EACLaunch ->
                    // TheDivision2, BattleEye, Ubisoft Connect, Steam bootstrappers) hand off
                    // to a game process with a DIFFERENT executable name, and the launcher
                    // often stays alive in the background after the game exits. If we only
                    // watch the launcher process, the resolution lock is never released.
                    //
                    // This scan runs continuously for up to 60 seconds (120 × 500ms). It
                    // finds ANY recently spawned process with a game-sized window and binds
                    // the resolution lock to it, re-targeting whenever a NEWER qualifying
                    // process appears. This covers:
                    //   - rootProcessId == 0 (launcher already exited before WatchProcess)
                    //   - launcher chains (lock bound to the launcher, then re-targeted to the
                    //     real game process when it creates its window)
                    //   - slow startup chains where the game takes a while to initialize a window
                    var svc = _displayResolutionService;
                    var currentLockedPid = rootProcessId;
                    _ = Task.Run(async () =>
                    {
                        for (var i = 0; i < 120; i++)
                        {
                            var foundPid = FindAnyGameProcess();
                            if (foundPid > 0 && foundPid != currentLockedPid)
                            {
                                if (currentLockedPid > 0)
                                {
                                    WriteTrace($"[LauncherItemLaunchManager] Found newer game process via FindAnyGameProcess: PID {foundPid}. Re-targeting resolution lock (was PID {currentLockedPid}).");
                                    if (!svc.RetargetResolutionLock(currentLockedPid, foundPid))
                                    {
                                        // The lock we knew may not exist (e.g. the launcher had no
                                        // saved DEVMODE because the display was already at the target
                                        // resolution when we launched). Fall back to binding the new
                                        // process directly with the pending original mode if any.
                                        WriteTrace($"[LauncherItemLaunchManager] Re-target failed (no saved lock for PID {currentLockedPid}); binding PID {foundPid} directly.");
                                        svc.AttachPendingResolution(foundPid);
                                        svc.WatchProcess(foundPid);
                                    }
                                }
                                else
                                {
                                    WriteTrace($"[LauncherItemLaunchManager] Found real game process via FindAnyGameProcess: PID {foundPid}. Binding resolution lock.");
                                    var parsed = DisplayResolutionService.ParseResolution(item.GameResolution);
                                    if (parsed != null)
                                        svc.TrySetResolution(parsed.Value.Width, parsed.Value.Height);
                                    svc.AttachPendingResolution(foundPid);
                                    svc.WatchProcess(foundPid);
                                }
                                currentLockedPid = foundPid;
                            }

                            try { await Task.Delay(500).ConfigureAwait(false); }
                            catch { return; }
                        }

                        WriteTrace($"[LauncherItemLaunchManager] FindAnyGameProcess gave up after 60s for {item.DisplayName}. No qualifying game process found.");
                    });
                }


                _onLaunching?.Invoke(item, CreateFpsTargetConfig(item, workingDirectory, launchedProcess));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {item.Path}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Finds ANY recently spawned GUI process that looks like a game.
        ///
        /// Combines two window-discovery sources so it works for BOTH fullscreen
        /// windowed games (Process.MainWindowHandle) AND exclusive fullscreen /
        /// DX12 games whose main window is not exposed through MainWindowHandle
        /// (EnumWindows + IsWindowVisible, which also sees tool-window styled
        /// main windows that MainWindowHandle hides).
        ///
        /// Filters:
        ///   - Window must be game-sized (at least 960x540) to exclude small
        ///     launchers (e.g. EACLaunch at 800x450) and other non-game windows.
        ///   - Process must have started within the last 120s (it was just
        ///     launched from IconGrid) to exclude long-running system processes
        ///     (SystemSettings, copilot, explorer, etc.).
        ///
        /// Prefers the newest qualifying process — that is the actual game.
        /// </summary>
        private static int FindAnyGameProcess()
        {
            const int MinWindowWidth = 960;
            const int MinWindowHeight = 540;
            var recencyWindow = TimeSpan.FromSeconds(120);
            var now = DateTime.Now;

            var candidates = new Dictionary<int, DateTime>();
            var currentPid = Process.GetCurrentProcess().Id;

            // Source 1: .NET MainWindowHandle — covers most windowed and
            // fullscreen-windowed games.
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == currentPid || process.HasExited)
                        continue;

                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                        continue;

                    if (!GetWindowRect(hwnd, out var rect))
                        continue;

                    if (rect.Right - rect.Left < MinWindowWidth ||
                        rect.Bottom - rect.Top < MinWindowHeight)
                    {
                        continue;
                    }

                    var startTime = process.StartTime;
                    if (startTime < now - recencyWindow)
                        continue;

                    candidates[process.Id] = startTime;
                }
                catch
                {
                    // Skip inaccessible or transient processes.
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Source 2: EnumWindows — visible top-level windows. Catches games
            // whose main window has no .NET MainWindowHandle (exclusive
            // fullscreen, tool-window styled main windows, etc.).
            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                if (!GetWindowRect(hwnd, out var rect))
                    return true;

                if (rect.Right - rect.Left < MinWindowWidth ||
                    rect.Bottom - rect.Top < MinWindowHeight)
                {
                    return true;
                }

                GetWindowThreadProcessId(hwnd, out uint ownerPid);
                if (ownerPid == 0 || ownerPid == (uint)currentPid)
                    return true;

                if (candidates.ContainsKey((int)ownerPid))
                    return true;

                try
                {
                    using var process = Process.GetProcessById((int)ownerPid);
                    if (process.HasExited)
                        return true;

                    var startTime = process.StartTime;
                    if (startTime < now - recencyWindow)
                        return true;

                    candidates[(int)ownerPid] = startTime;
                }
                catch
                {
                    // Skip transient process.
                }

                return true;
            }, IntPtr.Zero);

            // Prefer the newest qualifying process.
            var bestPid = 0;
            var bestStartTime = DateTime.MinValue;
            foreach (var candidate in candidates)
            {
                if (candidate.Value > bestStartTime)
                {
                    bestStartTime = candidate.Value;
                    bestPid = candidate.Key;
                }
            }

            return bestPid;
        }

        private bool TrySwitchResolution(LauncherItem item)
        {
            if (_displayResolutionService == null)
                return false;

            if (string.IsNullOrWhiteSpace(item.GameResolution))
                return false;

            try
            {
                return _displayResolutionService.TrySetResolution(item.GameResolution);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to switch display resolution for {item.Path}: {ex}");
                return false;
            }
        }

        private static int TryGetRootProcessId(Process? launchedProcess)
        {
            if (launchedProcess == null)
                return 0;

            try
            {
                if (!launchedProcess.HasExited)
                    return launchedProcess.Id;
            }
            catch
            {
            }

            return 0;
        }

        private static FpsTargetConfig CreateFpsTargetConfig(LauncherItem item, string? workingDirectory, Process? launchedProcess)
        {
            var launchCapturedAtUtc = DateTime.UtcNow;
            var resolvedExecutablePath = NormalizePath(item.Path);
            var executableName = Path.GetFileName(resolvedExecutablePath);
            var rootProcessId = default(int?);
            var rootProcessStartFileTimeUtc = default(long?);

            if (launchedProcess != null)
            {
                try
                {
                    if (!launchedProcess.HasExited)
                    {
                        rootProcessId = launchedProcess.Id;
                        rootProcessStartFileTimeUtc = launchedProcess.StartTime.ToUniversalTime().ToFileTimeUtc();
                        var mainModulePath = launchedProcess.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(mainModulePath))
                        {
                            resolvedExecutablePath = NormalizePath(mainModulePath);
                            executableName = Path.GetFileName(resolvedExecutablePath);
                            workingDirectory ??= Path.GetDirectoryName(resolvedExecutablePath);
                        }
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(executableName))
            {
                executableName = Path.GetFileName(item.Path);
            }

            return new FpsTargetConfig
            {
                DisplayName = item.DisplayName,
                LauncherPath = item.Path,
                ResolvedExecutablePath = resolvedExecutablePath,
                ExecutableName = executableName,
                Arguments = item.Arguments,
                WorkingDirectory = NormalizePath(workingDirectory),
                RootProcessId = rootProcessId,
                RootProcessStartFileTimeUtc = rootProcessStartFileTimeUtc,
                LaunchCapturedFileTimeUtc = launchCapturedAtUtc.ToFileTimeUtc()
            };
        }

        private static void WriteTrace(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "IconGrid");
                Directory.CreateDirectory(folder);
                var logPath = Path.Combine(folder, "trace.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
            }
            catch
            {
                // logging must never break launch handling
            }
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }
    }
}