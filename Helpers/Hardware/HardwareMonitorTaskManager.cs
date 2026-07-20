using System;
using System.Diagnostics;
using System.IO;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorTaskManager
{
    private const string MonitorAgentArgument = "--monitor-agent";
    private const string ShutdownEventArgument = "--shutdown-event";

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
            var shutdownEventName = GetShutdownEventName(Environment.ProcessId);
            using var shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, shutdownEventName);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"{MonitorAgentArgument} --parent-pid {Environment.ProcessId} {ShutdownEventArgument} \"{shutdownEventName}\"",
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

    public static void SignalCurrentAgentToStop(Action<string>? log = null)
    {
        try
        {
            var shutdownEventName = GetShutdownEventName(Environment.ProcessId);
            using var shutdownEvent = EventWaitHandle.OpenExisting(shutdownEventName);
            shutdownEvent.Set();
            log?.Invoke($"Signaled hardware monitor agent shutdown via event {shutdownEventName}.");
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            log?.Invoke("Hardware monitor shutdown event was not present.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to signal hardware monitor agent shutdown: {ex.Message}");
        }
    }

    private static string GetShutdownEventName(int parentProcessId) => $@"Local\IconGrid.HardwareMonitorAgent.Stop.{parentProcessId}";
}
