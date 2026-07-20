using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorAgent
{
    private const string AgentMutexName = @"Local\IconGrid.HardwareMonitorAgent";
    private const int SnapshotIntervalMs = 500;
    private const int FpsStateIntervalMs = 10;
    private const int AtomicWriteRetryCount = 8;
    private const int AtomicWriteRetryDelayMs = 6;
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int Run(string[] args, Action<string>? log = null)
    {
        using var mutex = new Mutex(true, AgentMutexName, out var createdNew);
        if (!createdNew)
        {
            log?.Invoke("Hardware monitor agent is already running.");
            return 0;
        }

        var parentPid = TryReadParentPid(args);
        var configManager = new ConfigManager();
        var baseDir = configManager.BaseDirectory;
        var statePath = Path.Combine(baseDir, "monitor-state.json");
        var tempPath = statePath + ".tmp";
        var fpsStatePath = Path.Combine(baseDir, "fps-state.json");
        var fpsTempPath = fpsStatePath + ".tmp";
        var nativeFpsStatePath = Path.Combine(baseDir, "native-fps-state.json");
        using var shutdownEvent = TryOpenShutdownEvent(args, log);

        var config = configManager.LoadConfig();
        var fpsTarget = config.FpsTarget;
        var gameExeNames = new System.Collections.Generic.List<string>();
        if (fpsTarget != null)
        {
            if (!string.IsNullOrWhiteSpace(fpsTarget.ExecutableName))
                gameExeNames.Add(fpsTarget.ExecutableName);
            if (!string.IsNullOrWhiteSpace(fpsTarget.ResolvedExecutablePath))
            {
                var fileName = Path.GetFileNameWithoutExtension(fpsTarget.ResolvedExecutablePath);
                if (!string.IsNullOrWhiteSpace(fileName))
                    gameExeNames.Add(fileName);
            }
        }

        try
        {
            Directory.CreateDirectory(baseDir);
            using var collector = new HardwareSnapshotCollector();
            using var nativeFpsAgent = new NativeFpsAgentRunner(nativeFpsStatePath, log);
            using var fpsMeter = new FpsMeter(config.GamingOverlayFpsResponsiveness, log);

            var nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Start(parentPid);
            log?.Invoke($"Hardware monitor agent started. NativeFpsStarted={nativeFpsStarted}");

            var nativeFpsLoggedSummary = string.Empty;
            DateTime? parentMissingSinceUtc = null;

            while (true)
            {
                if (shutdownEvent?.WaitOne(0) == true)
                {
                    log?.Invoke("Exiting because shutdown event was signaled.");
                    break;
                }

                if (!ParentIsAlive(parentPid))
                {
                    parentMissingSinceUtc ??= DateTime.UtcNow;
                    if (DateTime.UtcNow - parentMissingSinceUtc.Value > OrphanGracePeriod)
                    {
                        log?.Invoke("Exiting because parent has been gone past orphan grace period.");
                        break;
                    }
                }
                else
                {
                    parentMissingSinceUtc = null;
                }

                // Full hardware snapshot (every 500ms)
                var snapshot = collector.Capture();

                var nativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;
                var nativeFpsStatus = nativeState?.FpsStatus;
                var hasNativeFps = int.TryParse(nativeFpsStatus, out var nativeFpsValue) && nativeFpsValue > 0;

                if (nativeState != null)
                {
                    var summary =
                        $"FPS={nativeState.FpsStatus ?? "--"} Elevated={nativeState.IsElevated} EtwRunning={nativeState.EtwRunning} " +
                        $"Events={nativeState.EtwEventsReceived} ParentPid={nativeState.ParentPid} RootPid={nativeState.RootPid} " +
                        $"TargetPid={nativeState.TargetPid} Target={nativeState.TargetProcessName ?? ""} CandidatePid={nativeState.CandidatePid} " +
                        $"Attempts={nativeState.EtwStartAttemptCount} Failures={nativeState.EtwStartFailureCount} " +
                        $"LastAttempt={nativeState.LastEtwAttemptAtUtc ?? ""} LastLock={nativeState.LastTargetLockAtUtc ?? ""} " +
                        $"DxgKrnlEnabled={nativeState.DxgKrnlEnabled} DxgiEnabled={nativeState.DxgiEnabled} D3D9Enabled={nativeState.D3D9Enabled} " +
                        $"DXGI={nativeState.MatchedDxgiEventCount} D3D9={nativeState.MatchedD3D9EventCount} DXGKRNL={nativeState.MatchedDxgKrnlEventCount} " +
                        $"LastDxgKrnlError={nativeState.LastDxgKrnlError ?? ""} LastDxgiError={nativeState.LastDxgiError ?? ""} LastD3D9Error={nativeState.LastD3D9Error ?? ""} " +
                        $"LastError={nativeState.LastEtwError ?? ""} Error={nativeState.Error ?? ""} Debug={nativeState.DebugMessage ?? ""}";
                    if (!string.Equals(summary, nativeFpsLoggedSummary, StringComparison.Ordinal))
                    {
                        log?.Invoke($"[NativeFpsAgent] {summary}");
                        nativeFpsLoggedSummary = summary;
                    }
                }

                if (hasNativeFps)
                {
                    fpsMeter.SetFps(nativeFpsValue);
                    snapshot.FpsStatus = nativeFpsValue.ToString();
                    snapshot.FpsSource = nativeState?.FpsSource ?? "NativeFpsAgent";
                }
                else if (nativeFpsStarted)
                {
                    snapshot.FpsStatus = fpsMeter.GetCurrentFpsFormatted();
                    snapshot.FpsSource = "NativeFpsAgent";
                }
                else
                {
                    // Native agent unavailable; degrade to local fallback formatting only.
                    snapshot.FpsStatus = fpsMeter.GetCurrentFpsFormatted();
                    snapshot.FpsSource = "FpsMeter";
                }

                // Write full snapshot to disk
                WriteSnapshot(statePath, tempPath, snapshot);
                log?.Invoke($"Snapshot: FPS={snapshot.FpsStatus} GPU={snapshot.GpuUsagePercent:F1}% Source={snapshot.FpsSource}");

                // Write FPS-state at 20ms intervals for the next 500ms
                for (int i = 0; i < SnapshotIntervalMs / FpsStateIntervalMs; i++)
                {
                    if (shutdownEvent?.WaitOne(0) == true)
                    {
                        goto exit;
                    }

                    if (i % 25 == 0 && !ParentIsAlive(parentPid))
                    {
                        parentMissingSinceUtc ??= DateTime.UtcNow;
                        if (DateTime.UtcNow - parentMissingSinceUtc.Value > OrphanGracePeriod)
                            goto exit;
                    }
                    else if (i % 25 == 0)
                    {
                        parentMissingSinceUtc = null;
                    }

                    var fpsStatus = snapshot.FpsStatus;
                    var fpsSource = snapshot.FpsSource;

                    var fpsState = new FpsState
                    {
                        CapturedAtUtc = DateTime.UtcNow,
                        FpsStatus = fpsStatus,
                        FpsSource = fpsSource
                    };

                    WriteFpsState(fpsStatePath, fpsTempPath, fpsState);
                    Thread.Sleep(FpsStateIntervalMs);
                }
            }

exit:
            log?.Invoke("Hardware monitor agent exited gracefully.");
            return 0;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Hardware monitor agent failed: {ex}");
            return -1;
        }
    }

    private static EventWaitHandle? TryOpenShutdownEvent(string[] args, Action<string>? log)
    {
        var eventName = TryReadArgument(args, "--shutdown-event");
        if (string.IsNullOrWhiteSpace(eventName))
            return null;

        try
        {
            return EventWaitHandle.OpenExisting(eventName);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            log?.Invoke($"Shutdown event was not found: {eventName}");
            return null;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to open shutdown event {eventName}: {ex.Message}");
            return null;
        }
    }

    private static int? TryReadParentPid(string[] args)
    {
        var value = TryReadArgument(args, "--parent-pid");
        if (int.TryParse(value, out var pid) && pid > 0)
            return pid;
        return null;
    }

    private static string? TryReadArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static bool ParentIsAlive(int? parentPid)
    {
        if (!parentPid.HasValue)
            return true;
        try
        {
            using var process = Process.GetProcessById(parentPid.Value);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteSnapshot(string statePath, string tempPath, HardwareMonitorSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(tempPath, json);
        MoveWithRetry(tempPath, statePath);
    }

    private static void WriteFpsState(string statePath, string tempPath, FpsState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(tempPath, json);
        MoveWithRetry(tempPath, statePath);
    }

    private static void MoveWithRetry(string tempPath, string statePath)
    {
        for (var attempt = 0; attempt < AtomicWriteRetryCount; attempt++)
        {
            try
            {
                File.Move(tempPath, statePath, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < AtomicWriteRetryCount - 1)
            {
                Thread.Sleep(AtomicWriteRetryDelayMs);
            }
            catch (IOException) when (attempt < AtomicWriteRetryCount - 1)
            {
                Thread.Sleep(AtomicWriteRetryDelayMs);
            }
        }

        File.Move(tempPath, statePath, overwrite: true);
    }
}

public sealed class FpsState
{
    public DateTime CapturedAtUtc { get; set; }
    public string FpsStatus { get; set; } = "--";
    public string FpsSource { get; set; } = "";
}
