using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// FastCopy-inspired multi-worker copy service (principles from FastCopy-master,
    /// translated 1:1 to .NET):
    /// - TRIGGER_MULTI_THREAD_THRESHOLD = 20: fewer than 20 files runs sequentially
    ///   (multi-thread overhead does not pay off).
    /// - SMALL_FILE_SIZE = 20000 (20 KB): files ≤20 KB are packed into a temporary
    ///   ZIP, copied to the destination as ONE stream, then unpacked — this removes
    ///   the thousands-of-small-random-writes bottleneck (the biggest win on USB/HDD
    ///   and for lots of small web files).
    /// - Files >64 MB are chunk-split and written in parallel at positioned offsets
    ///   (destination pre-allocated with File.SetLength).
    /// - Everything else is copied per-file in parallel across the worker pool.
    /// Every file/chunk/package event is logged with a worker id.
    /// </summary>
    public sealed class MultiWorkerCopyService
    {
        public const int MultiThreadThreshold = 20;        // FastCopy TRIGGER_MULTI_THREAD_THRESHHOLD
        public const int SmallFileSize = 20 * 1024;        // FastCopy SMALL_FILE_SIZE (20 KB)
        public const long LargeFileThreshold = 64L * 1024 * 1024; // >64 MB => chunk-split

        private const long ChunkBase = 4L * 1024 * 1024;   // base chunk size for large files

        private readonly UsbCopyEngine _engine;
        private readonly Action<string> _log;
        private readonly Action<string> _pipeline;

        public MultiWorkerCopyService(UsbCopyEngine engine, Action<string> log, Action<string> pipeline)
        {
            _engine = engine;
            _log = log;
            _pipeline = pipeline;
        }

        public async Task<long> CopyAsync(
            IReadOnlyList<(string Source, long Size)> files,
            string sourceRoot,
            string targetRoot,
            int bufferSize,
            int workerCount,
            CancellationToken cancellationToken)
        {
            var totalBytes = files.Sum(f => f.Size);
            var state = new CopyProgressState(totalBytes, files.Count, _engine);
            var effectiveWorkers = Math.Max(1, Math.Min(workerCount, Environment.ProcessorCount));
            var sequential = files.Count < MultiThreadThreshold || effectiveWorkers == 1;

            _log($"MULTI COPY START workers={effectiveWorkers} threshold={MultiThreadThreshold} files={files.Count} sequential={sequential} total_bytes={totalBytes}");

            if (sequential)
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = GetRelative(sourceRoot, file.Source);
                    var dest = Path.Combine(targetRoot, rel);
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    _log($"WORKER 1 FILE START {rel} size={file.Size}");
                    await _engine.CopySingleFileAsync(file.Source, dest, file.Size, bufferSize, null!, _ => { }, cancellationToken);
                    TryPreserveTime(file.Source, dest);
                    state.MarkFileCompleted(rel);
                    _log($"WORKER 1 FILE DONE {rel}");
                }

                _log($"MULTI COPY DONE bytes={state.TotalBytes}");
                return state.TotalBytes;
            }

            var small = files.Where(f => f.Size <= SmallFileSize).ToList();
            var large = files.Where(f => f.Size > LargeFileThreshold).ToList();
            var medium = files.Where(f => f.Size > SmallFileSize && f.Size <= LargeFileThreshold).ToList();

            // 1) Small files -> one temp ZIP -> single stream copy -> unpack.
            if (small.Count > 0)
            {
                await CopySmallBatchAsync(small, sourceRoot, targetRoot, bufferSize, state, cancellationToken);
            }

            // 2) Large files -> parallel chunk-split copies.
            if (large.Count > 0)
            {
                await CopyLargeFilesAsync(large, sourceRoot, targetRoot, bufferSize, effectiveWorkers, state, cancellationToken);
            }

            // 3) Medium files -> parallel per-file copies across the worker pool.
            if (medium.Count > 0)
            {
                await Parallel.ForEachAsync(medium, new ParallelOptions
                {
                    MaxDegreeOfParallelism = effectiveWorkers,
                    CancellationToken = cancellationToken
                }, async (file, token) =>
                {
                    var rel = GetRelative(sourceRoot, file.Source);
                    var dest = Path.Combine(targetRoot, rel);
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var worker = ThreadId();
                    _log($"WORKER {worker} FILE START {rel} size={file.Size}");
                    await _engine.CopySingleFileAsync(file.Source, dest, file.Size, bufferSize, null!, _ => { }, token);
                    TryPreserveTime(file.Source, dest);
                    state.MarkFileCompleted(rel);
                    _log($"WORKER {worker} FILE DONE {rel}");
                });
            }

            _log($"MULTI COPY DONE bytes={state.TotalBytes}");
            return state.TotalBytes;
        }

        private async Task CopySmallBatchAsync(
            IReadOnlyList<(string Source, long Size)> small,
            string sourceRoot,
            string targetRoot,
            int bufferSize,
            CopyProgressState state,
            CancellationToken cancellationToken)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), $"fastcopy-{Guid.NewGuid():N}.zip");
            var destZip = Path.Combine(targetRoot, $".fastcopy-pkg-{Guid.NewGuid():N}.zip");
            var payloadBytes = small.Sum(f => f.Size);

            try
            {
                _log($"WORKER 1 PACK START small_files={small.Count} bytes={payloadBytes}");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in small)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rel = GetRelative(sourceRoot, file.Source).Replace('\\', '/');
                        archive.CreateEntryFromFile(file.Source, rel, CompressionLevel.Fastest);
                    }
                }

                var zipInfo = new FileInfo(zipPath);
                _log($"WORKER 1 PACK ZIP DONE zip_bytes={zipInfo.Length}");

                _pipeline("pack:copy");
                _log("WORKER 1 PACK COPY");
                await _engine.CopySingleFileAsync(zipPath, destZip, zipInfo.Length, bufferSize, null!, _ => { }, cancellationToken);

                _pipeline("pack:unpack");
                _log("WORKER 1 PACK UNPACK");
                ZipFile.ExtractToDirectory(destZip, targetRoot, overwriteFiles: true);

                state.AddSmallBatch(payloadBytes, small.Count, "pack");
                _log($"WORKER 1 PACK DONE small_files={small.Count}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log($"WORKER 1 PACK ERROR {ex.Message}");
                throw;
            }
            finally
            {
                TryDelete(zipPath);
                TryDelete(destZip);
            }
        }

        private async Task CopyLargeFilesAsync(
            IReadOnlyList<(string Source, long Size)> large,
            string sourceRoot,
            string targetRoot,
            int bufferSize,
            int workerCount,
            CopyProgressState state,
            CancellationToken cancellationToken)
        {
            await Parallel.ForEachAsync(large, new ParallelOptions
            {
                MaxDegreeOfParallelism = workerCount,
                CancellationToken = cancellationToken
            }, async (file, token) =>
            {
                var rel = GetRelative(sourceRoot, file.Source);
                var dest = Path.Combine(targetRoot, rel);
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var worker = ThreadId();
                _log($"WORKER {worker} LARGE START {rel} size={file.Size} mode=chunk");
                await CopyFileChunkedAsync(file.Source, dest, file.Size, bufferSize, workerCount, rel, state, token);
                TryPreserveTime(file.Source, dest);
                state.MarkFileCompleted(rel);
                _log($"WORKER {worker} LARGE DONE {rel}");
            });
        }

        private async Task CopyFileChunkedAsync(
            string source,
            string dest,
            long length,
            int bufferSize,
            int chunkWorkers,
            string rel,
            CopyProgressState state,
            CancellationToken cancellationToken)
        {
            // Pre-allocate so positioned writes land quickly and do not fragment.
            using (var prealloc = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, bufferSize))
            {
                prealloc.SetLength(length);
            }

            var chunkCount = (int)Math.Max(1, Math.Min(chunkWorkers * 4L, length / ChunkBase));
            var chunkSize = (length + chunkCount - 1) / chunkCount;

            await Parallel.ForEachAsync(Enumerable.Range(0, chunkCount), new ParallelOptions
            {
                MaxDegreeOfParallelism = chunkWorkers,
                CancellationToken = cancellationToken
            }, async (i, token) =>
            {
                var offset = i * chunkSize;
                var size = Math.Min(chunkSize, length - offset);
                var worker = ThreadId();
                _log($"WORKER {worker} CHUNK {i + 1}/{chunkCount} {rel} offset={offset} bytes={size}");

                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
                input.Position = offset;
                await using var output = new FileStream(dest, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, bufferSize, FileOptions.WriteThrough);
                output.Position = offset;

                var buffer = new byte[bufferSize];
                long remaining = size;
                while (remaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bufferSize, remaining)), token).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    remaining -= read;
                    state.AddBytes(read, rel);
                }
            });
        }

        private static string GetRelative(string root, string path)
        {
            var relative = Path.GetRelativePath(root, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) && !IsUnder(root, path))
            {
                relative = Path.GetFileName(path);
            }

            return relative;
        }

        private static bool IsUnder(string root, string path)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }

        private static int ThreadId() => Math.Abs(Environment.CurrentManagedThreadId % 1000);

        private static void TryPreserveTime(string source, string dest)
        {
            try
            {
                File.SetLastWriteTimeUtc(dest, new FileInfo(source).LastWriteTimeUtc);
            }
            catch (Exception)
            {
                // Best effort; never break the copy pipeline.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Best effort cleanup.
            }
        }

        /// <summary>
        /// Lock-protected throughput/bytes accounting shared across workers.
        /// </summary>
        private sealed class CopyProgressState
        {
            private readonly object _lock = new();
            private readonly long _totalBytes;
            private readonly int _filesTotal;
            private readonly UsbCopyEngine _engine;
            private long _totalCopied;
            private double _peak;
            private int _filesCompleted;
            private readonly DateTime _start = DateTime.UtcNow;

            public CopyProgressState(long totalBytes, int filesTotal, UsbCopyEngine engine)
            {
                _totalBytes = totalBytes;
                _filesTotal = filesTotal;
                _engine = engine;
            }

            public long TotalBytes => _totalBytes;

            public void AddBytes(long delta, string fileName)
            {
                lock (_lock)
                {
                    _totalCopied += delta;
                    EmitLocked(fileName);
                }
            }

            public void AddSmallBatch(long bytes, int files, string fileName)
            {
                lock (_lock)
                {
                    _totalCopied += bytes;
                    _filesCompleted += files;
                    EmitLocked(fileName);
                }
            }

            public void MarkFileCompleted(string fileName)
            {
                lock (_lock)
                {
                    _filesCompleted++;
                    EmitLocked(fileName);
                }
            }

            private void EmitLocked(string fileName)
            {
                var elapsed = DateTime.UtcNow - _start;
                var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                var average = _totalCopied / seconds;
                if (average > _peak)
                {
                    _peak = average;
                }

                _engine.ReportProgress(new UsbCopyProgressArgs
                {
                    FileName = fileName,
                    FileBytesCopied = 0,
                    FileTotalBytes = 0,
                    TotalBytesCopied = _totalCopied,
                    TotalBytes = _totalBytes,
                    BytesPerSecond = 0,
                    AverageBytesPerSecond = average,
                    PeakBytesPerSecond = _peak,
                    Elapsed = elapsed,
                    FilesCompleted = _filesCompleted,
                    FilesTotal = _filesTotal
                });
            }
        }
    }
}