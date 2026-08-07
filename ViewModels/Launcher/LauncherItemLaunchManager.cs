using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemLaunchManager
    {
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
                if (rootProcessId > 0 && resolutionChanged && _displayResolutionService != null &&
                    (_shouldRestoreResolution?.Invoke() ?? true))
                {
                    _displayResolutionService.AttachPendingResolution(rootProcessId);
                    _displayResolutionService.WatchProcess(rootProcessId);
                }
                else if (rootProcessId == 0 && resolutionChanged && _displayResolutionService != null &&
                         (_shouldRestoreResolution?.Invoke() ?? true) &&
                         !string.IsNullOrWhiteSpace(item.Path))
                {
                    // Root process died before we could grab its PID (e.g. EACLaunch).
                    // Scan for the real game process in a background task and attach
                    // the resolution lock when found.
                    var gameExeName = Path.GetFileName(item.Path);
                    var svc = _displayResolutionService;
                    _ = Task.Run(async () =>
                    {
                        for (var i = 0; i < 30; i++)
                        {
                            var foundPid = FindProcessByName(gameExeName);
                            if (foundPid > 0)
                            {
                                var parsed = DisplayResolutionService.ParseResolution(item.GameResolution);
                                if (parsed != null)
                                    svc.TrySetResolution(parsed.Value.Width, parsed.Value.Height);
                                svc.AttachPendingResolution(foundPid);
                                svc.WatchProcess(foundPid);
                                return;
                            }

                            try { await Task.Delay(500).ConfigureAwait(false); }
                            catch { return; }
                        }
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

        private static int FindProcessByName(string exeName)
        {
            try
            {
                return Process.GetProcesses()
                    .Where(p =>
                    {
                        try { return !p.HasExited && string.Equals(p.ProcessName + ".exe", exeName, StringComparison.OrdinalIgnoreCase); }
                        catch { return false; }
                    })
                    .Select(p => p.Id)
                    .FirstOrDefault();
            }
            catch
            {
                return 0;
            }
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