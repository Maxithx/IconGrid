using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using IconGrid.Helpers.Settings;
using IconGrid.Models;

namespace IconGrid.Helpers.Hardware;

internal sealed class NativeFpsAgentRunner : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan StateMaxAge = TimeSpan.FromSeconds(10);
    private readonly string _statePath;
    private readonly Action<string>? _log;
    private readonly ConfigManager _configManager = new();
    private Process? _process;

    public NativeFpsAgentRunner(string statePath, Action<string>? log = null)
    {
        _statePath = statePath;
        _log = log;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(TryResolveExecutablePath());

    public bool Start(int? parentPid)
    {
        var executablePath = TryResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            Trace("Native FPS agent executable was not found.");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);

            var arguments = $"--state-path \"{_statePath}\"";
            if (parentPid.HasValue && parentPid.Value > 0)
            {
                arguments += $" --parent-pid {parentPid.Value}";
            }

            var fpsTarget = LoadFpsTarget();
            arguments += BuildTargetArguments(fpsTarget);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
                }
            };

            var started = _process.Start();
            Trace(
                $"Native FPS agent start requested. Success={started}, Path={executablePath}, TargetExe={fpsTarget.ExecutableName ?? "null"}, TargetPath={fpsTarget.ResolvedExecutablePath ?? "null"}, RootPid={fpsTarget.RootProcessId?.ToString() ?? "null"}, RootStartFileTime={fpsTarget.RootProcessStartFileTimeUtc?.ToString() ?? "null"}, WorkingDir={fpsTarget.WorkingDirectory ?? "null"}");
            return started;
        }
        catch (Exception ex)
        {
            Trace($"Native FPS agent failed to start: {ex.Message}");
            return false;
        }
    }

    public bool Restart(int? parentPid)
    {
        DisposeProcess();
        return Start(parentPid);
    }

    public static string CreateTargetSignature(FpsTargetConfig? fpsTarget)
    {
        if (fpsTarget == null)
        {
            return string.Empty;
        }

        return string.Join(
            "|",
            fpsTarget.DisplayName ?? string.Empty,
            fpsTarget.LauncherPath ?? string.Empty,
            fpsTarget.ResolvedExecutablePath ?? string.Empty,
            fpsTarget.ExecutableName ?? string.Empty,
            fpsTarget.Arguments ?? string.Empty,
            fpsTarget.WorkingDirectory ?? string.Empty,
            fpsTarget.RootProcessId?.ToString() ?? string.Empty,
            fpsTarget.RootProcessStartFileTimeUtc?.ToString() ?? string.Empty,
            fpsTarget.LaunchCapturedFileTimeUtc?.ToString() ?? string.Empty);
    }

    private static string BuildTargetArguments(FpsTargetConfig fpsTarget)
    {
        var arguments = string.Empty;
            if (!string.IsNullOrWhiteSpace(fpsTarget.ExecutableName))
            {
                arguments += $" --target-exe \"{fpsTarget.ExecutableName}\"";
            }

            if (!string.IsNullOrWhiteSpace(fpsTarget.ResolvedExecutablePath))
            {
                arguments += $" --target-path \"{fpsTarget.ResolvedExecutablePath}\"";
            }

            if (!string.IsNullOrWhiteSpace(fpsTarget.WorkingDirectory))
            {
                arguments += $" --working-dir \"{fpsTarget.WorkingDirectory}\"";
            }

            if (fpsTarget.RootProcessId.HasValue && fpsTarget.RootProcessId.Value > 0)
            {
                arguments += $" --root-pid {fpsTarget.RootProcessId.Value}";
            }

            if (fpsTarget.RootProcessStartFileTimeUtc.HasValue && fpsTarget.RootProcessStartFileTimeUtc.Value > 0)
            {
                arguments += $" --root-start-filetime {fpsTarget.RootProcessStartFileTimeUtc.Value}";
            }

            if (fpsTarget.LaunchCapturedFileTimeUtc.HasValue && fpsTarget.LaunchCapturedFileTimeUtc.Value > 0)
            {
                arguments += $" --launch-filetime {fpsTarget.LaunchCapturedFileTimeUtc.Value}";
            }

        return arguments;
    }

    public NativeFpsAgentState? ReadState()
    {
        try
        {
            NativeFpsAgentState? fileState = null;
            if (!File.Exists(_statePath))
            {
                return ReadSharedMemoryState(null);
            }

            var info = new FileInfo(_statePath);
            if (DateTime.UtcNow - info.LastWriteTimeUtc > StateMaxAge)
            {
                return ReadSharedMemoryState(null);
            }

            var json = File.ReadAllText(_statePath);
            fileState = JsonSerializer.Deserialize<NativeFpsAgentState>(json, JsonOptions);
            if (fileState == null || DateTime.UtcNow - fileState.CapturedAtUtc > StateMaxAge)
            {
                fileState = null;
            }

            return ReadSharedMemoryState(fileState);
        }
        catch (Exception ex)
        {
            Trace($"Native FPS agent state read failed: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        DisposeProcess();
    }

    private string? TryResolveExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Tools", "FpsAgent", "IconGridFpsAgent.exe"),
            Path.Combine(AppContext.BaseDirectory, "Native", "FpsAgent", "bin", "Debug", "IconGridFpsAgent.exe"),
            Path.Combine(AppContext.BaseDirectory, "Native", "FpsAgent", "bin", "Release", "IconGridFpsAgent.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Native", "FpsAgent", "bin", "Debug", "IconGridFpsAgent.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Native", "FpsAgent", "bin", "Release", "IconGridFpsAgent.exe"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private FpsTargetConfig LoadFpsTarget()
    {
        try
        {
            var config = _configManager.LoadConfig();
            return config.FpsTarget ?? new FpsTargetConfig();
        }
        catch (Exception ex)
        {
            Trace($"Failed to load FPS target config: {ex.Message}");
            return new FpsTargetConfig();
        }
    }

    private void Trace(string message)
    {
        _log?.Invoke($"[NativeFpsAgentRunner] {message}");
    }

    private static NativeFpsAgentState? ReadSharedMemoryState(NativeFpsAgentState? fallbackState)
    {
        var liveState = NativeFpsSharedMemory.TryRead();
        if (!liveState.HasValue)
        {
            return fallbackState;
        }

        if (DateTime.UtcNow - liveState.Value.CapturedAtUtc > StateMaxAge)
        {
            return fallbackState;
        }

        if (fallbackState == null)
        {
            return new NativeFpsAgentState
            {
                CapturedAtUtc = liveState.Value.CapturedAtUtc,
                FpsValue = liveState.Value.FpsValue,
                FpsStatus = liveState.Value.FpsValue.HasValue ? Math.Round(liveState.Value.FpsValue.Value).ToString("F0") : "--",
                FpsSource = "NativeFpsAgent",
                TargetPid = liveState.Value.TargetPid,
                EtwRunning = liveState.Value.EtwRunning
            };
        }

        if (liveState.Value.CapturedAtUtc >= fallbackState.CapturedAtUtc)
        {
            fallbackState.CapturedAtUtc = liveState.Value.CapturedAtUtc;
            fallbackState.FpsValue = liveState.Value.FpsValue;
            fallbackState.FpsStatus = liveState.Value.FpsValue.HasValue ? Math.Round(liveState.Value.FpsValue.Value).ToString("F0") : "--";
            fallbackState.TargetPid = liveState.Value.TargetPid;
            fallbackState.EtwRunning = liveState.Value.EtwRunning;
        }

        return fallbackState;
    }

    private void DisposeProcess()
    {
        if (_process == null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
