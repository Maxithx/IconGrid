using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Result of applying the temporary "game is in the background" GPU profile.
/// </summary>
public readonly record struct GpuLimitApplyResult(bool Success, bool Changed, string Message);

/// <summary>
/// Read-only snapshot of the values the GPU power-saving feature works with.
/// </summary>
public sealed record GpuPowerStatus(
    string GpuName,
    uint PowerLimitMw,
    uint DefaultPowerMw,
    uint MinPowerMw,
    uint MaxPowerMw,
    uint TempTargetC,
    uint CurrentTempC,
    uint FanPercent,
    bool TempLimitSupported,
    bool FanControlSupported,
    uint SmClockMhz = 0,
    uint MaxSmClockMhz = 0,
    bool ClockLimitSupported = false,
    uint ClockCeilingMhz = 0,
    uint PowerDrawMw = 0);

/// <summary>
/// Applies a temporary lower NVIDIA power limit and temperature limit while a
/// game session is alive but the game window is not foreground, and restores the
/// original driver values afterwards.
///
/// This is a focused helper (one driver concern) instead of logic inside
/// <see cref="HardwareMonitorAgent"/>:
///  - NVML writes require administrator rights, so this only runs inside the
///    elevated hardware monitor agent.
///  - The temperature target uses the NVML acoustic threshold (the same driver
///    knob MSI Afterburner exposes as "Temp Limit"), clamped to the card's own
///    minimum/maximum.
///  - A small marker file records the original values before the first write so
///    a restarted agent can undo a limit left behind by a crash.
///
/// The controller never raises a limit above the value captured at startup and
/// backs off if another tool (Afterburner / the user) changes the limit while it
/// is active.
/// </summary>
public sealed class NvmlGpuLimitController : IDisposable
{
    public const int MinPowerLimitPercent = 50;
    public const int MaxPowerLimitPercent = 100;
    public const int MinTempLimitCelsius = 60;
    public const int MaxTempLimitCelsius = 85;

    /// <summary>Lowest clock ceiling the governor will ever ask for (MHz).</summary>
    public const int MinClockCeilingMhz = 400;

    /// <summary>Allowed range for the user-facing background clock cap (percent of max SM clock).</summary>
    public const int MinClockLimitPercent = 30;
    public const int MaxClockLimitPercent = 100;

    /// <summary>Clock ceiling step used while the card is above the temperature target.</summary>
    public const int ClockStepDownMhz = 150;

    /// <summary>Clock ceiling step used while the card has headroom again.</summary>
    public const int ClockStepUpMhz = 100;

    /// <summary>Degrees below the target before the ceiling is raised again.</summary>
    public const int ClockReleaseHysteresisC = 3;

    /// <summary>Minimum time between clock ceiling changes (driver thrash guard).</summary>
    public const int ClockGovernorIntervalMs = 2000;

    private const int NvmlSuccess = 0;
    private const int NvmlErrorNotSupported = 3;
    private const uint NvmlClockTypeSm = 1;
    private const uint MinLockedClockMhz = 300;
    private const uint AcousticMinThreshold = 4;
    private const uint AcousticCurrentThreshold = 5;
    private const uint AcousticMaxThreshold = 6;

    private readonly Action<string>? _log;
    private readonly string? _restoreMarkerPath;

    private bool _nvmlInitialized;
    private bool _baselineCaptured;
    private bool _applied;
    private bool _externallyManaged;
    private bool _fanBoostApplied;
    private uint _fanCount;
    private IntPtr _device = IntPtr.Zero;

    private uint _minPowerMw;
    private uint _maxPowerMw;
    private uint _defaultPowerMw;
    private uint _baselinePowerMw;
    private uint _appliedPowerMw;
    private uint _minTempC;
    private uint _maxTempC;
    private uint _baselineTempC;
    private uint _appliedTempC;

    private uint _maxSmClockMhz;
    private uint _clockCeilingMhz;
    private bool _clockCeilingDirty;
    private bool _clockLockApplied;
    private bool _clockLimitUnsupported;
    private uint _lastGovernedTempC;
    private int _appliedTargetTempC;
    private int _appliedCeilingFloorMhz;
    private int _appliedCeilingCapMhz;
    private DateTime _lastClockGovernUtc = DateTime.MinValue;

    public NvmlGpuLimitController(string? restoreMarkerPath, Action<string>? log = null)
    {
        _restoreMarkerPath = restoreMarkerPath;
        _log = log;
        TryInitialize();
    }

    /// <summary>True when NVML initialised and the power limit could be read.</summary>
    public bool IsAvailable => _nvmlInitialized && _device != IntPtr.Zero && _baselineCaptured;

    public string GpuName { get; private set; } = string.Empty;

    public string? InitializationError { get; private set; }

    /// <summary>True when the driver exposes the acoustic temperature threshold.</summary>
    public bool TempLimitSupported { get; private set; }

    public uint MinPowerLimitMilliwatts => _minPowerMw;

    public uint MaxPowerLimitMilliwatts => _maxPowerMw;

    public uint DefaultPowerLimitMilliwatts => _defaultPowerMw;

    public uint BaselinePowerLimitMilliwatts => _baselinePowerMw;

    public uint BaselineTempLimitCelsius => _baselineTempC;

    /// <summary>True while the temporary low-power profile is applied.</summary>
    public bool IsLowPowerProfileApplied => _applied;

    /// <summary>True when NVML reports at least one controllable fan.</summary>
    public bool FanControlSupported => _fanCount > 0;

    /// <summary>
    /// True when the driver accepts locked GPU clocks (the same mechanism as
    /// <c>nvidia-smi --lock-gpu-clocks</c>). This is the only lever that actually
    /// lowers the core clock on a card whose power limit is already at its
    /// minimum, which is what makes the card cool down while a game keeps
    /// rendering in the background.
    /// </summary>
    public bool ClockLimitSupported => IsAvailable && _maxSmClockMhz > 0 && !_clockLimitUnsupported;

    /// <summary>True while the clock governor has a ceiling locked in the driver.</summary>
    public bool IsClockLimitApplied => _clockLockApplied;

    /// <summary>Clock ceiling currently requested from the driver (MHz, 0 = none).</summary>
    public uint AppliedClockCeilingMhz => _clockLockApplied ? _clockCeilingMhz : 0;

    /// <summary>Highest SM clock reported by the driver (MHz).</summary>
    public uint MaxSmClockMhz => _maxSmClockMhz;

    /// <summary>
    /// Reads the current driver values without changing anything. Works without
    /// administrator rights, so the settings page can show live status.
    /// </summary>
    public GpuPowerStatus? ReadStatus()
    {
        if (!IsAvailable)
        {
            return null;
        }

        var powerMw = 0u;
        if (nvmlDeviceGetPowerManagementLimit(_device, out var currentPowerMw) == NvmlSuccess)
        {
            powerMw = currentPowerMw;
        }

        var tempC = 0u;
        if (nvmlDeviceGetTemperature(_device, 0, out var currentTempC) == NvmlSuccess)
        {
            tempC = currentTempC;
        }

        var targetC = 0u;
        if (TempLimitSupported &&
            nvmlDeviceGetTemperatureThreshold(_device, AcousticCurrentThreshold, out var currentTargetC) == NvmlSuccess)
        {
            targetC = currentTargetC;
        }

        var fanPercent = 0u;
        if (FanControlSupported &&
            nvmlDeviceGetFanSpeed(_device, out var currentFanPercent) == NvmlSuccess)
        {
            fanPercent = currentFanPercent;
        }

        var smClockMhz = 0u;
        if (nvmlDeviceGetClockInfo(_device, NvmlClockTypeSm, out var currentSmClockMhz) == NvmlSuccess)
        {
            smClockMhz = currentSmClockMhz;
        }

        var powerDrawMw = 0u;
        if (nvmlDeviceGetPowerUsage(_device, out var currentPowerDrawMw) == NvmlSuccess)
        {
            powerDrawMw = currentPowerDrawMw;
        }

        return new GpuPowerStatus(
            GpuName,
            powerMw,
            _defaultPowerMw,
            _minPowerMw,
            _maxPowerMw,
            targetC,
            tempC,
            fanPercent,
            TempLimitSupported,
            FanControlSupported,
            smClockMhz,
            _maxSmClockMhz,
            ClockLimitSupported,
            AppliedClockCeilingMhz,
            powerDrawMw);
    }

    /// <summary>
    /// Applies the temporary low-power profile. Only ever lowers the values that
    /// were captured at startup; if the target is not lower than the baseline the
    /// method restores the baseline instead of raising anything.
    /// </summary>
    public GpuLimitApplyResult Apply(int powerLimitPercent, bool applyTempLimit, int tempLimitCelsius, bool fanBoost)
    {
        if (!IsAvailable)
        {
            return new GpuLimitApplyResult(false, false, InitializationError ?? "NVML is not available.");
        }

        if (_externallyManaged)
        {
            return new GpuLimitApplyResult(false, false, "GPU limits were changed by another tool; backing off.");
        }

        var percent = Math.Clamp(powerLimitPercent, MinPowerLimitPercent, MaxPowerLimitPercent);
        var baseMilliwatts = _defaultPowerMw > 0 ? _defaultPowerMw : _baselinePowerMw;
        var targetPowerMw = (uint)Math.Round(baseMilliwatts * (percent / 100.0));
        targetPowerMw = Math.Clamp(targetPowerMw, _minPowerMw, _maxPowerMw);

        uint? targetTempC = null;
        if (applyTempLimit && TempLimitSupported)
        {
            var requestedTemp = Math.Clamp(tempLimitCelsius, MinTempLimitCelsius, MaxTempLimitCelsius);
            if (_minTempC > 0)
            {
                requestedTemp = Math.Max(requestedTemp, (int)_minTempC);
            }

            if (_maxTempC > 0)
            {
                requestedTemp = Math.Min(requestedTemp, (int)_maxTempC);
            }

            // Only lower the limit; never make the card hotter than it already allows.
            if (requestedTemp < _baselineTempC)
            {
                targetTempC = (uint)requestedTemp;
            }
        }

        var fanRequested = fanBoost && FanControlSupported;

        if (targetPowerMw >= _baselinePowerMw && targetTempC == null && !fanRequested)
        {
            var restoreResult = Restore();
            return new GpuLimitApplyResult(
                restoreResult.Success,
                restoreResult.Changed,
                restoreResult.Message);
        }

        if (_applied &&
            targetPowerMw == _appliedPowerMw &&
            (targetTempC ?? _baselineTempC) == _appliedTempC &&
            _fanBoostApplied == fanRequested)
        {
            return new GpuLimitApplyResult(true, false, "Low-power GPU profile is already applied.");
        }

        if (!IsCurrentPowerLimitStillOurs())
        {
            _externallyManaged = true;
            var message = "GPU power limit changed externally (for example MSI Afterburner); leaving it untouched.";
            _log?.Invoke(message);
            return new GpuLimitApplyResult(false, false, message);
        }

        WriteRestoreMarker(targetPowerMw, targetTempC ?? _baselineTempC, fanRequested);

        var rc = NvmlSuccess;
        if (targetPowerMw < _baselinePowerMw)
        {
            rc = nvmlDeviceSetPowerManagementLimit(_device, targetPowerMw);
            if (rc != NvmlSuccess)
            {
                var message = $"Could not set GPU power limit: {Describe(rc)}.";
                _log?.Invoke(message);
                return new GpuLimitApplyResult(false, false, message);
            }
        }

        var tempApplied = _baselineTempC;
        if (targetTempC.HasValue)
        {
            var requested = (int)targetTempC.Value;
            rc = nvmlDeviceSetTemperatureThreshold(_device, AcousticCurrentThreshold, ref requested);
            if (rc == NvmlSuccess)
            {
                tempApplied = targetTempC.Value;
            }
            else
            {
                _log?.Invoke($"Could not set GPU temperature limit: {Describe(rc)}. Power limit is still applied.");
            }
        }

        var fanApplied = false;
        if (fanRequested)
        {
            if (TryApplyFanBoost(out var fanError))
            {
                fanApplied = true;
            }
            else
            {
                _log?.Invoke($"Could not raise the GPU fan speed: {fanError}. Power limit is still applied.");
            }
        }
        else if (_fanBoostApplied)
        {
            if (!TryRestoreFanControl(out var fanError))
            {
                _log?.Invoke($"Could not restore GPU fan control: {fanError}.");
            }
        }

        _applied = true;
        _appliedPowerMw = targetPowerMw;
        _appliedTempC = tempApplied;

        var summary = $"GPU power limit lowered to {targetPowerMw / 1000.0:F0} W ({percent}% of default)";
        if (tempApplied != _baselineTempC)
        {
            summary += $", temperature limit {tempApplied} °C";
        }

        if (fanApplied)
        {
            summary += ", fan boost at 100 %";
        }

        return new GpuLimitApplyResult(true, true, summary + ".");
    }

    /// <summary>
    /// Restores the power/temperature limits captured at startup. Safe to call
    /// multiple times; a second call is a no-op.
    /// </summary>
    public GpuLimitApplyResult Restore()
    {
        if (!_nvmlInitialized || _device == IntPtr.Zero)
        {
            return new GpuLimitApplyResult(true, false, "NVML is not available; nothing to restore.");
        }

        if (!_applied && !_clockLockApplied)
        {
            TryDeleteRestoreMarker();
            return new GpuLimitApplyResult(true, false, "No temporary GPU limit is active.");
        }

        if (!IsCurrentPowerLimitStillOurs())
        {
            _externallyManaged = true;
            _applied = false;
            TryDeleteRestoreMarker();
            var message = "GPU power limit changed externally; leaving the external value in place.";
            _log?.Invoke(message);
            return new GpuLimitApplyResult(true, false, message);
        }

        var success = true;
        var rc = nvmlDeviceSetPowerManagementLimit(_device, _baselinePowerMw);
        if (rc != NvmlSuccess)
        {
            success = false;
            _log?.Invoke($"Could not restore GPU power limit: {Describe(rc)}.");
        }

        if (_appliedTempC != _baselineTempC && TempLimitSupported)
        {
            var baselineTemp = (int)_baselineTempC;
            rc = nvmlDeviceSetTemperatureThreshold(_device, AcousticCurrentThreshold, ref baselineTemp);
            if (rc != NvmlSuccess)
            {
                success = false;
                _log?.Invoke($"Could not restore GPU temperature limit: {Describe(rc)}.");
            }
        }

        if (_fanBoostApplied && !TryRestoreFanControl(out var fanError))
        {
            success = false;
            _log?.Invoke($"Could not restore GPU fan control: {fanError}.");
        }

        if (_clockLockApplied && !TryReleaseClockLimit(out var clockError))
        {
            success = false;
            _log?.Invoke($"Could not release the locked GPU clock: {clockError}.");
        }

        if (success)
        {
            _applied = false;
            _clockCeilingMhz = 0;
            TryDeleteRestoreMarker();
            return new GpuLimitApplyResult(true, true, $"GPU limits restored to {_baselinePowerMw / 1000.0:F0} W / {_baselineTempC} °C.");
        }

        return new GpuLimitApplyResult(false, false, "Could not restore the original GPU limits.");
    }

    /// <summary>
    /// Closed-loop clock governor used while the low-power profile is active.
    ///
    /// The power limit and the driver's temperature target cannot cool a card
    /// that keeps rendering in the background (the power limit is usually already
    /// at the card's minimum, and the temperature knob is an acoustic target the
    /// driver does not turn into a hard clock limit). Locking the SM clock is the
    /// one lever that actually removes heat: the ceiling is stepped down while the
    /// card is above <paramref name="targetTempC"/> and stepped back up once the
    /// card is at least <see cref="ClockReleaseHysteresisC"/> degrees below it.
    /// </summary>
    public GpuLimitApplyResult GovernClocks(
        int targetTempC,
        int minCeilingMhz = MinClockCeilingMhz,
        int maxCeilingMhz = 0)
    {
        if (!IsAvailable)
        {
            return new GpuLimitApplyResult(false, false, InitializationError ?? "NVML is not available.");
        }

        if (!ClockLimitSupported)
        {
            var unsupported = _clockLimitUnsupported
                ? "The driver does not support locked GPU clocks on this card."
                : "The GPU clock range could not be read.";
            return new GpuLimitApplyResult(false, false, unsupported);
        }

        if (targetTempC <= 0)
        {
            return new GpuLimitApplyResult(true, false, "No temperature target for the clock governor.");
        }

        var floor = (uint)Math.Clamp(
            minCeilingMhz,
            MinClockCeilingMhz,
            (int)Math.Max(_maxSmClockMhz, MinClockCeilingMhz));

        var cap = maxCeilingMhz > 0
            ? (uint)Math.Clamp((int)maxCeilingMhz, (int)floor, (int)Math.Max(_maxSmClockMhz, (int)floor))
            : Math.Max(_maxSmClockMhz, floor);

        // A different target/floor/cap means a different profile (background
        // low-power vs. in-game undervolt): re-seed the ceiling at the cap so the
        // governor converges from above instead of inheriting a stale low ceiling.
        if (targetTempC != _appliedTargetTempC ||
            (int)floor != _appliedCeilingFloorMhz ||
            (int)cap != _appliedCeilingCapMhz)
        {
            var capChanged = (int)cap != _appliedCeilingCapMhz;
            _appliedTargetTempC = targetTempC;
            _appliedCeilingFloorMhz = (int)floor;
            _appliedCeilingCapMhz = (int)cap;

            // A new cap (the user moved the clock slider, or the profile changed)
            // must take effect immediately instead of waiting for the temperature
            // governor to walk there in 150 MHz steps.
            _clockCeilingMhz = capChanged
                ? cap
                : Math.Max(floor, Math.Min(_clockCeilingMhz, cap));
            _clockCeilingDirty = true;
            _lastClockGovernUtc = DateTime.MinValue;
        }

        var nowUtc = DateTime.UtcNow;
        if (_clockLockApplied && (nowUtc - _lastClockGovernUtc).TotalMilliseconds < ClockGovernorIntervalMs)
        {
            return new GpuLimitApplyResult(
                true,
                false,
                $"Clock ceiling {_clockCeilingMhz} MHz · GPU {_lastGovernedTempC} °C (target {targetTempC} °C).");
        }

        if (nvmlDeviceGetTemperature(_device, 0, out var currentTempC) != NvmlSuccess)
        {
            return new GpuLimitApplyResult(false, false, "The GPU temperature could not be read.");
        }

        _lastGovernedTempC = currentTempC;

        var ceiling = _clockLockApplied && _clockCeilingMhz > 0 ? _clockCeilingMhz : cap;
        var target = (uint)Math.Clamp(targetTempC, MinTempLimitCelsius, MaxTempLimitCelsius);

        if (currentTempC > target)
        {
            ceiling = (uint)Math.Max((int)floor, (int)ceiling - ClockStepDownMhz);
        }
        else if (currentTempC + ClockReleaseHysteresisC <= target)
        {
            ceiling = Math.Min(cap, ceiling + ClockStepUpMhz);
        }

        ceiling = (uint)Math.Clamp((int)ceiling, (int)floor, (int)Math.Max(cap, floor));

        if (_clockLockApplied && !_clockCeilingDirty && ceiling == _clockCeilingMhz)
        {
            _lastClockGovernUtc = nowUtc;
            return new GpuLimitApplyResult(
                true,
                false,
                $"Clock ceiling {ceiling} MHz · GPU {currentTempC} °C (target {target} °C).");
        }

        var rc = nvmlDeviceSetGpuLockedClocks(_device, MinLockedClockMhz, ceiling);
        if (rc != NvmlSuccess)
        {
            if (rc == NvmlErrorNotSupported)
            {
                _clockLimitUnsupported = true;
            }

            var failure = $"Could not lock the GPU clock to {ceiling} MHz: {Describe(rc)}.";
            _log?.Invoke($"GPU clock governor: {failure}");
            return new GpuLimitApplyResult(false, false, failure);
        }

        _clockCeilingMhz = ceiling;
        _clockLockApplied = true;
        _clockCeilingDirty = false;
        _lastClockGovernUtc = nowUtc;
        WriteRestoreMarker(_appliedPowerMw, _appliedTempC, _fanBoostApplied);
        _log?.Invoke($"GPU clock governor: ceiling {ceiling} MHz at {currentTempC} °C (target {target} °C).");
        return new GpuLimitApplyResult(
            true,
            true,
            $"Clock ceiling {ceiling} MHz · GPU {currentTempC} °C (target {target} °C).");
    }

    /// <summary>
    /// Removes a clock ceiling immediately. Used when the game regains focus, so
    /// the player does not get a low-clock frame spike while the slower
    /// power-limit hysteresis is still counting down.
    /// </summary>
    public bool ReleaseClockLimit(out string? error)
    {
        return TryReleaseClockLimit(out error);
    }

    private bool TryReleaseClockLimit(out string? error)
    {
        error = null;

        if (!_clockLockApplied)
        {
            return true;
        }

        var rc = nvmlDeviceResetGpuLockedClocks(_device);
        if (rc != NvmlSuccess)
        {
            error = Describe(rc);
            return false;
        }

        _clockLockApplied = false;
        _clockCeilingMhz = 0;
        _clockCeilingDirty = false;
        _appliedTargetTempC = 0;
        _appliedCeilingFloorMhz = 0;
        _appliedCeilingCapMhz = 0;
        _lastClockGovernUtc = DateTime.MinValue;

        // Keep the crash-recovery marker in sync: it must only claim a clock
        // ceiling while one is actually locked in the driver.
        if (_applied)
        {
            WriteRestoreMarker(_appliedPowerMw, _appliedTempC, _fanBoostApplied);
        }
        else
        {
            TryDeleteRestoreMarker();
        }

        _log?.Invoke("GPU clock governor released the clock ceiling.");
        return true;
    }

    private bool IsCurrentPowerLimitStillOurs()
    {
        if (nvmlDeviceGetPowerManagementLimit(_device, out var currentMw) != NvmlSuccess)
        {
            // Reading failed. Do not block on a driver hiccup; the write below
            // will fail on its own if the device is really gone.
            return true;
        }

        if (!_applied)
        {
            // Before the first write the driver value must still be the one we
            // captured at startup; otherwise another tool changed it meanwhile.
            return currentMw == _baselinePowerMw;
        }

        return currentMw == _baselinePowerMw || currentMw == _appliedPowerMw;
    }

    private void TryInitialize()
    {
        try
        {
            var rc = nvmlInit_v2();
            if (rc != NvmlSuccess)
            {
                InitializationError = $"NVML could not be initialised: {Describe(rc)}.";
                return;
            }

            _nvmlInitialized = true;

            rc = nvmlDeviceGetHandleByIndex_v2(0, out var device);
            if (rc != NvmlSuccess)
            {
                InitializationError = $"No NVIDIA GPU handle available: {Describe(rc)}.";
                ShutdownNvml();
                return;
            }

            _device = device;

            var nameBuffer = new StringBuilder(96);
            if (nvmlDeviceGetName(_device, nameBuffer, (uint)nameBuffer.Capacity) == NvmlSuccess)
            {
                GpuName = nameBuffer.ToString();
            }

            ReadPowerEnvelope();
            RecoverLimitLeftByCrashedSession();
            CaptureBaseline();
        }
        catch (DllNotFoundException)
        {
            // No NVIDIA driver on this machine (AMD/Intel). The feature silently
            // stays unavailable instead of taking the monitor agent down.
            InitializationError = "NVML (NVIDIA driver) was not found.";
            ShutdownNvml();
        }
        catch (EntryPointNotFoundException)
        {
            InitializationError = "The installed NVIDIA driver does not expose the required NVML functions.";
            ShutdownNvml();
        }
        catch (Exception ex)
        {
            InitializationError = $"NVML initialisation failed: {ex.Message}";
            ShutdownNvml();
        }
    }

    private void ReadPowerEnvelope()
    {
        if (nvmlDeviceGetPowerManagementLimitConstraints(_device, out var minMw, out var maxMw) == NvmlSuccess)
        {
            _minPowerMw = minMw;
            _maxPowerMw = maxMw;
        }

        if (nvmlDeviceGetPowerManagementDefaultLimit(_device, out var defaultMw) == NvmlSuccess)
        {
            _defaultPowerMw = defaultMw;
        }

        if (nvmlDeviceGetTemperatureThreshold(_device, AcousticMinThreshold, out var minTempC) == NvmlSuccess)
        {
            _minTempC = minTempC;
        }

        if (nvmlDeviceGetTemperatureThreshold(_device, AcousticMaxThreshold, out var maxTempC) == NvmlSuccess)
        {
            _maxTempC = maxTempC;
        }

        TempLimitSupported =
            nvmlDeviceGetTemperatureThreshold(_device, AcousticCurrentThreshold, out _) == NvmlSuccess;

        if (nvmlDeviceGetNumFans(_device, out var fanCount) == NvmlSuccess)
        {
            _fanCount = fanCount;
        }

        if (nvmlDeviceGetMaxClockInfo(_device, NvmlClockTypeSm, out var maxSmClockMhz) == NvmlSuccess)
        {
            _maxSmClockMhz = maxSmClockMhz;
        }
    }

    private bool TryApplyFanBoost(out string? error)
    {
        error = null;

        if (!FanControlSupported)
        {
            error = "the driver reports no controllable fan";
            return false;
        }

        for (uint fan = 0; fan < _fanCount; fan++)
        {
            var rc = nvmlDeviceSetFanSpeed_v2(_device, fan, 100);
            if (rc != NvmlSuccess)
            {
                error = Describe(rc);
                return false;
            }
        }

        _fanBoostApplied = true;
        return true;
    }

    private bool TryRestoreFanControl(out string? error)
    {
        error = null;
        var success = true;

        for (uint fan = 0; fan < _fanCount; fan++)
        {
            var rc = nvmlDeviceSetDefaultFanSpeed_v2(_device, fan);
            if (rc != NvmlSuccess)
            {
                // Some drivers only accept the policy reset instead.
                var policyRc = nvmlDeviceSetFanControlPolicy(_device, fan, 0);
                if (policyRc != NvmlSuccess)
                {
                    success = false;
                    error = Describe(rc);
                }
            }
        }

        if (success)
        {
            _fanBoostApplied = false;
        }

        return success;
    }

    private void CaptureBaseline()
    {
        if (nvmlDeviceGetPowerManagementLimit(_device, out var powerMw) != NvmlSuccess)
        {
            InitializationError = "The current GPU power limit could not be read.";
            return;
        }

        if (_minPowerMw == 0 || _maxPowerMw == 0)
        {
            InitializationError = "The GPU power limit range could not be read.";
            return;
        }

        _baselinePowerMw = powerMw;
        _appliedPowerMw = powerMw;

        if (TempLimitSupported &&
            nvmlDeviceGetTemperatureThreshold(_device, AcousticCurrentThreshold, out var tempC) == NvmlSuccess)
        {
            _baselineTempC = tempC;
        }

        _appliedTempC = _baselineTempC;
        _baselineCaptured = true;
    }

    /// <summary>
    /// Undo a limit that a previous agent process left behind when it was killed
    /// before it could restore the driver values.
    /// </summary>
    private void RecoverLimitLeftByCrashedSession()
    {
        if (string.IsNullOrWhiteSpace(_restoreMarkerPath) || !File.Exists(_restoreMarkerPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_restoreMarkerPath);
            var state = JsonConvert.DeserializeObject<RestoreMarker>(json);
            if (state == null || state.OriginalPowerMw == 0)
            {
                TryDeleteRestoreMarker();
                return;
            }

            var currentIsOurs = nvmlDeviceGetPowerManagementLimit(_device, out var currentMw) != NvmlSuccess
                                || currentMw == state.AppliedPowerMw;

            if (currentIsOurs)
            {
                var rc = nvmlDeviceSetPowerManagementLimit(_device, state.OriginalPowerMw);
                if (rc == NvmlSuccess)
                {
                    _log?.Invoke(
                        $"Restored GPU power limit left by a previous session: {state.OriginalPowerMw / 1000.0:F0} W.");

                    if (state.OriginalTempC > 0 && TempLimitSupported)
                    {
                        var originalTemp = (int)state.OriginalTempC;
                        if (nvmlDeviceSetTemperatureThreshold(_device, AcousticCurrentThreshold, ref originalTemp) != NvmlSuccess)
                        {
                            _log?.Invoke("Could not restore the GPU temperature limit left by a previous session.");
                        }
                    }
                }
                else
                {
                    _log?.Invoke($"Could not restore the GPU power limit left by a previous session: {Describe(rc)}.");
                }
            }
            else
            {
                _log?.Invoke("A GPU limit marker from a previous session was found, but the current value was changed by someone else. Leaving it untouched.");
            }

            if (state.FanBoostApplied && FanControlSupported &&
                !TryRestoreFanControl(out var fanError))
            {
                _log?.Invoke($"Could not restore the GPU fan control left by a previous session: {fanError}.");
            }

            // A clock ceiling is a driver-wide setting that survives our process,
            // so a crashed session must never leave the card locked at a low clock.
            if (state.ClockLockApplied)
            {
                var clockRc = nvmlDeviceResetGpuLockedClocks(_device);
                if (clockRc == NvmlSuccess)
                {
                    _log?.Invoke(
                        $"Released the GPU clock ceiling left by a previous session ({state.AppliedClockCeilingMhz} MHz).");
                }
                else
                {
                    _log?.Invoke($"Could not release the GPU clock ceiling left by a previous session: {Describe(clockRc)}.");
                }
            }

            TryDeleteRestoreMarker();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not read the GPU limit restore marker: {ex.Message}");
        }
    }

    private void WriteRestoreMarker(uint appliedPowerMw, uint appliedTempC, bool fanBoostApplied)
    {
        if (string.IsNullOrWhiteSpace(_restoreMarkerPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_restoreMarkerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new RestoreMarker
            {
                GpuName = GpuName,
                OriginalPowerMw = _baselinePowerMw,
                AppliedPowerMw = appliedPowerMw,
                OriginalTempC = _baselineTempC,
                AppliedTempC = appliedTempC,
                FanBoostApplied = fanBoostApplied,
                ClockLockApplied = _clockLockApplied,
                AppliedClockCeilingMhz = _clockCeilingMhz,
                CapturedAtUtc = DateTime.UtcNow
            };

            File.WriteAllText(_restoreMarkerPath, JsonConvert.SerializeObject(state, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not write the GPU limit restore marker: {ex.Message}");
        }
    }

    private void TryDeleteRestoreMarker()
    {
        if (string.IsNullOrWhiteSpace(_restoreMarkerPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_restoreMarkerPath))
            {
                File.Delete(_restoreMarkerPath);
            }
        }
        catch
        {
            // Best-effort cleanup; the next startup will still recover it.
        }
    }

    private static string Describe(int rc) => rc switch
    {
        1 => "NVML is not initialised",
        2 => "invalid argument",
        3 => "not supported by this GPU or driver",
        4 => "permission denied (administrator rights are required)",
        5 => "NVML is already initialised",
        6 => "GPU not found",
        8 => "insufficient power",
        9 => "driver not loaded",
        14 => "GPU is lost",
        _ => $"NVML error {rc}"
    };

    private void ShutdownNvml()
    {
        if (!_nvmlInitialized)
        {
            return;
        }

        try
        {
            nvmlShutdown();
        }
        catch
        {
            // The driver may already be unloading; nothing sensible to do here.
        }

        _nvmlInitialized = false;
        _device = IntPtr.Zero;
    }

    public void Dispose()
    {
        try
        {
            if (_applied)
            {
                Restore();
            }
        }
        catch
        {
            // Best-effort restore during shutdown.
        }

        ShutdownNvml();
    }

    private sealed class RestoreMarker
    {
        public string GpuName { get; set; } = string.Empty;

        public uint OriginalPowerMw { get; set; }

        public uint AppliedPowerMw { get; set; }

        public uint OriginalTempC { get; set; }

        public uint AppliedTempC { get; set; }

        public bool FanBoostApplied { get; set; }

        public bool ClockLockApplied { get; set; }

        public uint AppliedClockCeilingMhz { get; set; }

        public DateTime CapturedAtUtc { get; set; }
    }

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetPowerManagementLimit(IntPtr device, out uint limitMw);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint powerMw);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetPowerManagementDefaultLimit(IntPtr device, out uint limitMw);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetPowerManagementLimitConstraints(IntPtr device, out uint minLimitMw, out uint maxLimitMw);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetPowerManagementLimit(IntPtr device, uint limitMw);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint tempC);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperatureThreshold(IntPtr device, uint thresholdType, out uint tempC);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetTemperatureThreshold(IntPtr device, uint thresholdType, ref int tempC);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetNumFans(IntPtr device, out uint fanCount);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetFanSpeed(IntPtr device, out uint speedPercent);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetFanSpeed_v2(IntPtr device, uint fan, uint speedPercent);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetDefaultFanSpeed_v2(IntPtr device, uint fan);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetFanControlPolicy(IntPtr device, uint fan, uint policy);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetClockInfo(IntPtr device, uint clockType, out uint clockMhz);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMaxClockInfo(IntPtr device, uint clockType, out uint clockMhz);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceSetGpuLockedClocks(IntPtr device, uint minClockMhz, uint maxClockMhz);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceResetGpuLockedClocks(IntPtr device);
}
