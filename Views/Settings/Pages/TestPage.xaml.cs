using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace IconGrid.Views;

public partial class TestPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private string _instanceInfo = string.Empty;
    private string _processInfo = string.Empty;
    private string _startupDiagnosticsInfo = string.Empty;
    private string _summaryText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string InstanceInfo
    {
        get => _instanceInfo;
        private set => SetField(ref _instanceInfo, value);
    }

    public string ProcessInfo
    {
        get => _processInfo;
        private set => SetField(ref _processInfo, value);
    }

    public string StartupDiagnosticsInfo
    {
        get => _startupDiagnosticsInfo;
        private set => SetField(ref _startupDiagnosticsInfo, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetField(ref _summaryText, value);
    }

    public TestPage()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UserControl_Loaded;
        InstanceInfo = BuildInstanceInfo();
        ProcessInfo = BuildProcessInfo();
        StartupDiagnosticsInfo = BuildStartupDiagnosticsInfo();
        SummaryText = BuildSummaryText();
    }

    private static string BuildInstanceInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ProcessId: {Environment.ProcessId}");
        sb.AppendLine($"ProcessName: {Process.GetCurrentProcess().ProcessName}");
        sb.AppendLine($"ProcessPath: {Environment.ProcessPath}");
        sb.AppendLine($"CommandLine: {Environment.CommandLine}");
        sb.AppendLine($"CurrentUser: {Environment.UserName}");
        sb.AppendLine($"MachineName: {Environment.MachineName}");
        sb.AppendLine($"Elevated: {IsCurrentProcessElevated()}");
        sb.AppendLine($"AgentMode: {Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--monitor-agent", StringComparison.OrdinalIgnoreCase))}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildProcessInfo()
    {
        var sb = new StringBuilder();
        var processes = Process.GetProcessesByName("IconGrid");
        sb.AppendLine($"IconGrid.exe count: {processes.Length}");

        foreach (var process in processes.OrderBy(p => p.Id))
        {
            sb.AppendLine($"- PID {process.Id}, Started={SafeStartTime(process)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildStartupDiagnosticsInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Task Scheduler");
        sb.AppendLine(BuildTaskSchedulerInfo());
        sb.AppendLine();
        sb.AppendLine("Legacy startup entries");
        sb.AppendLine(BuildLegacyStartupInfo());
        return sb.ToString().TrimEnd();
    }

    private static string BuildTaskSchedulerInfo()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/Query /FO LIST /V",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return "Unable to query task scheduler.";
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                return string.IsNullOrWhiteSpace(error)
                    ? $"Task query failed. ExitCode={process.ExitCode}"
                    : error.Trim();
            }

            var matches = ParseScheduledTasks(output);
            if (matches.Count == 0)
            {
                return "No Task Scheduler entries containing IconGrid were found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {matches.Count} Task Scheduler entr{(matches.Count == 1 ? "y" : "ies")} containing IconGrid:");
            foreach (var match in matches)
            {
                sb.AppendLine($"- {match}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Task query failed: {ex.Message}";
        }
    }

    private static List<string> ParseScheduledTasks(string output)
    {
        var matches = new List<string>();
        var blocks = output.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            string? taskName = null;
            string? status = null;

            foreach (var rawLine in block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("TaskName:", StringComparison.OrdinalIgnoreCase))
                {
                    taskName = line["TaskName:".Length..].Trim();
                }
                else if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                {
                    status = line["Status:".Length..].Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(taskName) &&
                taskName.Contains("IconGrid", StringComparison.OrdinalIgnoreCase))
            {
                var entry = taskName;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    entry += $" ({status})";
                }

                matches.Add(entry);
            }
        }

        return matches.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildLegacyStartupInfo()
    {
        var matches = new List<string>();

        AddRunEntryMatches(matches, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU Run");
        AddRunEntryMatches(matches, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM Run");
        AddStartupApprovedMatches(matches, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKCU StartupApproved");
        AddStartupApprovedMatches(matches, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKLM StartupApproved");
        AddStartupFolderMatches(matches, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user Startup folder");
        AddStartupFolderMatches(matches, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users Startup folder");

        if (matches.Count == 0)
        {
            return "No legacy startup entries containing IconGrid were found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {matches.Count} legacy startup entr{(matches.Count == 1 ? "y" : "ies")} containing IconGrid:");
        foreach (var match in matches)
        {
            sb.AppendLine($"- {match}");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AddRunEntryMatches(List<string> matches, RegistryKey root, string subKeyPath, string label)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath, writable: false);
            if (key == null)
            {
                return;
            }

            var value = key.GetValue("IconGrid");
            if (value != null)
            {
                matches.Add($"{label}: {value}");
            }
        }
        catch
        {
        }
    }

    private static void AddStartupApprovedMatches(List<string> matches, RegistryKey root, string subKeyPath, string label)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath, writable: false);
            if (key == null)
            {
                return;
            }

            if (key.GetValue("IconGrid") != null)
            {
                matches.Add($"{label}: IconGrid");
            }
        }
        catch
        {
        }
    }

    private static void AddStartupFolderMatches(List<string> matches, string folder, string label)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*IconGrid*.lnk", SearchOption.TopDirectoryOnly))
            {
                matches.Add($"{label}: {Path.GetFileName(file)}");
            }
        }
        catch
        {
        }
    }

    private static string BuildSummaryText()
    {
        var mode = Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--monitor-agent", StringComparison.OrdinalIgnoreCase))
            ? "AGENT"
            : "UI";
        var elevated = IsCurrentProcessElevated() ? "ELEVATED" : "STANDARD";
        return $"Mode: {mode}    Elevation: {elevated}    PID: {Environment.ProcessId}";
    }

    private static string SafeStartTime(Process process)
    {
        try
        {
            return process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "--";
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
