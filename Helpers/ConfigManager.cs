using System;
using System.IO;
using System.Linq;
using IconGrid.Models;
using Newtonsoft.Json;

namespace IconGrid.Helpers;

public class ConfigManager
{
    private const string PrimaryFolderName = "IconGrid";
    private const string LegacyFolderName = "DesktopLauncherBar";

    public string BaseDirectory { get; }
    public string ConfigPath { get; }

    public ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        BaseDirectory = Path.Combine(appData, PrimaryFolderName);
        var legacyDirectory = Path.Combine(appData, LegacyFolderName);

        // Ensure base dir exists and migrate any legacy data if missing.
        Directory.CreateDirectory(BaseDirectory);
        if (Directory.Exists(legacyDirectory))
        {
            foreach (var fileName in new[] { "config.json", "items.json" })
            {
                CopyIfMissing(Path.Combine(legacyDirectory, fileName), Path.Combine(BaseDirectory, fileName));
            }

            // Migrate icon pack if the new location is missing or empty.
            var legacyPack = Path.Combine(legacyDirectory, "IconPack");
            var targetPack = Path.Combine(BaseDirectory, "IconPack");
            CopyDirectoryIfMissing(legacyPack, targetPack);
        }

        ConfigPath = Path.Combine(BaseDirectory, "config.json");
    }

    public ConfigModel LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = ConfigModel.CreateDefault();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonConvert.DeserializeObject<ConfigModel>(json) ?? ConfigModel.CreateDefault();
            return config;
        }
        catch
        {
            var fallback = ConfigModel.CreateDefault();
            SaveConfig(fallback);
            return fallback;
        }
    }

    public void SaveConfig(ConfigModel config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }

    private static void CopyIfMissing(string sourcePath, string targetPath)
    {
        try
        {
            if (!File.Exists(sourcePath) || File.Exists(targetPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: false);
        }
        catch
        {
            // best-effort migration; ignore errors
        }
    }

    private static void CopyDirectoryIfMissing(string sourceDir, string targetDir)
    {
        try
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            var targetHasFiles = Directory.Exists(targetDir) &&
                                 Directory.EnumerateFileSystemEntries(targetDir).Any();
            if (targetHasFiles)
            {
                return;
            }

            foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, targetDir));
            }

            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var targetPath = filePath.Replace(sourceDir, targetDir);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(filePath, targetPath, overwrite: false);
            }
        }
        catch
        {
            // best-effort migration; ignore errors
        }
    }
}
