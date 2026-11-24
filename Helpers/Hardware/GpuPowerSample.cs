using System;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// One measured GPU reading tagged with the window-focus state it was taken in.
/// Used by the settings page to show what the card does with and without the game
/// window in focus, so the effect of the low-power profile is visible.
/// </summary>
public sealed record GpuPowerSample(
    DateTime CapturedAtUtc,
    uint TempC,
    uint SmClockMhz,
    uint PowerDrawMw,
    uint FanPercent);

/// <summary>
/// Keeps the most recent sample for each focus state. Sampling itself is done by
/// the elevated hardware monitor agent (only it can read NVML), and the values are
/// published through <see cref="GpuPowerSavingState"/>.
/// </summary>
public sealed class GpuPowerSamples
{
    public GpuPowerSample? GameInFocus { get; private set; }

    public GpuPowerSample? GameNotInFocus { get; private set; }

    public void Record(bool gameIsForeground, GpuPowerStatus? status)
    {
        if (status == null)
        {
            return;
        }

        var sample = new GpuPowerSample(
            DateTime.UtcNow,
            status.CurrentTempC,
            status.SmClockMhz,
            status.PowerDrawMw,
            status.FanPercent);

        if (gameIsForeground)
        {
            GameInFocus = sample;
        }
        else
        {
            GameNotInFocus = sample;
        }
    }
}
