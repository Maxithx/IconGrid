using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Result of a single benchmark run (per buffer size or per test).
    /// </summary>
    public sealed class UsbBenchmarkResult
    {
        public required string TestName { get; init; }

        public string? BaselineMethod { get; init; }

        public int WorkerCount { get; init; }

        public int BufferSize { get; init; }

        public long BytesTransferred { get; init; }

        public double ThroughputMiBS { get; init; }

        /// <summary>UI string describing the measured throughput (and workers, if any).</summary>
        public string ResultDisplay =>
            WorkerCount > 0
                ? $"Workers {WorkerCount}: {ThroughputMiBS:0.00} MiB/s avg · {Elapsed.TotalSeconds:0.0}s"
                : $"{ThroughputMiBS:0.00} MiB/s · {Elapsed.TotalSeconds:0.0}s";

        public double AverageMiBS { get; init; }

        public double PeakMiBS { get; init; }

        public int StallCount { get; init; }

        public double FlushSeconds { get; init; }

        public TimeSpan Elapsed { get; init; }
    }

    public sealed class UsbBenchmarkProgressArgs : EventArgs
    {
        public required string Stage { get; init; }

        public int CurrentBufferIndex { get; init; }

        public int BufferCount { get; init; }

        public double CurrentMiBS { get; init; }

        public TimeSpan Elapsed { get; init; }
    }

    /// <summary>
    /// Runs synthetic speed/read/write/buffer/stability benchmarks on a USB device.
    /// All tests measure throughput and detect stalls/flush time so the copy engine
    /// buffer can be tuned per device.
    /// </summary>
    public sealed class UsbBenchmarkRunner
    {
        // Buffer sizes for the stress test, in bytes.
        public static readonly int[] BufferStressSizes =
        {
            64 * 1024,
            128 * 1024,
            256 * 1024,
            512 * 1024,
            1024 * 1024,
            2 * 1024 * 1024
        };

        private readonly UsbCopyLogger _logger;

        public UsbBenchmarkRunner(UsbCopyLogger? logger = null)
        {
            _logger = logger ?? new UsbCopyLogger();
        }

        public event EventHandler<UsbBenchmarkProgressArgs>? Progress;

        public event EventHandler<string>? Error;

        public async Task<UsbBenchmarkResult> PortSpeedTestAsync(
            string targetRoot,
            int bufferSize,
            int totalBytes,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"PORT SPEED TEST start target={targetRoot} buffer={bufferSize} total={totalBytes}");
            var result = await RunSyntheticWriteAsync("PortSpeed", targetRoot, bufferSize, totalBytes, logPath, cancellationToken);
            _logger.AppendTimestamped(logPath, $"PORT SPEED TEST done avg_mib_s={result.AverageMiBS:0.00} peak_mib_s={result.PeakMiBS:0.00}");
            return result;
        }

        public async Task<UsbBenchmarkResult> DeviceReadBenchmarkAsync(
            string testFilePath,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"READ TEST start file={testFilePath} buffer={bufferSize}");
            var result = await RunReadTestAsync(testFilePath, bufferSize, logPath, cancellationToken);
            _logger.AppendTimestamped(logPath, $"READ TEST done avg_mib_s={result.AverageMiBS:0.00} peak_mib_s={result.PeakMiBS:0.00}");
            return result;
        }

        public async Task<UsbBenchmarkResult> DeviceWriteBenchmarkAsync(
            string targetRoot,
            int bufferSize,
            int totalBytes,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"WRITE TEST start target={targetRoot} buffer={bufferSize} total={totalBytes}");
            var result = await RunSyntheticWriteAsync("WriteTest", targetRoot, bufferSize, totalBytes, logPath, cancellationToken);
            _logger.AppendTimestamped(logPath, $"WRITE TEST done avg_mib_s={result.AverageMiBS:0.00} peak_mib_s={result.PeakMiBS:0.00}");
            return result;
        }

        public async Task<IReadOnlyList<UsbBenchmarkResult>> BufferStressTestAsync(
            string targetRoot,
            int totalBytes,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"BUFFER STRESS TEST start target={targetRoot} total={totalBytes}");
            var results = new List<UsbBenchmarkResult>();
            for (var i = 0; i < BufferStressSizes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Progress?.Invoke(this, new UsbBenchmarkProgressArgs
                {
                    Stage = "buffer",
                    CurrentBufferIndex = i,
                    BufferCount = BufferStressSizes.Length,
                    Elapsed = TimeSpan.Zero
                });

                var bufferSize = BufferStressSizes[i];
                _logger.AppendTimestamped(logPath, $"BUFFER TEST buffer={bufferSize}");
                try
                {
                    var r = await RunSyntheticWriteAsync($"BufferStress_{bufferSize}", targetRoot, bufferSize, totalBytes, logPath, cancellationToken);
                    results.Add(r);
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, $"buffer test failed size={bufferSize}: {ex.Message}");
                    _logger.AppendTimestamped(logPath, $"BUFFER TEST failed size={bufferSize} error={ex.Message}");
                }
            }

            _logger.AppendTimestamped(logPath, "BUFFER STRESS TEST done");
            return results;
        }

        /// <summary>
        /// Worker-scaling benchmark: writes <paramref name="totalBytes"/> split across
        /// N parallel temp files with 1, 2, 4 and 8 workers, measuring aggregate
        /// throughput for each. Returns one result per worker count.
        /// </summary>
        public async Task<IReadOnlyList<UsbBenchmarkResult>> WorkerScalingTestAsync(
            string targetRoot,
            int bufferSize,
            int totalBytes,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"WORKER SCALING TEST start target={targetRoot} buffer={bufferSize} total={totalBytes}");
            var results = new List<UsbBenchmarkResult>();
            var writerCounts = new[] { 1, 2, 4, 8 };

            for (var i = 0; i < writerCounts.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workers = writerCounts[i];
                _logger.AppendTimestamped(logPath, $"SCALING TEST workers={workers}");

                var perFile = (int)Math.Max(bufferSize, totalBytes / workers);
                var start = DateTime.UtcNow;
                var peak = 0d;
                var samples = new List<double>();
                var transferred = 0L;
                var lockObj = new object();
                var rngBase = new Random(4321 + workers);
                var tempFiles = new List<string>();
                try
                {
                    await Parallel.ForEachAsync(Enumerable.Range(0, workers), new ParallelOptions
                    {
                        MaxDegreeOfParallelism = workers,
                        CancellationToken = cancellationToken
                    }, async (w, token) =>
                    {
                        var tempFile = Path.Combine(targetRoot, $".icongrid-scale-{workers}w-{w}-{Guid.NewGuid():N}.tmp");
                        tempFiles.Add(tempFile);
                        var buffer = new byte[bufferSize];
                        var localRng = new Random(rngBase.Next());
                        localRng.NextBytes(buffer);

                        await using var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough);
                        var localTransferred = 0L;
                        while (localTransferred < perFile)
                        {
                            token.ThrowIfCancellationRequested();
                            await output.WriteAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                            await output.FlushAsync(token).ConfigureAwait(false);
                            output.Flush(true);
                            localTransferred += buffer.Length;
                        }

                        lock (lockObj)
                        {
                            transferred += localTransferred;
                            var elapsed = DateTime.UtcNow - start;
                            var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                            var mibs = transferred / (1024.0 * 1024.0) / seconds;
                            samples.Add(mibs);
                            if (mibs > peak)
                            {
                                peak = mibs;
                            }
                        }
                    });
                }
                finally
                {
                    foreach (var f in tempFiles)
                    {
                        try { File.Delete(f); } catch (IOException) { }
                    }
                }

                var finalElapsed = DateTime.UtcNow - start;
                var average = samples.Count > 0 ? AverageSamples(samples) : 0d;
                results.Add(new UsbBenchmarkResult
                {
                    TestName = "WorkerScaling",
                    WorkerCount = workers,
                    BufferSize = bufferSize,
                    BytesTransferred = transferred,
                    ThroughputMiBS = average,
                    AverageMiBS = average,
                    PeakMiBS = peak,
                    StallCount = 0,
                    FlushSeconds = 0,
                    Elapsed = finalElapsed
                });
                _logger.AppendTimestamped(logPath, $"SCALING TEST done workers={workers} avg_mib_s={average:0.00} peak_mib_s={peak:0.00}");
            }

            _logger.AppendTimestamped(logPath, "WORKER SCALING TEST done");
            return results;
        }

        public async Task<UsbBenchmarkResult> StabilityTestAsync(
            string targetRoot,
            int bufferSize,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"STABILITY TEST start target={targetRoot} buffer={bufferSize} duration_s={duration.TotalSeconds:0}");
            var chunks = Math.Max(2, (int)(duration.TotalSeconds / 5));
            var chunkBytes = Math.Max(bufferSize, bufferSize * 16);
            var result = await RunSyntheticWriteAsync("Stability", targetRoot, bufferSize, chunkBytes * chunks, logPath, cancellationToken);
            _logger.AppendTimestamped(logPath, $"STABILITY TEST done avg_mib_s={result.AverageMiBS:0.00} peak_mib_s={result.PeakMiBS:0.00} stalls={result.StallCount}");
            return result;
        }

        /// <summary>
        /// Windows baseline: copies an incompressible 32 MiB file to the USB device via
        /// File.Copy (same API Explorer uses internally). Used to compare Windows
        /// throughput against the optimized pipeline.
        /// </summary>
        public async Task<UsbBenchmarkResult> WindowsBaselineTestAsync(
            string targetRoot,
            int totalBytes,
            CancellationToken cancellationToken)
        {
            var logPath = _logger.CreateBenchmarkLogFile();
            _logger.AppendTimestamped(logPath, $"WINDOWS BASELINE TEST start target={targetRoot} total={totalBytes}");
            var source = Path.Combine(Path.GetTempPath(), $".icongrid-basesrc-{Guid.NewGuid():N}.tmp");
            var destination = Path.Combine(targetRoot, $".icongrid-baseout-{Guid.NewGuid():N}.tmp");
            var start = DateTime.UtcNow;
            try
            {
                using (var fs = new FileStream(source, FileMode.Create, FileAccess.Write, FileShare.None, 1 * 1024 * 1024))
                {
                    var rng = new Random(4242);
                    var chunk = new byte[1024 * 1024];
                    rng.NextBytes(chunk);
                    var written = 0L;
                    while (written < Math.Min(totalBytes, 32L * 1024L * 1024L))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await fs.WriteAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
                        written += chunk.Length;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(source, destination, overwrite: true);

                var elapsed = DateTime.UtcNow - start;
                var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                var bytes = new FileInfo(destination).Length;
                var mibs = bytes / (1024.0 * 1024.0) / seconds;
                Progress?.Invoke(this, new UsbBenchmarkProgressArgs
                {
                    Stage = "baseline",
                    CurrentMiBS = mibs,
                    Elapsed = elapsed
                });

                _logger.AppendTimestamped(logPath, $"WINDOWS BASELINE TEST done avg_mib_s={mibs:0.00}");
                return new UsbBenchmarkResult
                {
                    TestName = "WindowsBaseline",
                    BaselineMethod = "File.Copy (Explorer)",
                    BufferSize = 0,
                    BytesTransferred = bytes,
                    ThroughputMiBS = mibs,
                    AverageMiBS = mibs,
                    PeakMiBS = mibs,
                    StallCount = 0,
                    FlushSeconds = 0,
                    Elapsed = elapsed
                };
            }
            finally
            {
                try { File.Delete(source); } catch (IOException) { }
                try { File.Delete(destination); } catch (IOException) { }
            }
        }

        private async Task<UsbBenchmarkResult> RunSyntheticWriteAsync(
            string tag,
            string targetRoot,
            int bufferSize,
            int totalBytes,
            string logPath,
            CancellationToken cancellationToken)
        {
            var tempFile = Path.Combine(targetRoot, $".icongrid-bench-{tag}-{Guid.NewGuid():N}.tmp");
            var buffer = new byte[bufferSize];
            var rng = new Random(12345);
            rng.NextBytes(buffer);

            var transferred = 0L;
            var peak = 0d;
            var samples = new List<double>();
            var stalls = 0;
            var flushWatch = new System.Diagnostics.Stopwatch();
            var start = DateTime.UtcNow;

            try
            {
                await using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                {
                    while (transferred < totalBytes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await output.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                        flushWatch.Reset();
                        flushWatch.Start();
                        output.Flush(true);
                        flushWatch.Stop();

                        transferred += buffer.Length;
                        var elapsed = DateTime.UtcNow - start;
                        var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                        var mibs = transferred / (1024.0 * 1024.0) / seconds;
                        samples.Add(mibs);
                        if (mibs > peak)
                        {
                            peak = mibs;
                        }

                        if (flushWatch.ElapsedMilliseconds > 100)
                        {
                            stalls++;
                        }

                        Progress?.Invoke(this, new UsbBenchmarkProgressArgs
                        {
                            Stage = "write",
                            CurrentMiBS = mibs,
                            Elapsed = elapsed
                        });
                    }
                }
            }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (IOException)
                {
                    // Best effort cleanup.
                }
            }

            var finalElapsed = DateTime.UtcNow - start;
            var average = samples.Count > 0 ? AverageSamples(samples) : 0d;
            return new UsbBenchmarkResult
            {
                TestName = tag,
                BufferSize = bufferSize,
                BytesTransferred = transferred,
                ThroughputMiBS = average,
                AverageMiBS = average,
                PeakMiBS = peak,
                StallCount = stalls,
                FlushSeconds = flushWatch.Elapsed.TotalSeconds,
                Elapsed = finalElapsed
            };
        }

        private async Task<UsbBenchmarkResult> RunReadTestAsync(
            string testFilePath,
            int bufferSize,
            string logPath,
            CancellationToken cancellationToken)
        {
            // Source file size — must be a multiple of the sector size so the
            // NoBuffering reads below are valid.
            var fileLength = new FileInfo(testFilePath).Length;
            var buffer = new byte[bufferSize];
            var transferred = 0L;
            var peak = 0d;
            var samples = new List<double>();
            var start = DateTime.UtcNow;

            // FILE_FLAG_NO_BUFFERING (0x20000000) bypasses the Windows page cache.
            // Without it, a freshly created 8 MB probe file is served from RAM and
            // the "USB read speed" reports RAM speed (e.g. 1169 MB/s) instead of
            // the actual device speed. With NoBuffering, every read goes straight
            // to the device. bufferSize (1 MB) and the 8 MB probe are multiples of
            // 512-byte sectors, which is required by this flag.
            var rawOptions = FileOptions.SequentialScan | (FileOptions)0x20000000;
            _logger.AppendTimestamped(logPath, $"READ TEST file_bytes={fileLength}");

            await using (var input = new FileStream(
                File.OpenHandle(testFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, rawOptions),
                FileAccess.Read,
                bufferSize,
                isAsync: true))
            {
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    transferred += read;
                    var elapsed = DateTime.UtcNow - start;
                    var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
                    var mibs = transferred / (1024.0 * 1024.0) / seconds;
                    samples.Add(mibs);
                    if (mibs > peak)
                    {
                        peak = mibs;
                    }

                    Progress?.Invoke(this, new UsbBenchmarkProgressArgs
                    {
                        Stage = "read",
                        CurrentMiBS = mibs,
                        Elapsed = elapsed
                    });
                }
            }

            var finalElapsed = DateTime.UtcNow - start;
            var average = samples.Count > 0 ? AverageSamples(samples) : 0d;
            return new UsbBenchmarkResult
            {
                TestName = "ReadTest",
                BufferSize = bufferSize,
                BytesTransferred = transferred,
                ThroughputMiBS = average,
                AverageMiBS = average,
                PeakMiBS = peak,
                StallCount = 0,
                FlushSeconds = 0,
                Elapsed = finalElapsed
            };
        }

        private static double AverageSamples(List<double> samples)
        {
            var sum = 0d;
            foreach (var s in samples)
            {
                sum += s;
            }

            return sum / samples.Count;
        }
    }
}