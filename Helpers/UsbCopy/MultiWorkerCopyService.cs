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
        private readonly IOverwriteConflictResolver _conflictResolver;

        public MultiWorkerCopyService(UsbCopyEngine engine, Action<string> log, Action<string> pipeline, IOverwriteConflictResolver? conflictResolver = null)
        {
            _engine = engine;
            _log = log;
            _pipeline = pipeline;
            _conflictResolver = conflictResolver ?? AlwaysOverwriteResolver.Instance;
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

            var session = new OverwriteSession(_conflictResolver);

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

                    if (!await session.ShouldWriteAsync(file.Source, dest, cancellationToken).ConfigureAwait(false))
                    {
                        _log($"WORKER 1 FILE SKIP exists {rel}");
                        state.MarkFileCompleted(rel, file.Size);
                        continue;
                    }

                    _log($"WORKER 1 FILE START {rel} size={file.Size}");
                    await _engine.CopySingleFileAsync(file.Source, dest, file.Size, bufferSize, null!, _ => { }, cancellationToken).ConfigureAwait(false);
                    TryPreserveTime(file.Source, dest);
                    state.MarkFileCompleted(rel, file.Size);
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
                await CopySmallBatchAsync(small, sourceRoot, targetRoot, bufferSize, state, session, cancellationToken).ConfigureAwait(false);
            }

            // 2) Large files -> parallel chunk-split copies.
            if (large.Count > 0)
            {
                await CopyLargeFilesAsync(large, sourceRoot, targetRoot, bufferSize, effectiveWorkers, state, session, cancellationToken).ConfigureAwait(false);
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
                    if (!await session.ShouldWriteAsync(file.Source, dest, token).ConfigureAwait(false))
                    {
                        _log($"WORKER {worker} FILE SKIP exists {rel}");
                        state.MarkFileCompleted(rel, file.Size);
                        return;
                    }

                    _log($"WORKER {worker} FILE START {rel} size={file.Size}");
                    await _engine.CopySingleFileAsync(file.Source, dest, file.Size, bufferSize, null!, _ => { }, token).ConfigureAwait(false);
                    TryPreserveTime(file.Source, dest);
                    state.MarkFileCompleted(rel, file.Size);
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
            OverwriteSession session,
            CancellationToken cancellationToken)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), $"fastcopy-{Guid.NewGuid():N}.zip");
            var destZip = Path.Combine(targetRoot, $".fastcopy-pkg-{Guid.NewGuid():N}.zip");
            var payloadBytes = small.Sum(f => f.Size);

            try
            {
                _log($"WORKER 1 PACK START small_files={small.Count} bytes={payloadBytes}");
                var zipBytes = await Task.Run(() =>
                {
                    // Packing thousands of small files synchronously (ZipFile +
                    // CreateEntryFromFile) can take 30+ seconds for e.g. 2742 files.
                    // It must run on a worker thread so the UI thread never freezes
                    // at copy start while the ZIP is built.
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        foreach (var file in small)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var rel = GetRelative(sourceRoot, file.Source).Replace('\\', '/');
                            archive.CreateEntryFromFile(file.Source, rel, CompressionLevel.Fastest);
                            // Report progress per packed file so "Files remaining"
                            // counts down live while the ZIP is being built (this
                            // is the long 30+ s phase for thousands of small files).
                            state.AddSmallBatch(file.Size, 1, rel);
                        }
                    }

                    return new FileInfo(zipPath).Length;
                }, cancellationToken).ConfigureAwait(false);

                _log($"WORKER 1 PACK ZIP DONE zip_bytes={zipBytes}");

                _pipeline("pack:copy");
                _log("WORKER 1 PACK COPY");
                await _engine.CopySingleFileAsync(zipPath, destZip, zipBytes, bufferSize, null!, _ => { }, cancellationToken).ConfigureAwait(false);

                _pipeline("pack:unpack");
                _log("WORKER 1 PACK UNPACK");
                if (!await UnpackZipAsync(destZip, targetRoot, session, cancellationToken).ConfigureAwait(false))
                {
                    // A single bad/unsupported ZIP entry must never fail the whole
                    // copy. Fall back to copying the small files individually.
                    _log("WORKER 1 PACK UNPACK FAILED - falling back to per-file copy");
                    await CopySmallFallbackAsync(small, sourceRoot, targetRoot, bufferSize, state, session, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _log($"WORKER 1 PACK DONE small_files={small.Count}");
                }
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
            OverwriteSession session,
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

                if (!await session.ShouldWriteAsync(file.Source, dest, token).ConfigureAwait(false))
                {
                    state.MarkFileCompleted(rel, file.Size);
                    return;
                }

                var worker = ThreadId();
                _log($"WORKER {worker} LARGE START {rel} size={file.Size} mode=chunk");
                await CopyFileChunkedAsync(file.Source, dest, file.Size, bufferSize, workerCount, rel, state, token);
                TryPreserveTime(file.Source, dest);
                state.MarkFileCompleted(rel);
                _log($"WORKER {worker} LARGE DONE {rel}");
            }).ConfigureAwait(false);
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
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Session-scoped conflict decisions shared by all workers: once the user
        /// picks "Yes to all" / "No to all", the remaining conflicts in this copy
        /// are resolved without further prompts.
        /// </summary>
        private sealed class OverwriteSession
        {
            private readonly object _lock = new();
            private readonly IOverwriteConflictResolver _resolver;
            private bool _overwriteAll;
            private bool _skipAll;

            public OverwriteSession(IOverwriteConflictResolver resolver)
            {
                _resolver = resolver;
            }

            public async Task<bool> ShouldWriteAsync(string source, string dest, CancellationToken cancellationToken)
            {
                if (!File.Exists(dest))
                {
                    return true;
                }

                lock (_lock)
                {
                    if (_overwriteAll)
                    {
                        return true;
                    }

                    if (_skipAll)
                    {
                        return false;
                    }
                }

                // Resolve outside the lock (the resolver shows a modal dialog and
                // must never block other workers from progressing).
                var decision = await _resolver.ResolveAsync(source, dest).ConfigureAwait(false);

                lock (_lock)
                {
                    if (decision == OverwriteDecision.OverwriteAll)
                    {
                        _overwriteAll = true;
                        return true;
                    }

                    if (decision == OverwriteDecision.SkipAll)
                    {
                        _skipAll = true;
                        return false;
                    }

                    // Re-check after the dialog: another worker may have chosen
                    // "to all" while this one was waiting for the user.
                    if (_overwriteAll)
                    {
                        return true;
                    }

                    if (_skipAll)
                    {
                        return false;
                    }
                }

                return decision == OverwriteDecision.Overwrite;
            }
        }

        /// <summary>
        /// Extracts every ZIP entry to the target root in parallel, resolving
        /// existing destinations through the copy session's overwrite policy
        /// (per-file prompt, or yes/no-to-all). Returns false when the ZIP
        /// cannot be unpacked (e.g. an unsupported compression method) so the
        /// caller can fall back to copying the files individually — a single
        /// bad entry must never fail the whole copy.
        ///
        /// ZipArchive is NOT thread-safe for concurrent reads: multiple threads
        /// calling ExtractToFile on the same archive corrupt the shared stream.
        /// Each worker therefore opens its OWN fresh archive handle per entry.
        /// </summary>
        private static async Task<bool> UnpackZipAsync(string zipPath, string targetRoot, OverwriteSession session, CancellationToken cancellationToken)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entries = archive.Entries.ToList();
                await Parallel.ForEachAsync(entries, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2),
                    CancellationToken = cancellationToken
                }, (entry, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var clean = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var dest = Path.Combine(targetRoot, clean);
                    if (!session.ShouldWriteAsync(zipPath, dest, token).GetAwaiter().GetResult())
                    {
                        return ValueTask.CompletedTask;
                    }

                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // Open a fresh archive on THIS thread so concurrent reads
                    // never share/race on one ZipArchive stream.
                    using var localArchive = ZipFile.OpenRead(zipPath);
                    var localEntry = localArchive.GetEntry(entry.FullName);
                    if (localEntry == null)
                    {
                        throw new InvalidDataException($"zip entry missing: {entry.FullName}");
                    }

                    localEntry.ExtractToFile(dest, overwrite: true);
                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Copies the small files one-by-one (per-file, parallel) when the ZIP
        /// unpacking failed. Keeps the copy alive instead of aborting it.
        /// </summary>
        private async Task CopySmallFallbackAsync(
            IReadOnlyList<(string Source, long Size)> small,
            string sourceRoot,
            string targetRoot,
            int bufferSize,
            CopyProgressState state,
            OverwriteSession session,
            CancellationToken cancellationToken)
        {
            await Parallel.ForEachAsync(small, new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2),
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

                if (!await session.ShouldWriteAsync(file.Source, dest, token).ConfigureAwait(false))
                {
                    state.MarkFileCompleted(rel, file.Size);
                    return;
                }

                await _engine.CopySingleFileAsync(file.Source, dest, file.Size, bufferSize, null!, _ => { }, token).ConfigureAwait(false);
                TryPreserveTime(file.Source, dest);
                state.MarkFileCompleted(rel, file.Size);
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

            public void MarkFileCompleted(string fileName, long bytes = 0)
            {
                lock (_lock)
                {
                    _totalCopied += bytes;
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