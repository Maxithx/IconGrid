using System;
using System.IO;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;
using IconGrid.Models;

namespace IconGrid.Helpers
{
    public static class ShortcutHelper
    {
        /// <summary>
        /// Returnerer true hvis filen er en Windows-genvej (.lnk).
        /// </summary>
        public static bool IsShortcut(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return string.Equals(
                Path.GetExtension(path),
                ".lnk",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returnerer true hvis filen er en eksekverbar (.exe).
        /// </summary>
        public static bool IsExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return string.Equals(
                Path.GetExtension(path),
                ".exe",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returnerer true hvis stien peger på noget, vi kan pinne som en launcher-genvej.
        /// Det omfatter mapper og eksisterende filer som .exe, .lnk, .txt, .url osv.
        /// </summary>
        public static bool IsSupportedLauncherFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (Directory.Exists(path))
                return true;

            return File.Exists(path);
        }

        /// <summary>
        /// Opretter et LauncherItem ud fra en filsti og den aktuelle kategori/tab.
        /// Vi bruger selve filstien som Path (Process.Start håndterer .lnk fint).
        /// </summary>
        public static LauncherItem? CreateLauncherItemFromFile(string filePath, string category)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            if (!IsSupportedLauncherFile(filePath))
                return null;

            var exists = File.Exists(filePath) || Directory.Exists(filePath);
            if (!exists)
                return null;

            var isDirectory = Directory.Exists(filePath);
            string displayName;
            int iconIndex = 0;
            string iconPath = filePath;
            string launcherPath = filePath;
            string? arguments = null;

            if (isDirectory)
            {
                displayName = Path.GetFileName(filePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var folderIcon = IconHelper.TryGetFolderIconFromDesktopIni(filePath);
                if (folderIcon.HasValue && !string.IsNullOrWhiteSpace(folderIcon.Value.Path))
                {
                    iconPath = folderIcon.Value.Path!;
                    iconIndex = folderIcon.Value.Index;
                }
            }
            else
            {
                displayName = Path.GetFileNameWithoutExtension(filePath);
                if (IsShortcut(filePath) && TryResolveShortcut(filePath, out var shortcut))
                {
                    if (!string.IsNullOrWhiteSpace(shortcut.TargetPath))
                    {
                        launcherPath = shortcut.TargetPath;
                    }

                    if (!string.IsNullOrWhiteSpace(shortcut.Arguments))
                    {
                        arguments = shortcut.Arguments;
                    }

                    if (!string.IsNullOrWhiteSpace(shortcut.IconPath))
                    {
                        iconPath = shortcut.IconPath;
                        iconIndex = shortcut.IconIndex;
                    }
                    else if (!string.IsNullOrWhiteSpace(shortcut.TargetPath))
                    {
                        iconPath = shortcut.TargetPath;
                    }
                }
            }

            var item = new LauncherItem
            {
                DisplayName = displayName,
                Path        = launcherPath,
                Arguments   = arguments,
                Category    = category,
                IconPath    = iconPath,
                IconIndex   = iconIndex
            };

            return item;
        }

        private static bool TryResolveShortcut(string shortcutPath, out ShortcutTarget shortcut)
        {
            shortcut = default;

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return false;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic link = shell.CreateShortcut(shortcutPath);

                string? targetPath = link.TargetPath as string;
                string? shortcutArguments = link.Arguments as string;
                string? iconLocation = link.IconLocation as string;
                var (resolvedIconPath, resolvedIconIndex) = ParseIconLocation(iconLocation);

                shortcut = new ShortcutTarget(targetPath, shortcutArguments, resolvedIconPath, resolvedIconIndex);
                return !string.IsNullOrWhiteSpace(targetPath);
            }
            catch
            {
                return false;
            }
        }

        private static (string? Path, int Index) ParseIconLocation(string? iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation))
            {
                return (null, 0);
            }

            var parts = iconLocation.Split(',');
            var path = parts[0].Trim();
            var index = 0;

            if (parts.Length > 1)
            {
                _ = int.TryParse(parts[^1].Trim(), out index);
            }

            return (string.IsNullOrWhiteSpace(path) ? null : path, index);
        }

        private readonly record struct ShortcutTarget(string? TargetPath, string? Arguments, string? IconPath, int IconIndex);
    }
}
