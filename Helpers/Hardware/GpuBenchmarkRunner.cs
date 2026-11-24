using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IconGrid.Helpers.Hardware;

public sealed record BenchmarkRequest(string Label, int Seconds, IReadOnlyList<int> Profiles);

public sealed record BenchmarkPassResult(
    int Profile,
    string Label,
    int Frames,
    double AvgFps,
    double P99FrameMs,
    double Over20MsPercent,
    uint MaxTempC,
    double AvgPowerW,
    double AvgClockMhz);

public sealed record BenchmarkSummary(string Label, DateTime CompletedAtUtc, IReadOnlyList<BenchmarkPassResult> Passes);

/// <summary>
/// Measures GPU behaviour while a game runs, with and without an undervolt profile.
///
/// Frame times come from the PresentMon CLI that ships in <c>Tools\PresentMon</c>
/// (the same binary the diagnostics use), so "1% low"/dropped-frame numbers are real
/// per-frame measurements instead of an average. Temperatures, power and clocks come
/// from NVML through the caller's status snapshot.
///
/// Runs are deliberately slow and stateful: warmup (discarded), measurement, then a
/// cooldown before the next profile so heat soak does not favour one configuration.
/// The agent calls <see cref="Tick"/> once per loop iteration, so the FPS pipeline and
/// the overlay keep running during a test.
/// </summary>
public sealed class GpuBenchmarkRunner
{
    private const int WarmupSeconds = 10;
    private const int CooldownSeconds = 15;
    private const double VsyncFrameMs = 20.0;

    private readonly string _baseDir;
    private readonly Action<string>? _log;
    private readonly List<BenchmarkPassResult> _results = new();
    private readonly List<int> _queue = new();

    private BenchmarkRequest? _request;
    private Process? _presentMon;
    private string? _csvPath;
    private int _currentProfile;
    private DateTime _phaseStartUtc;
    private bool _cooldown;

    private uint _maxTempC;
    private double _sumTemp;
    private double _sumPowerMw;
    private double _sumClockMhz;
    private int _samples;

    public GpuBenchmarkRunner(string baseDir, Action<string>? log)
    {
        _baseDir = baseDir;
        _log = log;
    }

    public bool IsRunning => _request != null;

    public string StatusText { get; private set; } = string.Empty;

    public string ProgressText { get; private set; } = string.Empty;

    public BenchmarkSummary? LastSummary { get; private set; }

    /// <summary>Reads (and consumes) a pending benchmark request written by the UI.</summary>
    public BenchmarkRequest? TryLoadRequest()
    {
        var path = Path.Combine(_baseDir, "gpu-benchmark-request.json");

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            File.Delete(path);

            var dto = JsonSerializer.Deserialize<RequestDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (dto == null || dto.Seconds <= 0)
            {
                return null;
            }

            var profiles = dto.Profiles?.Where(profile => profile > 0).ToList() ?? new List<int>();
            return new BenchmarkRequest(
                string.IsNullOrWhiteSpace(dto.Label) ? "Benchmark" : dto.Label!.Trim(),
                Math.Clamp(dto.Seconds, 10, 300),
                profiles);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Benchmark request could not be read: {ex.Message}");
            return null;
        }
    }

    public void Start(BenchmarkRequest request, int gamePid, AfterburnerProfileController afterburner, ref int lastProfile)
    {
        _request = request;
        _results.Clear();
        _queue.Clear();
        _queue.AddRange(request.Profiles.Count > 0 ? request.Profiles : new List<int> { 0 });
        _currentProfile = _queue[0];
        LastSummary = null;
        _cooldown = false;
        StatusText = $"Benchmark '{request.Label}' started.";
        _log?.Invoke(StatusText);

        BeginPass(gamePid, afterburner, ref lastProfile);
    }

    private sealed class RequestDto
    {
        public string? Label { get; set; }

        public int Seconds { get; set; }

        public List<int>? Profiles { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private void BeginPass(int gamePid, AfterburnerProfileController afterburner, ref int lastProfile)
    {
        if (_currentProfile > 0 && afterburner.IsAvailable)
        {
            lastProfile = 0; // Force the profile to be applied for this pass.
            var apply = afterburner.Apply(_currentProfile, ref lastProfile);
            _log?.Invoke(apply.Message);
        }

        _maxTempC = 0;
        _sumTemp = 0;
        _sumPowerMw = 0;
        _sumClockMhz = 0;
        _samples = 0;
        _phaseStartUtc = DateTime.UtcNow;
        _cooldown = false;

        StartPresentMon(gamePid);

        StatusText =
            $"Benchmark '{_request!.Label}': profile {_currentProfile} running " +
            $"(warmup {WarmupSeconds}s + {_request.Seconds}s).";
        _log?.Invoke(StatusText);
    }

    /// <summary>Called by the agent every loop iteration; never blocks the loop.</summary>
    public void Tick(int gamePid, GpuPowerStatus? status, AfterburnerProfileController afterburner, ref int lastProfile)
    {
        if (_request == null)
        {
            return;
        }

        var elapsed = (DateTime.UtcNow - _phaseStartUtc).TotalSeconds;

        if (_cooldown)
        {
            ProgressText = $"Cooling down {Math.Max(0, CooldownSeconds - elapsed):F0}s";
            if (elapsed >= CooldownSeconds)
            {
                _currentProfile = _queue[0];
                BeginPass(gamePid, afterburner, ref lastProfile);
            }

            return;
        }

        // Measure only the part after the warmup window.
        if (elapsed >= WarmupSeconds && status != null)
        {
            _samples++;
            _sumTemp += status.CurrentTempC;
            _sumPowerMw += status.PowerDrawMw;
            _sumClockMhz += status.SmClockMhz;

            if (status.CurrentTempC > _maxTempC)
            {
                _maxTempC = status.CurrentTempC;
            }
        }

        var total = WarmupSeconds + _request.Seconds;
        ProgressText = $"Profile {_currentProfile}: {Math.Min(elapsed, total):F0}/{total}s";

        if (elapsed >= total)
        {
            FinishPass();
        }
    }

    private void FinishPass()
    {
        try
        {
            if (_presentMon is { HasExited: false } && !_presentMon.WaitForExit(5000))
            {
                _presentMon.Kill();
            }
        }
        catch
        {
            // Best effort: a still-running PresentMon only costs the CSV tail.
        }

        var stats = ReadFrameStats(_csvPath);
        if (stats != null)
        {
            _results.Add(new BenchmarkPassResult(
                _currentProfile,
                _request!.Label,
                stats.Value.Frames,
                stats.Value.AvgFps,
                stats.Value.P99Ms,
                stats.Value.Over20Pct,
                _maxTempC,
                _samples > 0 ? _sumPowerMw / _samples / 1000.0 : 0,
                _samples > 0 ? _sumClockMhz / _samples : 0));

            _log?.Invoke(
                $"Benchmark pass done (profile {_currentProfile}): {stats.Value.AvgFps:F1} fps, " +
                $"p99 {stats.Value.P99Ms:F1} ms, {stats.Value.Over20Pct:F1}% over 20 ms, max {_maxTempC} °C.");
        }
        else
        {
            _log?.Invoke($"Benchmark pass for profile {_currentProfile} produced no frames.");
        }

        _queue.RemoveAt(0);
        if (_queue.Count == 0)
        {
            Finish();
            return;
        }

        _cooldown = true;
        _phaseStartUtc = DateTime.UtcNow;
    }

    private void Finish()
    {
        LastSummary = new BenchmarkSummary(_request!.Label, DateTime.UtcNow, _results.ToList());
        StatusText = $"Benchmark '{_request.Label}' finished ({_results.Count} pass(es)).";
        ProgressText = string.Empty;
        _log?.Invoke(StatusText);
        WriteResults(LastSummary);
        _request = null;
    }

    private static string? ResolvePresentMonPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon", "PresentMon-2.5.1-x64.exe"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon", "PresentMon.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void StartPresentMon(int gamePid)
    {
        try
        {
            var exe = ResolvePresentMonPath();
            if (exe == null || gamePid <= 0)
            {
                _log?.Invoke("Benchmark: PresentMon or the game process id is missing; frame times will be skipped.");
                _presentMon = null;
                _csvPath = null;
                return;
            }

            _csvPath = Path.Combine(
                _baseDir,
                $"benchmark-{DateTime.Now:yyyyMMdd-HHmmss}-p{_currentProfile}.csv");

            var total = WarmupSeconds + (_request?.Seconds ?? 60);
            var info = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!
            };

            info.ArgumentList.Add("-process_id");
            info.ArgumentList.Add(gamePid.ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add("-output_file");
            info.ArgumentList.Add(_csvPath);
            info.ArgumentList.Add("-timed");
            info.ArgumentList.Add(total.ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add("-terminate_after_timed");
            info.ArgumentList.Add("-no_console_stats");

            // A leftover ETW session from an aborted run makes PresentMon fail with
            // "a trace session named PresentMon is already running". Use our own name
            // and stop any stale instance of it before starting.
            info.ArgumentList.Add("-session_name");
            info.ArgumentList.Add("IconGridBenchmark");
            info.ArgumentList.Add("-stop_existing_session");

            _presentMon = Process.Start(info);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Benchmark could not start PresentMon: {ex.Message}");
            _presentMon = null;
        }
    }

    private static (int Frames, double AvgFps, double P99Ms, double Over20Pct)? ReadFrameStats(string? csvPath)
    {
        if (csvPath == null || !File.Exists(csvPath))
        {
            return null;
        }

        var lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2)
        {
            return null;
        }

        var header = lines[0].Split(',');
        var msIndex = Array.FindIndex(header, name => name.Trim() == "MsBetweenPresents");
        var timeIndex = Array.FindIndex(header, name => name.Trim() == "TimeInMs");
        if (msIndex < 0 || timeIndex < 0)
        {
            return null;
        }

        var frames = new List<double>();
        var firstTime = -1.0;

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length <= Math.Max(msIndex, timeIndex))
            {
                continue;
            }

            if (!double.TryParse(parts[msIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var frameMs) ||
                !double.TryParse(parts[timeIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var timeMs))
            {
                continue;
            }

            if (firstTime < 0)
            {
                firstTime = timeMs;
            }

            if (timeMs - firstTime < WarmupSeconds * 1000.0)
            {
                continue;
            }

            frames.Add(frameMs);
        }

        if (frames.Count < 30)
        {
            return null;
        }

        var sorted = frames.OrderBy(value => value).ToList();
        var average = frames.Average();
        var p99Index = (int)Math.Clamp(Math.Ceiling(sorted.Count * 0.99) - 1, 0, sorted.Count - 1);
        var over20 = frames.Count(value => value > VsyncFrameMs) * 100.0 / frames.Count;

        return (frames.Count, 1000.0 / average, sorted[p99Index], over20);
    }

    private void WriteResults(BenchmarkSummary summary)
    {
        try
        {
            var path = Path.Combine(_baseDir, "gpu-benchmark-results.json");
            var all = new List<BenchmarkSummary>();

            if (File.Exists(path))
            {
                try
                {
                    all = JsonSerializer.Deserialize<List<BenchmarkSummary>>(File.ReadAllText(path), JsonOptions)
                          ?? new List<BenchmarkSummary>();
                }
                catch
                {
                    all = new List<BenchmarkSummary>();
                }
            }

            all.Add(summary);
            if (all.Count > 20)
            {
                all.RemoveRange(0, all.Count - 20);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(all, JsonOptions));
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Benchmark results could not be saved: {ex.Message}");
        }
    }
}
