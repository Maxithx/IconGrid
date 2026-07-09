using System;
using System.Diagnostics;
using System.IO;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorTaskManager
{
    private const string MonitorAgentArgument = "--monitor-agent";

    public static bool StartAgent(Func<string>? executablePathProvider = null, Action<string>? log = null, bool elevate = true)
    {
        var executablePath = executablePathProvider?.Invoke() ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            log?.Invoke("Hardware monitor agent could not be started because the executable path was missing.");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"{MonitorAgentArgument} --parent-pid {Environment.ProcessId}",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (elevate)
            {
                startInfo.Verb = "runas";
            }

            return Process.Start(startInfo) != null;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to launch hardware monitor agent: {ex.Message}");
            return false;
        }
    }
}
