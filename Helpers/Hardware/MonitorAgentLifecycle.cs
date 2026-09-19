using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Cross-process liveness contract for the elevated hardware-monitor agent.
///
/// The agent can be started in two ways: by the launcher (which passes its own
/// PID plus a per-launcher shutdown event) or by the "IconGrid Monitor" Task
/// Scheduler task, which starts <c>IconGrid.exe --monitor-agent</c> with no
/// parent and no shutdown event at all. Without the contract below that
/// scheduled agent can never be asked to stop, so it - and the native FPS agent
/// it owns - stayed alive after IconGrid was closed (trace evidence
/// 2026-09-19: "Hardware monitor shutdown event was not present." followed by
/// two orphaned processes that had to be killed manually).
///
/// Two structural rules fix that, without any process-name list:
/// 1. A well-known request-stop event (not tied to a launcher PID) that any
///    IconGrid instance may signal.
/// 2. A launcher-presence check: an agent started without a parent PID exits
///    once no launcher process exists for a grace period.
/// </summary>
internal static class MonitorAgentLifecycle
{
    /// <summary>Well-known per-session stop request, independent of any launcher PID.</summary>
    public const string RequestStopEventName = @"Local\IconGrid.HardwareMonitorAgent.RequestStop";

    /// <summary>
    /// Creates (or opens) the request-stop event the agent waits on. The caller
    /// owns the handle for the agent's lifetime.
    /// </summary>
    public static EventWaitHandle OpenRequestStopEvent(Action<string>? log = null)
    {
        try
        {
            return new EventWaitHandle(false, EventResetMode.ManualReset, RequestStopEventName);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to open the monitor agent request-stop event: {ex.Message}");
            return new EventWaitHandle(false, EventResetMode.ManualReset);
        }
    }

    /// <summary>
    /// Clears a stop request that was consumed while a new agent instance was
    /// taking over the mutex. Must be called right after the mutex is acquired,
    /// otherwise the new agent would stop again on its first loop iteration.
    /// </summary>
    public static void ClearStopRequest(EventWaitHandle? requestStopEvent)
    {
        try
        {
            requestStopEvent?.Reset();
        }
        catch (Exception)
        {
            // A handle without reset rights can never carry a stop request; ignore.
        }
    }

    /// <summary>
    /// Asks whichever monitor agent is currently running to stop. Used when a new
    /// instance cannot take over the mutex and when the launcher shuts down
    /// without a matching per-launcher shutdown event.
    /// </summary>
    public static bool TrySignalRequestStop(Action<string>? log = null)
    {
        try
        {
            using var requestStopEvent = EventWaitHandle.OpenExisting(RequestStopEventName);
            requestStopEvent.Set();
            log?.Invoke($"Signaled the monitor agent request-stop event {RequestStopEventName}.");
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            log?.Invoke("No monitor agent was listening for a stop request.");
            return false;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to signal the monitor agent request-stop event: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Detects whether any IconGrid launcher process is still present. Used by an
/// agent that was started without a parent PID (Task Scheduler), so it cannot
/// outlive the application it belongs to.
/// </summary>
internal sealed class LauncherPresenceTracker
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);

    private readonly string _imageName;
    private DateTime? _launcherMissingSinceUtc;
    private DateTime _nextProbeUtc = DateTime.MinValue;
    private bool _launcherPresent;

    public LauncherPresenceTracker()
    {
        var imageName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        _imageName = string.IsNullOrWhiteSpace(imageName) ? "IconGrid" : imageName;
    }

    /// <summary>
    /// Returns true when the agent should exit because no launcher process has
    /// been present for the whole grace period.
    /// </summary>
    public bool ShouldExit(TimeSpan absenceGracePeriod, Action<string>? log = null)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc >= _nextProbeUtc)
        {
            _nextProbeUtc = nowUtc.Add(ProbeInterval);
            _launcherPresent = AnyOtherInstanceRunning();
        }

        if (_launcherPresent)
        {
            _launcherMissingSinceUtc = null;
            return false;
        }

        _launcherMissingSinceUtc ??= nowUtc;
        var missingFor = nowUtc - _launcherMissingSinceUtc.Value;
        if (missingFor <= absenceGracePeriod)
        {
            return false;
        }

        log?.Invoke(
            $"Exiting because no {_imageName} launcher process has been present for {missingFor.TotalSeconds:F0}s.");
        return true;
    }

    private bool AnyOtherInstanceRunning()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(_imageName))
            {
                using (process)
                {
                    if (process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Process enumeration can be blocked; treat it as "unknown" and keep running.
        }

        return false;
    }
}
