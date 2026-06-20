using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace IconGrid.Helpers;

public static class PawnIoHelper
{
    private const string DriverFileName = "PawnIO.sys";
    private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly string[] PawnIoServiceNames = { "PawnIO" };
    private const string PawnIoDisplayNameFragment = "pawnio";

    public static bool IsPawnIoInstalled()
    {
        if (IsProcessRunning())
        {
            return true;
        }

        if (IsServicePresent())
        {
            return true;
        }

        foreach (var candidate in GetCandidateDriverPaths())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return true;
                }
            }
            catch
            {
                // ignore read failures
            }
        }

        foreach (var folder in GetPawnIoInstallFolders())
        {
            if (Directory.Exists(folder))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProcessRunning()
    {
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var processName = process.ProcessName;
                    if (processName.Contains("pawnio", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("pawn", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // ignore inaccessible process details
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // ignore process enumeration failures
        }

        return false;
    }

    private static bool IsServicePresent()
    {
        try
        {
            foreach (var service in ServiceController.GetServices())
            {
                if (MatchesPawnIoName(service.ServiceName) || MatchesPawnIoName(service.DisplayName))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore permission or Win32 failures
        }

        return false;
    }

    private static bool MatchesPawnIoName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var name in PawnIoServiceNames)
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetCandidateDriverPaths()
    {
        foreach (var path in GetInstallPathsFromRegistry())
        {
            yield return path;
        }

        foreach (var basePath in GetExpectedDriverPaths())
        {
            yield return basePath;
        }
    }

    private static IEnumerable<string> GetInstallPathsFromRegistry()
    {
        var paths = new List<string>();

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(UninstallRegistryPath);

                    if (key == null)
                    {
                        continue;
                    }

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null)
                            {
                                continue;
                            }

                            var displayName = (subKey.GetValue("DisplayName") as string) ?? string.Empty;
                            if (displayName.Contains(PawnIoDisplayNameFragment, StringComparison.OrdinalIgnoreCase))
                            {
                                if (subKey.GetValue("InstallLocation") is string installLocation && !string.IsNullOrWhiteSpace(installLocation))
                                {
                                    paths.Add(Path.Combine(installLocation, DriverFileName));
                                }

                                return paths;
                            }
                        }
                        catch
                        {
                            // ignore registry path errors
                        }
                    }
                }
                catch
                {
                    // ignore registry read issues
                }
            }
        }

        return paths;
    }

    private static IEnumerable<string> GetExpectedDriverPaths()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "PawnIO", DriverFileName);
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "PawnIO", DriverFileName);
        }

        var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsPath))
        {
            yield return Path.Combine(windowsPath, "System32", "drivers", DriverFileName);
        }
    }

    private static IEnumerable<string> GetPawnIoInstallFolders()
    {
        var programFiles64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles64))
        {
            yield return Path.Combine(programFiles64, "PawnIO");
        }

        var programFiles32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles32))
        {
            yield return Path.Combine(programFiles32, "PawnIO");
        }
    }
}
