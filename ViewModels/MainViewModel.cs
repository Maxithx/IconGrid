using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows.Input;
using System.Windows;
using Microsoft.Win32;
using WMedia = System.Windows.Media;
using IconGrid.Helpers;
using IconGrid.Helpers.Settings;
using IconGrid.Models;
using IconGrid.ViewModels.Launcher;
using IconGrid.ViewModels.Settings;

namespace IconGrid.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        // ---------- Fields ----------

        private int _iconsPerRow = 4;
        private double _icon_scale = 1.0;


        // NEW: UI settings backing fields
        private bool _isAlwaysOnTop = true;
        private bool _showScrollButtons = true;
        private bool _startWithWindows = false;
        private StartupLaunchMode _startupLaunchMode = StartupLaunchMode.TaskScheduler;
        private bool _isFloatingIconTopmost = true;
        private double _uiScale = 1.0;
        private bool _showDesktopIcon = true;
        private bool _startDirectlyInLauncher = false;
        private bool _showDevOverlay = false;
        private bool _resetSettingsToggle;
        private double _fixedContentWidth = 720;
        private double _gamingOverlayUiScale = 1.0;
        private double _gamingOverlayFpsResponsiveness = 1.0;
        private bool _gamingOverlayTransparentBackground = false;
        private bool _gamingOverlayAutoTransparentBackground = false;
        private string _gamingOverlayTextColor = "#FFFFFF";
        private const double GamingOverlayBaseWidth = 720;
        private const double GamingOverlayBaseHeight = 44;
        private bool _isFullWindowVisible = false;
        private bool _enableSlideUpAnimation = true;
        private bool _enableContentScroll = true;
        private int _windowAnimationDurationMs = 250;

        private readonly LauncherLayoutMeasurements _layoutMeasurements = new();
        private readonly LauncherLayoutState _layoutState = new();
        private readonly WindowStateStore _windowStateStore = new();
        private readonly string _dataFolder;
        private readonly string _legacyDataFolder;
        private readonly string _iconPackFolder;
        private readonly ConfigManager _configManager;
        private readonly SystemMonitor _systemMonitor = new();
        private readonly LauncherTabsState _tabsState;
        private readonly LauncherItemsManager _itemsManager;
        private readonly LauncherItemIconManager _itemIconManager;
        private readonly LauncherItemLaunchManager _itemLaunchManager;
        private readonly LauncherThemeState _themeState = new();
        private readonly LauncherThemeCoordinator _themeCoordinator = new();
        private readonly LauncherLocalizationState _localizationState = new();
        private readonly LauncherOverlayState _overlayState = new();
        private readonly LauncherShortcutManager _shortcutManager;
        private readonly LauncherItemsPersistence _itemsPersistence;
        private readonly MainViewModelSettingsPersistence _settingsPersistence;
        private ConfigModel _config;
        private bool _isInitializing = true;
        private FpsTargetConfig _fpsTarget = new();

        public SystemMonitor SystemMonitor => _systemMonitor;

        // ---------- Constructor ----------

        public MainViewModel()
        {
            _configManager = new ConfigManager();
            _settingsPersistence = new MainViewModelSettingsPersistence(_configManager);
            _config = LoadConfiguredState();
            (_dataFolder, _legacyDataFolder, _iconPackFolder) = CreateStoragePaths();
            EnsureIconPackFolder();
            InitializeAppearance();
            _systemMonitor.PropertyChanged += SystemMonitor_PropertyChanged;
            _tabsState = CreateTabsState();
            _tabsState.PropertyChanged += TabsState_PropertyChanged;
            Items = new ObservableCollection<LauncherItem>();
            _layoutMeasurements.PropertyChanged += LayoutMeasurements_PropertyChanged;
            (_itemsManager, _itemIconManager, _itemLaunchManager, _shortcutManager, _itemsPersistence) = CreateManagers();
            Items.CollectionChanged += (s, e) =>
            {
                SaveItemsToFile();
                OnPropertyChanged(nameof(CurrentItems));
                OnPropertyChanged(nameof(ContentMinWidth));
                OnPropertyChanged(nameof(ContentAreaHeight));
                OnPropertyChanged(nameof(ContentHostHeight));
                OnPropertyChanged(nameof(ContentWidth));
                OnPropertyChanged(nameof(WindowDesiredHeight));
                OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                OnPropertyChanged(nameof(WindowDesiredWidth));
                OnPropertyChanged(nameof(IconMargin));
            };

            (SelectTabCommand, ResetSettingsCommand) = CreateCommands();
            RunStartupInitialization();
            _isInitializing = false;
        }

        // ---------- Public properties ----------

        public ObservableCollection<string> Tabs => _tabsState?.Tabs ?? new ObservableCollection<string>();

        public ObservableCollection<LauncherItem> Items { get; }

        public ICommand SelectTabCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public string SelectedTab
        {
            get => _tabsState?.SelectedTab ?? string.Empty;
            set
            {
                if (_tabsState == null)
                    return;

                _tabsState.SelectedTab = value;
            }
        }

        /// <summary>
        /// Items shown in the currently selected tab.
        /// </summary>
        public ObservableCollection<LauncherItem> CurrentItems
        {
            get
            {
                return new ObservableCollection<LauncherItem>(GetItemsForSelectedTabSnapshot());
            }
        }

    public double ContentAreaHeight => _layoutMeasurements.CalculateContentAreaHeight(CurrentItems.Count, IconsPerRow, EffectiveIconScale, _enableContentScroll);
    public double ContentAreaMaxHeight => _layoutMeasurements.ContentAreaMaxHeight;
    public double ContentHostHeight => _layoutMeasurements.ContentHostHeight(IsOverlayOpen, CurrentItems.Count, IconsPerRow, EffectiveIconScale, _enableContentScroll);
        public int IconsPerRow
        {
            get => _iconsPerRow;
            set
            {
                var clamped = Math.Max(4, value);
                if (SetField(ref _iconsPerRow, clamped))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(ContentMinWidth));
                    OnPropertyChanged(nameof(CarouselCellWidth));
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentWidth));
                    OnPropertyChanged(nameof(WindowDesiredWidth));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                    OnPropertyChanged(nameof(IconMargin));
                }
            }
        }

        /// <summary>
        /// Used to scale icon size (0.8 – 1.5 etc.).
        /// </summary>
        public double IconScale
        {
            get => _icon_scale;
            set
            {
                if (SetField(ref _icon_scale, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(EffectiveIconScale));
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentHostHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                }
            }
        }

        /// <summary>
        /// Overall UI scale (shrinks/expands whole UI). Clamped between 0.8 and 1.2.
        /// </summary>
        public double UiScale
        {
            get => _uiScale;
            set
            {
                var clamped = Math.Max(0.8, Math.Min(1.0, value));
                if (SetField(ref _uiScale, clamped))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(EffectiveIconScale));
                    OnPropertyChanged(nameof(ContentMinWidth));
                    OnPropertyChanged(nameof(ContentWidth));
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentHostHeight));
                    OnPropertyChanged(nameof(WindowDesiredWidth));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                }
            }
        }

        public double GamingOverlayUiScale
        {
            get => _gamingOverlayUiScale;
            set
            {
                var clamped = Math.Max(0.7, Math.Min(1.2, value));
                if (SetField(ref _gamingOverlayUiScale, clamped))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(GamingOverlayUiScale));
                    OnPropertyChanged(nameof(GamingOverlayWindowWidth));
                    OnPropertyChanged(nameof(GamingOverlayWindowHeight));
                }
            }
        }

        public double GamingOverlayFpsResponsiveness
        {
            get => _gamingOverlayFpsResponsiveness;
            set
            {
                var clamped = Math.Max(0.15, Math.Min(1.0, value));
                if (SetField(ref _gamingOverlayFpsResponsiveness, clamped))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(GamingOverlayFpsResponsiveness));
                    OnPropertyChanged(nameof(GamingOverlayFpsResponsivenessPercent));
                    OnPropertyChanged(nameof(GamingOverlayFpsResponsivenessDescription));
                }
            }
        }

        public bool GamingOverlayTransparentBackground
        {
            get => _gamingOverlayTransparentBackground;
            set
            {
                if (SetField(ref _gamingOverlayTransparentBackground, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(IsGamingOverlayTextColorCustom));
                    OnPropertyChanged(nameof(GamingOverlayTransparentBackgroundEffective));
                    OnPropertyChanged(nameof(GamingOverlayAnyTransparentEnabled));
                }
            }
        }

        /// <summary>
        /// When enabled, transparent background is only active while a game is running.
        /// Independent from GamingOverlayTransparentBackground.
        /// </summary>
        public bool GamingOverlayAutoTransparentBackground
        {
            get => _gamingOverlayAutoTransparentBackground;
            set
            {
                if (SetField(ref _gamingOverlayAutoTransparentBackground, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(GamingOverlayTransparentBackgroundEffective));
                    OnPropertyChanged(nameof(GamingOverlayAnyTransparentEnabled));
                }
            }
        }

        /// <summary>
        /// True when either transparent option is enabled (used to show text color options).
        /// </summary>
        public bool GamingOverlayAnyTransparentEnabled =>
            _gamingOverlayTransparentBackground || _gamingOverlayAutoTransparentBackground;

        /// <summary>
        /// Effective transparent state. The two toggles are independent:
        /// - TransparentBackground: always transparent when enabled.
        /// - AutoTransparentBackground: transparent only while a game is tracked (IsInGame).
        /// If either applies, the overlay is transparent.
        /// </summary>
        public bool GamingOverlayTransparentBackgroundEffective =>
            _gamingOverlayTransparentBackground ||
            (_gamingOverlayAutoTransparentBackground && _systemMonitor.IsInGame);

        public string GamingOverlayTextColor
        {
            get => _gamingOverlayTextColor;
            set
            {
                var clamped = string.IsNullOrWhiteSpace(value) ? "#FFFFFF" : value;
                if (SetField(ref _gamingOverlayTextColor, clamped))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(GamingOverlayTextBrush));
                    OnPropertyChanged(nameof(GamingOverlayTextColorHex));
                }
            }
        }

        public bool IsGamingOverlayTextColorCustom => GamingOverlayTransparentBackgroundEffective;

        public string GamingOverlayTextColorHex
        {
            get
            {
                if (GamingOverlayTransparentBackgroundEffective)
                    return _gamingOverlayTextColor;
                return IsLightTheme ? "#111111" : "#FFFFFF";
            }
        }

        public WMedia.Brush GamingOverlayTextBrush
        {
            get
            {
                try
                {
                    return new WMedia.SolidColorBrush((WMedia.Color)WMedia.ColorConverter.ConvertFromString(GamingOverlayTextColorHex));
                }
                catch
                {
                    return new WMedia.SolidColorBrush(WMedia.Colors.White);
                }
            }
        }

        public string GamingOverlayFpsResponsivenessPercent => $"{Math.Round(_gamingOverlayFpsResponsiveness * 100):F0}%";

        public string GamingOverlayFpsResponsivenessDescription
        {
            get
            {
                if (_gamingOverlayFpsResponsiveness >= 0.88)
                    return string.Equals(_language, "da", StringComparison.OrdinalIgnoreCase)
                        ? "Næsten realtime opdatering med meget lidt smoothing."
                        : "Near-realtime updates with very little smoothing.";
                if (_gamingOverlayFpsResponsiveness >= 0.65)
                    return string.Equals(_language, "da", StringComparison.OrdinalIgnoreCase)
                        ? "Meget hurtig opdatering med mindre smoothing."
                        : "Very fast updates with less smoothing.";
                if (_gamingOverlayFpsResponsiveness >= 0.45)
                    return string.Equals(_language, "da", StringComparison.OrdinalIgnoreCase)
                        ? "Balanceret mellem realtime og stabil visning."
                        : "Balanced between realtime feel and stable display.";

                return string.Equals(_language, "da", StringComparison.OrdinalIgnoreCase)
                    ? "Mere smoothing og roligere FPS-tal."
                    : "More smoothing and calmer FPS numbers.";
            }
        }

        public double GamingOverlayWindowWidth => GamingOverlayBaseWidth * _gamingOverlayUiScale;
        public double GamingOverlayWindowHeight => GamingOverlayBaseHeight * _gamingOverlayUiScale;

        public double EffectiveIconScale => _icon_scale;

        /// <summary>
        /// Whether the settings overlay is open.
        /// </summary>
        public bool IsSettingsOpen
        {
            get => _overlayState.IsSettingsOpen;
            set
            {
                if (!_overlayState.SetSettingsOpen(value, out var layoutsChanged, out var helpChanged))
                    return;

                NotifyOverlayStateChanged(nameof(IsSettingsOpen), layoutsChanged, nameof(IsLayoutsOpen), helpChanged, nameof(IsHelpOpen));
            }
        }

        /// <summary>
        /// Whether the layouts overlay is open.
        /// </summary>
        public bool IsLayoutsOpen
        {
            get => _overlayState.IsLayoutsOpen;
            set
            {
                if (!_overlayState.SetLayoutsOpen(value, out var settingsChanged, out var helpChanged))
                    return;

                NotifyOverlayStateChanged(nameof(IsLayoutsOpen), settingsChanged, nameof(IsSettingsOpen), helpChanged, nameof(IsHelpOpen));
            }
        }

        public bool IsHelpOpen
        {
            get => _overlayState.IsHelpOpen;
            set
            {
                if (!_overlayState.SetHelpOpen(value, out var settingsChanged, out var layoutsChanged))
                    return;

                NotifyOverlayStateChanged(nameof(IsHelpOpen), settingsChanged, nameof(IsSettingsOpen), layoutsChanged, nameof(IsLayoutsOpen));
            }
        }

        /// <summary>
        /// Indicates any overlay (settings or layouts) is active.
        /// </summary>
        public bool IsOverlayOpen => _overlayState.IsOverlayOpen;

        /// <summary>
        /// Whether the full IconGrid UI is visible (vs. floating icon mode).
        /// Not persisted; purely runtime state.
        /// </summary>
        public bool IsFullWindowVisible
        {
            get => _isFullWindowVisible;
            set
            {
                if (SetField(ref _isFullWindowVisible, value))
                {
                    OnPropertyChanged(nameof(IsFullWindowVisible));
                    OnPropertyChanged(nameof(TopmostState));
                }
            }
        }

        public bool EnableSlideUpAnimation
        {
            get => _enableSlideUpAnimation;
            set
            {
                if (SetField(ref _enableSlideUpAnimation, value))
                {
                    SaveSettingsToConfig();
                    // If the user disables the animation, ensure the panel is expanded.
                    if (!_enableSlideUpAnimation)
                    {
                        IsIconPanelExpanded = true;
                    }
                }
            }
        }

        public bool EnableContentScroll
        {
            get => _enableContentScroll;
            set
            {
                if (SetField(ref _enableContentScroll, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentHostHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                }
            }
        }

        /// <summary>
        /// Current view mode for the shortcut icons: "Grid" or "Carousel".
        /// Owned by <see cref="LauncherLayoutMeasurements"/> so layout height follows.
        /// </summary>
        public string IconViewMode
        {
            get => _layoutMeasurements.IconViewMode;
            set
            {
                if (_layoutMeasurements.SetIconViewMode(value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(IconViewMode));
                    OnPropertyChanged(nameof(IsCarouselViewMode));
                }
            }
        }

        /// <summary>
        /// True when the shortcut icons are shown as a horizontal carousel.
        /// </summary>
        public bool IsCarouselViewMode
        {
            get => string.Equals(IconViewMode, LauncherLayoutMeasurements.CarouselViewMode, StringComparison.OrdinalIgnoreCase);
            set
            {
                var target = value
                    ? LauncherLayoutMeasurements.CarouselViewMode
                    : LauncherLayoutMeasurements.GridViewMode;

                if (IconViewMode == target)
                    return;

                IconViewMode = target;
            }
        }

        public int WindowAnimationDurationMs
        {
            get => _windowAnimationDurationMs;
            set
            {
                var clamped = Math.Max(0, Math.Min(1000, value)); // Clamp between 0 and 1000ms
                if (SetField(ref _windowAnimationDurationMs, clamped))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool IsIconPanelExpanded
        {
            get => _layoutMeasurements.IsIconPanelExpanded;
            set => _layoutMeasurements.IsIconPanelExpanded = value;
        }

        public bool TryGetSavedWindowPosition(out double left, out double top)
        {
            return _windowStateStore.TryGetSavedWindowPosition(out left, out top);
        }

        public void SaveWindowPosition(double left, double top)
        {
            _windowStateStore.SaveWindowPosition(left, top);
            SaveSettingsToConfig();
        }

        public bool TryGetSavedSettingsWindowPosition(out double left, out double top)
        {
            return _windowStateStore.TryGetSavedSettingsWindowPosition(out left, out top);
        }

        public void SaveSettingsWindowPosition(double left, double top)
        {
            _windowStateStore.SaveSettingsWindowPosition(left, top);
            SaveSettingsToConfig();
        }

        public bool TryGetSavedGamingOverlayWindowPosition(out double left, out double top)
        {
            return _windowStateStore.TryGetSavedGamingOverlayWindowPosition(out left, out top);
        }

        public void SaveGamingOverlayWindowPosition(double left, double top)
        {
            _windowStateStore.SaveGamingOverlayWindowPosition(left, top);
            SaveSettingsToConfig();
        }

        public bool TryGetSavedFloatingPosition(out double left, out double top)
        {
            return _windowStateStore.TryGetSavedFloatingPosition(out left, out top);
        }

        public void SaveFloatingIconPosition(double left, double top)
        {
            _windowStateStore.SaveFloatingIconPosition(left, top);
            SaveSettingsToConfig();
        }

        /// <summary>
        /// Binds to Window.Topmost in XAML.
        /// </summary>
        public bool IsAlwaysOnTop
        {
            get => _isAlwaysOnTop;
            set
            {
                if (SetField(ref _isAlwaysOnTop, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(TopmostState));
                }
            }
        }

        /// <summary>
        /// Controls whether the floating icon stays on top while the main UI is hidden.
        /// </summary>
        public bool IsFloatingIconTopmost
        {
            get => _isFloatingIconTopmost;
            set
            {
                if (SetField(ref _isFloatingIconTopmost, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(TopmostState));
                }
            }
        }

        /// <summary>
        /// Effective Topmost flag used by the window depending on current mode.
        /// </summary>
        public bool TopmostState => IsFullWindowVisible ? _isAlwaysOnTop : _isFloatingIconTopmost;

        /// <summary>
        /// Shows or hides the tab scroll buttons.
        /// </summary>
        public bool ShowScrollButtons
        {
            get => _showScrollButtons;
            set
            {
                if (SetField(ref _showScrollButtons, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool ShowDesktopIcon
        {
            get => _showDesktopIcon;
            set
            {
                if (SetField(ref _showDesktopIcon, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool StartDirectlyInLauncher
        {
            get => _startDirectlyInLauncher;
            set
            {
                if (SetField(ref _startDirectlyInLauncher, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool ShowDevOverlay
        {
            get => _showDevOverlay;
            set
            {
                if (SetField(ref _showDevOverlay, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool ResetSettingsToggle
        {
            get => _resetSettingsToggle;
            set
            {
                if (SetField(ref _resetSettingsToggle, value) && value)
                {
                    ResetSettingsToDefaults();
                    _resetSettingsToggle = false;
                    OnPropertyChanged(nameof(ResetSettingsToggle));
                }
            }
        }

        /// <summary>
        /// Register/unregister app to start with Windows.
        /// </summary>
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (SetField(ref _startWithWindows, value))
                {
                    SaveSettingsToConfig();
                    TryUpdateStartupRegistration(value, _startupLaunchMode);
                }
            }
        }

        /// <summary>
        /// Fixed width for the content area so all categories align the same.
        /// </summary>
        public double FixedContentWidth
        {
            get => _fixedContentWidth;
            set => SetField(ref _fixedContentWidth, value);
        }

        /// <summary>
        /// Keeps the shortcut area width consistent across tabs using the configured column count.
        /// </summary>
        public double ContentMinWidth => _layoutMeasurements.ContentMinWidth(IconsPerRow);

        /// <summary>
        /// Width of each carousel cell (viewport width / icons per row),
        /// identical to the grid's UniformGrid column width.
        /// </summary>
        public double CarouselCellWidth => _layoutMeasurements.CarouselCellWidth(IconsPerRow);

        /// <summary>
        /// Actual content width to bind in the view.
        /// </summary>
        public double ContentWidth => ContentMinWidth;

        public double ContentMaxWidth => _layoutMeasurements.ContentMaxWidth;

        /// <summary>
        /// Desired window dimensions so chrome tracks content size.
        /// </summary>
        public double WindowDesiredWidth => _layoutMeasurements.WindowDesiredWidth(ContentWidth, UiScale);
        public double WindowDesiredHeight => _layoutMeasurements.WindowDesiredHeight(IsOverlayOpen, ContentHostHeight, UiScale);

        /// <summary>
        /// Height used by the window; when settings are open, give extra space so the form fits without scrolling.
        /// </summary>
        public double WindowDesiredHeightEffective => WindowDesiredHeight;

        /// <summary>
        /// Update measured header height (top bar + tabs) based on actual visuals.
        /// </summary>
        public void SetHeaderHeight(double value)
        {
            _layoutMeasurements.SetHeaderHeight(value);
        }

        public void NotifyWorkAreaChanged()
        {
            _layoutMeasurements.NotifyWorkAreaChanged();
        }

        public void RefreshLayoutMeasurements()
        {
            OnPropertyChanged(nameof(CurrentItems));
            _layoutMeasurements.RefreshLayoutMeasurements();
        }

        public void RefreshSelectedTab()
        {
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(CurrentItems));
        }

        /// <summary>
        /// Margin applied to each icon tile (horizontal fixed, vertical derived from row spacing).
        /// </summary>
        public System.Windows.Thickness IconMargin => _layoutMeasurements.IconMargin;

        public bool IsLightTheme
        {
            get => _themeState.IsLightTheme;
            set
            {
                if (!_themeState.SetIsLightTheme(value))
                    return;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDarkTheme));
                SaveSettingsToConfig();
            }
        }

        public bool IsDarkTheme => !IsLightTheme;

        public WMedia.Brush AccentBrush => _themeState.AccentBrush;

        public WMedia.Brush TopBarBackground => _themeState.TopBarBackground;

        public WMedia.Brush TopBarForeground => _themeState.TopBarForeground;

        public WMedia.Brush SettingsWindowBackground => _themeState.SettingsWindowBackground;

        public WMedia.Brush SettingsCardBackground => _themeState.SettingsCardBackground;

        public WMedia.Brush SettingsCardBorderBrush => _themeState.SettingsCardBorderBrush;

        public WMedia.Brush SettingsSubtextForeground => _themeState.SettingsSubtextForeground;

        public WMedia.Color SettingsShadowColor => _themeState.SettingsShadowColor;

        private void NotifySelectedTabContentChanged()
        {
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(CurrentItems));
            _layoutMeasurements.NotifyContentHeightChanged();
        }

        private void NotifyOverlayStateChanged(
            string propertyName,
            bool secondaryChanged,
            string secondaryPropertyName,
            bool tertiaryChanged,
            string tertiaryPropertyName)
        {
            OnPropertyChanged(propertyName);
            if (secondaryChanged)
                OnPropertyChanged(secondaryPropertyName);
            if (tertiaryChanged)
                OnPropertyChanged(tertiaryPropertyName);

            OnPropertyChanged(nameof(IsOverlayOpen));
            _layoutMeasurements.NotifyContentHeightChanged();
        }


        private (string DataFolder, string LegacyDataFolder, string IconPackFolder) CreateStoragePaths()
        {
            var dataFolder = _configManager.BaseDirectory;
            var legacyDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IconGrid");
            var iconPackFolder = Path.Combine(dataFolder, "IconPack");
            return (dataFolder, legacyDataFolder, iconPackFolder);
        }

        private void InitializeAppearance()
        {
            ApplyTheme(_themeCoordinator.GetCurrentTheme());
            _themeCoordinator.ThemeChanged += ThemeCoordinator_ThemeChanged;
            ApplyLocalizationState();
        }

        private LauncherTabsState CreateTabsState()
        {
            return new LauncherTabsState(_config.TabNames, "Games");
        }

        private (LauncherItemsManager ItemsManager, LauncherItemIconManager ItemIconManager, LauncherItemLaunchManager ItemLaunchManager, LauncherShortcutManager ShortcutManager, LauncherItemsPersistence ItemsPersistence) CreateManagers()
        {
            var itemIconManager = new LauncherItemIconManager();
            return (
                new LauncherItemsManager(Items, () => SelectedTab),
                itemIconManager,
                new LauncherItemLaunchManager(RememberFpsTarget),
                new LauncherShortcutManager(Items, itemIconManager),
                new LauncherItemsPersistence(_dataFolder, Path.Combine(_legacyDataFolder, "items.json")));
        }

        private (ICommand SelectTabCommand, ICommand ResetSettingsCommand) CreateCommands()
        {
            return (
                new RelayCommand(p =>
                {
                    // When a tab is selected, close any settings/theme overlays so the main content becomes visible.
                    IsSettingsOpen = false;
                    if (p is string name && !string.IsNullOrWhiteSpace(name))
                        SelectedTab = name;
                }),
                new RelayCommand(_ => ResetSettingsToDefaults()));
        }

        private void RunStartupInitialization()
        {
            // Load persisted items if present.
            MaybeMigrateItemsFromLegacy();
            LoadItemsFromFile();
        }

        private void NotifyConfigApplied()
        {
            NotifyLayoutCollectionsChanged();
            OnPropertyChanged(nameof(ShowDevOverlay));
        }

        private void NotifyDefaultSettingsApplied()
        {
            RefreshLayoutMeasurements();
            NotifyAllLayoutPropertiesChanged();
            NotifyThemePropertiesChanged();
            NotifyLocalizationPropertiesChanged();
            OnPropertyChanged(nameof(IsAlwaysOnTop));
            OnPropertyChanged(nameof(IsFloatingIconTopmost));
            OnPropertyChanged(nameof(TopmostState));
            OnPropertyChanged(nameof(ShowScrollButtons));
            OnPropertyChanged(nameof(StartWithWindows));
            OnPropertyChanged(nameof(ShowDevOverlay));
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(IconsPerRow));
            OnPropertyChanged(nameof(IconRowSpacing));
            OnPropertyChanged(nameof(LastRowPaddingAdjust));
            OnPropertyChanged(nameof(IconScale));
            OnPropertyChanged(nameof(UiScale));
            OnPropertyChanged(nameof(EnableContentScroll));
            OnPropertyChanged(nameof(IconViewMode));
            OnPropertyChanged(nameof(IsCarouselViewMode));
            OnPropertyChanged(nameof(WindowAnimationDurationMs));
            OnPropertyChanged(nameof(PawnIoMissingMessage));
            OnPropertyChanged(nameof(PawnIoDownloadLink));
        }

        // ---------- Tema (enkle brushes - kun WPF Media) ----------
        // Fully-qualified WPF types to avoid ambiguity with System.Drawing

        public System.Windows.Media.Brush WindowBackground => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 245, 246, 250));
        public System.Windows.Media.Brush TileBackground   => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(64,   0,   0,   0));

        // ---------- Commands / handling ----------

        [SupportedOSPlatform("windows")]
        private void TryUpdateStartupRegistration(bool enable, StartupLaunchMode mode)
        {
            if (!OperatingSystem.IsWindows())
                return;
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                    return;

                var needsElevatedTaskSchedulerChange =
                    mode == StartupLaunchMode.TaskScheduler &&
                    !IsCurrentProcessElevated() &&
                    (enable || StartupTaskManager.TaskSchedulerExists() || StartupTaskManager.MonitorTaskSchedulerExists());

                if (needsElevatedTaskSchedulerChange)
                {
                    LaunchStartupTaskInstallerElevated();
                    return;
                }

                if (enable)
                {
                    StartupTaskManager.ApplyStartupMode(exePath, mode, enable);
                }
                else
                {
                    StartupTaskManager.Unregister();
                    StartupTaskManager.UnregisterTaskScheduler();
                    StartupTaskManager.UnregisterMonitorTaskScheduler();
                    StartupTaskManager.CleanupLegacyStartupEntries();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to update startup registration: " + ex);
            }
        }

        [SupportedOSPlatform("windows")]
        private void LaunchStartupTaskInstallerElevated()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                    return;

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--install-startup-task",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to launch elevated startup task installer: " + ex);
            }
        }

        private static bool IsCurrentProcessElevated()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        // ---------- INotifyPropertyChanged ----------

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void LayoutMeasurements_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void SystemMonitor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(SystemMonitor.IsInGame), System.StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(GamingOverlayTransparentBackgroundEffective));
                OnPropertyChanged(nameof(GamingOverlayTextBrush));
                OnPropertyChanged(nameof(GamingOverlayTextColorHex));
            }
        }

        private void ThemeCoordinator_ThemeChanged(object? sender, ThemeSnapshot e)
        {
            ApplyTheme(e);
        }

        private void NotifyThemePropertiesChanged()
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(AccentBrush));
            OnPropertyChanged(nameof(TopBarBackground));
            OnPropertyChanged(nameof(TopBarForeground));
            OnPropertyChanged(nameof(SettingsWindowBackground));
            OnPropertyChanged(nameof(SettingsCardBackground));
            OnPropertyChanged(nameof(SettingsCardBorderBrush));
            OnPropertyChanged(nameof(SettingsSubtextForeground));
            OnPropertyChanged(nameof(SettingsShadowColor));
        }

        private void TabsState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LauncherTabsState.SelectedTab))
                return;

            NotifySelectedTabContentChanged();
        }

        private void ApplyTheme(ThemeSnapshot snapshot)
        {
            _themeState.ApplyThemeSnapshot(snapshot);
            NotifyThemePropertiesChanged();
        }

        /// <summary>
        /// Extra spacing between rows (user adjustable).
        /// </summary>
        public double IconRowSpacing
        {
            get => _layoutMeasurements.IconRowSpacing;
            set
            {
                if (_layoutMeasurements.SetIconRowSpacing(value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(IconMargin));
                }
            }
        }

        /// <summary>
        /// Fine-tune bottom space under the last visible row when not scrolling.
        /// </summary>
        public double LastRowPaddingAdjust
        {
            get => _layoutMeasurements.LastRowPaddingAdjust;
            set
            {
                if (_layoutMeasurements.SetLastRowPaddingAdjust(value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        private string _language = "en";
        public string Language
        {
            get => _language;
            set
            {
                if (SetField(ref _language, value))
                {
                    SaveSettingsToConfig();
                    ApplyLocalizationState();
                }
            }
        }

        /// <summary>
        /// Location where users can drop custom Windows 11 style icons.
        /// </summary>
        public string IconPackFolder => _iconPackFolder;

        private void ResetSettingsToDefaults()
        {
            // Apply default values without touching user tabs or items.
            var previousStartWithWindows = _startWithWindows;
            var previousStartupLaunchMode = _startupLaunchMode;

            ApplyDefaultSettingsState();
            _layoutState.ResetToDefaults();
            ApplyLocalizationState();
            ApplyTheme(_themeCoordinator.GetCurrentTheme());
            NotifyDefaultSettingsApplied();
            SaveSettingsToConfig();

            if (previousStartWithWindows != _startWithWindows || previousStartupLaunchMode != _startupLaunchMode)
            {
                TryUpdateStartupRegistration(_startWithWindows, _startupLaunchMode);
            }
        }
        private void EnsureIconPackFolder()
        {
            try
            {
                Directory.CreateDirectory(_iconPackFolder);
            }
            catch
            {
                // non-fatal; icon picking still works without seeding samples
            }
        }

        private void WriteSampleIcon(string fileName, string base64)
        {
            var path = Path.Combine(_iconPackFolder, fileName);
            if (File.Exists(path))
            {
                return;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
                File.WriteAllBytes(path, bytes);
            }
            catch
            {
                // ignore write failures
            }
        }
    }
}







