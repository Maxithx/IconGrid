using System;
using System.Collections.Generic;
using IconGrid.Helpers.Launcher;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>
        /// Returns the default overlay scale for a resolution like "3840x2160".
        /// Falls back to the built-in defaults (4K=100%, 1440p=135%, everything else 100%)
        /// when the user has not overridden it.
        /// </summary>
        public double GetGamingOverlayScaleForResolution(string? resolution)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key != null && _gamingOverlayResolutionScales.TryGetValue(key, out var saved))
            {
                return Math.Max(1.0, Math.Min(1.5, saved));
            }

            return GetBuiltInOverlayScaleDefault(key);
        }

        /// <summary>
        /// Stores a user-chosen default overlay scale for a resolution like "3840x2160".
        /// </summary>
        public void SetGamingOverlayScaleForResolution(string? resolution, double scale)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key == null)
                return;

            var clamped = Math.Max(1.0, Math.Min(1.5, scale));
            _gamingOverlayResolutionScales[key] = clamped;
            SaveSettingsToConfig();
        }

        /// <summary>
        /// Removes the user override for a resolution, falling back to the built-in default.
        /// </summary>
        public void RemoveGamingOverlayScaleForResolution(string? resolution)
        {
            var key = NormalizeResolutionKey(resolution);
            if (key == null)
                return;

            if (_gamingOverlayResolutionScales.Remove(key))
            {
                SaveSettingsToConfig();
            }
        }

        /// <summary>
        /// Copies of the per-resolution scales for settings UI display.
        /// </summary>
        public IReadOnlyDictionary<string, double> GamingOverlayResolutionScalesSnapshot =>
            new Dictionary<string, double>(_gamingOverlayResolutionScales);

        private static string? NormalizeResolutionKey(string? resolution)
        {
            var parsed = DisplayResolutionService.ParseResolution(resolution);
            return parsed.HasValue ? $"{parsed.Value.Width}x{parsed.Value.Height}" : null;
        }

        private static double GetBuiltInOverlayScaleDefault(string? key)
        {
            switch (key)
            {
                case "3840x2160":
                    return 1.0;
                case "2560x1440":
                    return 1.35;
                default:
                    return 1.0;
            }
        }
    }
}