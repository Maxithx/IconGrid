using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace IconGrid.Helpers.Settings;

public static class StartupTaskManager
{
    private const string RegistryRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedStartupFolderPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    private const string AppCompatLayersPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string ValueName = "IconGrid";

    public static bool Register(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, writable: true);
            if (key == null)
                return false;

            key.SetValue(ValueName, exePath, RegistryValueKind.String);
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
}
