using System.Collections.Generic;
using IconGrid.Models;

namespace IconGrid.ViewModels.Settings
{
    public class MainViewModelSettingsState
    {
        public double ContentAreaHeight { get; set; }
        public int IconsPerRow { get; set; }
        public double IconScale { get; set; }
        public double UiScale { get; set; }
        public double GamingOverlayUiScale { get; set; }
        public double GamingOverlayFpsResponsiveness { get; set; }
        public bool GamingOverlayTransparentBackground { get; set; }
        public bool GamingOverlayAutoTransparentBackground { get; set; }
        public string GamingOverlayTextColor { get; set; } = "#FFFFFF";
        public string GamingOverlayPositionPreset { get; set; } = "TopRight";
        public int GameLauncherAutoBehavior { get; set; }
        public bool AutoShowGamingOverlayOnGameStart { get; set; }
        public bool AutoCloseGamingOverlayOnGameEnd { get; set; }
        public bool RestoreLauncherAfterOverlayClosed { get; set; }
        public bool ShowDesktopIcon { get; set; }
        public bool StartDirectlyInLauncher { get; set; }
        public bool IsAlwaysOnTop { get; set; }
        public bool IsFloatingIconTopmost { get; set; }
        public bool ShowScrollButtons { get; set; }
        public bool IsLightTheme { get; set; }
        public bool StartWithWindows { get; set; }
        public StartupLaunchMode StartupLaunchMode { get; set; }
        public bool ShowDevOverlay { get; set; }
        public double IconRowSpacing { get; set; }
        public double LastRowPaddingAdjust { get; set; }
        public int CarouselVisibleIcons { get; set; } = 4;
        public List<string> TabNames { get; set; } = new();
        public string Language { get; set; } = "da";
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? SettingsWindowLeft { get; set; }
        public double? SettingsWindowTop { get; set; }
        public double? GamingOverlayWindowLeft { get; set; }
        public double? GamingOverlayWindowTop { get; set; }
        public double? FloatingIconLeft { get; set; }
        public double? FloatingIconTop { get; set; }
        public string LayoutPreset { get; set; } = "Auto";
        public bool LayoutSkipMinimized { get; set; }
        public bool LayoutCurrentMonitorOnly { get; set; }
        public int LayoutIconGridSlot { get; set; }
        public bool LayoutReserveIconGridSlot { get; set; }
        public double LayoutIconGridSlotOffsetX { get; set; } = 0.5;
        public double LayoutIconGridSlotOffsetY { get; set; } = 0.5;
        public Dictionary<string, int> LayoutIconGridSlots { get; set; } = new();
        public Dictionary<string, int[]> LayoutLinks { get; set; } = new();
        public Dictionary<string, List<CustomLayoutSlot>> SavedLayouts { get; set; } = new();
        public List<CustomLayoutSlot> FavoriteLayoutSlots { get; set; } = new();
        public bool EnableSlideUpAnimation { get; set; }
        public bool EnableContentScroll { get; set; }
        public string IconViewMode { get; set; } = "Grid";
        public bool RestoreGameResolutionAfterExit { get; set; } = true;
        public int LauncherHideMode { get; set; }
        public int IdleAutoHideDelaySeconds { get; set; }
        public int PeekActivationMode { get; set; }
        public bool AllowMultiMonitorDrag { get; set; }
        public Dictionary<string, double> GamingOverlayResolutionScales { get; set; } = new();
        public int WindowAnimationDurationMs { get; set; }
        public FpsTargetConfig FpsTarget { get; set; } = new();
        public double MonitorPingToNetGap { get; set; } = 4;
        public double MonitorNetToDownloadGap { get; set; } = 4;
        public double MonitorDownloadToUploadGap { get; set; } = 4;
        public double MonitorUploadToCpuGap { get; set; } = 4;
        public double MonitorCpuToGpuGap { get; set; } = 4;
        public bool MonitorDivider0Visible { get; set; } = true;
        public bool MonitorDivider1Visible { get; set; } = true;
        public bool MonitorDivider2Visible { get; set; } = true;
        public bool MonitorDivider3Visible { get; set; } = true;
        public double MonitorDividerGap { get; set; } = 16;
        public double MonitorCpuBarGap { get; set; } = 8;
        public double MonitorGpuBarGap { get; set; } = 8;
        public double MonitorDownloadLabelToValueGap { get; set; } = 4;
        public double MonitorUploadLabelToValueGap { get; set; } = 4;
        public double MonitorDownloadValueToUnitGap { get; set; } = 4;
        public double MonitorUploadValueToUnitGap { get; set; } = 4;
        public double MonitorDownloadValueWidth { get; set; } = 20;
        public double MonitorUploadValueWidth { get; set; } = 20;
        public string MonitorPingTargetMode { get; set; } = "Auto";
        public string MonitorPingCustomTarget { get; set; } = "";
    }
}