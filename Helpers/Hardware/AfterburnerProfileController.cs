using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// One MSI Afterburner profile as found in <c>Profiles\VEN_*.cfg</c>.
/// </summary>
public sealed record AfterburnerProfileInfo(
    int Number,
    int PowerLimitPercent,
    int ThermalLimitC,
    int CoreClockOffsetMhz,
    int MemoryClockOffsetMhz,
    int FanPercent,
    bool HasVoltageCurve)
{
    public bool IsEmpty =>
        PowerLimitPercent == 0 &&
        CoreClockOffsetMhz == 0 &&
        MemoryClockOffsetMhz == 0 &&
        !HasVoltageCurve;

    /// <summary>Short, localized-neutral label used by the settings drop-downs.</summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string> { $"Profile {Number}" };

            if (PowerLimitPercent > 0)
            {
                parts.Add($"{PowerLimitPercent}% power");
            }

            if (ThermalLimitC > 0)
            {
                parts.Add($"{ThermalLimitC} °C limit");
            }

            if (CoreClockOffsetMhz != 0)
            {
                parts.Add($"core {(CoreClockOffsetMhz > 0 ? "+" : string.Empty)}{CoreClockOffsetMhz} MHz");
            }

            if (MemoryClockOffsetMhz != 0)
            {
                parts.Add($"mem {(MemoryClockOffsetMhz > 0 ? "+" : string.Empty)}{MemoryClockOffsetMhz} MHz");
            }

            if (FanPercent > 0)
            {
                parts.Add($"fan {FanPercent}%");
            }

            if (HasVoltageCurve)
            {
                parts.Add("V/F curve");
            }

            return string.Join(" · ", parts);
        }
    }
}

public readonly record struct AfterburnerApplyResult(bool Success, bool Changed, string Message);

/// <summary>One point on the MSI Afterburner voltage/frequency curve.</summary>
public sealed record AfterburnerCurvePoint(float VoltageMv, float ClockMhz);

/// <summary>
/// Talks to MSI Afterburner instead of touching the GPU ourselves.
///
/// Afterburner owns the signed kernel driver that can write the voltage/frequency
/// curve, which no public API (NVML or NVAPI) exposes. It loads a profile either
/// at startup via its own command line switch (<c>-profile1</c> … <c>-profile5</c>,
/// the same switches Afterburner lists under "Application profiles") or when the
/// user clicks a profile button. A running instance ignores the switch, so a
/// profile switch means: stop Afterburner, start it again with the profile.
///
/// That restart requires administrator rights, so only the elevated hardware
/// monitor agent calls <see cref="Apply"/>.
/// </summary>
public sealed class AfterburnerProfileController
{
    private const string ExeName = "MSIAfterburner.exe";
    private const string ProfilesFolderName = "Profiles";

    private readonly Action<string>? _log;

    public AfterburnerProfileController(Action<string>? log = null)
    {
        _log = log;
        InstallPath = TryResolveInstallPath();
    }

    public string? InstallPath { get; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(InstallPath) &&
                               File.Exists(Path.Combine(InstallPath!, ExeName));

    /// <summary>All profiles found in the vendor profile file (profile 1-5).</summary>
    public IReadOnlyList<AfterburnerProfileInfo> ReadProfiles()
    {
        var profiles = new List<AfterburnerProfileInfo>();

        var path = TryResolveProfileFilePath();
        if (path == null)
        {
            return profiles;
        }

        try
        {
            var section = string.Empty;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Flush()
            {
                if (!section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(section.Substring("Profile".Length), out var number))
                {
                    return;
                }

                profiles.Add(new AfterburnerProfileInfo(
                    number,
                    ParseInt(values, "PowerLimit"),
                    ParseInt(values, "ThermalLimit"),
                    ParseInt(values, "CoreClkBoost"),
                    ParseInt(values, "MemClkBoost"),
                    ParseInt(values, "FanSpeed"),
                    values.TryGetValue("VFCurve", out var curve) &&
                    !string.IsNullOrWhiteSpace(curve) &&
                    curve.Length > 32));
            }

            var text = TryReadProfileText(path);
            if (text == null)
            {
                return profiles;
            }

            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                {
                    Flush();
                    section = line.Trim('[', ']');
                    values.Clear();
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator > 0)
                {
                    values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
                }
            }

            Flush();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not read the Afterburner profiles: {ex.Message}");
        }

        return profiles.OrderBy(profile => profile.Number).ToList();
    }

    /// <summary>
    /// Reads the stock V/F curve from the Afterburner defaults (fallback: profile 1)
    /// for the settings-page illustration. Empty when no curve is available.
    /// </summary>
    public IReadOnlyList<AfterburnerCurvePoint> ReadStockCurve()
        => ReadCurveSections("Defaults", "Profile1");

    /// <summary>
    /// Reads the V/F curve stored in a specific Afterburner profile. Empty when
    /// that profile has no curve (for example a stock profile with an empty VFCurve).
    /// </summary>
    public IReadOnlyList<AfterburnerCurvePoint> ReadCurve(int profileNumber)
        => profileNumber is < 1 or > 9
            ? Array.Empty<AfterburnerCurvePoint>()
            : ReadCurveSections($"Profile{profileNumber}");

    private IReadOnlyList<AfterburnerCurvePoint> ReadCurveSections(params string[] sectionNames)
    {
        var path = TryResolveProfileFilePath();
        if (path == null)
        {
            return Array.Empty<AfterburnerCurvePoint>();
        }

        try
        {
            var text = TryReadProfileText(path);
            if (text == null)
            {
                return Array.Empty<AfterburnerCurvePoint>();
            }

            CurveData? curve = null;
            foreach (var sectionName in sectionNames)
            {
                curve = FindCurve(text, sectionName);
                if (curve != null)
                {
                    break;
                }
            }

            if (curve == null || curve.Points.Count == 0)
            {
                return Array.Empty<AfterburnerCurvePoint>();
            }

            return curve.Points
                .Where(point => point.VoltageMv >= 100 && point.ClockMhz >= 100)
                .OrderBy(point => point.VoltageMv)
                .Select(point => new AfterburnerCurvePoint(point.VoltageMv, point.ClockMhz))
                .ToList();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not read the Afterburner curve: {ex.Message}");
            return Array.Empty<AfterburnerCurvePoint>();
        }
    }

    /// <summary>
    /// Applies a profile by restarting Afterburner with its profile switch. Does
    /// nothing when <paramref name="profileNumber"/> is already applied
    /// (<paramref name="lastApplied"/>) or when the number is 0.
    /// </summary>
    public AfterburnerApplyResult Apply(int profileNumber, ref int lastApplied, bool enableVoltageControl = false)
    {
        if (profileNumber <= 0)
        {
            return new AfterburnerApplyResult(true, false, "No Afterburner profile requested.");
        }

        if (!IsAvailable)
        {
            return new AfterburnerApplyResult(false, false, "MSI Afterburner was not found.");
        }

        if (lastApplied == profileNumber)
        {
            return new AfterburnerApplyResult(
                true,
                false,
                $"Afterburner profile {profileNumber} is already applied.");
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("MSIAfterburner"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    return new AfterburnerApplyResult(
                        false,
                        false,
                        $"Could not stop MSI Afterburner (administrator rights are required): {ex.Message}");
                }
            }

            if (enableVoltageControl)
            {
                EnsureVoltageControlUnlocked();
            }

            var exePath = Path.Combine(InstallPath!, ExeName);
            var startInfo = new ProcessStartInfo(exePath, $"-profile{profileNumber}")
            {
                WorkingDirectory = InstallPath!,
                UseShellExecute = true
            };

            Process.Start(startInfo);

            lastApplied = profileNumber;
            var message = $"MSI Afterburner profile {profileNumber} applied.";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(true, true, message);
        }
        catch (Exception ex)
        {
            var message = $"Could not apply the Afterburner profile {profileNumber}: {ex.Message}";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(false, false, message);
        }
    }

    // V/F curve blob layout (verified against a real 2060 SUPER profile):
    //   8-byte header (uint32 flags, uint32 pointCount) followed by
    //   pointCount records of 12 bytes: (uint32 flags, float voltageMv, float clockMhz).
    private const int CurveHeaderSize = 8;
    private const int CurveRecordSize = 12;
    private const int CurveMaxPoints = 512;

    /// <summary>
    /// Writes an undervolt curve into an Afterburner profile: every curve point at or
    /// above the stock point for <paramref name="targetClockMhz"/> is flattened to
    /// that clock, which is exactly what the Afterburner curve editor does when you
    /// drag a point down. The voltage of the chosen point is kept, so the card runs
    /// at the target clock with the stock voltage of that clock — no invented values.
    ///
    /// Writing needs administrator rights (Program Files) and Afterburner must be
    /// stopped, so only the elevated agent calls this. The profile file is backed up
    /// before it is touched.
    /// </summary>
    public AfterburnerApplyResult WriteUndervoltCurve(
        int targetClockMhz,
        int profileNumber,
        int voltageMv = 0,
        int powerLimitPercent = 0,
        int? coreOffsetMhz = null,
        int? memoryOffsetMhz = null)
    {
        if (!IsAvailable)
        {
            return new AfterburnerApplyResult(false, false, "MSI Afterburner was not found.");
        }

        if (profileNumber is < 1 or > 9)
        {
            return new AfterburnerApplyResult(false, false, "The Afterburner profile number must be 1-9.");
        }

        var path = TryResolveProfileFilePath();
        if (path == null)
        {
            return new AfterburnerApplyResult(false, false, "No Afterburner profile file was found.");
        }

        try
        {
            var text = File.ReadAllText(path);

            var stock = FindCurve(text, "Defaults") ?? FindCurve(text, "Profile1");
            if (stock == null || stock.Points.Count == 0)
            {
                return new AfterburnerApplyResult(false, false, "No stock V/F curve was found in the Afterburner profiles.");
            }

            var points = new List<CurvePoint>(stock.Points);
            int firstIndex;
            if (voltageMv > 0)
            {
                // A voltage target lets the caller undervolt below the stock voltage
                // for the clock (for example 1800 MHz at 925 mV instead of ~945 mV).
                // Every point at or above that voltage is flattened to the target
                // clock, so the card uses the lowest voltage that can sustain it.
                firstIndex = points.FindIndex(point => point.VoltageMv >= voltageMv);
            }
            else
            {
                firstIndex = points.FindIndex(point => point.ClockMhz >= targetClockMhz);
            }

            if (firstIndex < 0)
            {
                firstIndex = points.Count - 1;
            }

            var cappedClock = (float)Math.Clamp(targetClockMhz, 300, 2100);
            for (var i = firstIndex; i < points.Count; i++)
            {
                points[i] = points[i] with { ClockMhz = cappedClock };
            }

            var coreOffset = coreOffsetMhz.HasValue
                ? Math.Clamp(coreOffsetMhz.Value, -150, 150)
                : (int?)null;
            var memoryOffset = memoryOffsetMhz.HasValue
                ? Math.Clamp(memoryOffsetMhz.Value, -500, 500)
                : (int?)null;

            var hex = EncodeCurve(stock.HeaderFlags, stock.DeclaredCount, stock.TotalRecords, points);
            var backupPath = WriteBackup(path);
            var section = BuildProfileSection(
                text,
                profileNumber,
                hex,
                powerLimitPercent,
                coreOffset,
                memoryOffset);
            var updated = ReplaceProfileSection(text, profileNumber, section);
            File.WriteAllText(path, updated);

            var message =
                $"Tuning curve written to Afterburner profile {profileNumber}: " +
                $"max {cappedClock:F0} MHz from {points[firstIndex].VoltageMv:F0} mV " +
                $"({points.Count - firstIndex} points flattened)";
            if (coreOffset.HasValue)
            {
                message += $", core offset {coreOffset.Value:+#;-#;0} MHz";
            }

            if (memoryOffset.HasValue)
            {
                message += $", memory offset {memoryOffset.Value:+#;-#;0} MHz";
            }

            message += $". Backup: {backupPath}";

            _log?.Invoke(message);
            return new AfterburnerApplyResult(true, true, message);
        }
        catch (Exception ex)
        {
            var message = $"Could not write the Afterburner undervolt curve: {ex.Message}";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(false, false, message);
        }
    }

    /// <summary>
    /// Deletes one profile section from the Afterburner profile file. The file is
    /// backed up first and Afterburner is stopped while it is rewritten, because it
    /// may otherwise write its current settings back on exit. The caller (agent)
    /// owns the admin rights.
    /// </summary>
    public AfterburnerApplyResult DeleteProfile(int profileNumber)
    {
        if (!IsAvailable)
        {
            return new AfterburnerApplyResult(false, false, "MSI Afterburner was not found.");
        }

        if (profileNumber is < 1 or > 9)
        {
            return new AfterburnerApplyResult(false, false, "The Afterburner profile number must be 1-9.");
        }

        var path = TryResolveProfileFilePath();
        if (path == null)
        {
            return new AfterburnerApplyResult(false, false, "No Afterburner profile file was found.");
        }

        try
        {
            var text = File.ReadAllText(path);
            var header = $"[Profile{profileNumber}]";
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            var start = lines.FindIndex(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
            if (start < 0)
            {
                return new AfterburnerApplyResult(
                    false,
                    false,
                    $"Afterburner profile {profileNumber} was not found.");
            }

            var end = start + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                end++;
            }

            var wasRunning = false;
            foreach (var process in Process.GetProcessesByName("MSIAfterburner"))
            {
                try
                {
                    wasRunning = true;
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    return new AfterburnerApplyResult(
                        false,
                        false,
                        $"Could not stop MSI Afterburner (administrator rights are required): {ex.Message}");
                }
            }

            var backupPath = WriteBackup(path);
            lines.RemoveRange(start, end - start);
            File.WriteAllText(path, string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine);

            if (wasRunning)
            {
                var startInfo = new ProcessStartInfo(Path.Combine(InstallPath!, ExeName))
                {
                    WorkingDirectory = InstallPath!,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }

            var message = $"Afterburner profile {profileNumber} deleted. Backup: {backupPath}";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(true, true, message);
        }
        catch (Exception ex)
        {
            var message = $"Could not delete Afterburner profile {profileNumber}: {ex.Message}";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(false, false, message);
        }
    }

    /// <summary>
    /// Writes a "stock" profile (the Afterburner defaults, including the unmodified
    /// V/F curve) into <paramref name="profileNumber"/>. Used when the undervolt
    /// feature is switched off, so a previously applied power-limited profile cannot
    /// keep running after IconGrid stopped managing it.
    /// </summary>
    public AfterburnerApplyResult WriteStockProfile(int profileNumber)
    {
        if (!IsAvailable)
        {
            return new AfterburnerApplyResult(false, false, "MSI Afterburner was not found.");
        }

        if (profileNumber is < 1 or > 9)
        {
            return new AfterburnerApplyResult(false, false, "The Afterburner profile number must be 1-9.");
        }

        var path = TryResolveProfileFilePath();
        if (path == null)
        {
            return new AfterburnerApplyResult(false, false, "No Afterburner profile file was found.");
        }

        try
        {
            var text = File.ReadAllText(path);
            var defaults = ReadSectionValues(text, "Defaults");
            var curveHex = defaults != null && defaults.TryGetValue("VFCurve", out var curve) ? curve : string.Empty;

            var backupPath = WriteBackup(path);
            var section = BuildProfileSection(text, profileNumber, curveHex);
            File.WriteAllText(path, ReplaceProfileSection(text, profileNumber, section));

            var message =
                $"Stock profile written to Afterburner profile {profileNumber} " +
                $"(defaults: {defaults?["PowerLimit"] ?? "?"}% power). Backup: {backupPath}";

            _log?.Invoke(message);
            return new AfterburnerApplyResult(true, true, message);
        }
        catch (Exception ex)
        {
            var message = $"Could not write the Afterburner stock profile: {ex.Message}";
            _log?.Invoke(message);
            return new AfterburnerApplyResult(false, false, message);
        }
    }

    private sealed record CurvePoint(uint Flags, float VoltageMv, float ClockMhz);

    private sealed record CurveData(uint HeaderFlags, int DeclaredCount, int TotalRecords, List<CurvePoint> Points);

    private static CurveData? FindCurve(string text, string sectionName)
    {
        var values = ReadSectionValues(text, sectionName);
        if (values == null ||
            !values.TryGetValue("VFCurve", out var hex) ||
            string.IsNullOrWhiteSpace(hex) ||
            hex.Length < CurveHeaderSize * 2)
        {
            return null;
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        var headerFlags = BitConverter.ToUInt32(bytes, 0);
        var declaredCount = (int)BitConverter.ToUInt32(bytes, 4);
        var totalRecords = (bytes.Length - CurveHeaderSize) / CurveRecordSize;
        var points = new List<CurvePoint>(Math.Min(declaredCount, totalRecords));

        for (var i = 0; i < totalRecords; i++)
        {
            var offset = CurveHeaderSize + (i * CurveRecordSize);
            var flags = BitConverter.ToUInt32(bytes, offset);
            var voltage = BitConverter.ToSingle(bytes, offset + 4);
            var clock = BitConverter.ToSingle(bytes, offset + 8);

            if (voltage <= 0 && clock <= 0)
            {
                continue;
            }

            points.Add(new CurvePoint(flags, voltage, clock));
        }

        return new CurveData(headerFlags, declaredCount, totalRecords, points);
    }

    private static string EncodeCurve(uint headerFlags, int declaredCount, int totalRecords, IReadOnlyList<CurvePoint> points)
    {
        var records = Math.Max(totalRecords, points.Count);
        var bytes = new byte[CurveHeaderSize + (records * CurveRecordSize)];
        Buffer.BlockCopy(BitConverter.GetBytes(headerFlags), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((uint)declaredCount), 0, bytes, 4, 4);

        for (var i = 0; i < points.Count; i++)
        {
            var offset = CurveHeaderSize + (i * CurveRecordSize);
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].Flags), 0, bytes, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].VoltageMv), 0, bytes, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].ClockMhz), 0, bytes, offset + 8, 4);
        }

        var builder = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("X2"));
        }

        return builder.ToString();
    }

    private static Dictionary<string, string>? ReadSectionValues(string text, string sectionName)
    {
        Dictionary<string, string>? result = null;
        Dictionary<string, string>? current = null;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                var section = line.Trim('[', ']');
                if (section.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result = current;
                }
                else
                {
                    current = null;
                }

                continue;
            }

            if (current == null || line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                current[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
        }

        return result;
    }

    private static string BuildProfileSection(
        string text,
        int profileNumber,
        string curveHex,
        int powerLimitPercent = 0,
        int? coreOffsetMhz = null,
        int? memoryOffsetMhz = null)
    {
        var defaults = ReadSectionValues(text, "Defaults") ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keys = new[] { "Format", "PowerLimit", "ThermalLimit", "ThermalPrioritize", "CoreClkBoost", "VFCurve", "MemClkBoost", "FanMode", "FanSpeed" };

        var builder = new System.Text.StringBuilder();
        builder.Append('[').Append("Profile").Append(profileNumber).Append(']').AppendLine();

        foreach (var key in keys)
        {
            var value = key == "VFCurve"
                ? curveHex
                : key == "PowerLimit" && powerLimitPercent is >= 50 and <= 100
                    ? powerLimitPercent.ToString()
                    : key == "CoreClkBoost" && coreOffsetMhz.HasValue
                        ? coreOffsetMhz.Value.ToString()
                        : key == "MemClkBoost" && memoryOffsetMhz.HasValue
                            ? memoryOffsetMhz.Value.ToString()
                            : defaults.TryGetValue(key, out var existing) ? existing : string.Empty;

            builder.Append(key).Append('=').AppendLine(value);
        }

        return builder.ToString();
    }

    private static string ReplaceProfileSection(string text, int profileNumber, string sectionText)
    {
        var header = $"[Profile{profileNumber}]";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var start = lines.FindIndex(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));

        if (start < 0)
        {
            return string.Join(Environment.NewLine, lines).TrimEnd() +
                   Environment.NewLine +
                   sectionText;
        }

        var end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            end++;
        }

        var replacement = sectionText.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        lines.RemoveRange(start, end - start);
        lines.InsertRange(start, replacement);
        return string.Join(Environment.NewLine, lines);
    }

    private static string WriteBackup(string path)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IconGrid");

        Directory.CreateDirectory(folder);

        var backup = Path.Combine(folder, $"afterburner-profile-backup-{DateTime.Now:yyyyMMdd-HHmmss}.cfg");
        File.Copy(path, backup, overwrite: true);
        return backup;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return int.TryParse(text.Trim(), out var value) ? value : 0;
    }

    private static string? TryResolveInstallPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSI Afterburner"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MSI Afterburner")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(Path.Combine(candidate, ExeName)))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore unreadable candidates.
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the profile file with a few short retries. The elevated agent can
    /// rewrite it while Afterburner is restarting, and a single sharing violation
    /// used to make the settings drop-downs look empty.
    /// </summary>
    private static string? TryReadProfileText(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Afterburner may keep the profile file open while it restarts.
                // FileShare.ReadWrite|Delete lets us read it anyway.
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(80);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Afterburner ignores V/F curve changes unless voltage control is unlocked in
    /// its settings. The agent runs elevated and owns the profile file, so it turns
    /// the two settings on the first time a generated curve is applied. The config
    /// file is backed up before it is touched.
    /// </summary>
    private void EnsureVoltageControlUnlocked()
    {
        try
        {
            var cfgPath = Path.Combine(InstallPath!, "MSIAfterburner.cfg");
            if (!File.Exists(cfgPath))
            {
                _log?.Invoke("Could not find MSIAfterburner.cfg; the V/F curve may not be applied.");
                return;
            }

            var text = File.ReadAllText(cfgPath);
            var updated = SetIniValue(text, "UnlockVoltageControl", "1");
            updated = SetIniValue(updated, "UnlockVoltageMonitoring", "1");

            if (updated == text)
            {
                return;
            }

            var backupPath = cfgPath + ".bak-icongrid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(cfgPath, backupPath, true);
            File.WriteAllText(cfgPath, updated);
            _log?.Invoke($"Unlocked Afterburner voltage control (backup: {backupPath}).");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not unlock Afterburner voltage control: {ex.Message}");
        }
    }

    private static string SetIniValue(string text, string key, string value)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var found = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            var equals = trimmed.IndexOf('=');
            if (equals <= 0 ||
                !trimmed[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var indent = lines[i].Length - trimmed.Length;
            lines[i] = lines[i][..indent] + key + " = " + value;
            found = true;
            break;
        }

        if (!found)
        {
            lines.Add(key + " = " + value);
        }

        return string.Join(newline, lines);
    }

    private string? TryResolveProfileFilePath()
    {
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            return null;
        }

        try
        {
            var profilesFolder = Path.Combine(InstallPath!, ProfilesFolderName);
            if (!Directory.Exists(profilesFolder))
            {
                return null;
            }

            return Directory
                .EnumerateFiles(profilesFolder, "VEN_*.cfg", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
