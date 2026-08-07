using System;
using System.Collections.Generic;
using IconGrid.Helpers.Launcher;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>
        /// Standard corner/edge placement for the gaming overlay, chosen by the user
        /// ("TopLeft".."BottomRight", or "Custom"). The overlay window snaps to this
        /// preset whenever it opens or the display resolution changes — but the user
        /// can still drag the overlay anywhere afterwards (Custom stores that position).
        /// </summary>
        public string GamingOverlayPositionPreset
        {
            get => _gamingOverlayPositionPreset;
            set
            {
                var clamped = string.IsNullOrWhiteSpace(value) ? "TopRight" : value;
                if (SetField(ref _gamingOverlayPositionPreset, clamped))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Launcher behavior while a game is running: 0=None, 1=AutoHide, 2=MinimizeToTaskbar.
        /// Persisted in config.
        /// </summary>
        public int GameLauncherAutoBehavior
        {
            get => _gameLauncherAutoBehavior;
            set
            {
                var clamped = Math.Max(0, Math.Min(2, value));
                if (SetField(ref _gameLauncherAutoBehavior, clamped))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Automatically show the gaming overlay when a game starts (positioned at the
        /// chosen "Default position"). Persisted in config.
        /// </summary>
        public bool AutoShowGamingOverlayOnGameStart
        {
            get => _autoShowGamingOverlayOnGameStart;
            set
            {
                if (SetField(ref _autoShowGamingOverlayOnGameStart, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Automatically close the gaming overlay when the game exits. Persisted in config.
        /// </summary>
        public bool AutoCloseGamingOverlayOnGameEnd
        {
            get => _autoCloseGamingOverlayOnGameEnd;
            set
            {
                if (SetField(ref _autoCloseGamingOverlayOnGameEnd, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Restore the main launcher (if it was hidden/minimized for a game) when the
        /// gaming overlay is closed — manually or automatically. Persisted in config.
        /// </summary>
        public bool RestoreLauncherAfterOverlayClosed
        {
            get => _restoreLauncherAfterOverlayClosed;
            set
            {
                if (SetField(ref _restoreLauncherAfterOverlayClosed, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Returns the default overlay scale for a resolution like "3840x2160".
        /// Falls back to the built-in default (100% for every resolution — the scale is
        /// the real physical size; the 1440p=135% legacy default only applied when the
        /// overlay window still compensated for resolution) when the user has not
        /// overridden it.
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
            // The overlay scale is the REAL physical size: 100% is the design size
            // (720x44) on every resolution. There is no resolution compensation in
            // the overlay window anymore, so every resolution gets the same default
            // (100%). Users can override per resolution via the settings page or the
            // overlay scale slider.
            _ = key;
            return 1.0;
        }
    }
}