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
    private const int MinimumGameWindowWidth = 960;
    private const int MinimumGameWindowHeight = 540;
    private static readonly TimeSpan RecentVisibleWindowLaunchGracePeriod = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan StaleVisibleWindowPenaltyAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ConfigTargetLaunchGracePeriod = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan NonGameProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NonGameCooldown = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(5);
    // An agent started without a launcher parent (the "IconGrid Monitor" Task
    // Scheduler task passes no --parent-pid) must not stay alive as an orphan:
    // exit once no launcher process has been present for this long.
    private static readonly TimeSpan LauncherAbsenceGracePeriod = TimeSpan.FromSeconds(60);

    // A sticky game target is held while it is alive (so alt-tab does not retarget).
    // If a DIFFERENT valid foreground game candidate stays stable this long, the
    // sticky target is released — a bad target (e.g. a shell process confirmed
    // through the weak DxgKrnl fallback) must never block the real game forever.
    private static readonly TimeSpan StickyChallengerOverrideDelay = TimeSpan.FromSeconds(20);

    // FPS classification is EVIDENCE-BASED: a process is a game only when the
    // native agent observed application-level presents (DXGI/D3D9) for it. The
    // only name-based exclusion in this pipeline is our own processes — there is
    // deliberately no app blocklist here (see GameProcessClassifier).
    private static readonly string[] IgnoredForegroundProcesses = GameProcessClassifier.SelfProcessNames;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly FpsNormalizerState FpsNormalizer = new();

    // Win32 P/Invoke for foreground window detection
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    // Toolhelp32 for module enumeration — used to confidently identify
    // non-rendering programs (Notepad, Paint, etc.) via module list.
    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Module32FirstW(IntPtr hSnapshot, ref MODULEENTRY32W lpme);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Module32NextW(IntPtr hSnapshot, ref MODULEENTRY32W lpme);

    private const uint Th32csSnapmodule = 0x00000008;
    private const int MaxModuleName32 = 255;
    private const int MaxPath = 260;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MODULEENTRY32W
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxModuleName32 + 1)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
        public string szExePath;
    }

    private static readonly string[] GraphicsApiDlls =
    {
        "d3d9.dll",
        "d3d10.dll",
        "d3d10core.dll",
        "d3d11.dll",
        "d3d12.dll",
        "d3d12core.dll",
        "dxgi.dll",
        "vulkan-1.dll",
        "opengl32.dll"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Mirrors WINDOWPLACEMENT. NormalPosition is the restored (non-minimized) rect,
    /// which stays game-sized even while the window is minimized or hidden.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT MinPosition;
        public POINT MaxPosition;
        public RECT NormalPosition;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExNoActivate = 0x08000000L;

    private static readonly Launcher.ExternalGameRegistry ExternalGames = new();
    private static Launcher.DisplayResolutionService? _externalDisplayResolution;

    private sealed record VisibleWindowCandidate(int Pid, string ProcessName, long Area, DateTime? StartedAtUtc, int Score);

    public static int Run(string[] args, Action<string>? log = null)
    {
        using var mutex = new Mutex(true, AgentMutexName, out var createdNew);
        if (!createdNew)
        {
            // Another agent instance may still be shutting down (e.g. its parent
            // launcher was force-killed by deploy-test.cmd's taskkill, and it is
            // waiting out OrphanGracePeriod). If we give up immediately here, NO
            // agent remains running until the launcher restarts: the gaming overlay
            // stops showing FPS and the in-game state never updates when a game is
            // launched or closed. Wait for the previous instance to release the
            // mutex instead.
            log?.Invoke("Hardware monitor agent is already running. Waiting for the previous instance to exit...");
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(15)))
                {
                    // The previous agent may have been started by the "IconGrid
                    // Monitor" Task Scheduler task, which passes no launcher
                    // shutdown event. Ask it to stop through the well-known
                    // request-stop event and wait for that handover instead of
                    // leaving NO agent at all (which stopped FPS/overlay updates).
                    log?.Invoke("Timed out waiting for the previous hardware monitor agent to release the mutex. Requesting a stop and retrying.");
                    MonitorAgentLifecycle.TrySignalRequestStop(log);
                    if (!mutex.WaitOne(TimeSpan.FromSeconds(15)))
                    {
                        log?.Invoke("Previous hardware monitor agent still holds the mutex after the stop request.");
                        return 0;
                    }
                }
            }
            catch (AbandonedMutexException)
            {
                // Previous instance crashed without releasing the mutex; we now
                // own it and can continue as the active agent.
            }

            log?.Invoke("Previous hardware monitor agent exited; this instance is now the active agent.");
        }

        // Opened only once this instance owns the mutex, so an instance that is
        // still waiting for a takeover never consumes its own stop request.
        using var requestStopEvent = MonitorAgentLifecycle.OpenRequestStopEvent(log);
        MonitorAgentLifecycle.ClearStopRequest(requestStopEvent);
        var launcherPresence = new LauncherPresenceTracker();

        var parentPid = TryReadParentPid(args);
        var parentStartFileTimeUtc = TryReadParentStartFileTime(args);
        var configManager = new ConfigManager();
        var baseDir = configManager.BaseDirectory;
        var statePath = Path.Combine(baseDir, "monitor-state.json");
        var tempPath = statePath + ".tmp";
        var fpsStatePath = Path.Combine(baseDir, "fps-state.json");
        var fpsTempPath = fpsStatePath + ".tmp";
        var nativeFpsStatePath = Path.Combine(baseDir, "native-fps-state.json");
        using var shutdownEvent = TryOpenShutdownEvent(args, log);

        var config = configManager.LoadConfig();
        if (IsLikelyLauncherConfigTarget(config.FpsTarget))
        {
            config.FpsTarget = new Models.FpsTargetConfig();
        }
        else
        {
            TryClearStaleFpsTargetRuntimeMetadata(config, log);
        }
        var fpsTarget = config.FpsTarget;
        var activeFpsTargetSignature = NativeFpsAgentRunner.CreateTargetSignature(fpsTarget);

        try
        {
            Directory.CreateDirectory(baseDir);
            using var collector = new HardwareSnapshotCollector();
            using var nativeFpsAgent = new NativeFpsAgentRunner(nativeFpsStatePath, log);
            using var fpsMeter = new FpsMeter(config.GamingOverlayFpsResponsiveness, log);
            var currentForegroundGamePid = default(int?);
            var confirmedForegroundGamePid = default(int?);
            var hadConfirmedGameSession = false;
            // Declared here (not with the other loop state below) because the
            // startup bootstrap needs to start the non-game probe timer.
            var currentForegroundPidObservedAtUtc = default(DateTime?);

            int? initialForegroundPid = null;
            if (HasConfiguredTargetIdentity(fpsTarget))
            {
                log?.Invoke("Using configured FPS target at startup; skipping foreground-first bootstrap.");
            }
            else
            {
                initialForegroundPid = TryGetBootstrapGamePid(log);
                if (initialForegroundPid.HasValue)
                {
                    log?.Invoke($"Initial foreground game PID detected: {initialForegroundPid.Value}");
                    currentForegroundGamePid = initialForegroundPid;

                    // Start the non-game probe timer for the bootstrap target too,
                    // so a wrongly-acquired shell/Store process is rejected after
                    // the probe timeout instead of being held forever.
                    currentForegroundPidObservedAtUtc = DateTime.UtcNow;
                }
            }

            var nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Start(parentPid, initialForegroundPid);
            log?.Invoke($"Hardware monitor agent started. NativeFpsStarted={nativeFpsStarted} ForegroundPid={initialForegroundPid?.ToString() ?? "null"}");

            var nativeFpsLoggedSummary = string.Empty;
            var fpsAgentWatchdog = new FpsAgentWatchdog();
            DateTime? parentMissingSinceUtc = null;
            var foregroundPollCounter = 0;
            var foregroundOverridePid = default(int?);
            var overrideExpiresAtUtc = default(DateTime?);
            var rejectedForegroundPid = default(int?);
            var rejectedForegroundPidCooldownUntilUtc = default(DateTime?);
            var stickyChallengerPid = default(int?);
            var stickyChallengerSinceUtc = default(DateTime?);
            var lastTargetWasForegroundWindow = default(bool?);
            var lastTargetBecameForegroundAtUtc = DateTime.MinValue;

            while (true)
            {
                if (shutdownEvent?.WaitOne(0) == true)
                {
                    log?.Invoke("Exiting because shutdown event was signaled.");
                    break;
                }

                if (requestStopEvent?.WaitOne(0) == true)
                {
                    log?.Invoke("Exiting because a monitor agent stop was requested.");
                    break;
                }

                // An agent started without a launcher parent (Task Scheduler) has no
                // parent-liveness check, so it must prove on its own that the app it
                // belongs to still exists.
                if (!parentPid.HasValue && launcherPresence.ShouldExit(LauncherAbsenceGracePeriod, log))
                {
                    break;
                }

                if (!ParentIsAlive(parentPid, parentStartFileTimeUtc))
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
                TryClearStaleFpsTargetRuntimeMetadata(latestConfig, log);
                var nativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;

                // Watchdog: a native worker that is gone, hung, or whose output went
                // stale must never leave the FPS pipeline dead while the tracked game
                // PID stays the same (2026-09-19: 12 minutes with no FPS/overlay after
                // Call of Duty restarted itself).
                if (nativeFpsStarted)
                {
                    var workerExitCode = nativeFpsAgent.TryGetProcessExitCode(out var exitCode)
                        ? exitCode
                        : (int?)null;
                    var restartReason = fpsAgentWatchdog.Evaluate(
                        currentForegroundGamePid,
                        nativeState,
                        workerExitCode,
                        DateTime.UtcNow,
                        out var restartDetail);
                    if (restartReason != FpsAgentRestartReason.None)
                    {
                        log?.Invoke($"FPS agent watchdog: restarting the native FPS worker. Reason={restartReason}. {restartDetail}");
                        nativeFpsStarted = nativeFpsAgent.IsAvailable &&
                                           nativeFpsAgent.Restart(parentPid, nativeFpsAgent.LastForegroundGamePid);
                        fpsAgentWatchdog.OnRestarted(DateTime.UtcNow);
                        nativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;
                    }
                }

                // Poll foreground window every ~1s to detect the current game directly.
                foregroundPollCounter++;
                if (foregroundPollCounter >= ForegroundPollIntervalMs / SnapshotIntervalMs)
                {
                    foregroundPollCounter = 0;
                    TryUpdateForegroundGameTarget(
                        nativeFpsAgent,
                        latestConfig,
                        nativeState,
                        ref activeFpsTargetSignature,
                        ref nativeFpsStarted,
                        ref currentForegroundGamePid,
                        ref confirmedForegroundGamePid,
                        ref hadConfirmedGameSession,
                        ref overrideExpiresAtUtc,
                        ref foregroundOverridePid,
                        ref currentForegroundPidObservedAtUtc,
                        ref rejectedForegroundPid,
                        ref rejectedForegroundPidCooldownUntilUtc,
                        ref stickyChallengerPid,
                        ref stickyChallengerSinceUtc,
                        parentPid,
                        log);
                }

                // Full hardware snapshot (every 500ms)
                var snapshot = collector.Capture();
                var nativeFpsStatus = nativeState?.FpsStatus;
                var nativeFpsValue = nativeState?.FpsValue;
                var hasNativeFps = nativeFpsValue.HasValue && nativeFpsValue.Value > 0;
                UpdateConfirmedForegroundGamePid(nativeState, ref currentForegroundGamePid, ref confirmedForegroundGamePid, ref hadConfirmedGameSession);

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

                var foregroundProcessPid = TryGetForegroundProcessPid();
                var targetIsForegroundWindow = nativeState?.TargetPid > 0 &&
                                               foregroundProcessPid.HasValue &&
                                               nativeState.TargetPid == foregroundProcessPid.Value;
                if (targetIsForegroundWindow && lastTargetWasForegroundWindow != true)
                {
                    lastTargetBecameForegroundAtUtc = DateTime.UtcNow;
                    FpsNormalizer.ResetBaseline();
                    log?.Invoke("Target regained foreground; resetting FPS spike baseline.");
                }
                lastTargetWasForegroundWindow = targetIsForegroundWindow;

                // When sticky-target is confirmed and the target process is still alive,
                // bypass the spike filter entirely — the signal is trustworthy regardless
                // of foreground status (applies generically to all games).
                var useRawFps = confirmedForegroundGamePid.HasValue &&
                                ProcessIsAlive(confirmedForegroundGamePid.Value);
                double? effectiveFps;
                if (useRawFps && nativeFpsValue.HasValue && nativeFpsValue.Value > 0)
                {
                    effectiveFps = nativeFpsValue.Value;
                }
                else
                {
                    var normalization = FpsNormalizer.Normalize(
                        nativeFpsValue,
                        nativeState,
                        targetIsForegroundWindow,
                        log);
                    effectiveFps = normalization.EffectiveFps;
                }

                if (effectiveFps.HasValue && effectiveFps.Value > 0)
                {
                    fpsMeter.SetStickyHold(false);
                    fpsMeter.SetFps(effectiveFps.Value);
                    snapshot.FpsStatus = Math.Round(effectiveFps.Value).ToString("F0");
                    snapshot.FpsSource = nativeState?.FpsSource ?? "NativeFpsAgent";
                }
                else if (nativeFpsStarted)
                {
                    var holdLastFps = confirmedForegroundGamePid.HasValue &&
                                      ProcessIsAlive(confirmedForegroundGamePid.Value) &&
                                      (nativeState == null || (nativeState.EtwRunning && nativeState.TargetPid > 0));
                    fpsMeter.SetStickyHold(holdLastFps);
                    snapshot.FpsStatus = fpsMeter.GetSnapshot().LiveFpsFormatted;
                    snapshot.FpsSource = "NativeFpsAgent";
                }
                else
                {
                    snapshot.FpsStatus = fpsMeter.GetSnapshot().LiveFpsFormatted;
                    snapshot.FpsSource = "FpsMeter";
                }

                // Write full snapshot to disk
                WriteSnapshot(statePath, tempPath, snapshot);

                // Per-target dedicated VRAM, so it is visible in the trace whether a
                // target is genuinely using the GPU the way a game does. A shell
                // process reports a small figure (or none), a game reports hundreds
                // of MB to GB.
                var targetVramText = "Vram=--";
                if (nativeState?.TargetPid > 0)
                {
                    var targetVram = GpuProcessMemory.TryGetDedicatedUsageBytes(nativeState.TargetPid);
                    if (targetVram.HasValue)
                    {
                        targetVramText = $"Vram={GpuProcessMemory.Format(targetVram.Value)}";
                    }

                    // The classifier uses the peak (a game frees VRAM during
                    // loading/alt-tab), so show it next to the live value.
                    var targetVramPeak = GameVramEvidence.GetPeakBytes(nativeState.TargetPid, DateTime.UtcNow);
                    if (targetVramPeak.HasValue)
                    {
                        targetVramText += $" Peak={GpuProcessMemory.Format(targetVramPeak.Value)}";
                    }
                }

                log?.Invoke($"Snapshot: FPS={snapshot.FpsStatus} GPU={snapshot.GpuUsagePercent:F1}% Source={snapshot.FpsSource} {targetVramText}");

                // Write FPS-state at 20ms intervals for the next 500ms
                for (int i = 0; i < SnapshotIntervalMs / FpsStateIntervalMs; i++)
                {
                    if (shutdownEvent?.WaitOne(0) == true)
                    {
                        goto exit;
                    }

                    if (i % 25 == 0 && !ParentIsAlive(parentPid, parentStartFileTimeUtc))
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
                    var liveForegroundProcessPid = TryGetForegroundProcessPid();
                    var liveTargetIsForegroundWindow = liveNativeState?.TargetPid > 0 &&
                                                       liveForegroundProcessPid.HasValue &&
                                                       liveNativeState.TargetPid == liveForegroundProcessPid.Value;
                    if (liveTargetIsForegroundWindow && lastTargetWasForegroundWindow != true)
                    {
                        lastTargetBecameForegroundAtUtc = DateTime.UtcNow;
                        FpsNormalizer.ResetBaseline();
                    }
                    lastTargetWasForegroundWindow = liveTargetIsForegroundWindow;

                    var liveNormalization = FpsNormalizer.Normalize(
                        liveNativeFpsValue,
                        liveNativeState,
                        liveTargetIsForegroundWindow,
                        log: null);
                    var effectiveLiveNativeFps = liveNormalization.EffectiveFps;
                    var hasEffectiveLiveNativeFps = effectiveLiveNativeFps.HasValue && effectiveLiveNativeFps.Value > 0;
                    if (hasEffectiveLiveNativeFps)
                    {
                        fpsMeter.SetFps(effectiveLiveNativeFps!.Value);
                    }

                    var fpsSnapshot = fpsMeter.GetSnapshot();
                    var fpsSource = liveNativeState?.FpsSource ?? snapshot.FpsSource;
                    var fpsStatus = hasEffectiveLiveNativeFps
                        ? Math.Round(effectiveLiveNativeFps!.Value).ToString("F0")
                        : fpsSnapshot.LiveFpsFormatted;
                    var liveFpsValueForState = hasEffectiveLiveNativeFps
                        ? effectiveLiveNativeFps
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

    private static long? TryReadParentStartFileTime(string[] args)
    {
        var value = TryReadArgument(args, "--parent-start-filetime");
        if (long.TryParse(value, out var fileTime) && fileTime > 0)
            return fileTime;
        return null;
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
    /// Optimistic foreground-detection entry point. Filters out ignored
    /// processes and windows that are too small, then optionally uses the
    /// module-list check as a positive skip for known non-rendering programs.
    /// </summary>
    private static int? TryGetForegroundGamePid(Action<string>? log)
    {
        try
        {
            var foreground = TryGetForegroundWindowProcess(log);
            if (!foreground.HasValue)
            {
                return null;
            }

            var (foregroundHwnd, pid) = foreground.Value;

            string? processName = null;

            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
                foreach (var ignored in IgnoredForegroundProcesses)
                {
                    if (string.Equals(processName, ignored, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }

                // No process-name or installation-path filter here on purpose: the
                // candidate is classified by evidence afterwards (dedicated VRAM
                // peak, or application-level presents), which rejects shell/system
                // programs from their own measurements instead of from a list.

                if (!IsLikelyGameForegroundWindow(foregroundHwnd, processName, log))
                {
                    return null;
                }

                // Optimistic skip: if the module list is available and shows
                // zero graphics API DLLs, this process is NOT a game and we can
                // confidently skip it. When the module list is unavailable
                // (elevated anti-cheat, UWP container isolation), we DO NOT
                // reject — we let the native FPS agent probe the process.
                if (IsNonRenderingProcess((int)pid))
                {
                    log?.Invoke($"Skipping foreground PID {(int)pid} ({processName}) — module scan confirmed no graphics API DLLs.");
                    return null;
                }

                log?.Invoke($"Foreground candidate detected: PID={(int)pid} Name={processName}");
            }
            catch
            {
                return null;
            }

            return pid;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Foreground window detection failed: {ex.Message}");
            return null;
        }
    }

    private static int? TryGetForegroundProcessPid()
    {
        var foreground = TryGetForegroundWindowProcess(log: null);
        return foreground?.Pid;
    }

    private static int? TryGetBootstrapGamePid(Action<string>? log)
    {
        var foregroundPid = TryGetForegroundGamePid(log);
        if (foregroundPid.HasValue)
        {
            return foregroundPid;
        }

        return TryGetVisibleGamePidFallback(log);
    }

    private static (IntPtr Hwnd, int Pid)? TryGetForegroundWindowProcess(Action<string>? log)
    {
        try
        {
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero)
            {
                return null;
            }

            GetWindowThreadProcessId(foregroundHwnd, out var pid);
            if (pid == 0)
            {
                return null;
            }

            return (foregroundHwnd, (int)pid);
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to query foreground process: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Best-effort window size for candidate scoring. A minimized or hidden window
    /// reports an unusable (or iconic 160x28) rect, so the RESTORED placement is
    /// used as a fallback. That restored size is what separates a real game window
    /// from a Windows composition helper window: dwm.exe permanently owns a hidden
    /// 160x28 "DWM Notification Window" whose VRAM (measured: 921 MB on a 2560x1440
    /// desktop) otherwise reaches the game VRAM floor.
    /// </summary>
    private static bool TryGetWindowSize(IntPtr hwnd, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (GetWindowRect(hwnd, out var rect))
        {
            width = Math.Max(0, rect.Right - rect.Left);
            height = Math.Max(0, rect.Bottom - rect.Top);
        }

        if (width >= MinimumGameWindowWidth && height >= MinimumGameWindowHeight)
        {
            return true;
        }

        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (GetWindowPlacement(hwnd, ref placement))
        {
            width = Math.Max(width, Math.Max(0, placement.NormalPosition.Right - placement.NormalPosition.Left));
            height = Math.Max(height, Math.Max(0, placement.NormalPosition.Bottom - placement.NormalPosition.Top));
        }

        return width > 0 && height > 0;
    }

    private static int? TryGetVisibleGamePidFallback(Action<string>? log)
    {
        try
        {
            var bestCandidate = default(VisibleWindowCandidate);
            var nowUtc = DateTime.UtcNow;

            EnumWindows((hwnd, _) =>
            {
                var candidatePid = 0u;
                try
                {
                    if (hwnd == IntPtr.Zero)
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(hwnd, out candidatePid);
                    if (candidatePid == 0)
                    {
                        return true;
                    }

                    // A game that runs in the background (hidden or minimized) is still
                    // a running game. Its window rect is unusable then, so such a window
                    // is judged purely by the VRAM evidence below instead of being
                    // dropped - otherwise a borderless/background game stayed invisible
                    // to detection until it was brought to the foreground.
                    var windowIsUsable = IsWindowVisible(hwnd) && !IsIconic(hwnd);

                    using var process = Process.GetProcessById((int)candidatePid);
                    var processName = process.ProcessName;
                    foreach (var ignored in IgnoredForegroundProcesses)
                    {
                        if (string.Equals(processName, ignored, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    if (LooksLikeOverlayWindow(hwnd))
                    {
                        return true;
                    }

                    if (windowIsUsable && !IsLikelyGameForegroundWindow(hwnd, processName, log: null))
                    {
                        return true;
                    }

                    // Window geometry alone is NOT evidence of a game: apply the
                    // same dedicated-VRAM rule used everywhere else. A shell window
                    // (Task Manager, a dialog, the desktop) never holds game-like
                    // VRAM, so it can no longer steal the target from a running
                    // game — which is exactly what happened on 2026-09-19, when
                    // Task Manager (12 MB VRAM) replaced Call of Duty (5.3 GB).
                    var candidateVerdict = GameVramEvidence.Evaluate((int)candidatePid, null, nowUtc, out var candidateDetail);
                    if (candidateVerdict != GameEvidenceVerdict.Game)
                    {
                        log?.Invoke($"Visible window candidate PID={candidatePid} ({processName}) skipped: {candidateDetail}.");
                        return true;
                    }

                    // Same optimistic skip as the foreground path.
                    if (IsNonRenderingProcess((int)candidatePid))
                    {
                        return true;
                    }

                    // A background game must still own a GAME-SIZED window. Windows'
                    // own composition helper window (dwm.exe keeps a hidden 160x28
                    // "DWM Notification Window") reaches the VRAM floor on a large
                    // desktop; it was picked as the tracked target and even registered as
                    // an external game. The restored window size separates the two
                    // without any process-name list.
                    if (!TryGetWindowSize(hwnd, out var width, out var height) ||
                        width < MinimumGameWindowWidth ||
                        height < MinimumGameWindowHeight)
                    {
                        log?.Invoke($"Visible window candidate PID={candidatePid} ({processName}) skipped: window {width}x{height} is too small for a game.");
                        return true;
                    }

                    var startedAtUtc = TryGetProcessStartTimeUtc(process);
                    var area = (long)width * height;
                    var score = ScoreVisibleWindowCandidate(area, startedAtUtc, nowUtc);
                    if (score <= 0)
                    {
                        score = 1;
                    }

                    var candidate = new VisibleWindowCandidate((int)candidatePid, processName, area, startedAtUtc, score);

                    if (bestCandidate != null &&
                        candidate.Score < bestCandidate.Score)
                    {
                        return true;
                    }

                    if (bestCandidate != null &&
                        candidate.Score == bestCandidate.Score &&
                        candidate.Area < bestCandidate.Area)
                    {
                        return true;
                    }

                    bestCandidate = candidate;
                }
                catch (Exception ex)
                {
                    // Never drop a window candidate silently: a game protected by
                    // anti-cheat, or any other query failure, must be visible in the
                    // trace instead of silently disabling game detection.
                    log?.Invoke($"Visible window candidate PID={candidatePid} skipped: {ex.GetType().Name}: {ex.Message}");
                }

                return true;
            }, IntPtr.Zero);

            if (bestCandidate != null)
            {
                var ageText = bestCandidate.StartedAtUtc.HasValue
                    ? $"{Math.Max(0, (nowUtc - bestCandidate.StartedAtUtc.Value).TotalSeconds):F0}s"
                    : "unknown";
                log?.Invoke(
                    $"Visible game fallback candidate detected behind IconGrid foreground: PID={bestCandidate.Pid} Name={bestCandidate.ProcessName} Area={bestCandidate.Area} Score={bestCandidate.Score} Age={ageText}");
            }

            return bestCandidate?.Pid;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Visible game fallback detection failed: {ex.Message}");
            return null;
        }
    }

    private static DateTime? TryGetProcessStartTimeUtc(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreVisibleWindowCandidate(long area, DateTime? startedAtUtc, DateTime nowUtc)
    {
        var score = (int)Math.Min(500_000L, area / 2_500L);
        if (!startedAtUtc.HasValue)
        {
            return score;
        }

        var age = nowUtc - startedAtUtc.Value;
        if (age <= RecentVisibleWindowLaunchGracePeriod)
        {
            score += 1_000_000;
            score += (int)Math.Max(0, (RecentVisibleWindowLaunchGracePeriod - age).TotalMilliseconds / 10.0);
            return score;
        }

        if (age >= StaleVisibleWindowPenaltyAge)
        {
            score -= 200_000;
        }

        return score;
    }

    /// <summary>
    /// Returns true ONLY when module enumeration SUCCEEDS and proves the
    /// process has no graphics API DLLs loaded. This is a safe, conservative
    /// filter: it will never reject a game.
    ///
    /// When the enumeration is unavailable (elevated anti-cheat process
    /// denying TH32CS_SNAPMODULE, AppContainer-isolated UWP/GamePass process
    /// with hidden module lists), the method returns FALSE — we let the
    /// native FPS agent probe the process instead.
    /// </summary>
    private static bool IsNonRenderingProcess(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        var snapshot = CreateToolhelp32Snapshot(Th32csSnapmodule, (uint)pid);
        if (snapshot == IntPtr.Zero || snapshot == (IntPtr)(-1))
        {
            // Snapshot unavailable — don't reject. Anti-cheat and UWP
            // processes may block this API.
            return false;
        }

        try
        {
            var entry = new MODULEENTRY32W();
            entry.dwSize = (uint)Marshal.SizeOf<MODULEENTRY32W>();

            if (!Module32FirstW(snapshot, ref entry))
            {
                // Cannot enumerate modules — don't reject.
                return false;
            }

            do
            {
                foreach (var graphicsDll in GraphicsApiDlls)
                {
                    if (string.Equals(entry.szModule, graphicsDll, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found a graphics DLL — process COULD be a game.
                        // Do not skip it.
                        // Exit early since we found positive evidence.
                        return false;
                    }
                }
            } while (Module32NextW(snapshot, ref entry));

            // Module enumeration succeeded, and ZERO graphics DLLs were found.
            // This process is definitely NOT a game (e.g. Notepad, Paint).
            return true;
        }
        catch
        {
            // Exception during enumeration — don't reject.
            return false;
        }
        finally
        {
            // Can't use `using` on a HANDLE from CreateToolhelp32Snapshot.
            // CloseHandle is the only way to release it.
            if (snapshot != IntPtr.Zero && snapshot != (IntPtr)(-1))
            {
                try { Marshal.FreeHGlobal(snapshot); } catch { }
            }
        }
    }

    // Windows shell windows: the desktop, the taskbar and the task view. They are
    // screen-sized, visible, and owned by explorer.exe — so without this check they
    // pass the "looks like a game window" test and explorer.exe gets classified as
    // a game (it then became a sticky target and blocked the real game).
    //
    // This is a STRUCTURAL rule (window class), deliberately not an app-name
    // blocklist, so it cannot go stale the way a name list does.
    private static readonly string[] ShellWindowClasses =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "MultitaskingViewFrame", "TaskListThumbnailWnd", "XamlExplorerHostIslandWindow"
    };

    private static bool IsShellWindowClass(IntPtr hwnd, out string className)
    {
        className = string.Empty;

        try
        {
            var buffer = new System.Text.StringBuilder(256);
            if (GetClassName(hwnd, buffer, buffer.Capacity) <= 0)
            {
                return false;
            }

            className = buffer.ToString();
            foreach (var shellClass in ShellWindowClasses)
            {
                if (string.Equals(className, shellClass, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool LooksLikeOverlayWindow(IntPtr hwnd)
    {
        try
        {
            var exStyle = GetExtendedWindowStyle(hwnd);
            if ((exStyle & WsExNoActivate) != 0)
            {
                return true;
            }

            if ((exStyle & WsExToolWindow) != 0)
            {
                return true;
            }

            if ((exStyle & WsExTransparent) != 0)
            {
                return true;
            }

            if ((exStyle & WsExLayered) != 0 && (exStyle & WsExTopmost) != 0)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static long GetExtendedWindowStyle(IntPtr hwnd)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64();
        }

        return GetWindowLong32(hwnd, GwlExStyle);
    }

    private static bool IsLikelyGameForegroundWindow(IntPtr hwnd, string processName, Action<string>? log)
    {
        if (!IsWindowVisible(hwnd))
        {
            log?.Invoke($"Rejecting foreground PID because window is not visible. Name={processName}");
            return false;
        }

        if (IsShellWindowClass(hwnd, out var shellClassName))
        {
            log?.Invoke($"Rejecting foreground PID because the window is a Windows shell window. Name={processName} Class={shellClassName}");
            return false;
        }

        if (IsIconic(hwnd))
        {
            log?.Invoke($"Rejecting foreground PID because window is minimized. Name={processName}");
            return false;
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            log?.Invoke($"Rejecting foreground PID because window rect could not be read. Name={processName}");
            return false;
        }

        var width = Math.Max(0, rect.Right - rect.Left);
        var height = Math.Max(0, rect.Bottom - rect.Top);
        if (width < MinimumGameWindowWidth || height < MinimumGameWindowHeight)
        {
            log?.Invoke($"Rejecting foreground PID because window is too small for a likely game. Name={processName} Size={width}x{height}");
            return false;
        }

        return true;
    }


    /// <summary>
    /// Polls the foreground window PID. If it changes to a new game process,
    /// restarts the native FPS agent with the new foreground PID.
    /// Also checks if the config target process is still alive; if not, falls back to foreground detection.
    /// </summary>
    private static void TryUpdateForegroundGameTarget(
        NativeFpsAgentRunner nativeFpsAgent,
        Models.ConfigModel config,
        NativeFpsAgentState? nativeState,
        ref string activeFpsTargetSignature,
        ref bool nativeFpsStarted,
        ref int? currentForegroundGamePid,
        ref int? confirmedForegroundGamePid,
        ref bool hadConfirmedGameSession,
        ref DateTime? overrideExpiresAtUtc,
        ref int? foregroundOverridePid,
        ref DateTime? currentForegroundPidObservedAtUtc,
        ref int? rejectedForegroundPid,
        ref DateTime? rejectedForegroundPidCooldownUntilUtc,
        ref int? stickyChallengerPid,
        ref DateTime? stickyChallengerSinceUtc,
        int? parentPid,
        Action<string>? log)
    {
        if (confirmedForegroundGamePid.HasValue && !ProcessIsAlive(confirmedForegroundGamePid.Value))
        {
            log?.Invoke($"Confirmed game PID {confirmedForegroundGamePid.Value} exited. Clearing confirmation.");
            confirmedForegroundGamePid = null;
        }

        if (rejectedForegroundPidCooldownUntilUtc.HasValue &&
            DateTime.UtcNow >= rejectedForegroundPidCooldownUntilUtc.Value)
        {
            rejectedForegroundPid = null;
            rejectedForegroundPidCooldownUntilUtc = null;
        }

        var configTarget = config.FpsTarget;
        var configTargetIsAuthoritative = IsConfiguredTargetAuthoritative(configTarget, nativeState);
        if (configTargetIsAuthoritative)
        {
            var configTargetSignature = NativeFpsAgentRunner.CreateTargetSignature(configTarget);
            var configTargetChanged = !string.Equals(
                configTargetSignature,
                activeFpsTargetSignature,
                StringComparison.Ordinal);
            var nativeOwnsConfigTarget = nativeState != null &&
                                         nativeState.TargetPid > 0 &&
                                         MatchesConfigTargetProcess(configTarget!, nativeState.TargetPid);

            if (nativeOwnsConfigTarget)
            {
                if (currentForegroundGamePid != nativeState!.TargetPid)
                {
                    log?.Invoke($"Locked config target is active on PID {nativeState.TargetPid}. Suppressing foreground retarget.");
                }

                currentForegroundGamePid = nativeState.TargetPid;
                confirmedForegroundGamePid = nativeState.TargetPid;
                hadConfirmedGameSession = true;
                currentForegroundPidObservedAtUtc = null;
            }

            // Even when the config-target owns the native agent, still auto-register
            // any NEW foreground game that doesn't match the config-target. This covers
            // the case where PathOfExile.exe is the stale config-target from a previous
            // session, but the user starts COD from Battle.net — we want COD to show up
            // on the Game Resolution page.
            AttemptExternalGameRegistration(nativeState, log);

            if (nativeFpsStarted &&
                (nativeOwnsConfigTarget ||
                 (!configTargetChanged && (nativeState == null || nativeState.TargetPid == 0))))
            {
                return;
            }

            // When the native agent has locked onto a DIFFERENT process than the
            // stale config target (e.g. the user started COD externally), pass
            // that PID as the foreground game so the agent locks onto it for FPS.
            var externalForegroundPid = nativeState?.TargetPid > 0 ? nativeState.TargetPid : (int?)null;

            log?.Invoke($"Configured FPS target changed or does not own the native agent. Restarting in config-first mode. TargetExe={configTarget?.ExecutableName ?? "null"} TargetPath={configTarget?.ResolvedExecutablePath ?? "null"} RootPid={configTarget?.RootProcessId?.ToString() ?? "null"} RootStartFileTime={configTarget?.RootProcessStartFileTimeUtc?.ToString() ?? "null"} CurrentForegroundPid={currentForegroundGamePid?.ToString() ?? "null"} NativeTargetPid={nativeState?.TargetPid.ToString() ?? "null"} ExternalForegroundPid={externalForegroundPid?.ToString() ?? "null"}");
            activeFpsTargetSignature = configTargetSignature;
            currentForegroundGamePid = externalForegroundPid;
            confirmedForegroundGamePid = null;
            foregroundOverridePid = null;
            currentForegroundPidObservedAtUtc = null;
            nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Restart(parentPid, foregroundGamePid: externalForegroundPid);
            return;
        }

        // A wrong sticky target (a shell/Store process acquired through a stray
        // frame event) must never block the real game. Before honouring the
        // "current target is still owned" short-circuit below, re-evaluate the
        // foreground window: when a DIFFERENT valid game candidate stays stable
        // for the override delay, release the current target so the real game is
        // acquired on the retarget step. This is deliberately checked here and
        // NOT after the short-circuit, otherwise it would be unreachable while a
        // bad target is still alive — which is exactly how a locked Command
        // Palette process blocked COD from ever being detected.
        var stickyTargetConfirmed = confirmedForegroundGamePid.HasValue &&
                                    currentForegroundGamePid == confirmedForegroundGamePid &&
                                    ProcessIsAlive(confirmedForegroundGamePid.Value);
        if (stickyTargetConfirmed)
        {
            var observedChallengerPid = TryGetForegroundGamePid(log);
            if (observedChallengerPid.HasValue && observedChallengerPid.Value != currentForegroundGamePid!.Value)
            {
                if (stickyChallengerPid != observedChallengerPid.Value)
                {
                    stickyChallengerPid = observedChallengerPid.Value;
                    stickyChallengerSinceUtc = DateTime.UtcNow;
                }

                var challengerStableFor = stickyChallengerSinceUtc.HasValue
                    ? DateTime.UtcNow - stickyChallengerSinceUtc.Value
                    : TimeSpan.Zero;

                if (challengerStableFor >= StickyChallengerOverrideDelay)
                {
                    log?.Invoke(
                        $"Releasing sticky game target PID {currentForegroundGamePid.Value} after {challengerStableFor.TotalSeconds:F0}s: foreground PID {observedChallengerPid.Value} is a stable challenger. Retargeting.");
                    currentForegroundGamePid = null;
                    confirmedForegroundGamePid = null;
                    stickyChallengerPid = null;
                    stickyChallengerSinceUtc = null;
                    currentForegroundPidObservedAtUtc = null;
                    nativeFpsStarted = false;
                }
                else
                {
                    log?.Invoke(
                        $"Ignoring foreground PID {observedChallengerPid.Value} because sticky game target PID {currentForegroundGamePid.Value} is still alive ({challengerStableFor.TotalSeconds:F0}s/{StickyChallengerOverrideDelay.TotalSeconds:F0}s).");
                }
            }
            else
            {
                stickyChallengerPid = null;
                stickyChallengerSinceUtc = null;
            }
        }

        if (currentForegroundGamePid.HasValue &&
            ProcessIsAlive(currentForegroundGamePid.Value) &&
            IsCurrentTargetStillOwned(currentForegroundGamePid, confirmedForegroundGamePid, nativeState))
        {
            return;
        }

        if (currentForegroundGamePid.HasValue &&
            ProcessIsAlive(currentForegroundGamePid.Value) &&
            currentForegroundPidObservedAtUtc.HasValue &&
            DateTime.UtcNow - currentForegroundPidObservedAtUtc.Value >= NonGameProbeTimeout &&
            nativeState != null &&
            GameVramEvidence.IsMeasurablyNotGame(currentForegroundGamePid.Value, nativeState, DateTime.UtcNow, out var rejectionDetail))
        {
            rejectedForegroundPid = currentForegroundGamePid.Value;
            rejectedForegroundPidCooldownUntilUtc = DateTime.UtcNow.Add(NonGameCooldown);
            log?.Invoke(
                $"Rejecting foreground PID {currentForegroundGamePid.Value} as a non-game after the probe window: {rejectionDetail}. Cooldown until {rejectedForegroundPidCooldownUntilUtc.Value:HH:mm:ss}.");
            currentForegroundGamePid = null;
            currentForegroundPidObservedAtUtc = null;
            foregroundOverridePid = null;
            nativeFpsStarted = false;
        }
        else if (currentForegroundGamePid.HasValue &&
                 ProcessIsAlive(currentForegroundGamePid.Value) &&
                 currentForegroundPidObservedAtUtc.HasValue &&
                 DateTime.UtcNow - currentForegroundPidObservedAtUtc.Value >= NonGameProbeTimeout &&
                 nativeState == null)
        {
            log?.Invoke(
                $"Skipping non-game rejection for PID {currentForegroundGamePid.Value} because native FPS state was unavailable during probe timeout.");
        }

        var newForegroundPid = TryGetForegroundGamePid(log);

        // The visible-window fallback also runs as a challenger while a target WITHOUT
        // game evidence is held, so a running background game always gets the target
        // back on its own instead of requiring an IconGrid restart or an FPS-agent
        // reset.
        if (!newForegroundPid.HasValue &&
            (!currentForegroundGamePid.HasValue || !HasAcquisitionEvidence(nativeState)))
        {
            newForegroundPid = TryGetVisibleGamePidFallback(log);
            if (newForegroundPid.HasValue)
            {
                log?.Invoke($"Using visible-game fallback PID {newForegroundPid.Value} because no direct foreground game PID was available.");
            }
        }

        if (newForegroundPid.HasValue &&
            rejectedForegroundPid.HasValue &&
            rejectedForegroundPidCooldownUntilUtc.HasValue &&
            newForegroundPid.Value == rejectedForegroundPid.Value &&
            DateTime.UtcNow < rejectedForegroundPidCooldownUntilUtc.Value)
        {
            log?.Invoke(
                $"Ignoring foreground PID {newForegroundPid.Value} because it is on non-game cooldown until {rejectedForegroundPidCooldownUntilUtc.Value:HH:mm:ss}.");
            newForegroundPid = null;
        }

        if (!newForegroundPid.HasValue && currentForegroundGamePid.HasValue)
        {
            // Sticky-target: Foreground is no longer a game (e.g., user alt-tabbed or clicked overlay).
            // Do NOT restart — keep tracking the existing game PID.
            // Only restart if the game process actually exited.
            log?.Invoke($"Sticky-target: no game foreground detected. Holding current game PID {currentForegroundGamePid.Value}. NativeTargetPid={nativeState?.TargetPid.ToString() ?? "null"} EtwRunning={nativeState?.EtwRunning.ToString() ?? "null"}");
            if (!ProcessIsAlive(currentForegroundGamePid.Value))
            {
                log?.Invoke($"Current game PID {currentForegroundGamePid.Value} has exited. Clearing.");
                currentForegroundGamePid = null;
                confirmedForegroundGamePid = null;
                foregroundOverridePid = null;
                currentForegroundPidObservedAtUtc = null;
                nativeFpsStarted = false;
            }
            return;
        }

        if (newForegroundPid.HasValue && newForegroundPid != currentForegroundGamePid)
        {
            log?.Invoke($"Foreground game PID changed from {currentForegroundGamePid?.ToString() ?? "null"} to {newForegroundPid.Value}. Restarting native FPS agent.");
            currentForegroundGamePid = newForegroundPid;
            foregroundOverridePid = newForegroundPid;
            currentForegroundPidObservedAtUtc = DateTime.UtcNow;
            overrideExpiresAtUtc = DateTime.UtcNow.AddSeconds(30);
            log?.Invoke($"Foreground override activated for PID {newForegroundPid.Value} until {overrideExpiresAtUtc.Value:HH:mm:ss}.");

            // Do NOT auto-register here: at this point the native FPS agent has
            // not yet been restarted on this PID, so there is no ETW evidence.
            // Registration happens via AttemptExternalGameRegistration in the
            // snapshot loop once the agent confirms GPU present events — this is
            // the permanent anti-false-positive guard.

            // Check if this external game has a saved resolution — if so,
            // switch to it now so the player doesn't need to open the settings
            // page manually before every external launch.
            TryApplyExternalGameResolution(newForegroundPid.Value, log);

            nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Restart(parentPid, newForegroundPid);
        }
    }

    private static void UpdateConfirmedForegroundGamePid(
        NativeFpsAgentState? nativeState,
        ref int? currentForegroundGamePid,
        ref int? confirmedForegroundGamePid,
        ref bool hadConfirmedGameSession)
    {
        if (!currentForegroundGamePid.HasValue || nativeState == null)
        {
            return;
        }

        if (!IsStickyTargetConfirmed(currentForegroundGamePid, nativeState))
        {
            return;
        }

        confirmedForegroundGamePid = currentForegroundGamePid;
        hadConfirmedGameSession = true;
    }

    /// <summary>
    /// True when the native FPS agent reports USABLE frame-render evidence for a
    /// target — i.e. the process is genuinely rendering through a graphics API,
    /// not just producing the occasional system/kernel present event.
    ///
    /// A single DXGKRNL present event is deliberately NOT treated as a game signal.
    /// Non-rendering foreground processes (Windows Search's SearchHost, shell
    /// overlays, etc.) can occasionally emit one coarse kernel present event while
    /// producing zero usable frames. Confirming those as "games" let them become
    /// sticky targets that then blocked the real game from ever being acquired
    /// (the launcher stayed in-game on SearchHost.exe for hours, so the gaming
    /// overlay never appeared for the actual game when it started later).
    ///
    /// We confirm only on real, sustained render evidence:
    ///   - a usable FPS value, or
    ///   - at least two app-present (DXGI/D3D9) events in the rolling window, or
    ///   - at least two DXGKRNL present events (the native DxgKrnl fallback only
    ///     reports a frame rate once it has >=2 samples at >=20fps).
    /// This makes detection evidence-based and independent of any process-name
    /// blacklist.
    /// </summary>
    private static bool HasUsableFrameSignal(NativeFpsAgentState? nativeState)
    {
        // Delegated to the shared classifier so the "what counts as a game
        // signal" rule is defined in exactly one place.
        return GameProcessClassifier.IsGameEvidence(nativeState);
    }

    /// <summary>
    /// Evidence required to ACQUIRE/CONFIRM a target (as opposed to merely
    /// displaying a frame rate): dedicated VRAM (peak) or application-level
    /// DXGI/D3D9 presents. The rule itself lives in
    /// <see cref="GameVramEvidence"/>, so detection behavior is defined in exactly
    /// one place and needs no process-name or installation-path list.
    /// </summary>
    private static bool HasAcquisitionEvidence(NativeFpsAgentState? nativeState)
    {
        if (nativeState == null)
        {
            return false;
        }

        return GameVramEvidence.Evaluate(nativeState.TargetPid, nativeState, DateTime.UtcNow, out _) ==
               GameEvidenceVerdict.Game;
    }

    private static bool IsStickyTargetConfirmed(int? currentForegroundGamePid, NativeFpsAgentState? nativeState)
    {
        if (!currentForegroundGamePid.HasValue || nativeState == null)
        {
            return false;
        }

        // Never confirm our own process as the game target. Everything else is
        // decided by evidence (dedicated VRAM / application-level presents), never
        // by a process-name or installation-path list.
        if (GameProcessClassifier.IsSelfProcessName(nativeState.TargetProcessName))
        {
            return false;
        }

        if (nativeState.TargetPid != currentForegroundGamePid.Value)
        {
            return false;
        }

        return HasAcquisitionEvidence(nativeState);
    }

    private static bool IsCurrentTargetStillOwned(
        int? currentForegroundGamePid,
        int? confirmedForegroundGamePid,
        NativeFpsAgentState? nativeState)
    {
        if (!currentForegroundGamePid.HasValue || nativeState == null)
        {
            return false;
        }

        if (nativeState.TargetPid != currentForegroundGamePid.Value)
        {
            return false;
        }

        if (HasAcquisitionEvidence(nativeState))
        {
            return true;
        }

        // A previously-confirmed target that is merely paused (brief render stop)
        // stays owned as long as ETW is still running, so the overlay does not flap
        // during menu/cutscene dips.
        return confirmedForegroundGamePid.HasValue &&
               confirmedForegroundGamePid.Value == currentForegroundGamePid.Value &&
               nativeState.EtwRunning;
    }

    private static bool HasNativeGameSignal(NativeFpsAgentState? nativeState)
    {
        if (nativeState == null)
        {
            return false;
        }

        if (nativeState.TargetPid > 0)
        {
            return true;
        }

        return HasUsableFrameSignal(nativeState);
    }

    /// <summary>
    /// When a config-target is authoritative but the ACTUAL foreground window
    /// belongs to a DIFFERENT process (e.g. PathOfExile.exe config-target, but
    /// COD started from Battle.net), register the foreground process so
    /// GameResolutionPage can show it.
    /// </summary>
    private static void AttemptExternalGameRegistration(NativeFpsAgentState? nativeState, Action<string>? log)
    {
        // Prioritize the native agent's TargetPid — it's the process the agent
        // has actually locked onto (e.g. cod22-cod.exe after COD fully starts).
        // Fall back to foreground PID only when the agent has no target.
        if (nativeState?.TargetPid > 0)
        {
            TryAutoRegisterExternalGame(nativeState.TargetPid, nativeState, log);
        }
        else
        {
            var foregroundPid = TryGetForegroundProcessPid();
            if (foregroundPid.HasValue)
            {
                TryAutoRegisterExternalGame(foregroundPid.Value, nativeState, log);
            }
        }
    }

    /// <summary>
    /// Auto-registers an external game (started outside IconGrid) so
    /// GameResolutionPage can show it. Only called when foreground detection
    /// discovers a new game PID that is NOT already in the user's shortcut list.
    ///
    /// PERMANENT anti-false-positive guard: a process is only registered when
    /// the native FPS agent observed trustworthy game evidence for this PID AND
    /// the process is not a Windows system/shell program. DXGI/D3D9 application
    /// presents are strong evidence; the coarse DxgKrnl kernel fallback is weak
    /// (shell apps can emit a stray kernel present), so it is only accepted for
    /// processes that are not Windows system programs. This keeps taskmgr,
    /// TextInputHost, LockApp, SearchHost, Discord, WindowsTerminal etc. out of
    /// the external-games list while still registering real games.
    /// </summary>
    private static void TryAutoRegisterExternalGame(int pid, NativeFpsAgentState? nativeState, Action<string>? log)
    {
        if (nativeState == null || nativeState.TargetPid != pid)
            return;

        var hasTrustworthyEvidence = HasAcquisitionEvidence(nativeState);
        var isNonGameProcess = GameProcessClassifier.IsSelfProcessName(nativeState.TargetProcessName);
        if (!hasTrustworthyEvidence || isNonGameProcess)
        {
            log?.Invoke($"Skipping external game registration for PID {pid}: no trustworthy game evidence (anti-false-positive guard).");
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);

            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                // Process.MainModule is unavailable for UWP/GamePass/AppContainer
                // processes (e.g. cod22-cod.exe). Use the process name as a
                // fallback identifier.
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                // Fallback: use the process name as a synthetic path identifier
                // for UWP/GamePass games where MainModule is blocked.
                var processName = process.ProcessName;
                if (string.IsNullOrWhiteSpace(processName))
                    return;

                // Ensure .exe suffix for consistency with normal paths.
                if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processName += ".exe";

                path = processName;
            }

            if (ExternalGames.TryRegister(path))
            {
                log?.Invoke($"Auto-registered external game: {Path.GetFileName(path)} at {path}");
            }
        }
        catch
        {
            // Non-critical — external game registration is best-effort.
        }
    }

    /// <summary>
    /// Applies a saved GameResolution for an external game when it is
    /// first detected via foreground polling. Uses the same
    /// DisplayResolutionService pipeline as regular IconGrid shortcuts.
    /// </summary>
    private static void TryApplyExternalGameResolution(int pid, Action<string>? log)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            // Resolve the external game key the same way TryAutoRegisterExternalGame
            // stores it: use MainModule.FileName first, fall back to process name
            // for UWP/GamePass/AppContainer processes where MainModule is blocked.
            string? pathKey = null;
            try
            {
                pathKey = process.MainModule?.FileName;
            }
            catch
            {
                // MainModule unavailable (UWP/GamePass).
            }

            if (string.IsNullOrWhiteSpace(pathKey))
            {
                pathKey = process.ProcessName;
                if (string.IsNullOrWhiteSpace(pathKey))
                    return;

                // Match the .exe suffix convention used by TryAutoRegisterExternalGame
                // so the lookup key matches the stored entry.
                if (!pathKey.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    pathKey += ".exe";
            }

            var resolution = ExternalGames.GetResolution(pathKey);
            if (string.IsNullOrWhiteSpace(resolution))
                return;

            var parsed = Launcher.DisplayResolutionService.ParseResolution(resolution);
            if (parsed == null)
                return;

            _externalDisplayResolution ??= new Launcher.DisplayResolutionService();
            if (_externalDisplayResolution.TrySetResolution(parsed.Value.Width, parsed.Value.Height))
            {
                _externalDisplayResolution.AttachPendingResolution(pid);
                _externalDisplayResolution.WatchProcess(pid);
                log?.Invoke($"Applied external game resolution {resolution} for {Path.GetFileName(pathKey)} (PID={pid})");
            }
        }
        catch
        {
            // Non-critical — resolution switching is best-effort.
        }
    }

    private static int? TryGetCompatibleConfigForegroundPid(
        Models.FpsTargetConfig configTarget,
        Action<string>? log)
    {
        if (IsLikelyLauncherConfigTarget(configTarget))
        {
            return null;
        }

        var foregroundPid = TryGetForegroundGamePid(log);
        if (!foregroundPid.HasValue)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(foregroundPid.Value);
            var foregroundProcessName = process.ProcessName;
            var foregroundExecutableName = Path.GetFileNameWithoutExtension(configTarget.ExecutableName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(foregroundExecutableName) &&
                string.Equals(foregroundProcessName, foregroundExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                return foregroundPid.Value;
            }

            var mainModulePath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModulePath))
            {
                if (!string.IsNullOrWhiteSpace(configTarget.ResolvedExecutablePath) &&
                    string.Equals(
                        Path.GetFullPath(mainModulePath),
                        Path.GetFullPath(configTarget.ResolvedExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return foregroundPid.Value;
                }

                var processDirectory = Path.GetDirectoryName(mainModulePath);
                if (!string.IsNullOrWhiteSpace(processDirectory) &&
                    !string.IsNullOrWhiteSpace(configTarget.WorkingDirectory) &&
                    string.Equals(
                        Path.GetFullPath(processDirectory),
                        Path.GetFullPath(configTarget.WorkingDirectory),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return foregroundPid.Value;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool IsConfiguredTargetAlive(Models.FpsTargetConfig? configTarget)
    {
        return IsConfiguredTargetAlive(configTarget, nativeState: null);
    }

    private static bool IsConfiguredTargetAuthoritative(
        Models.FpsTargetConfig? configTarget,
        NativeFpsAgentState? nativeState)
    {
        if (configTarget == null || IsLikelyLauncherConfigTarget(configTarget))
        {
            return false;
        }

        // A config target WITHOUT any identity (no executable name, no path, no root
        // PID) matches every process in MatchesConfigTargetProcess. It therefore made
        // whatever the native agent had locked look like "the configured target" and
        // confirmed it without a single piece of evidence: a non-game target
        // (pythonw.exe) was held for hours, the non-game probe never ran
        // (currentForegroundPidObservedAtUtc was cleared) and the visible-window
        // fallback was skipped, so a running background game could never take the
        // target back without restarting IconGrid.
        if (!HasConfiguredTargetIdentity(configTarget))
        {
            return false;
        }

        if (nativeState != null &&
            nativeState.TargetPid > 0 &&
            MatchesConfigTargetProcess(configTarget, nativeState.TargetPid))
        {
            return true;
        }

        return DoesConfigRootProcessStillMatch(configTarget);
    }

    private static bool HasConfiguredTargetIdentity(Models.FpsTargetConfig? configTarget)
    {
        return configTarget != null &&
               !IsLikelyLauncherConfigTarget(configTarget) &&
               (!string.IsNullOrWhiteSpace(configTarget.ExecutableName) ||
                !string.IsNullOrWhiteSpace(configTarget.ResolvedExecutablePath) ||
                configTarget.RootProcessId is > 0);
    }

    private static bool IsConfiguredTargetAlive(
        Models.FpsTargetConfig? configTarget,
        NativeFpsAgentState? nativeState)
    {
        if (configTarget == null)
        {
            return false;
        }

        if (nativeState != null)
        {
            if (nativeState.TargetPid > 0 &&
                MatchesConfigTargetProcess(configTarget, nativeState.TargetPid))
            {
                return true;
            }

            if (nativeState.TargetPid > 0 &&
                MatchesConfigTargetProcess(configTarget, nativeState.TargetPid) &&
                (nativeState.MatchedDxgiEventCount > 0 ||
                 nativeState.MatchedD3D9EventCount > 0 ||
                 nativeState.MatchedDxgKrnlEventCount > 0))
            {
                return true;
            }
        }

        if (IsLikelyLauncherConfigTarget(configTarget))
        {
            return false;
        }

        if (DoesConfigRootProcessStillMatch(configTarget))
        {
            return true;
        }

        if (configTarget.LaunchCapturedFileTimeUtc.HasValue)
        {
            try
            {
                var launchedAtUtc = DateTime.FromFileTimeUtc(configTarget.LaunchCapturedFileTimeUtc.Value);
                if (DateTime.UtcNow - launchedAtUtc <= ConfigTargetLaunchGracePeriod)
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static bool DoesConfigRootProcessStillMatch(Models.FpsTargetConfig configTarget)
    {
        if (!configTarget.RootProcessId.HasValue || configTarget.RootProcessId.Value <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(configTarget.RootProcessId.Value);
            if (process.HasExited)
            {
                return false;
            }

            if (configTarget.RootProcessStartFileTimeUtc.HasValue &&
                configTarget.RootProcessStartFileTimeUtc.Value > 0)
            {
                var actualStartFileTimeUtc = process.StartTime.ToUniversalTime().ToFileTimeUtc();
                if (actualStartFileTimeUtc != configTarget.RootProcessStartFileTimeUtc.Value)
                {
                    return false;
                }
            }

            return MatchesConfigTargetProcess(configTarget, process);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesConfigTargetProcess(Models.FpsTargetConfig configTarget, int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return MatchesConfigTargetProcess(configTarget, process);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesConfigTargetProcess(Models.FpsTargetConfig configTarget, Process process)
    {
        try
        {
            // Never match on an identity-less config target: with no executable name
            // and no path to compare, this method reported a match for ANY process,
            // which confirmed arbitrary (non-game) targets as "the configured game".
            if (!HasConfiguredTargetIdentity(configTarget))
            {
                return false;
            }

            var expectedProcessName = Path.GetFileNameWithoutExtension(configTarget.ExecutableName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(expectedProcessName) &&
                !string.Equals(process.ProcessName, expectedProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var mainModulePath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(configTarget.ResolvedExecutablePath))
            {
                if (string.IsNullOrWhiteSpace(mainModulePath))
                {
                    return false;
                }

                if (!string.Equals(
                        Path.GetFullPath(mainModulePath),
                        Path.GetFullPath(configTarget.ResolvedExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(configTarget.WorkingDirectory))
            {
                if (string.IsNullOrWhiteSpace(mainModulePath))
                {
                    return false;
                }

                var processDirectory = Path.GetDirectoryName(mainModulePath);
                if (string.IsNullOrWhiteSpace(processDirectory) ||
                    !string.Equals(
                        Path.GetFullPath(processDirectory),
                        Path.GetFullPath(configTarget.WorkingDirectory),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyLauncherConfigTarget(Models.FpsTargetConfig? configTarget)
    {
        if (configTarget == null)
        {
            return false;
        }

        return IsLikelyLauncherName(configTarget.DisplayName) ||
               IsLikelyLauncherName(configTarget.ExecutableName) ||
               IsLikelyLauncherName(configTarget.LauncherPath) ||
               IsLikelyLauncherName(configTarget.ResolvedExecutablePath);
    }

    private static bool IsLikelyLauncherName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Path.GetFileNameWithoutExtension(value).Trim();
        return normalized.Equals("Battle.net", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Battle.net Launcher", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("upc", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("EADesktop", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("launcher", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryClearStaleFpsTargetConfig(
        Models.ConfigModel config,
        ref string activeFpsTargetSignature,
        Action<string>? log)
    {
        try
        {
            config.FpsTarget = new Models.FpsTargetConfig();
            var configManager = new Helpers.Settings.ConfigManager();
            configManager.SaveConfig(config);
            activeFpsTargetSignature = string.Empty;
            log?.Invoke("Cleared stale FpsTarget from config.json.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to clear stale FpsTarget from config.json: {ex.Message}");
        }
    }

    private static void TryClearStaleFpsTargetConfig(
        Models.ConfigModel config,
        Action<string>? log)
    {
        var dummySignature = string.Empty;
        TryClearStaleFpsTargetConfig(config, ref dummySignature, log);
    }

    private static void TryClearStaleFpsTargetRuntimeMetadata(
        Models.ConfigModel config,
        Action<string>? log)
    {
        var fpsTarget = config.FpsTarget;
        if (fpsTarget == null || IsLikelyLauncherConfigTarget(fpsTarget))
        {
            return;
        }

        var hasRuntimeMetadata = fpsTarget.RootProcessId.HasValue ||
                                 fpsTarget.RootProcessStartFileTimeUtc.HasValue ||
                                 fpsTarget.LaunchCapturedFileTimeUtc.HasValue;
        if (!hasRuntimeMetadata)
        {
            return;
        }

        var rootStillMatches = DoesConfigRootProcessStillMatch(fpsTarget);
        var isWithinLaunchGrace = false;
        if (fpsTarget.LaunchCapturedFileTimeUtc.HasValue)
        {
            try
            {
                var launchedAtUtc = DateTime.FromFileTimeUtc(fpsTarget.LaunchCapturedFileTimeUtc.Value);
                isWithinLaunchGrace = DateTime.UtcNow - launchedAtUtc <= ConfigTargetLaunchGracePeriod;
            }
            catch
            {
            }
        }

        if (rootStillMatches || isWithinLaunchGrace)
        {
            return;
        }

        // The registered root process no longer matches. If ANOTHER process with
        // the same executable name is still running (re-spawned game process,
        // launcher handoff), keep the target identity but drop the stale runtime
        // metadata so the FPS agent can re-lock by executable name.
        if (AnyProcessMatchesConfigTarget(fpsTarget))
        {
            fpsTarget.RootProcessId = null;
            fpsTarget.RootProcessStartFileTimeUtc = null;
            fpsTarget.LaunchCapturedFileTimeUtc = null;

            try
            {
                var configManager = new Helpers.Settings.ConfigManager();
                configManager.SaveConfig(config);
                log?.Invoke("Cleared stale FpsTarget runtime metadata from config.json.");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Failed to clear stale FpsTarget runtime metadata from config.json: {ex.Message}");
            }

            return;
        }

        // No process matches the configured executable — the game has fully
        // exited. Clear the ENTIRE FpsTarget so the next startup goes into
        // foreground-first mode instead of keeping a stale authoritative target.
        // A stale target caused the repeated "Configured FPS target changed or
        // does not own the native agent" restart loop (every ~10s) and no FPS
        // in the gaming overlay for Path of Exile after the game was closed.
        TryClearStaleFpsTargetConfig(config, log);
    }

    private static bool AnyProcessMatchesConfigTarget(Models.FpsTargetConfig configTarget)
    {
        var expectedProcessName = Path.GetFileNameWithoutExtension(configTarget.ExecutableName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(expectedProcessName))
        {
            return false;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName(expectedProcessName))
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    // A live process with the configured executable name means the
                    // game session is still active. Avoid strict module-path matching
                    // here: anti-cheat protected games (Path of Exile, COD, Division 2)
                    // deny MainModule access (Win32Exception) even to elevated callers.
                    return true;
                }
                catch
                {
                    // Access denied on process properties still means the process
                    // exists and is running — treat as a match (same rule as
                    // ProcessIsAlive).
                    return true;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // Process enumeration failed; treat as no match so stale targets are
            // cleared. If the game is actually running, the next launch re-creates
            // the FpsTarget.
        }

        return false;
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
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied: anti-cheat protected processes (EAC/BattlEye) deny
            // PROCESS_QUERY_INFORMATION even to elevated callers. Access denied is
            // ONLY raised for an existing process with restricted handles — a
            // non-existent PID throws ArgumentException instead. Treat as alive,
            // otherwise the agent thinks the game exited and closes the overlay.
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    // A PID alone is not a reliable liveness check: Windows reuses PIDs, so an
    // orphaned agent could find an unrelated process with "our" parent PID and
    // never exit — leaving an elevated agent and a stale FPS target alive after
    // the launcher was gone. Verify the parent's start time as well.
    private static bool ParentIsAlive(int? parentPid, long? parentStartFileTimeUtc)
    {
        if (!parentPid.HasValue)
            return true;
        try
        {
            using var process = Process.GetProcessById(parentPid.Value);
            if (process.HasExited)
            {
                return false;
            }

            if (parentStartFileTimeUtc is > 0)
            {
                try
                {
                    var actualStartFileTimeUtc = process.StartTime.ToUniversalTime().ToFileTimeUtc();
                    // Tolerate sub-second rounding; PID reuse implies a very
                    // different start time.
                    if (Math.Abs(actualStartFileTimeUtc - parentStartFileTimeUtc.Value) > 20_000_000L)
                    {
                        return false;
                    }
                }
                catch
                {
                    // Start time unavailable (access denied): fall back to PID-only.
                }
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Same anti-cheat protection as ProcessIsAlive: access denied means
            // the parent process exists but cannot be queried.
            return true;
        }
        catch (ArgumentException)
        {
            return false;
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
            catch (UnauthorizedAccessException)
            {
                if (attempt < AtomicWriteRetryCount - 1)
                    Thread.Sleep(AtomicWriteRetryDelayMs);
            }
            catch (IOException)
            {
                if (attempt < AtomicWriteRetryCount - 1)
                    Thread.Sleep(AtomicWriteRetryDelayMs);
            }
        }

        // Best-effort final attempt — native agent may be writing simultaneously.
        // Never throw — failing to write state files must not crash the monitor loop.
        try { File.Move(tempPath, statePath, overwrite: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
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