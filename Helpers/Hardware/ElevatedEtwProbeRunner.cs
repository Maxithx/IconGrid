using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using IconGrid.Helpers.Settings;

namespace IconGrid.Helpers.Hardware;

public static class ElevatedEtwProbeRunner
{
    private const string ProbeArgument = "--fps-etw-probe-agent";
    private const string ProbeOutputArgument = "--probe-output";

    public static async Task<string> RunAsync(Func<string>? executablePathProvider = null, Action<string>? log = null)
    {
        var executablePath = executablePathProvider?.Invoke() ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("IconGrid executable path is missing for elevated ETW probe.");
        }

        var configManager = new ConfigManager();
        var outputPath = Path.Combine(configManager.BaseDirectory, $"elevated-etw-probe-{Environment.ProcessId}.txt");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"{ProbeArgument} {ProbeOutputArgument} \"{outputPath}\"",
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = "runas"
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start elevated ETW probe: {ex.Message}", ex);
        }

        if (process == null)
        {
            throw new InvalidOperationException("Elevated ETW probe did not start.");
        }

        log?.Invoke($"Started elevated ETW probe PID={process.Id} Output={outputPath}");

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(outputPath))
            {
                var content = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
                try
                {
                    File.Delete(outputPath);
                }
                catch
                {
                }

                return content;
            }

            if (process.HasExited && process.ExitCode != 0)
            {
                break;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        if (process.HasExited)
        {
            throw new InvalidOperationException($"Elevated ETW probe exited with code {process.ExitCode} before writing output.");
        }

        throw new TimeoutException("Timed out waiting for elevated ETW probe output.");
    }
}
