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
            _gamingOverlayUiScale = state.GamingOverlayUiScale <= 0 ? 1.0 : Math.Max(1.0, Math.Min(1.5, state.GamingOverlayUiScale));
            _gamingOverlayFpsResponsiveness = state.GamingOverlayFpsResponsiveness <= 0 ? 1.0 : Math.Max(0.15, Math.Min(1.0, state.GamingOverlayFpsResponsiveness));
            _gamingOverlayTransparentBackground = state.GamingOverlayTransparentBackground;
            _gamingOverlayAutoTransparentBackground = state.GamingOverlayAutoTransparentBackground;
            _gamingOverlayTextColor = string.IsNullOrWhiteSpace(state.GamingOverlayTextColor) ? "#FFFFFF" : state.GamingOverlayTextColor;
            _gamingOverlayPositionPreset = string.IsNullOrWhiteSpace(state.GamingOverlayPositionPreset) ? "TopRight" : state.GamingOverlayPositionPreset;
            _gameLauncherAutoBehavior = state.GameLauncherAutoBehavior;
            _autoShowGamingOverlayOnGameStart = state.AutoShowGamingOverlayOnGameStart;
            _autoCloseGamingOverlayOnGameEnd = state.AutoCloseGamingOverlayOnGameEnd;
            _restoreLauncherAfterOverlayClosed = state.RestoreLauncherAfterOverlayClosed;
            _restoreGameResolutionAfterExit = state.RestoreGameResolutionAfterExit;
            _launcherHideMode = state.LauncherHideMode;
            _idleAutoHideDelaySeconds = state.IdleAutoHideDelaySeconds;
            _peekActivationMode = state.PeekActivationMode;
            _gamingOverlayResolutionScales = state.GamingOverlayResolutionScales ?? new Dictionary<string, double>();
            _showDesktopIcon = state.ShowDesktopIcon;
            _startDirectlyInLauncher = state.StartDirectlyInLauncher;
            _showDevOverlay = state.ShowDevOverlay;
            _layoutMeasurements.ApplyMeasurementState(state.IconRowSpacing, state.LastRowPaddingAdjust, state.IconViewMode);
            _enableSlideUpAnimation = state.EnableSlideUpAnimation;
            _enableContentScroll = state.EnableContentScroll;
            _monitorPingToNetGap = state.MonitorPingToNetGap;
            _monitorNetToDownloadGap = state.MonitorNetToDownloadGap;
            _monitorDownloadToUploadGap = state.MonitorDownloadToUploadGap;
            _monitorUploadToCpuGap = state.MonitorUploadToCpuGap;
            _monitorCpuToGpuGap = state.MonitorCpuToGpuGap;
            _monitorDivider0Visible = state.MonitorDivider0Visible;
            _monitorDivider1Visible = state.MonitorDivider1Visible;
            _monitorDivider2Visible = state.MonitorDivider2Visible;
            _monitorDivider3Visible = state.MonitorDivider3Visible;
            _monitorDividerGap = state.MonitorDividerGap;
            _monitorCpuBarGap = state.MonitorCpuBarGap;
            _monitorGpuBarGap = state.MonitorGpuBarGap;
            _monitorDownloadLabelToValueGap = state.MonitorDownloadLabelToValueGap;
            _monitorUploadLabelToValueGap = state.MonitorUploadLabelToValueGap;
            _monitorDownloadValueToUnitGap = state.MonitorDownloadValueToUnitGap;
            _monitorUploadValueToUnitGap = state.MonitorUploadValueToUnitGap;
            _monitorDownloadValueWidth = state.MonitorDownloadValueWidth;
            _monitorUploadValueWidth = state.MonitorUploadValueWidth;
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
            _gamingOverlayTransparentBackground = false;
            _gamingOverlayAutoTransparentBackground = false;
            _gamingOverlayTextColor = "#FFFFFF";
            _gamingOverlayPositionPreset = "TopRight";
            _gameLauncherAutoBehavior = 0;
            _autoShowGamingOverlayOnGameStart = false;
            _autoCloseGamingOverlayOnGameEnd = false;
            _restoreLauncherAfterOverlayClosed = false;
            _restoreGameResolutionAfterExit = true;
            _launcherHideMode = 0;
            _idleAutoHideDelaySeconds = 2;
            _peekActivationMode = 0;
            _gamingOverlayResolutionScales = new Dictionary<string, double>();
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
                GamingOverlayTransparentBackground = _gamingOverlayTransparentBackground,
                GamingOverlayAutoTransparentBackground = _gamingOverlayAutoTransparentBackground,
                GamingOverlayTextColor = _gamingOverlayTextColor,
                GamingOverlayPositionPreset = _gamingOverlayPositionPreset,
                GameLauncherAutoBehavior = _gameLauncherAutoBehavior,
                AutoShowGamingOverlayOnGameStart = _autoShowGamingOverlayOnGameStart,
                AutoCloseGamingOverlayOnGameEnd = _autoCloseGamingOverlayOnGameEnd,
                RestoreLauncherAfterOverlayClosed = _restoreLauncherAfterOverlayClosed,
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
                RestoreGameResolutionAfterExit = _restoreGameResolutionAfterExit,
                LauncherHideMode = _launcherHideMode,
                IdleAutoHideDelaySeconds = _idleAutoHideDelaySeconds,
                PeekActivationMode = _peekActivationMode,
                AllowMultiMonitorDrag = _allowMultiMonitorDrag,
                GamingOverlayResolutionScales = _gamingOverlayResolutionScales,
                WindowAnimationDurationMs = _windowAnimationDurationMs,
                FpsTarget = _fpsTarget,
                MonitorPingToNetGap = _monitorPingToNetGap,
                MonitorNetToDownloadGap = _monitorNetToDownloadGap,
                MonitorDownloadToUploadGap = _monitorDownloadToUploadGap,
                MonitorUploadToCpuGap = _monitorUploadToCpuGap,
                MonitorCpuToGpuGap = _monitorCpuToGpuGap,
                MonitorDivider0Visible = _monitorDivider0Visible,
                MonitorDivider1Visible = _monitorDivider1Visible,
                MonitorDivider2Visible = _monitorDivider2Visible,
                MonitorDivider3Visible = _monitorDivider3Visible,
                MonitorDividerGap = _monitorDividerGap,
                MonitorCpuBarGap = _monitorCpuBarGap,
                MonitorGpuBarGap = _monitorGpuBarGap,
                MonitorDownloadLabelToValueGap = _monitorDownloadLabelToValueGap,
                MonitorUploadLabelToValueGap = _monitorUploadLabelToValueGap,
                MonitorDownloadValueToUnitGap = _monitorDownloadValueToUnitGap,
                MonitorUploadValueToUnitGap = _monitorUploadValueToUnitGap,
                MonitorDownloadValueWidth = _monitorDownloadValueWidth,
                MonitorUploadValueWidth = _monitorUploadValueWidth,
            };

            _layoutState.ApplyToSettingsState(state);
            _windowStateStore.ApplyToSettingsState(state);
            _settingsPersistence.Save(_config, state);
        }
    }
}