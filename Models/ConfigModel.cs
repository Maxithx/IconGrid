using System.Collections.Generic;
using IconGrid.Models;

namespace IconGrid.Models;

public class ConfigModel
{
    public Dictionary<string, List<LauncherItem>> Tabs { get; set; } = new();
    public List<string> TabNames { get; set; } = new();
    public int IconsPerRow { get; set; } = 4;
    public double ContentAreaHeight { get; set; } = 310;
    public bool IsAlwaysOnTop { get; set; } = false;
    public double IconScale { get; set; } = 1.0;           // 100%
    public double UiScale { get; set; } = 1.0;             // 100% UI scaling
    public double GamingOverlayUiScale { get; set; } = 1.0; // 100% gaming overlay scaling
    public double GamingOverlayFpsResponsiveness { get; set; } = 1.0;
    public bool GamingOverlayTransparentBackground { get; set; } = false;
    public bool GamingOverlayAutoTransparentBackground { get; set; } = false;
    public string GamingOverlayTextColor { get; set; } = "#FFFFFF";
    public string GamingOverlayPositionPreset { get; set; } = "TopRight";
    public int GameLauncherAutoBehavior { get; set; } = 0;
    public bool AutoShowGamingOverlayOnGameStart { get; set; } = false;
    public bool AutoCloseGamingOverlayOnGameEnd { get; set; } = false;
    public bool RestoreLauncherAfterOverlayClosed { get; set; } = false;
    public bool ShowDesktopIcon { get; set; } = true;
    public bool StartDirectlyInLauncher { get; set; } = false;
    public bool ShowScrollButtons { get; set; } = true;
    public bool EnableContentScroll { get; set; } = true;
    public string IconViewMode { get; set; } = "Grid";
    public bool RestoreGameResolutionAfterExit { get; set; } = true;
    public int LauncherHideMode { get; set; } = 0;
    public int IdleAutoHideDelaySeconds { get; set; } = 2;
    public int PeekActivationMode { get; set; } = 0;
    public bool AllowMultiMonitorDrag { get; set; } = false;
    public Dictionary<string, double> GamingOverlayResolutionScales { get; set; } = new();
    public bool IsLightTheme { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public StartupLaunchMode StartupLaunchMode { get; set; } = StartupLaunchMode.TaskScheduler;
    public bool ShowDevOverlay { get; set; } = false;
    public double IconRowSpacing { get; set; } = 0;
    public double LastRowPaddingAdjust { get; set; } = 0;
    public string Language { get; set; } = "da";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? SettingsWindowLeft { get; set; }
    public double? SettingsWindowTop { get; set; }
    public double? GamingOverlayWindowLeft { get; set; }
    public double? GamingOverlayWindowTop { get; set; }
    public bool IsFloatingIconTopmost { get; set; } = true;
    public double? FloatingIconLeft { get; set; }
    public double? FloatingIconTop { get; set; }
    public string LayoutPreset { get; set; } = "Auto";
    public bool LayoutSkipMinimized { get; set; } = true;
    public bool LayoutCurrentMonitorOnly { get; set; } = true;
    public int LayoutIconGridSlot { get; set; } = 0;
    public Dictionary<string, int> LayoutIconGridSlots { get; set; } = new();
    public bool LayoutReserveIconGridSlot { get; set; } = true;
    public double LayoutIconGridSlotOffsetX { get; set; } = 0.5;
    public double LayoutIconGridSlotOffsetY { get; set; } = 0.5;
    public Dictionary<string, int[]> LayoutLinks { get; set; } = new();
    public Dictionary<string, List<CustomLayoutSlot>> SavedLayouts { get; set; } = new();
    public List<CustomLayoutSlot> FavoriteLayoutSlots { get; set; } = new();
    public bool EnableSlideUpAnimation { get; set; } = true;
    public int WindowAnimationDurationMs { get; set; } = 250;
    public FpsTargetConfig FpsTarget { get; set; } = new();

    // Monitor row layout (persisted) — defaults match the tuned "good" look, user can adjust via sliders
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

    /// <summary>
    /// User-saved monitor layout defaults (JSON blob of 18 properties).
    /// When set, the "Reset defaults" button uses these instead of the hardcoded factory defaults.
    /// Saved via "Save current as default" button on MonitorRowLayoutPage.
    /// </summary>
    public string? MonitorLayoutDefaults { get; set; }

    public static readonly string[] DefaultTabs = ["Games", "Software", "Develop"];

    public static ConfigModel CreateDefault()
    {
        var config = new ConfigModel();
        foreach (var tab in DefaultTabs)
        {
            config.Tabs[tab] = new List<LauncherItem>();
            config.TabNames.Add(tab);
        }
        return config;
    }

    public void EnsureDefaultTabs()
    {
        foreach (var tab in DefaultTabs)
        {
            if (!Tabs.ContainsKey(tab))
                Tabs[tab] = new List<LauncherItem>();
        }
    }

    public void EnsureTabNames()
    {
        if (TabNames == null || TabNames.Count == 0)
            TabNames = new List<string>(DefaultTabs);
    }
}

public class FpsTargetConfig
{
    public string? DisplayName { get; set; }
    public string? LauncherPath { get; set; }
    public string? ResolvedExecutablePath { get; set; }
    public string? ExecutableName { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? RootProcessId { get; set; }
    public long? RootProcessStartFileTimeUtc { get; set; }
    public long? LaunchCapturedFileTimeUtc { get; set; }
}