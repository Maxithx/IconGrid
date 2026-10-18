using System;
using System.Runtime.InteropServices;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// System-wide time since the last keyboard/mouse input.
///
/// Used as a structural discriminator in the game-detection telemetry: a game
/// keeps presenting frames even when the user is not touching anything, while a
/// terminal, editor or Electron UI only presents in response to input.
/// </summary>
internal static class SystemInputIdle
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary>
    /// Seconds since the last user input, or null when Windows cannot report it.
    /// </summary>
    public static double? TryGetSeconds()
    {
        try
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return null;
            }

            // Both clocks are "milliseconds since boot"; the API reports the low
            // 32 bits, so the subtraction must be done in the same 32-bit space to
            // stay correct across the ~49 day wrap.
            var idleMilliseconds = unchecked((uint)Environment.TickCount64 - info.Time);
            return idleMilliseconds / 1000.0;
        }
        catch
        {
            return null;
        }
    }
}
