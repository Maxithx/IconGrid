using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IconGrid.Helpers.Hardware;

namespace IconGrid.Views;

public partial class TestPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private string _instanceInfo = string.Empty;
    private string _processInfo = string.Empty;
    private string _taskInfo = string.Empty;
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

    public string TaskInfo
    {
        get => _taskInfo;
        private set => SetField(ref _taskInfo, value);
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
        TaskInfo = BuildTaskInfo();
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

    private static string BuildTaskInfo()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/Query /TN \"IconGrid Hardware Monitor Agent\" /FO LIST /V",
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

            return output.Trim();
        }
        catch (Exception ex)
        {
            return $"Task query failed: {ex.Message}";
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
