using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IconGrid.Helpers.UsbCopy;

namespace IconGrid.BenchmarkRunner
{
    /// <summary>
    /// Synthetic copy benchmark runner. Creates an incompressible test payload
    /// (mix of many small files + larger files, mirroring real web-site content)
    /// on the target drive and copies it back-and-forth through the exact same
    /// production pipeline (UsbCopyEngine.CopyPathsAsync -> MultiWorkerCopyService).
    ///
    /// Usage:
    ///   BenchmarkRunner.exe --size 100            # 100 MB (also: 300|500|1000)
    ///                      [--drive H:\]          # default: smallest removable/fixed drive != C:
    ///                      [--workers 4]
    ///                      [--buffer 1048576]
    ///
    /// Output:
    ///   - live log:   %APPDATA%\IconGrid\logs\fastusbcopy\benchmark-live.log
    ///   - summary:    %APPDATA%\IconGrid\logs\fastusbcopy\benchmark-summary.json
    /// </summary>
    internal static class Program
    {
        private static readonly string LogRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IconGrid", "logs", "fastusbcopy");

        private const long MiB = 1024L * 1024L;

        private static readonly Random Rng = new(1337);

        private static async Task<int> Main(string[] args)
        {
            try
            {
                Directory.CreateDirectory(LogRoot);
                var liveLog = Path.Combine(LogRoot, "benchmark-live.log");
                var summaryPath = Path.Combine(LogRoot, "benchmark-summary.json");

                var sizeMb = ParseInt(args, "--size", 100);
                var workers = ParseInt(args, "--workers", 4);
                var buffer = ParseInt(args, "--buffer", 1 * 1024 * 1024);
                var drive = ParseString(args, "--drive", PickTargetDrive());
                if (string.IsNullOrEmpty(drive) || !Directory.Exists(drive))
                {
                    Console.WriteLine($"ERROR: drive not found: '{drive}'");
                    return 2;
                }

                File.WriteAllText(liveLog, $"=== BENCHMARK START size={sizeMb}MB workers={workers} buffer={buffer} drive={drive} time={DateTime.Now:HH:mm:ss} ===\n", Encoding.UTF8);
                Console.WriteLine($"Creating {sizeMb} MB incompressible test payload on {drive} ...");

                var targetRoot = Path.Combine(drive, "icongrid-bench");
                Directory.CreateDirectory(targetRoot);

                // Build a realistic web-site-like payload: ~70% small files (1-20KB),
                // ~30% larger files, total ~ sizeMb MB (incompressible).
                var pending = sizeMb * MiB;
                var payloadDir = Path.Combine(targetRoot, "payload");
                Directory.CreateDirectory(payloadDir);
                var files = new List<string>();
                while (pending > 0)
                {
                    var roll = Rng.NextDouble();
                    long size;
                    if (roll < 0.70)
                    {
                        size = 1024 + Rng.Next(19 * 1024);          // 1-20 KB small files
                    }
                    else if (roll < 0.90)
                    {
                        size = 64 * 1024 + Rng.Next(1024 * 1024);   // 64 KB - 1 MB
                    }
                    else
                    {
                        size = 2 * MiB + Rng.Next((int)(6 * MiB));    // 2-8 MB
                    }

                    size = Math.Min(size, pending);
                    pending -= size;

                    var path = Path.Combine(payloadDir, $"file-{files.Count:D5}.dat");
                    WriteIncompressible(path, size);
                    files.Add(path);
                    if (files.Count % 500 == 0)
                    {
                        Console.WriteLine($"  {files.Count} files created, {pending / MiB} MB left ...");
                    }
                }

                Console.WriteLine($"Payload ready: {files.Count} files ({sizeMb} MB). Starting benchmark copy ...");

                // Copy the payload back to a second folder on the same drive (USB->SSD/USB->USB
                // realism is provided by targeting a different drive with --drive, otherwise this
                // measures the drive's sequential write/read path through our pipeline).
                var copyTarget = Path.Combine(targetRoot, "output");
                Directory.CreateDirectory(copyTarget);

                var engine = new UsbCopyEngine();
                var stopwatch = Stopwatch.StartNew();
                var cancelled = new CancellationTokenSource();

                var bytes = await engine.CopyPathsAsync(
                    payloadDir,
                    copyTarget,
                    new List<FileBrowserEntry>
                    {
                        new()
                        {
                            Name = "payload",
                            FullPath = payloadDir,
                            IsDirectory = true,
                            SizeBytes = 0,
                            ModifiedTime = DateTime.Now
                        }
                    },
                    buffer,
                    workers,
                    cancelled.Token);

                stopwatch.Stop();
                var elapsed = stopwatch.Elapsed;
                var mibs = bytes / (1024.0 * 1024.0) / Math.Max(elapsed.TotalSeconds, 0.001);

                var summary = new
                {
                    Timestamp = DateTime.Now,
                    SizeMb = sizeMb,
                    Workers = workers,
                    BufferSize = buffer,
                    Drive = drive,
                    Files = files.Count,
                    BytesCopied = bytes,
                    ElapsedSeconds = Math.Round(elapsed.TotalSeconds, 2),
                    AverageMiBs = Math.Round(mibs, 2),
                    Source = payloadDir,
                    Destination = copyTarget
                };

                File.AppendAllText(liveLog, $"=== BENCHMARK DONE bytes={bytes} elapsed_s={elapsed.TotalSeconds:0.0} avg_mib_s={mibs:0.00} ===\n", Encoding.UTF8);
                File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine($"DONE {sizeMb} MB -> {mibs:0.00} MiB/s avg  ({elapsed.TotalSeconds:0.0}s, {bytes / MiB} MB copied, {files.Count} files)");
                Console.WriteLine($"Summary: {summaryPath}");

                // Clean up payload + output so the drive is left tidy.
                try { Directory.Delete(targetRoot, recursive: true); } catch (Exception) { }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FATAL: {ex}");
                try
                {
                    var liveLog = Path.Combine(LogRoot, "benchmark-live.log");
                    File.AppendAllText(liveLog, $"=== BENCHMARK ERROR {ex.Message} ===\n", Encoding.UTF8);
                }
                catch (Exception)
                {
                    // ignore
                }

                return 1;
            }
        }

        private static void WriteIncompressible(string path, long size)
        {
            const int chunkSize = 256 * 1024;
            var chunk = new byte[chunkSize];
            Rng.NextBytes(chunk);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize, FileOptions.WriteThrough);
            long remaining = size;
            while (remaining > 0)
            {
                var take = (int)Math.Min(chunkSize, remaining);
                fs.Write(chunk, 0, take);
                remaining -= take;
            }
        }

        private static int ParseInt(string[] args, string key, int fallback)
        {
            var idx = Array.IndexOf(args, key);
            if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var value))
            {
                return value;
            }

            return fallback;
        }

        private static string? ParseString(string[] args, string key, string? fallback)
        {
            var idx = Array.IndexOf(args, key);
            if (idx >= 0 && idx + 1 < args.Length)
            {
                return args[idx + 1];
            }

            return fallback;
        }

        private static string? PickTargetDrive()
        {
            // Smallest fixed/removable drive that is not the system drive.
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable && !string.Equals(d.RootDirectory.FullName, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.TotalSize)
                .FirstOrDefault()?.RootDirectory.FullName;
        }
    }
}