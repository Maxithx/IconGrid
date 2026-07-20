using System;

namespace IconGrid.Helpers.Hardware;

public sealed class NativeFpsAgentState
{
    public DateTime CapturedAtUtc { get; set; }
    public string? FpsStatus { get; set; }
    public string? FpsSource { get; set; }
    public int ParentPid { get; set; }
    public int RootPid { get; set; }
    public int TargetPid { get; set; }
    public string? TargetProcessName { get; set; }
    public string? LockedExecutableName { get; set; }
    public string? LockedExecutablePath { get; set; }
    public int CandidatePid { get; set; }
    public string? CandidateProcessName { get; set; }
    public string? WorkerStartedAtUtc { get; set; }
    public string? LastTargetLockAtUtc { get; set; }
    public string? LastEtwAttemptAtUtc { get; set; }
    public int EtwStartAttemptCount { get; set; }
    public int EtwStartFailureCount { get; set; }
    public string? LastEtwError { get; set; }
    public bool DxgKrnlEnabled { get; set; }
    public bool DxgiEnabled { get; set; }
    public bool D3D9Enabled { get; set; }
    public string? LastDxgKrnlError { get; set; }
    public string? LastDxgiError { get; set; }
    public string? LastD3D9Error { get; set; }
    public bool IsElevated { get; set; }
    public bool EtwRunning { get; set; }
    public bool EtwEventsReceived { get; set; }
    public int PreFilterDxgiEventCount { get; set; }
    public int PreFilterD3D9EventCount { get; set; }
    public int PreFilterDxgKrnlEventCount { get; set; }
    public int MatchedDxgiEventCount { get; set; }
    public int MatchedD3D9EventCount { get; set; }
    public int MatchedDxgKrnlEventCount { get; set; }
    public int DxgiEventCount { get; set; }
    public int D3D9EventCount { get; set; }
    public int DxgKrnlEventCount { get; set; }
    public string? DebugMessage { get; set; }
    public string? Error { get; set; }
}
