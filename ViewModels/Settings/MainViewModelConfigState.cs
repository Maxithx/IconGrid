using IconGrid.Models;

namespace IconGrid.ViewModels.Settings
{
    public class MainViewModelConfigState
    {
        public int IconsPerRow { get; init; }
        public double IconScale { get; init; }
        public bool IsAlwaysOnTop { get; init; }
        public bool IsFloatingIconTopmost { get; init; }
        public bool ShowScrollButtons { get; init; }
        public bool StartWithWindows { get; init; }
        public StartupLaunchMode StartupLaunchMode { get; init; }
        public double UiScale { get; init; }
        public double GamingOverlayUiScale { get; init; }
        public bool ShowDesktopIcon { get; init; }
        public bool StartDirectlyInLauncher { get; init; }
        public bool ShowDevOverlay { get; init; }
        public double IconRowSpacing { get; init; }
        public double LastRowPaddingAdjust { get; init; }
        public bool EnableSlideUpAnimation { get; init; }
        public bool EnableContentScroll { get; init; }
        public int WindowAnimationDurationMs { get; init; }
        public string Language { get; init; } = "da";
        public double? WindowLeft { get; init; }
        public double? WindowTop { get; init; }
        public double? SettingsWindowLeft { get; init; }
        public double? SettingsWindowTop { get; init; }
        public double? GamingOverlayWindowLeft { get; init; }
        public double? GamingOverlayWindowTop { get; init; }
        public double? FloatingIconLeft { get; init; }
        public double? FloatingIconTop { get; init; }

        public static MainViewModelConfigState FromConfig(ConfigModel config)
        {
            return new MainViewModelConfigState
            {
                IconsPerRow = config.IconsPerRow < 4 ? 4 : config.IconsPerRow,
                IconScale = config.IconScale,
                IsAlwaysOnTop = config.IsAlwaysOnTop,
                IsFloatingIconTopmost = config.IsFloatingIconTopmost,
                ShowScrollButtons = config.ShowScrollButtons,
                StartWithWindows = config.StartWithWindows,
                StartupLaunchMode = config.StartupLaunchMode,
                UiScale = config.UiScale <= 0 ? 1.0 : Math.Max(0.8, Math.Min(1.0, config.UiScale)),
                GamingOverlayUiScale = config.GamingOverlayUiScale <= 0 ? 1.0 : Math.Max(0.7, Math.Min(1.2, config.GamingOverlayUiScale)),
                ShowDesktopIcon = config.ShowDesktopIcon,
                StartDirectlyInLauncher = config.StartDirectlyInLauncher,
                ShowDevOverlay = config.ShowDevOverlay,
                IconRowSpacing = config.IconRowSpacing,
                LastRowPaddingAdjust = config.LastRowPaddingAdjust,
                EnableSlideUpAnimation = config.EnableSlideUpAnimation,
                EnableContentScroll = config.EnableContentScroll,
                WindowAnimationDurationMs = config.WindowAnimationDurationMs,
                Language = string.IsNullOrWhiteSpace(config.Language) ? "da" : config.Language,
                WindowLeft = config.WindowLeft,
                WindowTop = config.WindowTop,
                SettingsWindowLeft = config.SettingsWindowLeft,
                SettingsWindowTop = config.SettingsWindowTop,
                GamingOverlayWindowLeft = config.GamingOverlayWindowLeft,
                GamingOverlayWindowTop = config.GamingOverlayWindowTop,
                FloatingIconLeft = config.FloatingIconLeft,
                FloatingIconTop = config.FloatingIconTop
            };
        }
    }
}
