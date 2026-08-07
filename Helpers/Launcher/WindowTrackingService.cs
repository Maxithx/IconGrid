using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using IconGrid.Models;
using RECT = IconGrid.Models.WindowTrackingRecord.RECT;

namespace IconGrid.Helpers.Launcher
{
    /// <summary>
    /// Real-time window tracking service that provides the layout system with
    /// reliable positional data across resolution changes. Runs a background
    /// poll (every 2 seconds) that enumerates top-level windows and stores
    /// their rect, state, and resolution in a multi-resolution history store.
    ///
    /// The layout system must NEVER use GetWindowRect() alone — it must always
    /// query this service for LastKnownRect, LastSeenOpenRect, confidence scores,
    /// and per-resolution history.
    /// </summary>
    public sealed class WindowTrackingService : IDisposable
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private readonly DispatcherTimer _pollTimer;
        private readonly object _storeLock = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WindowTrackingRecord>> _records
            = new(); // compositeKey -> resolution -> record

        private string _currentResolution = "3840x2160";
        private bool _disposed;

        // Confidence scoring constants
        private const int MaxSamplesForConfidence = 10;
        private const double OpenRectBonus = 0.3;
        private const double NotMinimizedBonus = 0.2;
        private static readonly TimeSpan RecentThreshold = TimeSpan.FromMinutes(5);

        public WindowTrackingService()
        {
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _pollTimer.Tick += OnPollTick;
        }

        /// <summary>Start background polling.</summary>
        public void Start()
        {
            if (_disposed)
                return;
            _pollTimer.Start();
            // Take an immediate poll so data is available from the start.
            PollWindows();
        }

        /// <summary>Stop background polling.</summary>
        public void Stop()
        {
            _pollTimer.Stop();
        }

        /// <summary>
        /// Notifies the tracking service that the display resolution has changed.
        /// The current resolution key is updated so subsequent polls tag records correctly.
        /// </summary>
        public void NotifyResolutionChange(string newResolution)
        {
            if (string.IsNullOrWhiteSpace(newResolution))
                return;
            _currentResolution = newResolution;
        }

        /// <summary>
        /// Returns the last known rectangle for the given window handle, optionally
        /// filtered to a specific resolution. If no record exists, falls back to
        /// calling GetWindowRect directly.
        /// </summary>
        public RECT GetLastKnownRect(IntPtr hwnd, string? resolution = null)
        {
            var record = FindBestRecord(hwnd, resolution);
            if (record != null)
                return record.LastKnownRect;

            // Fallback: ask the OS directly.
            if (GetWindowRect(hwnd, out var rect))
                return rect;

            return default;
        }

        /// <summary>
        /// Returns the last rectangle captured while the window was open (not minimized).
        /// This is the most trustworthy rect for restore operations. Falls back to
        /// LastKnownRect if no open rect was ever recorded.
        /// </summary>
        public RECT? GetLastSeenOpenRect(IntPtr hwnd, string? resolution = null)
        {
            var record = FindBestRecord(hwnd, resolution);
            if (record?.LastSeenOpenRect != null)
                return record.LastSeenOpenRect.Value;

            // Fallback: if we have a known rect that is not iconic-minimized, use it.
            if (record != null && record.LastState != WindowTrackingState.Minimized)
                return record.LastKnownRect;

            return null;
        }

        /// <summary>
        /// Returns the confidence score (0.0–1.0) for the given window at the
        /// specified resolution. Higher = more trustworthy positional data.
        /// </summary>
        public double GetConfidenceScore(IntPtr hwnd, string resolution)
        {
            var record = FindBestRecord(hwnd, resolution);
            if (record == null)
                return 0.0;

            RecalculateConfidence(record);
            return record.ConfidenceScore;
        }

        /// <summary>
        /// Returns all tracking records for the given resolution.
        /// Used by the layout system to find the best history when restoring.
        /// </summary>
        public IReadOnlyList<WindowTrackingRecord> GetHistoryForResolution(string resolution)
        {
            var result = new List<WindowTrackingRecord>();
            lock (_storeLock)
            {
                foreach (var kvp in _records)
                {
                    if (kvp.Value.TryGetValue(resolution, out var record))
                        result.Add(record);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the record with the highest confidence score for the given
        /// composite key (process|title), regardless of resolution.
        /// </summary>
        public WindowTrackingRecord? GetHighestConfidenceRecord(string compositeKey)
        {
            lock (_storeLock)
            {
                if (!_records.TryGetValue(compositeKey, out var byResolution))
                    return null;

                WindowTrackingRecord? best = null;
                foreach (var kvp in byResolution)
                {
                    if (best == null || kvp.Value.ConfidenceScore > best.ConfidenceScore)
                        best = kvp.Value;
                }
                return best;
            }
        }

        /// <summary>
        /// Safety pass: scans all tracked windows and moves any window that is
        /// fully or partially off-screen back inside the viewport.
        /// Uses a cascade offset (50,50 + increment) for each off-screen window
        /// so they don't stack on top of each other.
        /// </summary>
        public void SafetyPass(RECT viewport)
        {
            var viewW = Math.Max(100, viewport.Right - viewport.Left);
            var viewH = Math.Max(100, viewport.Bottom - viewport.Top);

            var cascadeX = viewport.Left + 50;
            var cascadeY = viewport.Top + 50;
            const int cascadeStep = 30;

            // Snapshot current records under lock to avoid enumeration issues.
            var records = new List<WindowTrackingRecord>();
            lock (_storeLock)
            {
                foreach (var kvp in _records)
                {
                    // Take the latest-resolution record for each composite key.
                    var latest = kvp.Value.Values
                        .OrderByDescending(r => r.LastSeenUtc)
                        .FirstOrDefault();
                    if (latest != null && IsWindow(latest.Hwnd))
                        records.Add(latest);
                }
            }

            foreach (var record in records)
            {
                if (!IsWindow(record.Hwnd) || !IsWindowVisible(record.Hwnd))
                    continue;

                if (!GetWindowRect(record.Hwnd, out var currentRect))
                    continue;

                var w = Math.Max(100, currentRect.Right - currentRect.Left);
                var h = Math.Max(50, currentRect.Bottom - currentRect.Top);

                // Check if the window is fully off-screen.
                var fullyOffScreen = currentRect.Right <= viewport.Left
                                  || currentRect.Bottom <= viewport.Top
                                  || currentRect.Left >= viewport.Right
                                  || currentRect.Top >= viewport.Bottom;

                // Check if the window is partially off-screen (less than 30% visible).
                var visibleX = Math.Max(0, Math.Min(currentRect.Right, viewport.Right) - Math.Max(currentRect.Left, viewport.Left));
                var visibleY = Math.Max(0, Math.Min(currentRect.Bottom, viewport.Bottom) - Math.Max(currentRect.Top, viewport.Top));
                var visibleArea = visibleX * visibleY;
                var totalArea = w * h;
                var partiallyOff = totalArea > 0 && visibleArea < totalArea * 0.3;

                if (!fullyOffScreen && !partiallyOff)
                    continue;

                // Determine a safe position.
                int newLeft, newTop;
                var openRect = record.LastSeenOpenRect;
                if (openRect != null)
                {
                    var or = openRect.Value;
                    var orW = Math.Max(100, or.Right - or.Left);
                    var orH = Math.Max(50, or.Bottom - or.Top);
                    newLeft = Math.Max(viewport.Left, Math.Min(or.Left, viewport.Right - orW));
                    newTop = Math.Max(viewport.Top, Math.Min(or.Top, viewport.Bottom - orH));
                }
                else
                {
                    newLeft = Math.Min(cascadeX, viewport.Right - w);
                    newTop = Math.Min(cascadeY, viewport.Bottom - h);
                    cascadeX += cascadeStep;
                    cascadeY += cascadeStep;
                    if (cascadeX > viewport.Right - 200)
                        cascadeX = viewport.Left + 50;
                    if (cascadeY > viewport.Bottom - 200)
                        cascadeY = viewport.Top + 50;
                }

                newLeft = Math.Max(viewport.Left, newLeft);
                newTop = Math.Max(viewport.Top, newTop);

                SetWindowPos(record.Hwnd, IntPtr.Zero, newLeft, newTop, w, h,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        /// <summary>Immediately poll all windows and update the tracking store.</summary>
        public void PollWindows()
        {
            var now = DateTime.UtcNow;
            var resolution = _currentResolution;

            EnumWindows((hwnd, _) =>
            {
                if (!ShouldTrack(hwnd))
                    return true;

                try
                {
                    var state = WindowTrackingState.Unknown;
                    if (IsIconic(hwnd))
                        state = WindowTrackingState.Minimized;
                    else if (IsZoomed(hwnd))
                        state = WindowTrackingState.Maximized;
                    else
                        state = WindowTrackingState.Normal;

                    if (!GetWindowRect(hwnd, out var rect))
                        return true;

                    var processName = GetProcessName(hwnd);
                    var windowTitle = GetWindowTitle(hwnd);

                    if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(windowTitle))
                        return true;

                    var record = new WindowTrackingRecord
                    {
                        Hwnd = hwnd,
                        ProcessName = processName,
                        WindowTitle = windowTitle,
                        Resolution = resolution,
                        LastKnownRect = rect,
                        LastState = state,
                        LastSeenUtc = now
                    };

                    // Only store the open rect if the window is NOT minimized.
                    if (state != WindowTrackingState.Minimized && state != WindowTrackingState.Unknown)
                    {
                        record.LastSeenOpenRect = rect;
                    }

                    UpsertRecord(record, now);
                }
                catch
                {
                    // Skip windows that cause transient errors.
                }

                return true;
            }, IntPtr.Zero);

            // Purge records that haven't been seen in 30+ seconds.
            PurgeStaleRecords(now, TimeSpan.FromSeconds(30));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pollTimer.Stop();
            _pollTimer.Tick -= OnPollTick;
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            PollWindows();
        }

        private void UpsertRecord(WindowTrackingRecord incoming, DateTime now)
        {
            var key = incoming.CompositeKey;

            lock (_storeLock)
            {
                var byResolution = _records.GetOrAdd(key, _ => new ConcurrentDictionary<string, WindowTrackingRecord>());

                if (byResolution.TryGetValue(incoming.Resolution, out var existing) && existing.Hwnd == incoming.Hwnd)
                {
                    // Update existing record.
                    existing.LastKnownRect = incoming.LastKnownRect;
                    existing.LastState = incoming.LastState;
                    existing.LastSeenUtc = now;
                    existing.TimesSeenAtThisResolution++;

                    if (incoming.LastSeenOpenRect != null)
                    {
                        existing.LastSeenOpenRect = incoming.LastSeenOpenRect.Value;
                    }

                    RecalculateConfidence(existing);
                }
                else if (existing == null || existing.Hwnd != incoming.Hwnd)
                {
                    // New record or handle changed (window reopened).
                    incoming.TimesSeenAtThisResolution = 1;
                    RecalculateConfidence(incoming);
                    byResolution[incoming.Resolution] = incoming;
                }
            }
        }

        private WindowTrackingRecord? FindBestRecord(IntPtr hwnd, string? resolution)
        {
            lock (_storeLock)
            {
                // Try exact hwnd + resolution match.
                foreach (var kvp in _records)
                {
                    foreach (var r in kvp.Value.Values)
                    {
                        if (r.Hwnd == hwnd && (resolution == null || r.Resolution == resolution))
                            return r;
                    }
                }

                // Fallback: any record for this hwnd.
                foreach (var kvp in _records)
                {
                    foreach (var r in kvp.Value.Values)
                    {
                        if (r.Hwnd == hwnd)
                            return r;
                    }
                }

                return null;
            }
        }

        private static void RecalculateConfidence(WindowTrackingRecord record)
        {
            var score = 0.0;

            // Sample count (capped at MaxSamplesForConfidence).
            score += Math.Min(1.0, record.TimesSeenAtThisResolution / (double)MaxSamplesForConfidence) * 0.5;

            // Open rect bonus.
            if (record.LastSeenOpenRect != null)
                score += OpenRectBonus;

            // Not minimized bonus.
            if (record.LastState != WindowTrackingState.Minimized && record.LastState != WindowTrackingState.Unknown)
                score += NotMinimizedBonus;

            // Recency.
            var age = DateTime.UtcNow - record.LastSeenUtc;
            if (age <= RecentThreshold)
                score += 0.2 * (1.0 - age.TotalMinutes / RecentThreshold.TotalMinutes);

            record.ConfidenceScore = Math.Max(0.0, Math.Min(1.0, score));
        }

        private void PurgeStaleRecords(DateTime now, TimeSpan maxAge)
        {
            lock (_storeLock)
            {
                var keysToRemove = new List<string>();

                foreach (var kvp in _records)
                {
                    var resolutionsToRemove = new List<string>();

                    foreach (var r in kvp.Value)
                    {
                        if (now - r.Value.LastSeenUtc > maxAge && !IsWindow(r.Value.Hwnd))
                            resolutionsToRemove.Add(r.Key);
                    }

                    foreach (var res in resolutionsToRemove)
                        kvp.Value.TryRemove(res, out _);

                    if (kvp.Value.IsEmpty)
                        keysToRemove.Add(kvp.Key);
                }

                foreach (var key in keysToRemove)
                    _records.TryRemove(key, out _);
            }
        }

        private static bool ShouldTrack(IntPtr hwnd)
        {
            if (!IsWindowVisible(hwnd))
                return false;

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return false;

            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            var name = className.ToString();
            if (name.Contains("ExploreWClass") || name.Contains("CabinetWClass"))
                return false;
            if (name.Contains("Shell_TrayWnd") || name.Contains("Progman"))
                return false;
            if (name.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static string GetProcessName(IntPtr hwnd)
        {
            try
            {
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0)
                    return string.Empty;

                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            try
            {
                var len = GetWindowTextLength(hwnd);
                if (len <= 0 || len > 512)
                    return string.Empty;

                var sb = new StringBuilder(len + 10);
                GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString().Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}