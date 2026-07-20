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
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Management;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;

namespace IconGrid.Views;

public partial class TestPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private string _instanceInfo = string.Empty;
    private string _processInfo = string.Empty;
    private string _startupDiagnosticsInfo = string.Empty;
    private string _summaryText = string.Empty;
    private string _etwProbeStatusText = "Idle";
    private string _etwProbeResultText = "Not run yet.";
    private string _elevatedEtwProbeStatusText = "Idle";
    private string _elevatedEtwProbeResultText = "Not run yet.";
    private string _nativeUiProbeStatusText = "Idle";
    private string _nativeUiProbeResultText = "Not run yet.";
    private bool _isProbeRunning;

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

    public string EtwProbeStatusText
    {
        get => _etwProbeStatusText;
        private set => SetField(ref _etwProbeStatusText, value);
    }

    public string EtwProbeResultText
    {
        get => _etwProbeResultText;
        private set => SetField(ref _etwProbeResultText, value);
    }

    public string ElevatedEtwProbeStatusText
    {
        get => _elevatedEtwProbeStatusText;
        private set => SetField(ref _elevatedEtwProbeStatusText, value);
    }

    public string ElevatedEtwProbeResultText
    {
        get => _elevatedEtwProbeResultText;
        private set => SetField(ref _elevatedEtwProbeResultText, value);
    }

    public string NativeUiProbeStatusText
    {
        get => _nativeUiProbeStatusText;
        private set => SetField(ref _nativeUiProbeStatusText, value);
    }

    public string NativeUiProbeResultText
    {
        get => _nativeUiProbeResultText;
        private set => SetField(ref _nativeUiProbeResultText, value);
    }

    public TestPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UserControl_Loaded;
        InstanceInfo = BuildInstanceInfo();
        ProcessInfo = BuildProcessInfo();
        StartupDiagnosticsInfo = BuildStartupDiagnosticsInfo();
        SummaryText = BuildSummaryText();
    }

    private async void RunEtwProbeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isProbeRunning)
        {
            return;
        }

        _isProbeRunning = true;
        EtwProbeStatusText = "Running...";
        EtwProbeResultText = "Starting ETW probe from UI process...";

        try
        {
            var result = await Task.Run(() => EtwFpsProvider.RunStartupProbe());
            EtwProbeResultText = result;
            EtwProbeStatusText = $"Completed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            EtwProbeResultText = ex.ToString();
            EtwProbeStatusText = "Failed";
        }
        finally
        {
            _isProbeRunning = false;
        }
    }

    private async void RunElevatedEtwProbeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isProbeRunning)
        {
            return;
        }

        _isProbeRunning = true;
        ElevatedEtwProbeStatusText = "Running...";
        ElevatedEtwProbeResultText = "Starting ETW probe from elevated helper process...";

        try
        {
            var result = await ElevatedEtwProbeRunner.RunAsync(() => Environment.ProcessPath ?? string.Empty);
            ElevatedEtwProbeResultText = result;
            ElevatedEtwProbeStatusText = $"Completed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ElevatedEtwProbeResultText = ex.ToString();
            ElevatedEtwProbeStatusText = "Failed";
        }
        finally
        {
            _isProbeRunning = false;
        }
    }

    private async void RunNativeUiProbeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isProbeRunning)
        {
            return;
        }

        _isProbeRunning = true;
        NativeUiProbeStatusText = "Running...";
        NativeUiProbeResultText = "Starting native FPS agent directly from the non-elevated UI process...";

        try
        {
            var result = await RunNativeUiProbeAsync();
            NativeUiProbeResultText = result;
            NativeUiProbeStatusText = $"Completed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            NativeUiProbeResultText = ex.ToString();
            NativeUiProbeStatusText = "Failed";
        }
        finally
        {
            _isProbeRunning = false;
        }
    }

    private static async Task<string> RunNativeUiProbeAsync()
    {
        var baseDir = new ConfigManager().BaseDirectory;
        Directory.CreateDirectory(baseDir);
        var statePath = Path.Combine(baseDir, "native-fps-ui-probe-state.json");

        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        using var runner = new NativeFpsAgentRunner(statePath);
        if (!runner.IsAvailable)
        {
            return "[NativeUiProbe] Native FPS agent executable was not found.";
        }

        if (!runner.Start(Environment.ProcessId))
        {
            return "[NativeUiProbe] Failed to start native FPS agent from UI process.";
        }

        var startedAtUtc = DateTime.UtcNow;
        NativeFpsAgentState? lastState = null;

        while (DateTime.UtcNow - startedAtUtc < TimeSpan.FromSeconds(8))
        {
            await Task.Delay(350);
            lastState = runner.ReadState();
            if (lastState == null)
            {
                continue;
            }

            if (lastState.EtwStartAttemptCount > 0 ||
                lastState.EtwRunning ||
                lastState.EtwEventsReceived ||
                !string.IsNullOrWhiteSpace(lastState.LastEtwError))
            {
                break;
            }
        }

        if (lastState == null)
        {
            return "[NativeUiProbe] No state file was produced within 8 seconds.";
        }

        return BuildNativeProbeSummary(lastState);
    }

    private static string BuildNativeProbeSummary(NativeFpsAgentState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[NativeUiProbe] CapturedAtUtc={state.CapturedAtUtc:O}");
        sb.AppendLine($"[NativeUiProbe] Elevated={state.IsElevated} ParentPid={state.ParentPid} RootPid={state.RootPid} TargetPid={state.TargetPid}");
        sb.AppendLine($"[NativeUiProbe] Target={state.TargetProcessName ?? ""} CandidatePid={state.CandidatePid} Candidate={state.CandidateProcessName ?? ""}");
        sb.AppendLine($"[NativeUiProbe] Attempts={state.EtwStartAttemptCount} Failures={state.EtwStartFailureCount} EtwRunning={state.EtwRunning} Events={state.EtwEventsReceived}");
        sb.AppendLine($"[NativeUiProbe] DxgKrnlEnabled={state.DxgKrnlEnabled} DxgiEnabled={state.DxgiEnabled} D3D9Enabled={state.D3D9Enabled}");
        sb.AppendLine($"[NativeUiProbe] LastDxgKrnlError={state.LastDxgKrnlError ?? ""}");
        sb.AppendLine($"[NativeUiProbe] LastDxgiError={state.LastDxgiError ?? ""}");
        sb.AppendLine($"[NativeUiProbe] LastD3D9Error={state.LastD3D9Error ?? ""}");
        sb.AppendLine($"[NativeUiProbe] LastEtwError={state.LastEtwError ?? ""}");
        sb.AppendLine($"[NativeUiProbe] Debug={state.DebugMessage ?? ""}");
        return sb.ToString().TrimEnd();
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
        sb.AppendLine($"SessionId: {SafeGetSessionId(Environment.ProcessId)}");
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
