using System;
using System.IO;

namespace IconGrid.Helpers.Hardware;

public static class FpsEtwProbeAgent
{
    public static int Run(string[] args, Action<string>? log = null)
    {
        var outputPath = TryReadArgument(args, "--probe-output");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            log?.Invoke("Elevated ETW probe missing --probe-output path.");
            return -1;
        }

        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var result = EtwFpsProvider.RunStartupProbe(log);
            File.WriteAllText(outputPath, result);
            log?.Invoke($"Elevated ETW probe wrote result to {outputPath}.");
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(outputPath, ex.ToString());
            }
            catch
            {
            }

            log?.Invoke($"Elevated ETW probe failed: {ex}");
            return -1;
        }
    }

    private static string? TryReadArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
