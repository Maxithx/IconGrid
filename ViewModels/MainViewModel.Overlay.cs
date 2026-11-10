using System;
using System.Collections.Generic;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;

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
        /// Shows the clock block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowClock
        {
            get => _gamingOverlayShowClock;
            set
            {
                if (SetField(ref _gamingOverlayShowClock, value, nameof(GamingOverlayShowClock)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerClockNetworkVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the ping dot + network status block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowNetwork
        {
            get => _gamingOverlayShowNetwork;
            set
            {
                if (SetField(ref _gamingOverlayShowNetwork, value, nameof(GamingOverlayShowNetwork)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerClockNetworkVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerNetworkCpuVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the CPU block (temperature + usage bar) in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowCpu
        {
            get => _gamingOverlayShowCpu;
            set
            {
                if (SetField(ref _gamingOverlayShowCpu, value, nameof(GamingOverlayShowCpu)))
                {
                    OnPropertyChanged(nameof(GamingOverlayDividerNetworkCpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerCpuGpuVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the GPU block (temperature + usage bar) in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowGpu
        {
            get => _gamingOverlayShowGpu;
            set
            {
                if (SetField(ref _gamingOverlayShowGpu, value, nameof(GamingOverlayShowGpu)))
                {
                    OnPropertyChanged(nameof(GamingOverlayGpuClockVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerCpuGpuVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerGpuFpsVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the FPS block in the gaming overlay. Persisted in config.
        /// </summary>
        public bool GamingOverlayShowFps
        {
            get => _gamingOverlayShowFps;
            set
            {
                if (SetField(ref _gamingOverlayShowFps, value, nameof(GamingOverlayShowFps)))
                {
                    OnPropertyChanged(nameof(GamingOverlayFrameTimeVisible));
                    OnPropertyChanged(nameof(GamingOverlayDividerGpuFpsVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the GPU core clock (MHz/GHz) next to the GPU temperature block.
        /// Persisted in config. Only rendered while the GPU block itself is visible.
        /// </summary>
        public bool GamingOverlayShowGpuClock
        {
            get => _gamingOverlayShowGpuClock;
            set
            {
                if (SetField(ref _gamingOverlayShowGpuClock, value, nameof(GamingOverlayShowGpuClock)))
                {
                    OnPropertyChanged(nameof(GamingOverlayGpuClockVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>
        /// Shows the frame time (1000 / FPS, in ms) next to the FPS value.
        /// Persisted in config. Only rendered while the FPS block itself is visible.
        /// </summary>
        public bool GamingOverlayShowFrameTime
        {
            get => _gamingOverlayShowFrameTime;
            set
            {
                if (SetField(ref _gamingOverlayShowFrameTime, value, nameof(GamingOverlayShowFrameTime)))
                {
                    OnPropertyChanged(nameof(GamingOverlayFrameTimeVisible));
                    SaveSettingsToConfig();
                }
            }
        }

        /// <summary>GPU clock is part of the GPU block, so the block must be visible too.</summary>
        public bool GamingOverlayGpuClockVisible => _gamingOverlayShowGpu && _gamingOverlayShowGpuClock;

        /// <summary>Frame time is part of the FPS block, so the block must be visible too.</summary>
        public bool GamingOverlayFrameTimeVisible => _gamingOverlayShowFps && _gamingOverlayShowFrameTime;

        // Dividers are only shown BETWEEN two visible blocks, so hiding a block never leaves
        // a dangling "|" at the start/end of the overlay row.
        public bool GamingOverlayDividerClockNetworkVisible => _gamingOverlayShowClock && _gamingOverlayShowNetwork;

        public bool GamingOverlayDividerNetworkCpuVisible => _gamingOverlayShowNetwork && _gamingOverlayShowCpu;

        public bool GamingOverlayDividerCpuGpuVisible => _gamingOverlayShowCpu && _gamingOverlayShowGpu;

        public bool GamingOverlayDividerGpuFpsVisible => _gamingOverlayShowGpu && _gamingOverlayShowFps;

        /// <summary>
        /// Active gaming overlay skin (colours + fonts only — never layout). Bindings inside
        /// GamingOverlayBar read straight from this instance, so editing it updates the overlay and
        /// the settings preview live.
        /// </summary>
        public GamingOverlaySkin GamingOverlaySkin => _gamingOverlaySkin;

        /// <summary>
        /// Applies another skin (preset or imported) onto the active instance and persists it.
        /// Copying into the existing instance keeps all nested bindings alive.
        /// </summary>
        public void ApplyGamingOverlaySkin(GamingOverlaySkin skin, bool save)
        {
            if (skin == null)
            {
                return;
            }

            _gamingOverlaySkin.CopyFrom(skin);

            if (save)
            {
                SaveSettingsToConfig();
            }

            OnPropertyChanged(nameof(GamingOverlaySkin));
        }

        /// <summary>
        /// Persists the current skin after an edit in the Skin Studio page.
        /// </summary>
        public void SaveGamingOverlaySkin() => SaveSettingsToConfig();

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