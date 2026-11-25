using System;
using System.Threading.Tasks;
using IconGrid.Helpers.Logging;

namespace IconGrid.Helpers.Hardware;

/// <summary>
/// Shares the hardware overview between HardwarePage instances.
///
/// SettingsWindow creates a new HardwarePage for every navigation. The page's
/// vendor logos are only visible when the overview says which vendor is present,
/// so without this cache every visit had to wait for WMI/LibreHardwareMonitor
/// before the logos appeared. The overview is loaded once in the background and
/// reused; a page can therefore render the logos immediately.
/// </summary>
public static class HardwareOverviewCache
{
    private static readonly object Gate = new();
    private static HardwareOverview? _current;
    private static Task<HardwareOverview?>? _loadTask;

    /// <summary>The most recently loaded overview, or null before the first load.</summary>
    public static HardwareOverview? Current
    {
        get
        {
            lock (Gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Returns the cached overview, or starts one shared background load.
    /// Never starts a second load while the first one is still running.
    /// </summary>
    public static Task<HardwareOverview?> GetOrLoadAsync()
    {
        lock (Gate)
        {
            if (_current is { } cached)
            {
                return Task.FromResult<HardwareOverview?>(cached);
            }

            return _loadTask ??= LoadCoreAsync();
        }
    }

    /// <summary>
    /// Starts loading the overview in the background during app startup, so the
    /// first Hardware page visit usually has data available immediately.
    /// </summary>
    public static void Preload()
    {
        _ = GetOrLoadAsync();
    }

    private static async Task<HardwareOverview?> LoadCoreAsync()
    {
        try
        {
            var overview = await Task.Run(HardwareInfoProvider.LoadOverview).ConfigureAwait(false);

            lock (Gate)
            {
                _current = overview;
                _loadTask = null;
            }

            AppTrace.Write("[HardwareOverviewCache] Hardware overview loaded.");
            return overview;
        }
        catch (Exception ex)
        {
            lock (Gate)
            {
                // Allow a later page visit to retry.
                _loadTask = null;
            }

            AppTrace.Write($"[HardwareOverviewCache] Failed to load hardware overview: {ex.Message}");
            return null;
        }
    }
}
