using System;
using System.Diagnostics;
using System.IO;
using IconGrid.Models;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherItemLaunchManager
    {
        private readonly Action<LauncherItem, FpsTargetConfig>? _onLaunching;

        public LauncherItemLaunchManager(Action<LauncherItem, FpsTargetConfig>? onLaunching = null)
        {
            _onLaunching = onLaunching;
        }

        public bool LaunchItem(LauncherItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return false;

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
                _onLaunching?.Invoke(item, CreateFpsTargetConfig(item, workingDirectory, launchedProcess));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {item.Path}: {ex}");
                return false;
            }
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
