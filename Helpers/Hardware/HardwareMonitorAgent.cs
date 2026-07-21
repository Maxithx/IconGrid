using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorAgent
{
    private const string AgentMutexName = @"Local\IconGrid.HardwareMonitorAgent";
    private const int SnapshotIntervalMs = 500;
    private const int FpsStateIntervalMs = 2;
    private const int AtomicWriteRetryCount = 8;
    private const int AtomicWriteRetryDelayMs = 6;
    private const int ForegroundPollIntervalMs = 1000;
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(5);
    private static readonly string[] IgnoredForegroundProcesses = { "explorer", "IconGrid", "ApplicationFrameHost", "SearchApp", "StartMenuExperienceHost" };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Win32 P/Invoke for foreground window detection
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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
        var activeFpsTargetSignature = NativeFpsAgentRunner.CreateTargetSignature(fpsTarget);
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
            var currentForegroundGamePid = default(int?);

            // Determine initial start mode: if no config target, pass foreground PID if available.
            var hasConfigTarget = fpsTarget != null &&
                (!string.IsNullOrWhiteSpace(fpsTarget.ExecutableName) ||
                 !string.IsNullOrWhiteSpace(fpsTarget.ResolvedExecutablePath) ||
                 fpsTarget.RootProcessId.HasValue);
            int? initialForegroundPid = null;
            if (!hasConfigTarget)
            {
                initialForegroundPid = TryGetForegroundGamePid(log);
                if (initialForegroundPid.HasValue)
                {
                    log?.Invoke($"Initial foreground game PID detected: {initialForegroundPid.Value}");
                    currentForegroundGamePid = initialForegroundPid;
                }
            }

            var nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Start(parentPid, initialForegroundPid);
            log?.Invoke($"Hardware monitor agent started. NativeFpsStarted={nativeFpsStarted} HasConfigTarget={hasConfigTarget} ForegroundPid={initialForegroundPid?.ToString() ?? "null"}");

            var nativeFpsLoggedSummary = string.Empty;
            DateTime? parentMissingSinceUtc = null;
            var foregroundPollCounter = 0;
            var foregroundOverridePid = default(int?);
            var overrideExpiresAtUtc = default(DateTime?);
            var lastStableFps = 0.0;
            var lastStableFpsSetAtUtc = DateTime.MinValue;

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

                var latestConfig = configManager.LoadConfig();
                var latestFpsTargetSignature = NativeFpsAgentRunner.CreateTargetSignature(latestConfig.FpsTarget);
                
                // If we have an active foreground override, suppress config reloads for 30s.
                if (overrideExpiresAtUtc.HasValue && DateTime.UtcNow < overrideExpiresAtUtc.Value)
                {
                    // Keep using the foreground PID; ignore stale config reloads.
                }
                else if (!string.Equals(latestFpsTargetSignature, activeFpsTargetSignature, StringComparison.Ordinal))
                {
                    log?.Invoke($"FPS target changed. Restarting native FPS agent. Old={activeFpsTargetSignature} New={latestFpsTargetSignature}");
                    activeFpsTargetSignature = latestFpsTargetSignature;
                    currentForegroundGamePid = null; // Config target takes priority; clear foreground PID.
                    nativeFpsLoggedSummary = string.Empty;
                    nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Restart(parentPid);
                    overrideExpiresAtUtc = null;
                    foregroundOverridePid = null;
                }

                    // Poll foreground window every ~1s to detect externally launched games.
                foregroundPollCounter++;
                if (foregroundPollCounter >= ForegroundPollIntervalMs / SnapshotIntervalMs)
                {
                    foregroundPollCounter = 0;
                    TryUpdateForegroundGameTarget(nativeFpsAgent, latestConfig, ref activeFpsTargetSignature, ref nativeFpsStarted, ref currentForegroundGamePid, ref overrideExpiresAtUtc, ref foregroundOverridePid, parentPid, log);
                }

                // Full hardware snapshot (every 500ms)
                var snapshot = collector.Capture();

                var nativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;
                var nativeFpsStatus = nativeState?.FpsStatus;
                var nativeFpsValue = nativeState?.FpsValue;
                var hasNativeFps = nativeFpsValue.HasValue && nativeFpsValue.Value > 0;

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

                // Emulator detection: if FPS > 120 but only DxgKrnl events (no DXGI),
                // the FPS is likely inflated by emulator compositing (e.g. Yuzu).
                var emulatorOvercount = false;
                if (hasNativeFps && nativeFpsValue!.Value > 120.0 && nativeState != null &&
                    nativeState.MatchedDxgiEventCount == 0 && nativeState.MatchedDxgKrnlEventCount > 0)
                {
                    emulatorOvercount = true;
                    log?.Invoke($"Emulator overcount detected: FPS={nativeFpsValue.Value:F0} DXGI=0 DXGKRNL={nativeState.MatchedDxgKrnlEventCount}");
                }

                // Spike guard: reject improbable FPS and smooth large jumps.
                var effectiveFps = nativeFpsValue;
                if (hasNativeFps)
                {
                    var now = DateTime.UtcNow;
                    var isSpike = false;

                    // Emulator cap: if only DxgKrnl events and FPS > 120, cap at 60.
                    if (emulatorOvercount)
                    {
                        effectiveFps = 60.0;
                        lastStableFps = 60.0; // Reset stable baseline immediately.
                        lastStableFpsSetAtUtc = now;
                        log?.Invoke($"Emulator cap applied: FPS capped to 60");
                    }
                    else
                    {
                        var rawFps = nativeFpsValue!.Value;
                    
                        // Hard cap: anything >1000 is always a spike.
                        if (rawFps > 1000.0)
                        {
                            isSpike = true;
                            log?.Invoke($"Spike filtered (>1000): rawFps={rawFps:F0} lastStable={lastStableFps:F0}");
                        }
                        // Large upward jump from a stable baseline: treat as spike unless it persists.
                        else if (lastStableFps > 10.0 && rawFps > lastStableFps * 1.5)
                        {
                            if ((now - lastStableFpsSetAtUtc).TotalSeconds < 6.0)
                            {
                                isSpike = true;
                                log?.Invoke($"Spike filtered (jump): rawFps={rawFps:F0} lastStable={lastStableFps:F0}");
                            }
                        }

                        if (isSpike)
                        {
                            effectiveFps = lastStableFps > 0.0 ? lastStableFps : null;
                        }
                        else
                        {
                            if (lastStableFps > 0.0)
                            {
                                const double emaAlpha = 0.3;
                                lastStableFps = (rawFps * emaAlpha) + (lastStableFps * (1.0 - emaAlpha));
                            }
                            else
                            {
                                lastStableFps = rawFps;
                            }
                            lastStableFpsSetAtUtc = now;
                        }
                    }
                }

                if (effectiveFps.HasValue && effectiveFps.Value > 0)
                {
                    fpsMeter.SetFps(effectiveFps.Value);
                    snapshot.FpsStatus = Math.Round(effectiveFps.Value).ToString("F0");
                    snapshot.FpsSource = nativeState?.FpsSource ?? "NativeFpsAgent";
                }
                else if (nativeFpsStarted)
                {
                    snapshot.FpsStatus = fpsMeter.GetSnapshot().LiveFpsFormatted;
                    snapshot.FpsSource = "NativeFpsAgent";
                }
                else
                {
                    // Native agent unavailable; degrade to local fallback formatting only.
                    snapshot.FpsStatus = fpsMeter.GetSnapshot().LiveFpsFormatted;
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

                    var liveNativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;
                    var liveNativeFpsStatus = liveNativeState?.FpsStatus;
                    var liveNativeFpsValue = liveNativeState?.FpsValue;
                    var hasLiveNativeFps = liveNativeFpsValue.HasValue && liveNativeFpsValue.Value > 0;
                    if (hasLiveNativeFps)
                    {
                        fpsMeter.SetFps(liveNativeFpsValue!.Value);
                    }

                    var fpsSnapshot = fpsMeter.GetSnapshot();
                    var fpsSource = liveNativeState?.FpsSource ?? snapshot.FpsSource;
                    var fpsStatus = hasLiveNativeFps
                        ? Math.Round(liveNativeFpsValue!.Value).ToString("F0")
                        : fpsSnapshot.LiveFpsFormatted;
                    var liveFpsValueForState = hasLiveNativeFps
                        ? liveNativeFpsValue
                        : (fpsSnapshot.LiveFps > 0 ? fpsSnapshot.LiveFps : null);

                    var fpsState = new FpsState
                    {
                        CapturedAtUtc = DateTime.UtcNow,
                        FpsStatus = fpsStatus,
                        FpsSource = fpsSource,
                        LiveFpsStatus = fpsStatus,
                        TrendFpsStatus = fpsSnapshot.SmoothedFpsFormatted,
                        LiveFpsValue = liveFpsValueForState,
                        TrendFpsValue = fpsSnapshot.SmoothedFps > 0 ? fpsSnapshot.SmoothedFps : null
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

    /// <summary>
    /// Attempts to detect the foreground window's process ID, excluding known system/IconGrid processes.
    /// Returns null if the foreground window belongs to an ignored process or cannot be determined.
    /// </summary>
    private static int? TryGetForegroundGamePid(Action<string>? log)
    {
        try
        {
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(foregroundHwnd, out var pid);
            if (pid == 0)
                return null;

            // Check if the process is one we should ignore (IconGrid itself, Explorer, etc.)
            try
            {
                using var process = Process.GetProcessById((int)pid);
                var processName = process.ProcessName;
                foreach (var ignored in IgnoredForegroundProcesses)
                {
                    if (string.Equals(processName, ignored, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }

            return (int?)pid;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Foreground window detection failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Polls the foreground window PID. If it changes to a new game process, 
    /// restarts the native FPS agent with the new foreground PID.
    /// Also checks if the config target process is still alive; if not, falls back to foreground detection.
    /// </summary>
    private static void TryUpdateForegroundGameTarget(
        NativeFpsAgentRunner nativeFpsAgent,
        Models.ConfigModel config,
        ref string activeFpsTargetSignature,
        ref bool nativeFpsStarted,
        ref int? currentForegroundGamePid,
        ref DateTime? overrideExpiresAtUtc,
        ref int? foregroundOverridePid,
        int? parentPid,
        Action<string>? log)
    {
        var configTarget = config.FpsTarget;
        var hasConfigTarget = configTarget != null &&
            (!string.IsNullOrWhiteSpace(configTarget.ExecutableName) ||
             !string.IsNullOrWhiteSpace(configTarget.ResolvedExecutablePath) ||
             configTarget.RootProcessId.HasValue);

        // If there's a config target, check if it's still alive.
        if (hasConfigTarget && configTarget != null)
        {
            var targetAlive = false;
            if (configTarget.RootProcessId.HasValue && configTarget.RootProcessId.Value > 0)
            {
                targetAlive = ProcessIsAlive(configTarget.RootProcessId.Value);
            }

            if (!targetAlive && !string.IsNullOrWhiteSpace(configTarget.ExecutableName))
            {
                // The config target process is dead. Clear the config signature so we
                // can fall back to foreground detection for externally launched games.
                log?.Invoke($"Config target process is no longer alive (RootPid={configTarget.RootProcessId}, Exe={configTarget.ExecutableName}). Clearing config target to enable foreground detection.");
                activeFpsTargetSignature = string.Empty;
                hasConfigTarget = false;
                
                // Also clear the stale FpsTarget from config.json to prevent reload oscillation.
                try
                {
                    config.FpsTarget = new Models.FpsTargetConfig();
                    var configManager = new Helpers.Settings.ConfigManager();
                    configManager.SaveConfig(config);
                    log?.Invoke("Cleared stale FpsTarget from config.json.");
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Failed to clear stale FpsTarget from config.json: {ex.Message}");
                }
            }
        }

        if (hasConfigTarget)
        {
            return; // Config target is alive; do not override with foreground.
        }

        var newForegroundPid = TryGetForegroundGamePid(log);
        if (!newForegroundPid.HasValue && currentForegroundGamePid.HasValue)
        {
            // Foreground is no longer a game (e.g., user alt-tabbed or clicked overlay).
            // Do NOT restart — keep tracking the existing game PID.
            // Only restart if the game process actually exited.
            if (!ProcessIsAlive(currentForegroundGamePid.Value))
            {
                log?.Invoke($"Current game PID {currentForegroundGamePid.Value} has exited. Clearing.");
                currentForegroundGamePid = null;
                foregroundOverridePid = null;
                nativeFpsStarted = false;
            }
            return;
        }

        if (newForegroundPid.HasValue && newForegroundPid != currentForegroundGamePid)
        {
            log?.Invoke($"Foreground game PID changed from {currentForegroundGamePid?.ToString() ?? "null"} to {newForegroundPid.Value}. Restarting native FPS agent.");
            currentForegroundGamePid = newForegroundPid;
            foregroundOverridePid = newForegroundPid;
            overrideExpiresAtUtc = DateTime.UtcNow.AddSeconds(30);
            log?.Invoke($"Foreground override activated for PID {newForegroundPid.Value} until {overrideExpiresAtUtc.Value:HH:mm:ss}.");
            nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Restart(parentPid, newForegroundPid);
        }
    }

    private static bool ProcessIsAlive(int pid)
    {
        if (pid <= 0)
            return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
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
    public string LiveFpsStatus { get; set; } = "--";
    public string TrendFpsStatus { get; set; } = "--";
    public double? LiveFpsValue { get; set; }
    public double? TrendFpsValue { get; set; }
}
