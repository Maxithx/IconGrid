using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IconGrid.Helpers.UsbCopy
{
    /// <summary>
    /// Writes structured copy and benchmark logs under
    /// %AppData%\Roaming\IconGrid\logs\fastusbcopy.
    /// Copy logs live in the root; benchmark logs in the benchmark/ sub-folder.
    /// </summary>
    public sealed class UsbCopyLogger
    {
        private readonly string _rootDirectory;

        public UsbCopyLogger(string? rootDirectory = null)
        {
            _rootDirectory = rootDirectory ?? GetDefaultRoot();
            Directory.CreateDirectory(_rootDirectory);
        }

        public string RootDirectory => _rootDirectory;

        public string BenchmarkDirectory => Path.Combine(_rootDirectory, "benchmark");

        public static string GetDefaultRoot()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "IconGrid", "logs", "fastusbcopy");
        }

        public string CreateCopyLogFile()
        {
            Directory.CreateDirectory(_rootDirectory);
            return Path.Combine(_rootDirectory, $"copy-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }

        public string CreateBenchmarkLogFile()
        {
            Directory.CreateDirectory(BenchmarkDirectory);
            return Path.Combine(BenchmarkDirectory, $"benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }

        public void AppendTimestamped(string path, string message)
        {
            try
            {
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging must never break the copy pipeline.
            }
        }

        public IReadOnlyList<string> GetLogFiles()
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(_rootDirectory, "*.log", SearchOption.AllDirectories);
        }

        public string? ReadLogFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, Encoding.UTF8);
                }
            }
            catch (IOException)
            {
                // Ignore unreadable log files.
            }

            return null;
        }

        /// <summary>
        /// Writes all benchmark results as CSV so throughput can be analyzed/optimized
        /// across runs (e.g. buffer curve, Windows baseline comparison).
        /// </summary>
        public string ExportBenchmarkCsv(IReadOnlyList<UsbBenchmarkResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TestName,BaselineMethod,WorkerCount,BufferSize,Bytes,AvgMiBS,PeakMiBS,Stalls,FlushSeconds,ElapsedSeconds");
            foreach (var r in results)
            {
                sb.AppendLine(
                    $"{r.TestName},{r.BaselineMethod ?? ""},{r.WorkerCount},{r.BufferSize},{r.BytesTransferred}," +
                    $"{r.AverageMiBS:0.00},{r.PeakMiBS:0.00},{r.StallCount},{r.FlushSeconds:0.00},{r.Elapsed.TotalSeconds:0.00}");
            }

            var path = Path.Combine(_rootDirectory, $"benchmark-results-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            try
            {
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (IOException)
            {
                // Fall back to the temp folder if the log directory is unavailable.
                path = Path.Combine(Path.GetTempPath(), $"iconGrid-benchmark-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }

            return path;
        }

        public void ClearAll()
        {
            foreach (var file in GetLogFiles())
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Ignore files in use.
                }
            }
        }

        public static string FormatMiBPerSecond(double bytesPerSecond) =>
            (bytesPerSecond / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture);
    }
}