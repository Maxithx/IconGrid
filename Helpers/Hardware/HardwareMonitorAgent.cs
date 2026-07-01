using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using IconGrid.Helpers;

namespace IconGrid.Helpers.Hardware;

public static class HardwareMonitorAgent
{
    private const string AgentMutexName = @"Local\IconGrid.HardwareMonitorAgent";
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int Run(string[] args, Action<string>? log = null)
    {
        using var mutex = new Mutex(true, AgentMutexName, out var createdNew);
        if (!createdNew)
        {
            log?.Invoke("Hardware monitor agent is already running.");
            return 0;
        }

        var parentPid = TryReadParentPid(args);
        var configManager = new ConfigManager();
        var statePath = Path.Combine(configManager.BaseDirectory, "monitor-state.json");
        var tempPath = statePath + ".tmp";

        try
        {
            Directory.CreateDirectory(configManager.BaseDirectory);
            using var collector = new HardwareSnapshotCollector();

            while (ParentIsAlive(parentPid))
            {
                var snapshot = collector.Capture();
                WriteSnapshot(statePath, tempPath, snapshot);
                Thread.Sleep(UpdateInterval);
            }

            log?.Invoke("Hardware monitor agent exited because parent process ended.");
            return 0;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Hardware monitor agent failed: {ex}");
            return -1;
        }
    }

    private static int? TryReadParentPid(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--parent-pid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(args[index + 1], out var pid) && pid > 0)
            {
                return pid;
            }
        }

        return null;
    }

    private static bool ParentIsAlive(int? parentPid)
    {
        if (!parentPid.HasValue)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(parentPid.Value);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteSnapshot(string statePath, string tempPath, HardwareMonitorSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, statePath, overwrite: true);
    }
}
