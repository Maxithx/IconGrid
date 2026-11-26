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
    // Gaming overlay bar background height in px (design size, before GamingOverlayUiScale is
    // applied). Adjustable on the Monitor Row Layout settings page. 44 px is the factory default.
    public double GamingOverlayBackgroundHeight { get; set; } = 44;
    public double GamingOverlayFpsResponsiveness { get; set; } = 1.0;
    public bool GamingOverlayTransparentBackground { get; set; } = false;
    public bool GamingOverlayAutoTransparentBackground { get; set; } = false;
    public string GamingOverlayTextColor { get; set; } = "#FFFFFF";
    public string GamingOverlayPositionPreset { get; set; } = "TopRight";
    // Which blocks the gaming overlay shows. All default to true so an existing config
    // (where the properties do not exist yet) keeps the current overlay content untouched.
    public bool GamingOverlayShowClock { get; set; } = true;
    public bool GamingOverlayShowNetwork { get; set; } = true;
    public bool GamingOverlayShowCpu { get; set; } = true;
    public bool GamingOverlayShowGpu { get; set; } = true;
    public bool GamingOverlayShowFps { get; set; } = true;
    public bool GamingOverlayShowGpuClock { get; set; } = true;
    public bool GamingOverlayShowFrameTime { get; set; } = true;
    // Gaming overlay skin (Skin Studio). The JSON carries the full token set; the name is shown in
    // the UI. Empty/invalid JSON falls back to the built-in "Night Ops" default.
    public string GamingOverlaySkinName { get; set; } = GamingOverlaySkin.DefaultName;
    public string GamingOverlaySkinJson { get; set; } = string.Empty;
    // Data-driven overlay blueprint (layers + modular block layout). Empty = built-in MW4.
    public string GamingOverlayBlueprintJson { get; set; } = string.Empty;
    public int GameLauncherAutoBehavior { get; set; } = 0;
    public bool AutoShowGamingOverlayOnGameStart { get; set; } = false;
    public bool AutoCloseGamingOverlayOnGameEnd { get; set; } = false;
    public bool RestoreLauncherAfterOverlayClosed { get; set; } = false;
    // Opt-in GPU power saving: while a game session is alive but the game window
    // is not foreground, the elevated hardware monitor agent lowers the NVIDIA
    // power limit (percent of the card's default) and, optionally, the
    // temperature limit. Original values are restored automatically.
    public bool GpuPowerSavingEnabled { get; set; } = false;
    public int GpuPowerSavingPowerLimitPercent { get; set; } = 70;
    public bool GpuPowerSavingTempLimitEnabled { get; set; } = true;
    public int GpuPowerSavingTempLimitCelsius { get; set; } = 70;
    // Direct clock cap while the game is in the background, as a percentage of
    // the card's maximum SM clock. The temperature governor can step further
    // down, but never above this cap.
    public int GpuPowerSavingClockLimitPercent { get; set; } = 60;
    // Optional in-game "undervolt effect": switch to an Afterburner profile with a
    // flattened V/F curve and cap the SM clock at the curve's target while the game
    // has focus. On Turing the voltage follows the clock (V/F curve), so the lower
    // clock also means a lower voltage.
    public bool GpuUndervoltEnabled { get; set; } = false;
    /// <summary>Afterburner profile applied while a game session is running (0 = none).</summary>
    public int GpuUndervoltGameProfile { get; set; } = 1;
    /// <summary>Afterburner profile applied when no game is running (0 = do not switch back).</summary>
    public int GpuUndervoltIdleProfile { get; set; } = 0;
    /// <summary>
    /// Clock ceiling (MHz) of the generated undervolt curve. While the game has focus
    /// the agent locks the SM clock to this value so the card cannot boost past it.
    /// 0 = no in-game clock ceiling.
    /// </summary>
    public int GpuUndervoltTargetClockMhz { get; set; } = 0;
    /// <summary>
    /// Voltage (mV) where the flattened part of the generated undervolt curve starts.
    /// 0 = keep the stock voltage for the target clock. The UI limits this to a safe
    /// range so the user can tune gradually (for example 1800 MHz at 875 mV).
    /// </summary>
    public int GpuUndervoltTargetVoltageMv { get; set; } = 0;
    /// <summary>
    /// Afterburner power limit (percent) written into the generated profile.
    /// 100 keeps the profile's normal power limit.
    /// </summary>
    public int GpuUndervoltTargetPowerLimitPercent { get; set; } = 100;
    /// <summary>
    /// Core clock offset (MHz) written into the generated Afterburner profile.
    /// 0 keeps the stock curve; negative values underclock and positive values
    /// overclock. The UI limits this to a conservative range so a failed experiment
    /// cannot push the card far beyond its rated boost clock.
    /// </summary>
    public int GpuUndervoltCoreOffsetMhz { get; set; } = 0;
    /// <summary>
    /// Memory clock offset (MHz) written into the generated Afterburner profile.
    /// 0 keeps the stock memory clock; negative values underclock and positive
    /// values overclock. Memory instability usually shows up as artifacts or a
    /// driver reset, so the UI keeps the range conservative.
    /// </summary>
    public int GpuUndervoltMemoryOffsetMhz { get; set; } = 0;
    /// <summary>Opt-in warning when the GPU hotspot gets too hot (cooler contact check).</summary>
    public bool GpuHotspotAlarmEnabled { get; set; } = false;
    // Hot spot alarm threshold in °C (90-105). The delta warning (>15 °C above
    // the GPU edge temperature) is independent of this value.
    public int GpuHotspotAlarmThresholdC { get; set; } = 100;
    public bool ShowDesktopIcon { get; set; } = true;
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
    public int CarouselVisibleIcons { get; set; } = 4;
    public string Language { get; set; } = "da";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? SettingsWindowLeft { get; set; }
    public double? SettingsWindowTop { get; set; }
    public double? SettingsWindowWidth { get; set; }
    public double? SettingsWindowHeight { get; set; }
    public bool SettingsWindowIsMaximized { get; set; }
    public double? GamingOverlayWindowLeft { get; set; }
    public double? GamingOverlayWindowTop { get; set; }
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

    // Ping target selection. Stored as string for forward-compat (new targets can be added
    // without breaking older configs that simply default to "Auto"). Valid values:
    // "Auto" (default — gateway -> Cloudflare -> Google), "Gateway", "Cloudflare", "Google", "Custom".
    public string MonitorPingTargetMode { get; set; } = "Auto";
    // Used when MonitorPingTargetMode == "Custom". Accepts IP or hostname.
    public string MonitorPingCustomTarget { get; set; } = "";

    /// <summary>
    /// User-saved monitor layout defaults (JSON blob of 18 properties).
    /// When set, the "Reset defaults" button uses these instead of the hardcoded factory defaults.
    /// Saved via the "Save current as default" button in MonitorRowLayoutSection.
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