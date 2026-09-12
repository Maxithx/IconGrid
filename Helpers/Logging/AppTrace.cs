using System;
using System.IO;

namespace IconGrid.Helpers.Logging;

/// <summary>
/// Central trace.log writer. The log is capped at a fixed maximum size and the
/// oldest content is dropped automatically so the diagnostics log can never grow
/// without bound (it previously reached 100+ MB). The launcher UI, the elevated
/// monitor agent, and the resolution service all log through here.
/// </summary>
public static class AppTrace
{
    /// <summary>Hard cap for trace.log. When exceeded, the oldest half is dropped.</summary>
    public const long MaxBytes = 10L * 1024 * 1024;

    /// <summary>Fraction of the cap kept after a trim so trimming stays infrequent.</summary>
    private const double KeepFraction = 0.5;

    private const int AppendRetryCount = 3;
    private const int AppendRetryDelayMs = 8;

    private static readonly object SyncRoot = new();

    public static string LogPath { get; } = BuildLogPath();

    private static string BuildLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "IconGrid", "trace.log");
    }

    /// <summary>
    /// Appends a timestamped line to trace.log and trims the file when it grows
    /// past <see cref="MaxBytes"/>. Never throws — logging must not break the host.
    /// </summary>
    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"[{DateTime.Now:O}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line);
                EnforceSizeLimit();
            }
        }
        catch (IOException)
        {
            // The file may be briefly held by another IconGrid process that is also
            // logging (launcher + elevated monitor agent). Retry a few times.
            for (var attempt = 0; attempt < AppendRetryCount; attempt++)
            {
                try
                {
                    System.Threading.Thread.Sleep(AppendRetryDelayMs);
                    lock (SyncRoot)
                    {
                        File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
                        EnforceSizeLimit();
                    }
                    return;
                }
                catch (IOException)
                {
                }
                catch
                {
                    return;
                }
            }
        }
        catch
        {
            // ignore all other logging failures
        }
    }

    public static long GetSizeBytes()
    {
        try
        {
            var info = new FileInfo(LogPath);
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static DateTime? GetLastWriteTime()
    {
        try
        {
            var info = new FileInfo(LogPath);
            return info.Exists ? info.LastWriteTime : null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetSizeText() => FormatSize(GetSizeBytes());

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    /// <summary>
    /// Trims the oldest content so the file stays under <see cref="MaxBytes"/>.
    /// Keeps the newest tail, aligned to a line boundary.
    /// </summary>
    public static void EnforceSizeLimit()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (!info.Exists || info.Length <= MaxBytes)
            {
                return;
            }

            var keepBytes = (long)(MaxBytes * KeepFraction);

            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var start = Math.Max(0, stream.Length - keepBytes);
            stream.Seek(start, SeekOrigin.Begin);

            var buffer = new byte[stream.Length - start];
            var read = stream.Read(buffer, 0, buffer.Length);

            // Start on a line boundary so the retained content never begins mid-line.
            var firstNewline = Array.IndexOf(buffer, (byte)'\n', 0, read);
            var offset = firstNewline >= 0 && firstNewline + 1 < read ? firstNewline + 1 : 0;

            var retained = new byte[read - offset];
            Array.Copy(buffer, offset, retained, 0, retained.Length);

            File.WriteAllBytes(LogPath, retained);
        }
        catch
        {
            // best-effort; never break the writer
        }
    }

    /// <summary>Deletes the trace log (manual "clean" action from the UI).</summary>
    public static bool Clear()
    {
        try
        {
            lock (SyncRoot)
            {
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
