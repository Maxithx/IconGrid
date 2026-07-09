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
    public bool ShowDesktopIcon { get; set; } = true;      // show floating desktop icon when minimized
    public bool ShowScrollButtons { get; set; } = true;
    public bool EnableContentScroll { get; set; } = true;
    public bool IsLightTheme { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public StartupLaunchMode StartupLaunchMode { get; set; } = StartupLaunchMode.LegacyRun;
    public bool ShowDevOverlay { get; set; } = false;
    public double IconRowSpacing { get; set; } = -20;       // px
    public double LastRowPaddingAdjust { get; set; } = -50; // px
    public string Language { get; set; } = "da";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? SettingsWindowLeft { get; set; }
    public double? SettingsWindowTop { get; set; }
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
            {
                Tabs[tab] = new List<LauncherItem>();
            }
        }
    }

    public void EnsureTabNames()
    {
        if (TabNames == null || TabNames.Count == 0)
        {
            TabNames = new List<string>(DefaultTabs);
        }
    }
}
