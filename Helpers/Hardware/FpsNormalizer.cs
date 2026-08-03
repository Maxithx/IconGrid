using System;

namespace IconGrid.Helpers.Hardware
{
    public sealed record FpsNormalizationResult(double? EffectiveFps, bool EmulatorOvercount);

    /// <summary>
    /// Owns FPS normalization state (last stable FPS, COD anchors, signal tracking) and the
    /// normalization logic that previously lived in HardwareMonitorAgent.NormalizeNativeFps.
    /// </summary>
    public sealed class FpsNormalizerState
    {
        public double LastStableFps { get; private set; }
        public DateTime LastStableFpsSetAtUtc { get; private set; } = DateTime.MinValue;
        public double LastForegroundTrustedCodFps { get; private set; }
        public DateTime LastForegroundTrustedCodFpsSetAtUtc { get; private set; } = DateTime.MinValue;
        public int LastPrimarySignalStrength { get; private set; }
        public int LastSignalTargetPid { get; private set; }

        public void ResetBaseline()
        {
            LastStableFps = 0.0;
            LastStableFpsSetAtUtc = DateTime.MinValue;
        }

        public FpsNormalizationResult Normalize(
            double? nativeFpsValue,
            NativeFpsAgentState? nativeState,
            bool targetIsForegroundWindow,
            Action<string>? log)
        {
            var now = DateTime.UtcNow;
            var isCodTarget = nativeState?.TargetProcessName?.IndexOf("cod", StringComparison.OrdinalIgnoreCase) >= 0;
            var targetPid = nativeState?.TargetPid ?? 0;
            if (targetPid > 0 && targetPid != LastSignalTargetPid)
            {
                log?.Invoke($"FPS target PID changed from {LastSignalTargetPid} to {targetPid}; resetting normalization state.");
                LastStableFps = 0.0;
                LastStableFpsSetAtUtc = DateTime.MinValue;
                LastForegroundTrustedCodFps = 0.0;
                LastForegroundTrustedCodFpsSetAtUtc = DateTime.MinValue;
                LastPrimarySignalStrength = 0;
                LastSignalTargetPid = targetPid;
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
                    LastStableFps > 35.0 &&
                    (now - LastStableFpsSetAtUtc).TotalSeconds < 20.0)
                {
                    log?.Invoke(
                        $"Holding COD background FPS during ambiguous ETW sample: holdFps={LastStableFps:F0} DXGI={nativeState!.MatchedDxgiEventCount} D3D9={nativeState.MatchedD3D9EventCount} DXGKRNL={nativeState.MatchedDxgKrnlEventCount}");
                    return new FpsNormalizationResult(LastStableFps, false);
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
                LastStableFps = 60.0;
                LastStableFpsSetAtUtc = DateTime.UtcNow;
                log?.Invoke("Emulator cap applied: FPS capped to 60");
                return new FpsNormalizationResult(60.0, true);
            }

            var rawFps = nativeFpsValue.Value;
            var isSpike = false;
            var primarySignalStrength = Math.Max(nativeState?.MatchedDxgiEventCount ?? 0, nativeState?.MatchedD3D9EventCount ?? 0);
            var sameSignalTarget = targetPid > 0 && targetPid == LastSignalTargetPid;
            var signalUpgraded = sameSignalTarget && primarySignalStrength > LastPrimarySignalStrength;
            var debugState = new FpsNormalizationDebugState(
                targetPid,
                nativeState?.TargetProcessName ?? string.Empty,
                targetIsForegroundWindow,
                rawFps,
                LastStableFps,
                LastForegroundTrustedCodFps,
                nativeState?.MatchedDxgiEventCount ?? 0,
                nativeState?.MatchedD3D9EventCount ?? 0,
                nativeState?.MatchedDxgKrnlEventCount ?? 0,
                LastForegroundTrustedCodFps > 35.0 &&
                (now - LastForegroundTrustedCodFpsSetAtUtc).TotalSeconds < 120.0);
            var hasCodStyleDxgiOnlySignal = nativeState != null &&
                                            nativeState.MatchedD3D9EventCount == 0 &&
                                            nativeState.MatchedDxgiEventCount >= 4 &&
                                            nativeState.MatchedDxgKrnlEventCount >= 2;
            if (isCodTarget &&
                targetIsForegroundWindow &&
                rawFps >= 35.0 &&
                rawFps <= 90.0)
            {
                LastForegroundTrustedCodFps = rawFps;
                LastForegroundTrustedCodFpsSetAtUtc = now;
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
                        LastStableFps = correctedFps;
                        LastStableFpsSetAtUtc = now;
                        return new FpsNormalizationResult(correctedFps, false);
                    }

                    log?.Invoke("Keeping COD background guard active after duplicate present correction.");
                }
            }

            if (!isSpike &&
                !targetIsForegroundWindow &&
                isCodTarget &&
                LastStableFps > 45.0 &&
                (now - LastStableFpsSetAtUtc).TotalSeconds < 30.0 &&
                rawFps > Math.Max(LastStableFps + 8.0, LastStableFps * 1.12))
            {
                var backgroundLimitedFps = Math.Min(rawFps, LastStableFps + 4.0);
                log?.Invoke($"COD background growth limited: rawFps={rawFps:F0} limitedFps={backgroundLimitedFps:F0} lastStable={LastStableFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
                rawFps = backgroundLimitedFps;
            }

            if (!isSpike &&
                !targetIsForegroundWindow &&
                isCodTarget &&
                LastStableFps > 35.0 &&
                (now - LastStableFpsSetAtUtc).TotalSeconds < 30.0)
            {
                var conservativeBackgroundCeiling = LastStableFps + 2.0;
                if (rawFps > conservativeBackgroundCeiling)
                {
                    log?.Invoke(
                        $"COD background sticky hold applied: rawFps={rawFps:F0} heldFps={conservativeBackgroundCeiling:F0} lastStable={LastStableFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
                    rawFps = conservativeBackgroundCeiling;
                }
            }

            if (!isSpike &&
                !targetIsForegroundWindow &&
                isCodTarget &&
                LastForegroundTrustedCodFps > 35.0 &&
                (now - LastForegroundTrustedCodFpsSetAtUtc).TotalSeconds < 120.0)
            {
                var codBackgroundCeiling = LastForegroundTrustedCodFps + 2.0;

                if (rawFps > codBackgroundCeiling)
                {
                    log?.Invoke(
                        $"COD background foreground-anchor ceiling applied: rawFps={rawFps:F0} ceilingFps={codBackgroundCeiling:F0} trustedForeground={LastForegroundTrustedCodFps:F0} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow}");
                    rawFps = codBackgroundCeiling;
                }
            }

            if (rawFps > 1000.0)
            {
                isSpike = true;
                log?.Invoke($"Spike filtered (>1000): rawFps={rawFps:F0} lastStable={LastStableFps:F0}");
            }
            else if (LastStableFps > 10.0 && rawFps > LastStableFps * 1.5)
            {
                if ((now - LastStableFpsSetAtUtc).TotalSeconds < 6.0)
                {
                    if (signalUpgraded)
                    {
                        log?.Invoke(
                            $"Spike bypassed due to stronger primary signal: rawFps={rawFps:F0} lastStable={LastStableFps:F0} PrevSignal={LastPrimarySignalStrength} NewSignal={primarySignalStrength} TargetPid={debugState.TargetPid} Foreground={debugState.IsForegroundWindow}");
                    }
                    else
                    {
                        isSpike = true;
                        log?.Invoke($"Spike filtered (jump): rawFps={rawFps:F0} lastStable={LastStableFps:F0} TargetPid={debugState.TargetPid} Target={debugState.TargetName} Foreground={debugState.IsForegroundWindow} DXGI={debugState.DxgiCount} D3D9={debugState.D3D9Count} DXGKRNL={debugState.DxgKrnlCount} CodAnchor={debugState.HasForegroundTrustedCodAnchor}");
                    }
                }
            }

            if (targetPid > 0)
            {
                LastSignalTargetPid = targetPid;
                LastPrimarySignalStrength = primarySignalStrength;
            }

            if (isSpike)
            {
                return new FpsNormalizationResult(LastStableFps > 0.0 ? LastStableFps : null, false);
            }

            if (LastStableFps > 0.0)
            {
                const double emaAlpha = 0.3;
                if (isCodTarget && !targetIsForegroundWindow && rawFps > LastStableFps)
                {
                    // COD can keep producing background presents; do not let background samples
                    // ratchet the stable reference upward while the game is unfocused.
                    LastStableFps = (LastStableFps * (1.0 - emaAlpha)) + (LastStableFps * emaAlpha);
                }
                else
                {
                    LastStableFps = (rawFps * emaAlpha) + (LastStableFps * (1.0 - emaAlpha));
                }
            }
            else
            {
                LastStableFps = rawFps;
            }

            LastStableFpsSetAtUtc = now;
            return new FpsNormalizationResult(rawFps, false);
        }

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
    }
}