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
    private static readonly TimeSpan ConfigTargetLaunchGracePeriod = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NonGameProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NonGameCooldown = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(5);
    private static readonly string[] IgnoredForegroundProcesses =
    {
        "explorer",
        "IconGrid",
        "ApplicationFrameHost",
        "SearchApp",
        "StartMenuExperienceHost",
        "Battle.net",
        "steam",
        "steamwebhelper",
        "upc",
        "EADesktop",
        "EpicGamesLauncher",
        "launcher",
        "Code",
        "brave",
        "chrome",
        "msedge",
        "firefox"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExNoActivate = 0x08000000L;

    private sealed record FpsNormalizationResult(double? EffectiveFps, bool EmulatorOvercount);
    private sealed record VisibleWindowCandidate(int Pid, string ProcessName, long Area, DateTime? StartedAtUtc, int Score);
    private sealed record FpsNormalizationDebugState(
        int TargetPid,
        string TargetName,
        bool IsForegroundWindow,
        double RawFps,
        double LastStableFps,
        double LastForegroundTrustedCodFps,
        int DxgiCount,
        int D3D9Count,
        int DxgKrnlCount,
        bool HasForegroundTrustedCodAnchor);

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
                }
            }

            var nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Start(parentPid, initialForegroundPid);
            log?.Invoke($"Hardware monitor agent started. NativeFpsStarted={nativeFpsStarted} ForegroundPid={initialForegroundPid?.ToString() ?? "null"}");

            var nativeFpsLoggedSummary = string.Empty;
            DateTime? parentMissingSinceUtc = null;
            var foregroundPollCounter = 0;
            var foregroundOverridePid = default(int?);
            var overrideExpiresAtUtc = default(DateTime?);
            var currentForegroundPidObservedAtUtc = default(DateTime?);
            var rejectedForegroundPid = default(int?);
            var rejectedForegroundPidCooldownUntilUtc = default(DateTime?);
            var lastStableFps = 0.0;
            var lastStableFpsSetAtUtc = DateTime.MinValue;
            var lastForegroundTrustedCodFps = 0.0;
            var lastForegroundTrustedCodFpsSetAtUtc = DateTime.MinValue;
            var lastTargetWasForegroundWindow = default(bool?);
            var lastTargetBecameForegroundAtUtc = DateTime.MinValue;
            var lastPrimarySignalStrength = 0;
            var lastSignalTargetPid = 0;

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
                TryClearStaleFpsTargetRuntimeMetadata(latestConfig, log);
                var nativeState = nativeFpsStarted ? nativeFpsAgent.ReadState() : null;

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
                    lastStableFps = 0.0;
                    lastStableFpsSetAtUtc = DateTime.MinValue;
                    log?.Invoke("Target regained foreground; resetting FPS spike baseline.");
                }
                lastTargetWasForegroundWindow = targetIsForegroundWindow;

                var normalization = NormalizeNativeFps(
                    nativeFpsValue,
                    nativeState,
                    targetIsForegroundWindow,
                    ref lastStableFps,
                    ref lastStableFpsSetAtUtc,
                    ref lastForegroundTrustedCodFps,
                    ref lastForegroundTrustedCodFpsSetAtUtc,
                    ref lastPrimarySignalStrength,
                    ref lastSignalTargetPid,
                    log);
                var effectiveFps = normalization.EffectiveFps;

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
                    var liveForegroundProcessPid = TryGetForegroundProcessPid();
                    var liveTargetIsForegroundWindow = liveNativeState?.TargetPid > 0 &&
                                                       liveForegroundProcessPid.HasValue &&
                                                       liveNativeState.TargetPid == liveForegroundProcessPid.Value;
                    if (liveTargetIsForegroundWindow && lastTargetWasForegroundWindow != true)
                    {
                        lastTargetBecameForegroundAtUtc = DateTime.UtcNow;
                        lastStableFps = 0.0;
                        lastStableFpsSetAtUtc = DateTime.MinValue;
                    }
                    lastTargetWasForegroundWindow = liveTargetIsForegroundWindow;

                    var liveNormalization = NormalizeNativeFps(
                        liveNativeFpsValue,
                        liveNativeState,
                        liveTargetIsForegroundWindow,
                        ref lastStableFps,
                        ref lastStableFpsSetAtUtc,
                        ref lastForegroundTrustedCodFps,
                        ref lastForegroundTrustedCodFpsSetAtUtc,
                        ref lastPrimarySignalStrength,
                        ref lastSignalTargetPid,
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
            var foreground = TryGetForegroundWindowProcess(log);
            if (!foreground.HasValue)
            {
                return null;
            }

            var (foregroundHwnd, pid) = foreground.Value;

            string? processName = null;

            // Check if the process is one we should ignore (IconGrid itself, Explorer, etc.)
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

                if (!IsLikelyGameForegroundWindow(foregroundHwnd, processName, log))
                {
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

    private static int? TryGetVisibleGamePidFallback(Action<string>? log)
    {
        try
        {
            var bestCandidate = default(VisibleWindowCandidate);
            var nowUtc = DateTime.UtcNow;

            EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(hwnd, out var candidatePid);
                    if (candidatePid == 0)
                    {
                        return true;
                    }

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

                    if (!IsLikelyGameForegroundWindow(hwnd, processName, log: null))
                    {
                        return true;
                    }

                    if (!GetWindowRect(hwnd, out var rect))
                    {
                        return true;
                    }

                    var width = Math.Max(0, rect.Right - rect.Left);
                    var height = Math.Max(0, rect.Bottom - rect.Top);
                    var area = (long)width * height;
                    var startedAtUtc = TryGetProcessStartTimeUtc(process);
                    var score = ScoreVisibleWindowCandidate(area, startedAtUtc, nowUtc);
                    if (score <= 0)
                    {
                        return true;
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
                catch
                {
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

            // Large fallback windows that are both layered and topmost are usually overlays,
            // not the actual game surface we want to track behind IconGrid.
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

    private static FpsNormalizationResult NormalizeNativeFps(
        double? nativeFpsValue,
        NativeFpsAgentState? nativeState,
        bool targetIsForegroundWindow,
        ref double lastStableFps,
        ref DateTime lastStableFpsSetAtUtc,
        ref double lastForegroundTrustedCodFps,
        ref DateTime lastForegroundTrustedCodFpsSetAtUtc,
        ref int lastPrimarySignalStrength,
        ref int lastSignalTargetPid,
        Action<string>? log)
    {
        var now = DateTime.UtcNow;
        var isCodTarget = nativeState?.TargetProcessName?.IndexOf("cod", StringComparison.OrdinalIgnoreCase) >= 0;
        var targetPid = nativeState?.TargetPid ?? 0;
        if (targetPid > 0 && targetPid != lastSignalTargetPid)
        {
            log?.Invoke($"FPS target PID changed from {lastSignalTargetPid} to {targetPid}; resetting normalization state.");
            lastStableFps = 0.0;
            lastStableFpsSetAtUtc = DateTime.MinValue;
            lastForegroundTrustedCodFps = 0.0;
            lastForegroundTrustedCodFpsSetAtUtc = DateTime.MinValue;
            lastPrimarySignalStrength = 0;
            lastSignalTargetPid = targetPid;
        }

        var hasNativeFps = nativeFpsValue.HasValue && nativeFpsValue.Value > 0;
        if (!hasNativeFps)
        {
            var hasResidualCodEtwSignal = nativeState != null &&
                                          nativeState.TargetPid > 0 &&
                                          nativeState.EtwRunning &&
                                          nativeState.EtwEventsReceived &&
                                          (nativeState.MatchedDxgiEventCount > 0 ||
                                           nativeState.MatchedD3D9EventCount > 0 ||
                                           nativeState.MatchedDxgKrnlEventCount > 0);
            if (isCodTarget &&
                !targetIsForegroundWindow &&
                hasResidualCodEtwSignal &&
                lastStableFps > 35.0 &&
                (now - lastStableFpsSetAtUtc).TotalSeconds < 20.0)
            {
                log?.Invoke(
                    $"Holding COD background FPS during ambiguous ETW sample: holdFps={lastStableFps:F0} DXGI={nativeState!.MatchedDxgiEventCount} D3D9={nativeState.MatchedD3D9EventCount} DXGKRNL={nativeState.MatchedDxgKrnlEventCount}");
                return new FpsNormalizationResult(lastStableFps, false);
            }

            return new FpsNormalizationResult(null, false);
        }

        var emulatorOvercount = nativeFpsValue!.Value > 120.0 &&
                                nativeState != null &&
                                nativeState.MatchedDxgiEventCount == 0 &&
                                nativeState.MatchedDxgKrnlEventCount > 0;
        if (emulatorOvercount)
        {
            log?.Invoke($"Emulator overcount detected: FPS={nativeFpsValue.Value:F0} DXGI=0 DXGKRNL={nativeState!.MatchedDxgKrnlEventCount}");
            lastStableFps = 60.0;
            lastStableFpsSetAtUtc = DateTime.UtcNow;
            log?.Invoke("Emulator cap applied: FPS capped to 60");
            return new FpsNormalizationResult(60.0, true);
        }

        var rawFps = nativeFpsValue.Value;
        var isSpike = false;
        var primarySignalStrength = Math.Max(nativeState?.MatchedDxgiEventCount ?? 0, nativeState?.MatchedD3D9EventCount ?? 0);
        var sameSignalTarget = targetPid > 0 && targetPid == lastSignalTargetPid;
        var signalUpgraded = sameSignalTarget && primarySignalStrength > lastPrimarySignalStrength;
        var debugState = new FpsNormalizationDebugState(
            targetPid,
            nativeState?.TargetProcessName ?? string.Empty,
            targetIsForegroundWindow,
            rawFps,
            lastStableFps,
            lastForegroundTrustedCodFps,
            nativeState?.MatchedDxgiEventCount ?? 0,
            nativeState?.MatchedD3D9EventCount ?? 0,
            nativeState?.MatchedDxgKrnlEventCount ?? 0,
            lastForegroundTrustedCodFps > 35.0 &&
            (now - lastForegroundTrustedCodFpsSetAtUtc).TotalSeconds < 120.0);
        var hasCodStyleDxgiOnlySignal = nativeState != null &&
                                        nativeState.MatchedD3D9EventCount == 0 &&
                                        nativeState.MatchedDxgiEventCount >= 4 &&
                                        nativeState.MatchedDxgKrnlEventCount >= 2;
        if (isCodTarget &&
            targetIsForegroundWindow &&
            rawFps >= 35.0 &&
            rawFps <= 90.0)
        {
            lastForegroundTrustedCodFps = rawFps;
            lastForegroundTrustedCodFpsSetAtUtc = now;
        }

        if (nativeState != null &&
            rawFps >= 80.0 &&
            hasCodStyleDxgiOnlySignal)
        {
            var providerRatio = nativeState.MatchedDxgKrnlEventCount > 0
                ? (double)nativeState.MatchedDxgiEventCount / nativeState.MatchedDxgKrnlEventCount
                : 0.0;
            var minimumCorrectionRatio = targetIsForegroundWindow ? 1.35 : 1.15;
            if (providerRatio >= minimumCorrectionRatio && providerRatio <= 2.4)
            {
                var correctedFps = rawFps / providerRatio;
                log?.Invoke(
                    $"Duplicate present correction applied: rawFps={rawFps:F0} correctedFps={correctedFps:F0} Ratio={providerRatio:F2} Foreground={targetIsForegroundWindow} DXGI={nativeState.MatchedDxgiEventCount} DXGKRNL={nativeState.MatchedDxgKrnlEventCount}");

                rawFps = correctedFps;
                if (!isCodTarget || targetIsForegroundWindow)
                {
                    lastStableFps = correctedFps;
                    lastStableFpsSetAtUtc = now;
                    return new FpsNormalizationResult(correctedFps, false);
                }

                log?.Invoke("Keeping COD background guard active after duplicate present correction.");
            }
        }

        if (!isSpike &&
            !targetIsForegroundWindow &&
            isCodTarget &&
            lastStableFps > 45.0 &&
            (now - lastStableFpsSetAtUtc).TotalSeconds < 30.0 &&
            rawFps > Math.Max(lastStableFps + 8.0, lastStableFps * 1.12))
        {
            var backgroundLimitedFps = Math.Min(rawFps, lastStableFps + 4.0);
            log?.Invoke($"COD background growth limited: rawFps={rawFps:F0} limitedFps={backgroundLimitedFps:F0} lastStable={lastStableFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
            rawFps = backgroundLimitedFps;
        }

        if (!isSpike &&
            !targetIsForegroundWindow &&
            isCodTarget &&
            lastStableFps > 35.0 &&
            (now - lastStableFpsSetAtUtc).TotalSeconds < 30.0)
        {
            var conservativeBackgroundCeiling = lastStableFps + 2.0;
            if (rawFps > conservativeBackgroundCeiling)
            {
                log?.Invoke(
                    $"COD background sticky hold applied: rawFps={rawFps:F0} heldFps={conservativeBackgroundCeiling:F0} lastStable={lastStableFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
                rawFps = conservativeBackgroundCeiling;
            }
        }

        if (!isSpike &&
            !targetIsForegroundWindow &&
            isCodTarget &&
            lastForegroundTrustedCodFps > 35.0 &&
            (now - lastForegroundTrustedCodFpsSetAtUtc).TotalSeconds < 120.0)
        {
            var codBackgroundCeiling = lastForegroundTrustedCodFps + 2.0;

            if (rawFps > codBackgroundCeiling)
            {
                log?.Invoke(
                    $"COD background foreground-anchor ceiling applied: rawFps={rawFps:F0} ceilingFps={codBackgroundCeiling:F0} trustedForeground={lastForegroundTrustedCodFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow}");
                rawFps = codBackgroundCeiling;
            }
        }

        if (rawFps > 1000.0)
        {
            isSpike = true;
            log?.Invoke($"Spike filtered (>1000): rawFps={rawFps:F0} lastStable={lastStableFps:F0}");
        }
        else if (lastStableFps > 10.0 && rawFps > lastStableFps * 1.5)
        {
            if ((now - lastStableFpsSetAtUtc).TotalSeconds < 6.0)
            {
                if (signalUpgraded)
                {
                    log?.Invoke(
                        $"Spike bypassed due to stronger primary signal: rawFps={rawFps:F0} lastStable={lastStableFps:F0} PrevSignal={lastPrimarySignalStrength} NewSignal={primarySignalStrength} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow}");
                }
                else
                {
                    isSpike = true;
                    log?.Invoke($"Spike filtered (jump): rawFps={rawFps:F0} lastStable={lastStableFps:F0} TargetPid={debugState.TargetPid} Target={debugState.TargetName} Foreground={debugState.IsForegroundWindow} DXGI={debugState.DxgiCount} D3D9={debugState.D3D9Count} DXGKRNL={debugState.DxgKrnlCount} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
                }
            }
        }

        if (targetPid > 0)
        {
            lastSignalTargetPid = targetPid;
            lastPrimarySignalStrength = primarySignalStrength;
        }

        if (isSpike)
        {
            return new FpsNormalizationResult(lastStableFps > 0.0 ? lastStableFps : null, false);
        }

        if (lastStableFps > 0.0)
        {
            const double emaAlpha = 0.3;
            if (isCodTarget && !targetIsForegroundWindow && rawFps > lastStableFps)
            {
                // COD can keep producing background presents; do not let background samples
                // ratchet the stable reference upward while the game is unfocused.
                lastStableFps = (lastStableFps * (1.0 - emaAlpha)) + (lastStableFps * emaAlpha);
            }
            else
            {
                lastStableFps = (rawFps * emaAlpha) + (lastStableFps * (1.0 - emaAlpha));
            }
        }
        else
        {
            lastStableFps = rawFps;
        }

        lastStableFpsSetAtUtc = now;
        return new FpsNormalizationResult(rawFps, false);
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

            if (nativeFpsStarted &&
                (nativeOwnsConfigTarget ||
                 (!configTargetChanged && (nativeState == null || nativeState.TargetPid == 0))))
            {
                return;
            }

            log?.Invoke($"Configured FPS target changed or does not own the native agent. Restarting in config-first mode. TargetExe={configTarget?.ExecutableName ?? "null"} TargetPath={configTarget?.ResolvedExecutablePath ?? "null"} RootPid={configTarget?.RootProcessId?.ToString() ?? "null"} RootStartFileTime={configTarget?.RootProcessStartFileTimeUtc?.ToString() ?? "null"} CurrentForegroundPid={currentForegroundGamePid?.ToString() ?? "null"} NativeTargetPid={nativeState?.TargetPid.ToString() ?? "null"}");
            activeFpsTargetSignature = configTargetSignature;
            currentForegroundGamePid = null;
            confirmedForegroundGamePid = null;
            foregroundOverridePid = null;
            currentForegroundPidObservedAtUtc = null;
            nativeFpsStarted = nativeFpsAgent.IsAvailable && nativeFpsAgent.Restart(parentPid, foregroundGamePid: null);
            return;
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
            !HasAnyGameSignal(currentForegroundGamePid, nativeState))
        {
            rejectedForegroundPid = currentForegroundGamePid.Value;
            rejectedForegroundPidCooldownUntilUtc = DateTime.UtcNow.Add(NonGameCooldown);
            log?.Invoke(
                $"Rejecting foreground PID {currentForegroundGamePid.Value} as a non-game after ETW probe timeout. Cooldown until {rejectedForegroundPidCooldownUntilUtc.Value:HH:mm:ss}.");
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

        var stickyTargetConfirmed = confirmedForegroundGamePid.HasValue &&
                                    currentForegroundGamePid == confirmedForegroundGamePid &&
                                    ProcessIsAlive(confirmedForegroundGamePid.Value);
        if (currentForegroundGamePid.HasValue && ProcessIsAlive(currentForegroundGamePid.Value))
        {
            var observedForegroundPid = TryGetForegroundGamePid(log);
            if (stickyTargetConfirmed && observedForegroundPid.HasValue && observedForegroundPid.Value != currentForegroundGamePid.Value)
            {
                log?.Invoke(
                    $"Ignoring foreground PID {observedForegroundPid.Value} because sticky game target PID {currentForegroundGamePid.Value} is still alive.");
                return;
            }
        }

        var newForegroundPid = TryGetForegroundGamePid(log);
        if (!newForegroundPid.HasValue && !currentForegroundGamePid.HasValue)
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
            // Foreground is no longer a game (e.g., user alt-tabbed or clicked overlay).
            // Do NOT restart — keep tracking the existing game PID.
            // Only restart if the game process actually exited.
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

    private static bool IsStickyTargetConfirmed(int? currentForegroundGamePid, NativeFpsAgentState? nativeState)
    {
        if (!currentForegroundGamePid.HasValue || nativeState == null)
        {
            return false;
        }

        if (nativeState.TargetPid != currentForegroundGamePid.Value)
        {
            return false;
        }

        if (nativeState.FpsValue.HasValue && nativeState.FpsValue.Value > 0)
        {
            return true;
        }

        return nativeState.MatchedDxgiEventCount > 0 ||
               nativeState.MatchedD3D9EventCount > 0 ||
               nativeState.MatchedDxgKrnlEventCount > 0;
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

        if (nativeState.FpsValue.HasValue && nativeState.FpsValue.Value > 0)
        {
            return true;
        }

        if (nativeState.MatchedDxgiEventCount > 0 ||
            nativeState.MatchedD3D9EventCount > 0 ||
            nativeState.MatchedDxgKrnlEventCount > 0)
        {
            return true;
        }

        return confirmedForegroundGamePid.HasValue &&
               confirmedForegroundGamePid.Value == currentForegroundGamePid.Value &&
               nativeState.EtwRunning;
    }

    private static bool HasAnyGameSignal(int? currentForegroundGamePid, NativeFpsAgentState? nativeState)
    {
        if (!currentForegroundGamePid.HasValue || nativeState == null)
        {
            return false;
        }

        if (nativeState.TargetPid != currentForegroundGamePid.Value)
        {
            return false;
        }

        if (nativeState.FpsValue.HasValue && nativeState.FpsValue.Value > 0)
        {
            return true;
        }

        return nativeState.MatchedDxgiEventCount > 0 ||
               nativeState.MatchedD3D9EventCount > 0 ||
               nativeState.MatchedDxgKrnlEventCount > 0;
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

        if (nativeState.FpsValue.HasValue && nativeState.FpsValue.Value > 0)
        {
            return true;
        }

        return nativeState.MatchedDxgiEventCount > 0 ||
               nativeState.MatchedD3D9EventCount > 0 ||
               nativeState.MatchedDxgKrnlEventCount > 0;
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
