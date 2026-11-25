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
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Management;
using IconGrid.Helpers.Logging;
using IconGrid.Helpers.Settings;

namespace IconGrid.Views;

public partial class TestSection : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private string _processInfo = string.Empty;
    private string _startupDiagnosticsInfo = string.Empty;
    private string _summaryText = string.Empty;
    private string _traceLogInfo = string.Empty;
    private string _traceLogStatusText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public string TraceLogInfo
    {
        get => _traceLogInfo;
        private set => SetField(ref _traceLogInfo, value);
    }

    public string TraceLogStatusText
    {
        get => _traceLogStatusText;
        private set => SetField(ref _traceLogStatusText, value);
    }

    public TestSection()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UserControl_Loaded;
        ProcessInfo = BuildProcessInfo();
        StartupDiagnosticsInfo = BuildStartupDiagnosticsInfo();
        SummaryText = BuildSummaryText();
        RefreshTraceLogInfo();
    }

    private static string BuildProcessInfo()
    {
        var sb = new StringBuilder();
        var processes = Process.GetProcessesByName("IconGrid");
        sb.AppendLine($"IconGrid.exe count: {processes.Length}");

        foreach (var process in processes.OrderBy(p => p.Id))
        {
            sb.AppendLine($"- PID {process.Id}, Started={SafeStartTime(process)}");
            sb.AppendLine($"  SessionId: {SafeGetSessionId(process.Id)}");
            sb.AppendLine($"  Elevated: {SafeIsProcessElevated(process)}");
            sb.AppendLine($"  ParentPid: {SafeGetParentProcessId(process.Id)}");
            sb.AppendLine($"  User: {SafeGetProcessOwner(process.Id)}");
            sb.AppendLine($"  CommandLine: {SafeGetProcessCommandLine(process.Id)}");
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

    private static string SafeGetSessionId(int processId)
    {
        try
        {
            return ProcessIdToSessionId((uint)processId, out var sessionId)
                ? sessionId.ToString()
                : "--";
        }
        catch
        {
            return "--";
        }
    }

    private static string SafeIsProcessElevated(Process process)
    {
        try
        {
            if (!OpenProcessToken(process.Handle, TokenQuery, out var tokenHandle))
            {
                return "--";
            }

            using var token = new SafeTokenHandle(tokenHandle);
            var elevation = new TOKEN_ELEVATION();
            var size = Marshal.SizeOf<TOKEN_ELEVATION>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token.DangerousGetHandle(), TOKEN_INFORMATION_CLASS.TokenElevation, buffer, size, out _))
                {
                    return "--";
                }

                elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(buffer);
                return elevation.TokenIsElevated != 0 ? "True" : "False";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return "--";
        }
    }

    private static string SafeGetParentProcessId(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            var parent = results.Cast<ManagementObject>().FirstOrDefault()?["ParentProcessId"];
            return parent?.ToString() ?? "--";
        }
        catch
        {
            return "--";
        }
    }

    private static string SafeGetProcessOwner(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            var process = results.Cast<ManagementObject>().FirstOrDefault();
            if (process == null)
            {
                return "--";
            }

            var args = new string[2];
            var returnCode = Convert.ToInt32(process.InvokeMethod("GetOwner", args));
            if (returnCode == 0)
            {
                return string.IsNullOrWhiteSpace(args[1]) ? args[0] : $@"{args[1]}\{args[0]}";
            }

            return "--";
        }
        catch
        {
            return "--";
        }
    }

    private static string SafeGetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            return results.Cast<ManagementObject>().FirstOrDefault()?["CommandLine"]?.ToString() ?? "--";
        }
        catch
        {
            return "--";
        }
    }

    private const uint TokenQuery = 0x0008;

    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenElevation = 20
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public int TokenIsElevated;
    }

    private sealed class SafeTokenHandle : IDisposable
    {
        private IntPtr _handle;

        public SafeTokenHandle(IntPtr handle)
        {
            _handle = handle;
        }

        public IntPtr DangerousGetHandle() => _handle;

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private void RefreshTraceLogButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshTraceLogInfo();
        TraceLogStatusText = "Refreshed.";
    }

    private void CleanTraceLogButton_Click(object sender, RoutedEventArgs e)
    {
        var cleared = AppTrace.Clear();
        RefreshTraceLogInfo();
        TraceLogStatusText = cleared ? "Trace log cleared." : "Could not clear the trace log.";
    }

    private void RefreshTraceLogInfo()
    {
        var lastWrite = AppTrace.GetLastWriteTime();
        var lastWriteText = lastWrite.HasValue ? lastWrite.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";
        TraceLogInfo =
            $"Path:  {AppTrace.LogPath}\n" +
            $"Size:  {AppTrace.GetSizeText()}  (auto-clean cap {AppTrace.FormatSize(AppTrace.MaxBytes)})\n" +
            $"Last write:  {lastWriteText}";
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
