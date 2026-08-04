using IconGrid.Helpers.Settings;
using IconGrid.Models;
using IconGrid.ViewModels.Settings;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel
    {
        private void ApplyConfig(ConfigModel config)
        {
            var state = MainViewModelConfigState.FromConfig(config);

            _iconsPerRow = state.IconsPerRow;
            _icon_scale = state.IconScale;
            _isAlwaysOnTop = state.IsAlwaysOnTop;
            _isFloatingIconTopmost = state.IsFloatingIconTopmost;
            _showScrollButtons = state.ShowScrollButtons;
            _themeState.ApplyConfig(config);
            _startWithWindows = state.StartWithWindows;
            _startupLaunchMode = StartupLaunchMode.TaskScheduler;
            _uiScale = state.UiScale;
            _gamingOverlayUiScale = state.GamingOverlayUiScale <= 0 ? 1.0 : Math.Max(0.7, Math.Min(1.2, state.GamingOverlayUiScale));
            _gamingOverlayFpsResponsiveness = state.GamingOverlayFpsResponsiveness <= 0 ? 1.0 : Math.Max(0.15, Math.Min(1.0, state.GamingOverlayFpsResponsiveness));
            _showDesktopIcon = state.ShowDesktopIcon;
            _startDirectlyInLauncher = state.StartDirectlyInLauncher;
            _showDevOverlay = state.ShowDevOverlay;
            _layoutMeasurements.ApplyMeasurementState(state.IconRowSpacing, state.LastRowPaddingAdjust, state.IconViewMode);
            _enableSlideUpAnimation = state.EnableSlideUpAnimation;
            _enableContentScroll = state.EnableContentScroll;
            _windowAnimationDurationMs = state.WindowAnimationDurationMs;
            _language = state.Language;
            _windowStateStore.ApplyConfig(
                state.WindowLeft,
                state.WindowTop,
                state.SettingsWindowLeft,
                state.SettingsWindowTop,
                state.GamingOverlayWindowLeft,
                state.GamingOverlayWindowTop,
                state.FloatingIconLeft,
                state.FloatingIconTop);
            _fpsTarget = state.FpsTarget ?? new FpsTargetConfig();
            _layoutState.ApplyConfig(config);
            NotifyConfigApplied();
        }

        private void ApplyDefaultSettingsState()
        {
            _iconsPerRow = 4;
            _layoutMeasurements.ApplyMeasurementState(0, 0, "Grid");
            _icon_scale = 1.0;
            _uiScale = 1.0;
            _gamingOverlayUiScale = 1.0;
            _gamingOverlayFpsResponsiveness = 1.0;
            _isAlwaysOnTop = false;
            _isFloatingIconTopmost = true;
            _showScrollButtons = true;
            _enableContentScroll = true;
            _startWithWindows = false;
            _startupLaunchMode = StartupLaunchMode.TaskScheduler;
            _showDevOverlay = false;
            _startDirectlyInLauncher = false;
            _language = "da";
            _themeState.SetIsLightTheme(true);
            _windowStateStore.ResetFloatingPosition();
            _windowAnimationDurationMs = 250;
            _fpsTarget = new FpsTargetConfig();
        }

        private ConfigModel LoadConfiguredState()
        {
            var config = _configManager.LoadConfig();
            config.EnsureTabNames();
            ApplyConfig(config);
            return config;
        }

        private void SaveSettingsToConfig()
        {
            if (_isInitializing || _tabsState == null || Items == null)
                return;

            var state = new MainViewModelSettingsState
            {
                ContentAreaHeight = _layoutMeasurements.CalculateContentAreaHeight(CurrentItems.Count, IconsPerRow, EffectiveIconScale, _enableContentScroll),
                IconsPerRow = _iconsPerRow,
                IconScale = _icon_scale,
                UiScale = _uiScale,
                GamingOverlayUiScale = _gamingOverlayUiScale,
                GamingOverlayFpsResponsiveness = _gamingOverlayFpsResponsiveness,
                ShowDesktopIcon = _showDesktopIcon,
                StartDirectlyInLauncher = _startDirectlyInLauncher,
                IsAlwaysOnTop = _isAlwaysOnTop,
                IsFloatingIconTopmost = _isFloatingIconTopmost,
                ShowScrollButtons = _showScrollButtons,
                IsLightTheme = _themeState.IsLightTheme,
                StartWithWindows = _startWithWindows,
                StartupLaunchMode = _startupLaunchMode,
                ShowDevOverlay = _showDevOverlay,
                IconRowSpacing = _layoutMeasurements.IconRowSpacing,
                LastRowPaddingAdjust = _layoutMeasurements.LastRowPaddingAdjust,
                TabNames = Tabs.ToList(),
                Language = _language,
                EnableSlideUpAnimation = _enableSlideUpAnimation,
                EnableContentScroll = _enableContentScroll,
                IconViewMode = _layoutMeasurements.IconViewMode,
                WindowAnimationDurationMs = _windowAnimationDurationMs,
                FpsTarget = _fpsTarget
            };

            _layoutState.ApplyToSettingsState(state);
            _windowStateStore.ApplyToSettingsState(state);
            _settingsPersistence.Save(_config, state);
        }
    }
}