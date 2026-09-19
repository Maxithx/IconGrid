using System;
using System.Diagnostics;
using System.IO;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorTaskManager
{
    private const string MonitorAgentArgument = "--monitor-agent";
    private const string ShutdownEventArgument = "--shutdown-event";

    // Kept alive for the lifetime of this (launcher) process. The spawned agent
    // opens the event by name at startup and can only do that while at least one
    // handle exists, so the handle must NOT be disposed right after
    // Process.Start. Doing that made the agent log
    // "Shutdown event was not found: Local\IconGrid.HardwareMonitorAgent.Stop.<launcherPid>"
    // and left it unstoppable when the launcher closed.
    private static EventWaitHandle? _launcherShutdownEvent;

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
            _launcherShutdownEvent ??= new EventWaitHandle(false, EventResetMode.ManualReset, shutdownEventName);

            // A parent PID alone is not a reliable liveness check: Windows reuses
            // PIDs, so an orphaned agent could find an unrelated process with "our"
            // PID and never exit. That left an elevated agent — and a stale FPS
            // target — alive after the launcher was gone. Pass our start time too
            // so the agent can verify PID *and* start time.
            long parentStartFileTimeUtc = 0;
            try
            {
                parentStartFileTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime().ToFileTimeUtc();
            }
            catch
            {
                // Fall back to PID-only validation when the start time is unavailable.
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"{MonitorAgentArgument} --parent-pid {Environment.ProcessId} --parent-start-filetime {parentStartFileTimeUtc} {ShutdownEventArgument} \"{shutdownEventName}\"",
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
            // The running agent may have been started by the "IconGrid Monitor"
            // Task Scheduler task, which passes no launcher shutdown event. Fall
            // back to the well-known request-stop event so the elevated agent (and
            // the native FPS agent it owns) cannot outlive the launcher.
            log?.Invoke("Hardware monitor shutdown event was not present.");
            MonitorAgentLifecycle.TrySignalRequestStop(log);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to signal hardware monitor agent shutdown: {ex.Message}");
        }
    }

    private static string GetShutdownEventName(int parentProcessId) => $@"Local\IconGrid.HardwareMonitorAgent.Stop.{parentProcessId}";
}
