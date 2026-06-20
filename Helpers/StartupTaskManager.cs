using System;
using System.Diagnostics;

namespace IconGrid.Helpers;

public static class StartupTaskManager
{
    private const string TaskName = "IconGrid";

    public static bool Register(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        var args = $"/Create /RL HIGHEST /SC ONLOGON /TN \"{TaskName}\" /TR \"{exePath}\" /F";
        return RunSchtasks(args);
    }

    public static bool Unregister()
    {
        var args = $"/Delete /TN \"{TaskName}\" /F";
        return RunSchtasks(args);
    }

    private static bool RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            if (process.ExitCode != 0)
            {
                var output = process.StandardError.ReadToEnd();
                Debug.WriteLine($"schtasks failed ({arguments}): {output}");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to run schtasks: {ex}");
            return false;
        }
    }
}
