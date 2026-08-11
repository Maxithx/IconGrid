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
        public double GamingOverlayFpsResponsiveness { get; init; }
        public bool GamingOverlayTransparentBackground { get; init; }
        public bool GamingOverlayAutoTransparentBackground { get; init; }
        public string GamingOverlayTextColor { get; init; } = "#FFFFFF";
        public string GamingOverlayPositionPreset { get; init; } = "TopRight";
        public int GameLauncherAutoBehavior { get; init; }
        public bool AutoShowGamingOverlayOnGameStart { get; init; }
        public bool AutoCloseGamingOverlayOnGameEnd { get; init; }
        public bool RestoreLauncherAfterOverlayClosed { get; init; }
        public bool ShowDesktopIcon { get; init; }
        public bool StartDirectlyInLauncher { get; init; }
        public bool ShowDevOverlay { get; init; }
        public double IconRowSpacing { get; init; }
        public double LastRowPaddingAdjust { get; init; }
        public bool EnableSlideUpAnimation { get; init; }
        public bool EnableContentScroll { get; init; }
        public string IconViewMode { get; init; } = "Grid";
        public bool RestoreGameResolutionAfterExit { get; init; } = true;
        public int LauncherHideMode { get; init; }
        public int IdleAutoHideDelaySeconds { get; init; }
        public int PeekActivationMode { get; init; }
        public bool AllowMultiMonitorDrag { get; init; }
        public Dictionary<string, double> GamingOverlayResolutionScales { get; init; } = new();
        public int WindowAnimationDurationMs { get; init; }
        public FpsTargetConfig FpsTarget { get; init; } = new();
        public double MonitorPingToNetGap { get; init; } = 4;
        public double MonitorNetToDownloadGap { get; init; } = 4;
        public double MonitorDownloadToUploadGap { get; init; } = 4;
        public double MonitorUploadToCpuGap { get; init; } = 4;
        public double MonitorCpuToGpuGap { get; init; } = 4;
        public bool MonitorDivider0Visible { get; init; } = true;
        public bool MonitorDivider1Visible { get; init; } = true;
        public bool MonitorDivider2Visible { get; init; } = true;
        public bool MonitorDivider3Visible { get; init; } = true;
        public double MonitorDividerGap { get; init; } = 16;
        public double MonitorCpuBarGap { get; init; } = 8;
        public double MonitorGpuBarGap { get; init; } = 8;
        public double MonitorDownloadLabelToValueGap { get; init; } = 4;
        public double MonitorUploadLabelToValueGap { get; init; } = 4;
        public double MonitorDownloadValueToUnitGap { get; init; } = 4;
        public double MonitorUploadValueToUnitGap { get; init; } = 4;
        public double MonitorDownloadValueWidth { get; init; } = 20;
        public double MonitorUploadValueWidth { get; init; } = 20;
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
                GamingOverlayUiScale = config.GamingOverlayUiScale <= 0 ? 1.0 : Math.Max(1.0, Math.Min(1.5, config.GamingOverlayUiScale)),
                GamingOverlayFpsResponsiveness = config.GamingOverlayFpsResponsiveness <= 0 ? 0.78 : Math.Max(0.15, Math.Min(0.95, config.GamingOverlayFpsResponsiveness)),
                GamingOverlayTransparentBackground = config.GamingOverlayTransparentBackground,
                GamingOverlayAutoTransparentBackground = config.GamingOverlayAutoTransparentBackground,
                GamingOverlayTextColor = string.IsNullOrWhiteSpace(config.GamingOverlayTextColor) ? "#FFFFFF" : config.GamingOverlayTextColor,
                GamingOverlayPositionPreset = string.IsNullOrWhiteSpace(config.GamingOverlayPositionPreset) ? "TopRight" : config.GamingOverlayPositionPreset,
                GameLauncherAutoBehavior = config.GameLauncherAutoBehavior,
                AutoShowGamingOverlayOnGameStart = config.AutoShowGamingOverlayOnGameStart,
                AutoCloseGamingOverlayOnGameEnd = config.AutoCloseGamingOverlayOnGameEnd,
                RestoreLauncherAfterOverlayClosed = config.RestoreLauncherAfterOverlayClosed,
                ShowDesktopIcon = config.ShowDesktopIcon,
                StartDirectlyInLauncher = config.StartDirectlyInLauncher,
                ShowDevOverlay = config.ShowDevOverlay,
                IconRowSpacing = config.IconRowSpacing,
                LastRowPaddingAdjust = Math.Max(-20, Math.Min(20, config.LastRowPaddingAdjust)),
                EnableSlideUpAnimation = config.EnableSlideUpAnimation,
                EnableContentScroll = config.EnableContentScroll,
                IconViewMode = string.Equals(config.IconViewMode, "Carousel", StringComparison.OrdinalIgnoreCase) ? "Carousel" : "Grid",
                RestoreGameResolutionAfterExit = config.RestoreGameResolutionAfterExit,
                LauncherHideMode = config.LauncherHideMode,
                IdleAutoHideDelaySeconds = Math.Max(1, Math.Min(10, config.IdleAutoHideDelaySeconds)),
                PeekActivationMode = config.PeekActivationMode == 1 ? 1 : 0,
                AllowMultiMonitorDrag = config.AllowMultiMonitorDrag,
                GamingOverlayResolutionScales = config.GamingOverlayResolutionScales ?? new Dictionary<string, double>(),
                WindowAnimationDurationMs = config.WindowAnimationDurationMs,
                FpsTarget = config.FpsTarget ?? new FpsTargetConfig(),
                Language = string.IsNullOrWhiteSpace(config.Language) ? "da" : config.Language,
                WindowLeft = config.WindowLeft,
                WindowTop = config.WindowTop,
                SettingsWindowLeft = config.SettingsWindowLeft,
                SettingsWindowTop = config.SettingsWindowTop,
                GamingOverlayWindowLeft = config.GamingOverlayWindowLeft,
                GamingOverlayWindowTop = config.GamingOverlayWindowTop,
                FloatingIconLeft = config.FloatingIconLeft,
                FloatingIconTop = config.FloatingIconTop,
                MonitorPingToNetGap = config.MonitorPingToNetGap,
                MonitorNetToDownloadGap = config.MonitorNetToDownloadGap,
                MonitorDownloadToUploadGap = config.MonitorDownloadToUploadGap,
                MonitorUploadToCpuGap = config.MonitorUploadToCpuGap,
                MonitorCpuToGpuGap = config.MonitorCpuToGpuGap,
                MonitorDivider0Visible = config.MonitorDivider0Visible,
                MonitorDivider1Visible = config.MonitorDivider1Visible,
                MonitorDivider2Visible = config.MonitorDivider2Visible,
                MonitorDivider3Visible = config.MonitorDivider3Visible,
                MonitorDividerGap = config.MonitorDividerGap,
                MonitorCpuBarGap = config.MonitorCpuBarGap,
                MonitorGpuBarGap = config.MonitorGpuBarGap,
                MonitorDownloadLabelToValueGap = config.MonitorDownloadLabelToValueGap,
                MonitorUploadLabelToValueGap = config.MonitorUploadLabelToValueGap,
                MonitorDownloadValueToUnitGap = config.MonitorDownloadValueToUnitGap,
                MonitorUploadValueToUnitGap = config.MonitorUploadValueToUnitGap,
                MonitorDownloadValueWidth = config.MonitorDownloadValueWidth,
                MonitorUploadValueWidth = config.MonitorUploadValueWidth
            };
        }
    }
}
