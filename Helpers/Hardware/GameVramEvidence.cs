using System;
using System.Collections.Generic;
using System.Linq;

namespace IconGrid.Helpers.Hardware;

internal enum GameEvidenceVerdict
{
    Unknown,
    Game,
    NotGame
}

/// <summary>
/// THE single game-classification rule. Everything that needs to decide whether a
/// process is a game goes through here, so there is exactly one definition.
///
/// The rule is evidence-based and deliberately contains NO process-name or
/// installation-path list (there are too many programs for that to stay correct):
///
///   GAME      = peak dedicated VRAM >= <see cref="ConfirmBytes"/>
///               OR application-level presents (DXGI/D3D9) for that PID
///   NOT GAME  = peak dedicated VRAM &lt; <see cref="NotGameBytes"/> AND no
///               application-level presents (only stray DxgKrnl kernel presents)
///   UNKNOWN   = everything in between, or VRAM not measurable
///
/// Kernel-only presents (DxgKrnl) never classify a process as a game: WDDM
/// composited shell apps (explorer, Task Manager, Command Palette, PowerToys)
/// emit stray kernel presents while holding only a few MB of dedicated VRAM, so
/// the VRAM measurement separates them from a real game (hundreds of MB to
/// several GB) without naming anything.
///
/// The peak is held per PID because a real game frees most of its VRAM during
/// loading screens and alt-tab. Measured on 2026-09-19: Call of Duty peaked at
/// 5.4 GB but reported 85 MB during one transition, while Task Manager stayed at
/// 12 MB and explorer at 12-28 MB for hours.
/// </summary>
internal static class GameVramEvidence
{
    /// <summary>Peak dedicated VRAM from which a process is trusted as a game.</summary>
    public const long ConfirmBytes = 500L * 1024 * 1024;

    /// <summary>
    /// Peak dedicated VRAM below which, with no application-level presents, a
    /// process is a non-game (shell/system/utility window). Measured on
    /// 2026-09-19: explorer peaked at 166 MB with many windows open, Task Manager
    /// at 12 MB, VS Code and browsers at 0 MB — while the running game held
    /// 0.8-5.4 GB. 250 MB keeps shell programs out with margin.
    /// </summary>
    public const long NotGameBytes = 250L * 1024 * 1024;

    /// <summary>
    /// How long a previously observed peak stays valid. Bounds both memory growth
    /// and the (small) window in which a recycled PID could inherit a peak.
    /// </summary>
    private static readonly TimeSpan PeakHold = TimeSpan.FromMinutes(15);

    private static readonly object Gate = new();
    private static readonly Dictionary<int, PidPeak> Peaks = new();


    /// <summary>
    /// Classifies a PID. <paramref name="nativeState"/> may be null (for a
    /// candidate that the native FPS agent does not track yet), in which case only
    /// the VRAM measurement is available.
    /// </summary>
    public static GameEvidenceVerdict Evaluate(
        int pid,
        NativeFpsAgentState? nativeState,
        DateTime nowUtc,
        out string detail)
    {
        detail = string.Empty;
        if (pid <= 0)
        {
            return GameEvidenceVerdict.Unknown;
        }

        if (nativeState != null &&
            nativeState.TargetPid == pid &&
            GameProcessClassifier.HasPrimaryGraphicsEvidence(nativeState))
        {
            detail = $"application-level presents for PID {pid} (DXGI/D3D9)";
            return GameEvidenceVerdict.Game;
        }

        var vramBytes = GpuProcessMemory.TryGetDedicatedUsageBytes(pid);
        var peakBytes = ObservePeak(pid, vramBytes, nowUtc);

        if (!peakBytes.HasValue)
        {
            detail = GpuProcessMemory.LastUnavailableReason is { Length: > 0 } reason
                ? $"dedicated VRAM unavailable ({reason})"
                : "dedicated VRAM unavailable for this PID";
            return GameEvidenceVerdict.Unknown;
        }

        if (peakBytes.Value >= ConfirmBytes)
        {
            detail = $"dedicated VRAM peak {GpuProcessMemory.Format(peakBytes.Value)} >= {GpuProcessMemory.Format(ConfirmBytes)}";
            return GameEvidenceVerdict.Game;
        }

        if (peakBytes.Value < NotGameBytes)
        {
            detail = $"dedicated VRAM peak {GpuProcessMemory.Format(peakBytes.Value)} < {GpuProcessMemory.Format(NotGameBytes)} and no application-level presents";
            return GameEvidenceVerdict.NotGame;
        }

        detail = $"dedicated VRAM peak {GpuProcessMemory.Format(peakBytes.Value)} below the confirm floor and no application-level presents";
        return GameEvidenceVerdict.Unknown;
    }

    /// <summary>
    /// True when the process is MEASURABLY a non-game: a measured low peak of
    /// dedicated VRAM and no application-level presents. An unmeasurable VRAM
    /// reading returns false, so a target is never rejected on missing data alone —
    /// only on evidence that actually contradicts it.
    /// </summary>
    public static bool IsMeasurablyNotGame(int pid, NativeFpsAgentState? nativeState, DateTime nowUtc, out string detail)
        => Evaluate(pid, nativeState, nowUtc, out detail) == GameEvidenceVerdict.NotGame;

    /// <summary>Peak dedicated VRAM seen for a PID, or null when never measurable.</summary>
    public static long? GetPeakBytes(int pid, DateTime nowUtc)
    {
        if (pid <= 0)
        {
            return null;
        }

        lock (Gate)
        {
            Prune(nowUtc);
            if (!Peaks.TryGetValue(pid, out var peak))
            {
                return null;
            }

            if (peak.Bytes.HasValue && nowUtc - peak.BytesObservedAtUtc > PeakHold)
            {
                return null;
            }

            return peak.Bytes;
        }
    }

    private static long? ObservePeak(int pid, long? bytes, DateTime nowUtc)
    {
        lock (Gate)
        {
            Prune(nowUtc);

            if (!Peaks.TryGetValue(pid, out var peak))
            {
                peak = new PidPeak();
                Peaks[pid] = peak;
            }

            peak.ObservedAtUtc = nowUtc;

            if (bytes.HasValue)
            {
                if (!peak.Bytes.HasValue || bytes.Value > peak.Bytes.Value)
                {
                    peak.Bytes = bytes.Value;
                }

                peak.BytesObservedAtUtc = nowUtc;
            }
            else if (peak.Bytes.HasValue && nowUtc - peak.BytesObservedAtUtc > PeakHold)
            {
                peak.Bytes = null;
            }

            return peak.Bytes;
        }
    }

    private static void Prune(DateTime nowUtc)
    {
        if (Peaks.Count == 0)
        {
            return;
        }

        var stale = Peaks
            .Where(pair => nowUtc - pair.Value.ObservedAtUtc > PeakHold)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var pid in stale)
        {
            Peaks.Remove(pid);
        }
    }

    private sealed class PidPeak
    {
        public long? Bytes { get; set; }
        public DateTime BytesObservedAtUtc { get; set; } = DateTime.MinValue;
        public DateTime ObservedAtUtc { get; set; } = DateTime.MinValue;
    }
}
