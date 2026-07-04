using System;
using System.Diagnostics;
using IconGrid.Helpers.Settings;
using IconGrid.Models;

namespace IconGrid.ViewModels.Settings
{
    public class MainViewModelSettingsPersistence
    {
        private readonly ConfigManager _configManager;

        public MainViewModelSettingsPersistence(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public void Save(ConfigModel config, MainViewModelSettingsState state)
        {
            try
            {
                config.ContentAreaHeight = state.ContentAreaHeight;
                config.IconsPerRow = state.IconsPerRow;
                config.IconScale = state.IconScale;
                config.UiScale = state.UiScale;
                config.ShowDesktopIcon = state.ShowDesktopIcon;
                config.IsAlwaysOnTop = state.IsAlwaysOnTop;
                config.IsFloatingIconTopmost = state.IsFloatingIconTopmost;
                config.ShowScrollButtons = state.ShowScrollButtons;
                config.IsLightTheme = state.IsLightTheme;
                config.StartWithWindows = state.StartWithWindows;
                config.ShowDevOverlay = state.ShowDevOverlay;
                config.IconRowSpacing = state.IconRowSpacing;
                config.LastRowPaddingAdjust = state.LastRowPaddingAdjust;
                config.TabNames = state.TabNames;
                config.Language = state.Language;
                config.WindowLeft = state.WindowLeft;
                config.WindowTop = state.WindowTop;
                config.SettingsWindowLeft = state.SettingsWindowLeft;
                config.SettingsWindowTop = state.SettingsWindowTop;
                config.FloatingIconLeft = state.FloatingIconLeft;
                config.FloatingIconTop = state.FloatingIconTop;
                config.LayoutPreset = state.LayoutPreset;
                config.LayoutSkipMinimized = state.LayoutSkipMinimized;
                config.LayoutCurrentMonitorOnly = state.LayoutCurrentMonitorOnly;
                config.LayoutIconGridSlot = state.LayoutIconGridSlot;
                config.LayoutReserveIconGridSlot = state.LayoutReserveIconGridSlot;
                config.LayoutIconGridSlotOffsetX = state.LayoutIconGridSlotOffsetX;
                config.LayoutIconGridSlotOffsetY = state.LayoutIconGridSlotOffsetY;
                config.LayoutIconGridSlots = state.LayoutIconGridSlots;
                config.LayoutLinks = state.LayoutLinks;
                config.SavedLayouts = state.SavedLayouts;
                config.FavoriteLayoutSlots = state.FavoriteLayoutSlots;
                config.EnableSlideUpAnimation = state.EnableSlideUpAnimation;
                config.EnableContentScroll = state.EnableContentScroll;
                config.WindowAnimationDurationMs = state.WindowAnimationDurationMs;

                _configManager.SaveConfig(config);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save settings: " + ex);
            }
        }
    }
}
