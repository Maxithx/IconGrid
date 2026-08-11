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

        private async Task CopySingleFileAsync(
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