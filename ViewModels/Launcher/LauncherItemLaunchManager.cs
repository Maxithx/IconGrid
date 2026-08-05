using System;
using System.Diagnostics;
using System.IO;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemLaunchManager
    {
        private readonly Action<LauncherItem, FpsTargetConfig>? _onLaunching;
        private readonly DisplayResolutionService? _displayResolutionService;
        private readonly Func<bool>? _shouldRestoreResolution;
        private WindowLayoutSnapshotService? _windowSnapshot; // owned by caller (MainViewModel)

        public void SetWindowLayoutSnapshotService(WindowLayoutSnapshotService? service) => _windowSnapshot = service;

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
            // The game exited (or the crash-fallback watchdog fired). Put the
            // windows back where they were before the resolution switch.
            _windowSnapshot?.Restore();
        }

        public bool LaunchItem(LauncherItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return false;

            // Remember where the desktop windows are right now so we can restore
            // them after the game exits and the resolution is restored.
            _windowSnapshot?.Capture();

            // Switch display resolution before launching (when configured on the shortcut).
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

                _onLaunching?.Invoke(item, CreateFpsTargetConfig(item, workingDirectory, launchedProcess));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {item.Path}: {ex}");
                return false;
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
