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
        public bool ShowDesktopIcon { get; set; }
        public bool IsAlwaysOnTop { get; set; }
        public bool IsFloatingIconTopmost { get; set; }
        public bool ShowScrollButtons { get; set; }
        public bool IsLightTheme { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowDevOverlay { get; set; }
        public double IconRowSpacing { get; set; }
        public double LastRowPaddingAdjust { get; set; }
        public List<string> TabNames { get; set; } = new();
        public string Language { get; set; } = "da";
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? SettingsWindowLeft { get; set; }
        public double? SettingsWindowTop { get; set; }
        public double? FloatingIconLeft { get; set; }
        public double? FloatingIconTop { get; set; }
        public string LayoutPreset { get; set; } = "Auto";
        public bool LayoutSkipMinimized { get; set; }
        public bool LayoutCurrentMonitorOnly { get; set; }
        public int LayoutIconGridSlot { get; set; }
        public bool LayoutReserveIconGridSlot { get; set; }
        public Dictionary<string, int> LayoutIconGridSlots { get; set; } = new();
        public Dictionary<string, int[]> LayoutLinks { get; set; } = new();
        public Dictionary<string, List<CustomLayoutSlot>> SavedLayouts { get; set; } = new();
        public List<CustomLayoutSlot> FavoriteLayoutSlots { get; set; } = new();
        public bool EnableSlideUpAnimation { get; set; }
        public bool EnableContentScroll { get; set; }
        public int WindowAnimationDurationMs { get; set; }
    }
}
