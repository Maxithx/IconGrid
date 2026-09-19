using System;

namespace IconGrid.Helpers.Hardware;

internal enum FpsAgentRestartReason
{
    None,
    ProcessExited,
    StateStale
}

/// <summary>
/// Keeps the native FPS worker honest.
///
/// The launcher only restarts the native agent when the tracked game PID
/// changes. When the worker process disappears (killed) or stops refreshing its
/// state (hung) while the game PID stays the same, every downstream consumer
/// silently reads nothing: no FPS numbers, <c>gameConfirmed</c> stays false and
/// the gaming overlay never appears. The trace from 2026-09-19 showed a 12
/// minute blackout like that after Call of Duty restarted itself: the agent was
/// started for the new PID, wrote one state file and then went silent, and
/// nothing restarted it until the tracked PID happened to change.
/// </summary>
internal sealed class FpsAgentWatchdog
{
    private static readonly TimeSpan DefaultStateStaleAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultMinimumRestartInterval = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _stateStaleAfter;
    private readonly TimeSpan _minimumRestartInterval;
    private DateTime? _stateMissingSinceUtc;
    private DateTime _lastRestartUtc = DateTime.MinValue;

    public FpsAgentWatchdog(TimeSpan? stateStaleAfter = null, TimeSpan? minimumRestartInterval = null)
    {
        _stateStaleAfter = stateStaleAfter ?? DefaultStateStaleAfter;
        _minimumRestartInterval = minimumRestartInterval ?? DefaultMinimumRestartInterval;
    }

    /// <summary>
    /// Decides whether the native FPS worker must be restarted. Only evaluates
    /// while a game target PID is tracked, and never more often than the minimum
    /// restart interval.
    /// </summary>
    public FpsAgentRestartReason Evaluate(
        int? gameTargetPid,
        NativeFpsAgentState? nativeState,
        int? processExitCode,
        DateTime nowUtc,
        out string detail)
    {
        detail = string.Empty;

        if (!gameTargetPid.HasValue)
        {
            _stateMissingSinceUtc = null;
            return FpsAgentRestartReason.None;
        }

        if (nowUtc - _lastRestartUtc < _minimumRestartInterval)
        {
            return FpsAgentRestartReason.None;
        }

        if (processExitCode.HasValue)
        {
            detail = $"The native FPS worker exited with code 0x{processExitCode.Value:X8} while game PID {gameTargetPid.Value} was tracked.";
            return FpsAgentRestartReason.ProcessExited;
        }

        if (nativeState != null)
        {
            _stateMissingSinceUtc = null;
            return FpsAgentRestartReason.None;
        }

        _stateMissingSinceUtc ??= nowUtc;
        var missingFor = nowUtc - _stateMissingSinceUtc.Value;
        if (missingFor < _stateStaleAfter)
        {
            return FpsAgentRestartReason.None;
        }

        detail = $"No native FPS state for {missingFor.TotalSeconds:F1}s while game PID {gameTargetPid.Value} was tracked.";
        return FpsAgentRestartReason.StateStale;
    }

    public void OnRestarted(DateTime nowUtc)
    {
        _lastRestartUtc = nowUtc;
        _stateMissingSinceUtc = null;
    }
}
