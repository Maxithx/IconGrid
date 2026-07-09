using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;
using IconGrid.Models;

namespace IconGrid.Helpers.Settings;

public static class StartupTaskManager
{
    private const string RegistryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedStartupFolderPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    private const string AppCompatLayersPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string ValueName = "IconGrid";
    private const string TaskName = "IconGrid";
    private const string MonitorTaskName = "IconGrid Monitor";
    private const string StartupLaunchArgument = "--startup-launch";
    private const string MonitorAgentArgument = "--monitor-agent";

    public static bool Register(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true);
            if (key == null)
                return false;

            key.SetValue(ValueName, BuildStartupCommand(exePath), RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to write startup registry key: " + ex);
            return false;
        }
    }

    public static bool Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true);
            if (key == null)
                return false;

            if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);

            DeleteStartupApprovedEntry(Registry.CurrentUser, StartupApprovedRunPath);
            DeleteStartupApprovedEntry(Registry.LocalMachine, StartupApprovedRunPath);
            DeleteStartupApprovedEntry(Registry.CurrentUser, StartupApprovedStartupFolderPath);
            DeleteStartupApprovedEntry(Registry.LocalMachine, StartupApprovedStartupFolderPath);
            DeleteCompatLayerEntries();
            DeleteStartupFolderShortcuts();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to delete startup registry key: " + ex);
            return false;
        }
    }

    public static void CleanupLegacyStartupEntries()
    {
        try
        {
            DeleteStartupApprovedEntry(Registry.CurrentUser, StartupApprovedRunPath);
            DeleteStartupApprovedEntry(Registry.LocalMachine, StartupApprovedRunPath);
            DeleteStartupApprovedEntry(Registry.CurrentUser, StartupApprovedStartupFolderPath);
            DeleteStartupApprovedEntry(Registry.LocalMachine, StartupApprovedStartupFolderPath);
            DeleteCompatLayerEntries();
            DeleteStartupFolderShortcuts();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to clean legacy startup entries: " + ex);
        }
    }

    public static bool RegisterTaskScheduler(string exePath, bool enabled)
    {
        return RegisterScheduledTask(exePath, TaskName, StartupLaunchArgument, "Limited", enabled);
    }

    public static bool RegisterMonitorTaskScheduler(string exePath, bool enabled)
    {
        return RegisterScheduledTask(exePath, MonitorTaskName, MonitorAgentArgument, "Highest", enabled);
    }

    public static bool TaskSchedulerExists()
    {
        try
        {
            var script = string.Join(Environment.NewLine, new[]
            {
                "Import-Module ScheduledTasks",
                $"$task = Get-ScheduledTask -TaskName '{TaskName}' -ErrorAction SilentlyContinue",
                "if ($null -ne $task) { exit 0 }",
                "exit 1"
            });

            return RunPowerShellScript(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to query scheduled startup task: " + ex);
            return false;
        }
    }

    public static bool UnregisterTaskScheduler()
    {
        return UnregisterScheduledTask(TaskName);
    }

    public static bool UnregisterMonitorTaskScheduler()
    {
        return UnregisterScheduledTask(MonitorTaskName);
    }

    public static bool ApplyStartupMode(string exePath, StartupLaunchMode mode, bool enabled)
    {
        try
        {
            if (!enabled)
            {
                var uiTaskRemoved = UnregisterTaskScheduler();
                var monitorTaskRemoved = UnregisterMonitorTaskScheduler();
                var legacyRemoved = Unregister();
                Debug.WriteLine($"Startup mode disabled. UiTaskRemoved={uiTaskRemoved}, MonitorTaskRemoved={monitorTaskRemoved}, LegacyRemoved={legacyRemoved}");
                return true;
            }

            if (mode == StartupLaunchMode.TaskScheduler)
            {
                var uiTaskCreated = RegisterTaskScheduler(exePath, enabled);
                var monitorTaskCreated = RegisterMonitorTaskScheduler(exePath, enabled);
                if (!uiTaskCreated || !monitorTaskCreated)
                {
                    return false;
                }

                var legacyRemoved = Unregister();
                Debug.WriteLine($"Startup mode switched to TaskScheduler. LegacyRemoved={legacyRemoved}");
                return true;
            }

            var schedulerRemoved = UnregisterTaskScheduler();
            var monitorRemoved = UnregisterMonitorTaskScheduler();
            var legacyCreated = Register(exePath);
            if (!legacyCreated)
                return false;

            Debug.WriteLine($"Startup mode switched to LegacyRun. TaskRemoved={schedulerRemoved}, MonitorTaskRemoved={monitorRemoved}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to apply startup mode: " + ex);
            return false;
        }
    }

    private static bool RegisterScheduledTask(string exePath, string taskName, string arguments, string runLevel, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            var currentUser = WindowsIdentity.GetCurrent().Name;
            var enabledLiteral = enabled ? "$true" : "$false";
            var script = string.Join(Environment.NewLine, new[]
            {
                "$ErrorActionPreference = 'Stop'",
                "Import-Module ScheduledTasks",
                $"$action = New-ScheduledTaskAction -Execute '{EscapePowerShellSingleQuotedString(exePath)}' -Argument '{EscapePowerShellSingleQuotedString(arguments)}'",
                "$trigger = New-ScheduledTaskTrigger -AtLogOn",
                $"$principal = New-ScheduledTaskPrincipal -UserId '{EscapePowerShellSingleQuotedString(currentUser)}' -LogonType Interactive -RunLevel {runLevel}",
                $"Register-ScheduledTask -TaskName '{EscapePowerShellSingleQuotedString(taskName)}' -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null",
                $"if ({enabledLiteral}) {{",
                $"    Enable-ScheduledTask -TaskName '{EscapePowerShellSingleQuotedString(taskName)}' | Out-Null",
                "} else {",
                $"    Disable-ScheduledTask -TaskName '{EscapePowerShellSingleQuotedString(taskName)}' | Out-Null",
                "}"
            });

            return RunPowerShellScript(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to create scheduled startup task: " + ex);
            return false;
        }
    }

    private static bool UnregisterScheduledTask(string taskName)
    {
        try
        {
            var script = string.Join(Environment.NewLine, new[]
            {
                "Import-Module ScheduledTasks",
                $"$task = Get-ScheduledTask -TaskName '{EscapePowerShellSingleQuotedString(taskName)}' -ErrorAction SilentlyContinue",
                "if ($null -ne $task) {",
                $"    Unregister-ScheduledTask -TaskName '{EscapePowerShellSingleQuotedString(taskName)}' -Confirm:$false",
                "}",
                "exit 0"
            });

            return RunPowerShellScript(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to delete scheduled startup task: " + ex);
            return false;
        }
    }

    private static void DeleteStartupApprovedEntry(RegistryKey root, string subKeyPath)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath, writable: true);
            if (key == null)
                return;

            if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clean startup approved entry {subKeyPath}: {ex}");
        }
    }

    private static void DeleteCompatLayerEntries()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey(AppCompatLayersPath, writable: true);
                if (key == null)
                    continue;

                if (key.GetValue(exePath) != null)
                    key.DeleteValue(exePath, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clean app compat layers for {exePath}: {ex}");
            }
        }
    }

    private static void DeleteStartupFolderShortcuts()
    {
        var startupFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        foreach (var folder in startupFolders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*IconGrid*.lnk", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete startup shortcut {file}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enumerate startup folder {folder}: {ex}");
            }
        }
    }

    private static bool RunPowerShellScript(string script)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"IconGrid.{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(tempPath, script);

            var startInfo = new ProcessStartInfo
            {
                FileName = "C:\\WINDOWS\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(10000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Failed to run scheduled task PowerShell script: " + ex);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

    private static string BuildStartupCommand(string exePath)
    {
        return $"\"{exePath}\" {StartupLaunchArgument}";
    }

}
