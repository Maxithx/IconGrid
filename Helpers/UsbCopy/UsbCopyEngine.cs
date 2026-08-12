using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Arguments for throughput/pipeline progress events.
    /// </summary>
    public sealed class UsbCopyProgressArgs : EventArgs
    {
        public required string FileName { get; init; }

        public long FileBytesCopied { get; init; }

        public long FileTotalBytes { get; init; }

        public long TotalBytesCopied { get; init; }

        public long TotalBytes { get; init; }

        public double BytesPerSecond { get; init; }

        public double AverageBytesPerSecond { get; init; }

        public double PeakBytesPerSecond { get; init; }

        public TimeSpan Elapsed { get; init; }

        public string? CurrentPipelineStage { get; init; }

        public int FilesCompleted { get; init; }

        public int FilesTotal { get; init; }
    }

    /// <summary>
    /// Buffer-optimized file copy pipeline. Uses a manual buffer loop with
    /// FileOptions.SequentialScan on read and WriteThrough on write so the USB
    /// stick receives data continuously instead of throttling through the
    /// Windows default cache behavior.
    /// </summary>
    public sealed class UsbCopyEngine
    {
        private readonly UsbCopyLogger _logger;

        public UsbCopyEngine(UsbCopyLogger? logger = null)
        {
            _logger = logger ?? new UsbCopyLogger();
        }

        public event EventHandler<UsbCopyProgressArgs>? Progress;

        /// <summary>
        /// Allows co-operating services (e.g. MultiWorkerCopyService) to publish
        /// consolidated throughput through the engine's public Progress event.
        /// </summary>
        internal void ReportProgress(UsbCopyProgressArgs args)
        {
            Progress?.Invoke(this, args);
        }

        public event EventHandler<string>? PipelineEvent;

        public event EventHandler<string>? Error;

        /// <summary>
        /// Copies files to the target directory with continuous throughput reporting.
        /// Returns the number of bytes copied.
        /// </summary>
        public async Task<long> CopyFilesAsync(
            IReadOnlyList<string> sourceFiles,
            string targetDirectory,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateCopyLogFile();
            var totalBytes = 0L;
            var totalCopied = 0L;
            var peak = 0d;
            var start = DateTime.UtcNow;

            _logger.AppendTimestamped(logPath, $"COPY START device={GetTargetDeviceLabel(targetDirectory)} buffer={bufferSize} files={sourceFiles.Count}");
            foreach (var file in sourceFiles)
            {
                totalBytes += new FileInfo(file).Length;
            }

            var filesCompleted = 0;
            foreach (var source in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(source);
                var destination = Path.Combine(targetDirectory, fileInfo.Name);
                var fileStart = DateTime.UtcNow;

                _logger.AppendTimestamped(logPath, $"FILE START {fileInfo.Name} size={fileInfo.Length}");
                PipelineEvent?.Invoke(this, $"read:{fileInfo.Name}");

                await CopySingleFileAsync(source, destination, fileInfo.Length, bufferSize, logPath,
                    (fileCopied) =>
                    {
                        var elapsed = DateTime.UtcNow - start;
                        var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                        var instantaneous = fileCopied / Math.Max((DateTime.UtcNow - fileStart).TotalSeconds, 0.001);
                        var average = totalCopied / seconds;
                        if (instantaneous > peak)
                        {
                            peak = instantaneous;
                        }

                        if (fileCopied < fileInfo.Length)
                        {
                            PipelineEvent?.Invoke(this, $"read:{fileInfo.Name}");
                        }
                        else
                        {
                            PipelineEvent?.Invoke(this, $"flush:{fileInfo.Name}");
                        }

                        Progress?.Invoke(this, new UsbCopyProgressArgs
                        {
                            FileName = fileInfo.Name,
                            FileBytesCopied = fileCopied,
                            FileTotalBytes = fileInfo.Length,
                            TotalBytesCopied = totalCopied,
                            TotalBytes = totalBytes,
                            BytesPerSecond = instantaneous,
                            AverageBytesPerSecond = average,
                            PeakBytesPerSecond = peak,
                            Elapsed = elapsed,
                            FilesCompleted = filesCompleted,
                            FilesTotal = sourceFiles.Count
                        });
                    },
                    cancellationToken);

                totalCopied += fileInfo.Length;
                filesCompleted++;
                _logger.AppendTimestamped(logPath, $"FILE DONE {fileInfo.Name} files_completed={filesCompleted} total_bytes={totalCopied}");
                PipelineEvent?.Invoke(this, $"flush:{fileInfo.Name}");
            }

            var finalElapsed = DateTime.UtcNow - start;
            _logger.AppendTimestamped(
                logPath,
                $"COPY DONE bytes={totalCopied} elapsed_ms={finalElapsed.TotalMilliseconds:0} avg_mib_s={UsbCopyLogger.FormatMiBPerSecond(totalCopied / Math.Max(finalElapsed.TotalSeconds, 0.001))} peak_mib_s={UsbCopyLogger.FormatMiBPerSecond(peak)}");

            return totalCopied;
        }

        /// <summary>
        /// Copies a set of files (or directory trees) into a target root,
        /// preserving each item's relative path under the source root. This is
        /// the engine-side counterpart of the dual-pane browser: the active pane
        /// selects files/directories, and they land in the opposite pane's
        /// current directory with their relative structure intact.
        ///
        /// Returns the number of bytes copied.
        /// </summary>
        public Task<long> CopyPathsAsync(
            string sourceRoot,
            string targetRoot,
            IReadOnlyList<FileBrowserEntry> entries,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            return CopyPathsAsync(sourceRoot, targetRoot, entries, bufferSize, workerCount: 1, cancellationToken);
        }

        /// <summary>
        /// Copies a set of files (or directory trees) into a target root,
        /// preserving each item's relative path under the source root, using the
        /// FastCopy-inspired MultiWorkerCopyService (20-file threshold, small-file
        /// ZIP packing, large-file chunk-split, parallel workers).
        /// </summary>
        public async Task<long> CopyPathsAsync(
            string sourceRoot,
            string targetRoot,
            IReadOnlyList<FileBrowserEntry> entries,
            int bufferSize,
            int workerCount,
            CancellationToken cancellationToken)
        {
            return await CopyPathsAsync(sourceRoot, targetRoot, entries, bufferSize, workerCount, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies a set of files (or directory trees) into a target root,
        /// preserving each item's relative path under the source root, using the
        /// FastCopy-inspired MultiWorkerCopyService (20-file threshold, small-file
        /// ZIP packing, large-file chunk-split, parallel workers).
        /// The optional conflict resolver is consulted when a destination file
        /// already exists (Overwrite / OverwriteAll / Skip / SkipAll).
        /// </summary>
        public async Task<long> CopyPathsAsync(
            string sourceRoot,
            string targetRoot,
            IReadOnlyList<FileBrowserEntry> entries,
            int bufferSize,
            int workerCount,
            IOverwriteConflictResolver? conflictResolver,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateCopyLogFile();

            // Scan the source tree on a background thread so a large folder
            // (e.g. thousands of small web files) does not freeze the UI before
            // the first byte is copied.
            var files = await Task.Run(() => ExpandToFiles(entries), cancellationToken).ConfigureAwait(false);

            var service = new MultiWorkerCopyService(
                this,
                msg => _logger.AppendTimestamped(logPath, msg),
                stage => PipelineEvent?.Invoke(this, stage),
                conflictResolver);

            // The ENTIRE copy pipeline (ZIP packing, unpacking, chunk splitting,
            // per-file IO) runs on the thread pool so copy work never captures the
            // UI SynchronizationContext and freezes the page at copy start.
            _logger.AppendTimestamped(logPath, $"PATH COPY START src={sourceRoot} dst={targetRoot} buffer={bufferSize} workers={workerCount} files={files.Count}");
            var copied = await Task.Run(
                () => service.CopyAsync(files, sourceRoot, targetRoot, bufferSize, workerCount, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            _logger.AppendTimestamped(logPath, $"PATH COPY DONE bytes={copied}");

            return copied;
        }
        private static List<(string Source, long Size)> ExpandToFiles(IReadOnlyList<FileBrowserEntry> entries)
        {
            var files = new List<(string, long)>();
            foreach (var entry in entries)
            {
                if (!entry.IsDirectory)
                {
                    try
                    {
                        files.Add((entry.FullPath, new FileInfo(entry.FullPath).Length));
                    }
                    catch (Exception)
                    {
                        // Skip unreadable file.
                    }
                    continue;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(entry.FullPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            files.Add((file, new FileInfo(file).Length));
                        }
                        catch (Exception)
                        {
                            // Skip unreadable file.
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip unreadable directory tree.
                }
            }

            return files;
        }

        private static bool IsUnder(string root, string path)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }

        internal async Task CopySingleFileAsync(
            string source,
            string destination,
            long fileLength,
            int bufferSize,
            string logPath,
            Action<long> onProgress,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var copied = 0L;

            var options = FileOptions.SequentialScan;
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, options))
            await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
            {
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    copied += read;
                    onProgress(copied);
                }
            }

            if (copied != fileLength)
            {
                Error?.Invoke(this, $"short copy {source}: {copied}/{fileLength}");
            }
        }

        private static string GetTargetDeviceLabel(string targetDirectory)
        {
            try
            {
                var root = Path.GetPathRoot(targetDirectory);
                return string.IsNullOrEmpty(root) ? targetDirectory : root;
            }
            catch (Exception)
            {
                return targetDirectory;
            }
        }
    }
}