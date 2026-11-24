using System;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// UI/status handoff written by the elevated hardware monitor agent so the
/// settings page can show what the GPU power-saving feature is actually doing
/// without needing NVML access of its own.
/// </summary>
public sealed class GpuPowerSavingState
{
    public DateTime CapturedAtUtc { get; set; }

    public bool Enabled { get; set; }

    public bool Available { get; set; }

    public bool LowPowerProfileApplied { get; set; }

    public string? GpuName { get; set; }

    public uint PowerLimitMw { get; set; }

    public uint DefaultPowerLimitMw { get; set; }

    public uint MinPowerLimitMw { get; set; }

    public uint MaxPowerLimitMw { get; set; }

    public uint TempTargetC { get; set; }

    public uint CurrentTempC { get; set; }

    public uint FanPercent { get; set; }

    public uint SmClockMhz { get; set; }

    public uint MaxSmClockMhz { get; set; }

    public bool ClockLimitSupported { get; set; }

    public uint ClockCeilingMhz { get; set; }

    public uint PowerDrawMw { get; set; }

    /// <summary>Most recent reading taken while the game window had focus.</summary>
    public GpuPowerSample? GameInFocusSample { get; set; }

    /// <summary>Most recent reading taken while the game window did not have focus.</summary>
    public GpuPowerSample? GameNotInFocusSample { get; set; }

    public int KnownGameProcessCount { get; set; }

    public string? Message { get; set; }
}
