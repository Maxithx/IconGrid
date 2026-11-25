using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;

namespace IconGrid.Helpers.Maintenance
{
    public enum CleanupTargetKind
    {
        NvidiaDxCache,
        NvidiaGlCache,
        DirectXShaderCache,
        IconGridTraceLog,
        IconGridErrorLog,
        IconGridAfterburnerBackups,
        IconGridConfigBackups
    }

    public sealed record CleanupTargetDefinition(
        CleanupTargetKind Kind,
        string Path,
        bool Recursive,
        IReadOnlyList<string> Patterns);

    public sealed record CleanupFileCandidate(
        CleanupTargetKind Kind,
        string Path,
        long SizeBytes,
        DateTime LastWriteTimeUtc);

    public sealed record CleanupDeleteResult(
        int DeletedCount,
        long DeletedBytes,
        IReadOnlyList<string> Errors,
        IReadOnlyCollection<string> DeletedPaths);

    /// <summary>
    /// Allowlisted cache and maintenance files only. Never scans or deletes user
    /// configuration, shortcuts, saved layouts, or game data.
    /// </summary>
    public static class CleanupService
    {
        public static IReadOnlyList<CleanupTargetDefinition> GetTargets()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return new[]
            {
                new CleanupTargetDefinition(
                    CleanupTargetKind.NvidiaDxCache,
                    Path.Combine(localAppData, "NVIDIA", "DXCache"),
                    Recursive: true,
                    Patterns: new[] { "*" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.NvidiaGlCache,
                    Path.Combine(localAppData, "NVIDIA", "GLCache"),
                    Recursive: true,
                    Patterns: new[] { "*" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.DirectXShaderCache,
                    Path.Combine(localAppData, "D3DSCache"),
                    Recursive: true,
                    Patterns: new[] { "*" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.IconGridTraceLog,
                    appData,
                    Recursive: false,
                    Patterns: new[] { "trace.log" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.IconGridErrorLog,
                    appData,
                    Recursive: false,
                    Patterns: new[] { "error.log" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.IconGridAfterburnerBackups,
                    appData,
                    Recursive: false,
                    Patterns: new[] { "afterburner-profile-backup-*.cfg" }),
                new CleanupTargetDefinition(
                    CleanupTargetKind.IconGridConfigBackups,
                    appData,
                    Recursive: false,
                    Patterns: new[] { "config.json.bak-*" })
            };
        }

        public static IReadOnlyList<CleanupFileCandidate> Scan(
            IEnumerable<CleanupTargetDefinition> targets,
            CancellationToken cancellationToken)
        {
            var results = new List<CleanupFileCandidate>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(target.Path))
                    {
                        AddCandidate(target, target.Path, results, seenPaths, cancellationToken);
                        continue;
                    }

                    if (!Directory.Exists(target.Path))
                    {
                        continue;
                    }

                    var options = new EnumerationOptions
                    {
                        RecurseSubdirectories = target.Recursive,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };

                    foreach (var path in Directory.EnumerateFiles(target.Path, "*", options))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddCandidate(target, path, results, seenPaths, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // A single inaccessible cache folder must not stop the scan.
                }
            }

            return results;
        }

        public static CleanupDeleteResult Delete(
            IEnumerable<CleanupFileCandidate> files,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var deletedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deletedBytes = 0L;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!File.Exists(file.Path))
                    {
                        continue;
                    }

                    File.Delete(file.Path);
                    deletedPaths.Add(file.Path);
                    deletedBytes += file.SizeBytes;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(file.Path)}: {ex.Message}");
                }
            }

            return new CleanupDeleteResult(
                deletedPaths.Count,
                deletedBytes,
                errors,
                deletedPaths);
        }

        private static void AddCandidate(
            CleanupTargetDefinition target,
            string path,
            ICollection<CleanupFileCandidate> results,
            ISet<string> seenPaths,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(path);
            if (!target.Patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true)))
            {
                return;
            }

            if (!seenPaths.Add(path))
            {
                return;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 0)
                {
                    results.Add(new CleanupFileCandidate(
                        target.Kind,
                        info.FullName,
                        info.Length,
                        info.LastWriteTimeUtc));
                }
            }
            catch
            {
                // Skip files that disappear or become inaccessible during the scan.
            }
        }
    }
}
