using System;
using System.Runtime.InteropServices;

namespace IconGrid.Models
{
    /// <summary>
    /// Tracks a single window's position, state, and confidence across resolutions.
    /// Used by <see cref="Helpers.Launcher.WindowTrackingService"/> to provide the
    /// layout system with reliable positional data, even for minimized windows
    /// whose GetWindowRect returns (-32000, -32000).
    /// </summary>
    public sealed class WindowTrackingRecord
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>The window handle this record tracks.</summary>
        public IntPtr Hwnd { get; init; }

        /// <summary>Process name without .exe extension (e.g. "PathOfExile").</summary>
        public string ProcessName { get; init; } = string.Empty;

        /// <summary>Window title text at the time of the last sample.</summary>
        public string WindowTitle { get; init; } = string.Empty;

        /// <summary>Resolution key (e.g. "3840x2160") this record was captured at.</summary>
        public string Resolution { get; init; } = "3840x2160";

        /// <summary>
        /// Last known rectangle (any window state).
        /// May be (-32000, -32000) if the window was last seen minimized.
        /// </summary>
        public RECT LastKnownRect;

        /// <summary>
        /// Last rectangle captured while the window was open (not minimized, not maximized).
        /// This is the most trustworthy rect for restore operations.
        /// </summary>
        public RECT? LastSeenOpenRect;

        /// <summary>Window state at the time of the last sample.</summary>
        public WindowTrackingState LastState;

        /// <summary>
        /// How many times this window has been sampled at this specific resolution.
        /// Higher counts increase the confidence score.
        /// </summary>
        public int TimesSeenAtThisResolution;

        /// <summary>
        /// Confidence score (0.0–1.0) indicating how reliable the positional data is.
        /// Calculated from sample count, open-rect availability, and recency.
        /// </summary>
        public double ConfidenceScore;

        /// <summary>UTC timestamp of the most recent sample.</summary>
        public DateTime LastSeenUtc = DateTime.UtcNow;

        /// <summary>
        /// Builds a stable lookup key for grouping windows across tracking cycles.
        /// Uses process name + window title as a lossy-but-stable identity.
        /// </summary>
        public string CompositeKey => $"{ProcessName}|{WindowTitle}";

        public override string ToString()
        {
            return $"[{ProcessName}] \"{WindowTitle}\" @ {Resolution} — state={LastState}, seen={TimesSeenAtThisResolution}x, confidence={ConfidenceScore:F2}";
        }
    }

    /// <summary>Window state as reported by Win32.</summary>
    public enum WindowTrackingState
    {
        Unknown,
        Normal,
        Minimized,
        Maximized
    }
}