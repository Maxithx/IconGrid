using System;
using System.IO;
using System.Threading;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Keeps the elevated hardware monitor agent alive for as long as the launcher
/// runs.
///
/// Why this exists: the agent (IconGrid.exe --monitor-agent) is the only process
/// that detects games and publishes FPS/VRAM evidence. It has crashed silently
/// with heap corruption (0xc0000374 in ntdll, Windows Application Error
/// 2026-10-23 19:28, plus 2026-10-22/21/19) while the launcher kept running. The
/// launcher then had no game detection at all: COD was started afterwards and
/// the gaming overlay never appeared, and "Nulstil FPS-agent" reported that no
/// monitor agent was listening.
///
/// The watchdog uses the agent's own heartbeat (monitor-state.json, rewritten
/// about once per second) instead of a mutex, because opening a mutex created by
/// an elevated process can be denied for the non-elevated launcher. Restarts go
/// through the existing "IconGrid Monitor" scheduled task so no UAC prompt is
/// shown.
/// </summary>
public sealed class MonitorAgentWatchdog : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatMaxAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromSeconds(60);

    private readonly Action<string>? _log;
    private readonly string _heartbeatPath;
    private readonly System.Threading.Timer _timer;
    private DateTime _lastRestartAttemptUtc = DateTime.MinValue;
    private int _checkInProgress;
    private bool _disposed;

    public MonitorAgentWatchdog(Action<string>? log = null)
    {
        _log = log;
        _heartbeatPath = Path.Combine(new ConfigManager().BaseDirectory, "monitor-state.json");
        _timer = new System.Threading.Timer(Check, null, CheckInterval, CheckInterval);
    }

    private void Check(object? state)
    {
        if (_disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref _checkInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            if (DateTime.UtcNow - _lastRestartAttemptUtc < RestartCooldown)
            {
                return;
            }

            if (IsHeartbeatFresh())
            {
                return;
            }

            _lastRestartAttemptUtc = DateTime.UtcNow;
            _log?.Invoke(
                $"Hardware monitor agent heartbeat is missing or stale (last write: {DescribeHeartbeatAge()}). Restarting the agent.");

            var started = HardwareMonitorTaskManager.TryRunScheduledMonitorTask(_log) ||
                          HardwareMonitorTaskManager.StartAgent(log: _log);

            _log?.Invoke(started
                ? "Hardware monitor agent restart requested by the watchdog."
                : "Hardware monitor agent could not be restarted by the watchdog.");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Hardware monitor agent watchdog failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _checkInProgress, 0);
        }
    }

    private bool IsHeartbeatFresh()
    {
        try
        {
            var info = new FileInfo(_heartbeatPath);
            return info.Exists && DateTime.UtcNow - info.LastWriteTimeUtc <= HeartbeatMaxAge;
        }
        catch
        {
            // Never restart on an access problem; missing evidence is not proof.
            return true;
        }
    }

    private string DescribeHeartbeatAge()
    {
        try
        {
            var info = new FileInfo(_heartbeatPath);
            if (!info.Exists)
            {
                return "file missing";
            }

            return $"{Math.Max(0, (DateTime.UtcNow - info.LastWriteTimeUtc).TotalSeconds):F0}s ago";
        }
        catch
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
