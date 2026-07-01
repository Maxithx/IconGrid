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
using IconGrid.Models;

namespace IconGrid.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // ---------- Fields ----------

        private int _iconsPerRow = 4;
        private double _icon_scale = 1.0;

        private bool _isSettingsOpen;
        private bool _isLayoutsOpen;
        private bool _isHelpOpen;

        // NEW: UI settings backing fields
        private bool _isAlwaysOnTop = true;
        private bool _showScrollButtons = true;
        private bool _startWithWindows = false;
        private bool _isFloatingIconTopmost = true;
        private double _lastRowPaddingAdjust = 0;
        private double _uiScale = 1.0;
        private bool _showDesktopIcon = true;
        private bool _showDevOverlay = false;
        private bool _resetSettingsToggle;
        private double _fixedContentWidth = 720;
        private bool _isFullWindowVisible = false;
        private bool _enableSlideUpAnimation = true;
        private bool _enableContentScroll = true;
        private int _windowAnimationDurationMs = 250;
        private bool _isIconPanelExpanded = true;

        private double? _windowLeft;
        private double? _windowTop;
        private double? _settingsWindowLeft;
        private double? _settingsWindowTop;
        private double? _floatingLeft;
        private double? _floatingTop;
        private readonly LauncherLayoutState _layoutState = new();
        private const double TileSlotWidth = 172;             // approximate width per icon tile including margin
        private const double BaseTileSlotHeight = 152;        // base row height (icon + label) before spacing
        private const double ContentVerticalPaddingTop = 36;  // upper padding portion (matches XAML padding)
        private const double ContentVerticalPaddingBottom = 0; // remove bottom padding to eliminate extra space
        private const double ExtraBottomPaddingPerRow = 0;    // no extra bottom padding per row
        private const double ContentHorizontalPadding = 40;   // content border padding left+right
        private const double WindowHorizontalPadding = 0;     // remove outer shell padding to keep full-mode window tight to content
        private double _headerHeight = 140;                   // measured height for top chrome + tabs
        private double _iconRowSpacing = -20;                 // adjustable extra spacing between rows (default tightened)
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
        private readonly LauncherShortcutManager _shortcutManager;
        private readonly LauncherItemsPersistence _itemsPersistence;
        private readonly MainViewModelSettingsPersistence _settingsPersistence;
        private ConfigModel _config;
        private bool _isInitializing = true;

        public SystemMonitor SystemMonitor => _systemMonitor;
        private const double SettingsMinWindowHeight = 620;
        private const double FixedSettingsHeight = 680;

        // ---------- Constructor ----------

        public MainViewModel()
        {
            _configManager = new ConfigManager();
            _settingsPersistence = new MainViewModelSettingsPersistence(_configManager);
            _config = _configManager.LoadConfig();
            _config.EnsureTabNames();
            ApplyConfig(_config);

            _dataFolder = _configManager.BaseDirectory;
            _legacyDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IconGrid");
            _iconPackFolder = Path.Combine(_dataFolder, "IconPack");
            EnsureIconPackFolder();

            ApplyTheme(ThemeHelper.GetTheme());
            ThemeHelper.ThemeChanged += ThemeHelper_ThemeChanged;

            UpdatePawnIoLocalizationStrings();

            _tabsState = new LauncherTabsState(_config.TabNames, "Games");
            _tabsState.PropertyChanged += TabsState_PropertyChanged;

            Items = new ObservableCollection<LauncherItem>();
            _itemsManager = new LauncherItemsManager(Items, () => SelectedTab);
            _itemIconManager = new LauncherItemIconManager();
            _itemLaunchManager = new LauncherItemLaunchManager();
            _shortcutManager = new LauncherShortcutManager(Items, _itemIconManager);
            _itemsPersistence = new LauncherItemsPersistence(_dataFolder, Path.Combine(_legacyDataFolder, "items.json"));
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

            // Ensure startup registration matches the saved setting on app launch.
            TryUpdateStartupRegistration(_startWithWindows);

            // Command for selecting tabs from the UI
            SelectTabCommand = new RelayCommand(p =>
            {
                // When a tab is selected, close any settings/theme overlays so the main content becomes visible.
                IsSettingsOpen = false;
                if (p is string name && !string.IsNullOrWhiteSpace(name))
                    SelectedTab = name;
            });

            ResetSettingsCommand = new RelayCommand(_ => ResetSettingsToDefaults());

            // Load persisted items if present
            MaybeMigrateItemsFromLegacy();
            LoadItemsFromFile();
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

        public double ContentAreaHeight => CalculateContentAreaHeight();
        public double ContentAreaMaxHeight => Math.Max(200, SystemParameters.WorkArea.Height - _headerHeight - 60);
        private double SettingsContentHeight => Math.Max(200, FixedSettingsHeight - _headerHeight);
        public double ContentHostHeight => IsOverlayOpen ? SettingsContentHeight : ContentAreaHeight;
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

        public double EffectiveIconScale => _icon_scale;

        /// <summary>
        /// Whether the settings overlay is open.
        /// </summary>
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set
            {
                if (SetField(ref _isSettingsOpen, value))
                {
                    if (_isSettingsOpen)
                    {
                        _isLayoutsOpen = false;
                        _isHelpOpen = false;
                        OnPropertyChanged(nameof(IsLayoutsOpen));
                        OnPropertyChanged(nameof(IsHelpOpen));
                    }
                    OnPropertyChanged(nameof(IsOverlayOpen));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                    OnPropertyChanged(nameof(ContentHostHeight));
                }
            }
        }

        /// <summary>
        /// Whether the layouts overlay is open.
        /// </summary>
        public bool IsLayoutsOpen
        {
            get => _isLayoutsOpen;
            set
            {
                if (SetField(ref _isLayoutsOpen, value))
                {
                    if (_isLayoutsOpen)
                    {
                        _isSettingsOpen = false;
                        _isHelpOpen = false;
                        OnPropertyChanged(nameof(IsSettingsOpen));
                        OnPropertyChanged(nameof(IsHelpOpen));
                    }
                    OnPropertyChanged(nameof(IsOverlayOpen));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                    OnPropertyChanged(nameof(ContentHostHeight));
                }
            }
        }

        public bool IsHelpOpen
        {
            get => _isHelpOpen;
            set
            {
                if (SetField(ref _isHelpOpen, value))
                {
                    if (_isHelpOpen)
                    {
                        _isSettingsOpen = false;
                        _isLayoutsOpen = false;
                        OnPropertyChanged(nameof(IsSettingsOpen));
                        OnPropertyChanged(nameof(IsLayoutsOpen));
                    }
                    OnPropertyChanged(nameof(IsOverlayOpen));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                    OnPropertyChanged(nameof(ContentHostHeight));
                }
            }
        }

        /// <summary>
        /// Indicates any overlay (settings or layouts) is active.
        /// </summary>
        public bool IsOverlayOpen => IsSettingsOpen || IsLayoutsOpen || IsHelpOpen;

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
                }
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
            get => _isIconPanelExpanded;
            set
            {
                if (SetField(ref _isIconPanelExpanded, value))
                {
                    // When the panel state changes, the window height needs to be re-evaluated.
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentHostHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
                }
            }
        }

        public bool TryGetSavedWindowPosition(out double left, out double top)
        {
            if (_windowLeft.HasValue && _windowTop.HasValue)
            {
                left = _windowLeft.Value;
                top = _windowTop.Value;
                return true;
            }

            left = 0;
            top = 0;
            return false;
        }

        public void SaveWindowPosition(double left, double top)
        {
            _windowLeft = left;
            _windowTop = top;
            SaveSettingsToConfig();
        }

        public bool TryGetSavedSettingsWindowPosition(out double left, out double top)
        {
            if (_settingsWindowLeft.HasValue && _settingsWindowTop.HasValue)
            {
                left = _settingsWindowLeft.Value;
                top = _settingsWindowTop.Value;
                return true;
            }

            left = 0;
            top = 0;
            return false;
        }

        public void SaveSettingsWindowPosition(double left, double top)
        {
            _settingsWindowLeft = left;
            _settingsWindowTop = top;
            SaveSettingsToConfig();
        }

        public bool TryGetSavedFloatingPosition(out double left, out double top)
        {
            if (_floatingLeft.HasValue && _floatingTop.HasValue)
            {
                left = _floatingLeft.Value;
                top = _floatingTop.Value;
                return true;
            }

            left = 0;
            top = 0;
            return false;
        }

        public void SaveFloatingIconPosition(double left, double top)
        {
            _floatingLeft = left;
            _floatingTop = top;
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
                    TryUpdateStartupRegistration(value);
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
        public double ContentMinWidth
        {
            get
            {
                var slots = Math.Max(1, IconsPerRow);
                var baseWidth = (slots * TileSlotWidth) + ContentHorizontalPadding;
                return baseWidth;
            }
        }

        /// <summary>
        /// Actual content width to bind in the view.
        /// </summary>
        public double ContentWidth => ContentMinWidth;

        public double ContentMaxWidth =>
            Math.Max(0, SystemParameters.WorkArea.Width - WindowHorizontalPadding - 24);

        /// <summary>
        /// Desired window dimensions so chrome tracks content size.
        /// </summary>
        public double WindowDesiredWidth => (ContentWidth + WindowHorizontalPadding) * UiScale;
        public double WindowDesiredHeight => IsOverlayOpen ? (FixedSettingsHeight * UiScale) : ((ContentHostHeight + _headerHeight) * UiScale);

        /// <summary>
        /// Height used by the window; when settings are open, give extra space so the form fits without scrolling.
        /// </summary>
        public double WindowDesiredHeightEffective => WindowDesiredHeight;

        /// <summary>
        /// Update measured header height (top bar + tabs) based on actual visuals.
        /// </summary>
        public void SetHeaderHeight(double value)
        {
            var clamped = Math.Max(0, value);
            if (SetField(ref _headerHeight, clamped))
            {
                RefreshLayoutMeasurements();
            }
        }

        public void NotifyWorkAreaChanged()
        {
            OnPropertyChanged(nameof(ContentMaxWidth));
        }

        public void RefreshLayoutMeasurements()
        {
            OnPropertyChanged(nameof(CurrentItems));
            OnPropertyChanged(nameof(ContentAreaHeight));
            OnPropertyChanged(nameof(ContentAreaMaxHeight));
            OnPropertyChanged(nameof(ContentHostHeight));
            OnPropertyChanged(nameof(ContentMinWidth));
            OnPropertyChanged(nameof(ContentWidth));
            OnPropertyChanged(nameof(ContentMaxWidth));
            OnPropertyChanged(nameof(WindowDesiredWidth));
            OnPropertyChanged(nameof(WindowDesiredHeight));
            OnPropertyChanged(nameof(WindowDesiredHeightEffective));
            OnPropertyChanged(nameof(IconMargin));
        }

        public void RefreshSelectedTab()
        {
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(CurrentItems));
        }

        /// <summary>
        /// Margin applied to each icon tile (horizontal fixed, vertical derived from row spacing).
        /// </summary>
        public System.Windows.Thickness IconMargin => new System.Windows.Thickness(14, IconRowSpacing / 2, 14, IconRowSpacing / 2);

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

        public string LayoutPreset
        {
            get => _layoutState.LayoutPreset;
            set
            {
                if (!_layoutState.SetLayoutPreset(value, out var slotForPreset))
                    return;

                OnPropertyChanged();
                LayoutIconGridSlot = slotForPreset;
                SaveSettingsToConfig();
                OnPropertyChanged(nameof(LayoutPresetToolTip));
            }
        }

        public string LayoutPresetToolTip
        {
            get
            {
                if (string.Equals(_layoutState.LayoutPreset, "Auto", StringComparison.OrdinalIgnoreCase))
                    return "Kør auto layout";

                if (TryGetSavedLayout(_layoutState.LayoutPreset, out _, out var canonical) || string.Equals(_layoutState.LayoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase))
                    return $"Kør {canonical ?? _layoutState.LayoutPreset} layout";

                return "Kør standard layout";
            }
        }

        public bool LayoutSkipMinimized
        {
            get => _layoutState.LayoutSkipMinimized;
            set
            {
                if (!_layoutState.SetLayoutSkipMinimized(value))
                    return;

                OnPropertyChanged();
                SaveSettingsToConfig();
            }
        }

        public bool LayoutCurrentMonitorOnly
        {
            get => _layoutState.LayoutCurrentMonitorOnly;
            set
            {
                if (!_layoutState.SetLayoutCurrentMonitorOnly(value))
                    return;

                OnPropertyChanged();
                SaveSettingsToConfig();
            }
        }

        public bool LayoutReserveIconGridSlot
        {
            get => _layoutState.LayoutReserveIconGridSlot;
            set
            {
                if (!_layoutState.SetLayoutReserveIconGridSlot(value))
                    return;

                OnPropertyChanged();
                SaveSettingsToConfig();
            }
        }

        public int LayoutIconGridSlot
        {
            get => _layoutState.LayoutIconGridSlot;
            set
            {
                if (!_layoutState.SetLayoutIconGridSlot(value))
                    return;

                OnPropertyChanged();
                SaveSettingsToConfig();
            }
        }

        public IReadOnlyDictionary<string, int[]> LayoutLinks => _layoutState.LayoutLinks;

        public IReadOnlyDictionary<string, List<CustomLayoutSlot>> SavedLayouts => _layoutState.SavedLayouts;

        public IEnumerable<string> SavedLayoutNames => _layoutState.SavedLayoutNames;

        public IEnumerable<string> LayoutPresetChoices => _layoutState.LayoutPresetChoices;

        public IReadOnlyList<CustomLayoutSlot> FavoriteLayoutSlots => _layoutState.FavoriteLayoutSlots;

        public void SetLayoutLink(string preset, int[]? slots)
        {
            _layoutState.SetLayoutLink(preset, slots);
            SaveSettingsToConfig();
        }

        public IReadOnlyList<CustomLayoutSlot> GetSavedLayoutSlots(string layoutName)
        {
            return _layoutState.GetSavedLayoutSlots(layoutName);
        }

        public bool TryGetSavedLayout(string layoutName, out List<CustomLayoutSlot> slots, out string? canonicalName)
        {
            return _layoutState.TryGetSavedLayout(layoutName, out slots, out canonicalName);
        }

        public void SaveLayout(string layoutName, IEnumerable<CustomLayoutSlot> slots)
        {
            _layoutState.LayoutIconGridSlot = LayoutIconGridSlot;
            _layoutState.SaveLayout(layoutName, slots);
            NotifyLayoutCollectionsChanged();
            SaveSettingsToConfig();
        }

        public bool RenameLayout(string oldName, string newName)
        {
            if (!_layoutState.RenameLayout(oldName, newName))
                return false;

            NotifyLayoutPresetChanged();
            NotifyLayoutCollectionsChanged();
            SaveSettingsToConfig();
            return true;
        }

        public bool DeleteLayout(string layoutName)
        {
            var removed = _layoutState.DeleteLayout(layoutName);
            if (!removed)
                return false;

            NotifyLayoutPresetChanged();
            NotifyLayoutCollectionsChanged();
            SaveSettingsToConfig();
            return true;
        }

        public void SetFavoriteLayoutSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            _layoutState.SetFavoriteLayoutSlots(slots);
            SaveSettingsToConfig();
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
        }

        private void NotifyLayoutPresetChanged()
        {
            OnPropertyChanged(nameof(LayoutPreset));
            OnPropertyChanged(nameof(LayoutPresetToolTip));
        }

        private void NotifyLayoutCollectionsChanged()
        {
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
            OnPropertyChanged(nameof(SavedLayouts));
            OnPropertyChanged(nameof(SavedLayoutNames));
            OnPropertyChanged(nameof(LayoutPresetChoices));
        }

        private void NotifyAllLayoutPropertiesChanged()
        {
            NotifyLayoutPresetChanged();
            OnPropertyChanged(nameof(LayoutSkipMinimized));
            OnPropertyChanged(nameof(LayoutCurrentMonitorOnly));
            OnPropertyChanged(nameof(LayoutReserveIconGridSlot));
            OnPropertyChanged(nameof(LayoutIconGridSlot));
            NotifyLayoutCollectionsChanged();
        }

        private double CalculateContentAreaHeight()
        {
            if (!IsIconPanelExpanded) return 0;

            var scaledTileHeight = BaseTileSlotHeight * EffectiveIconScale;
            var itemCount = CurrentItems.Count;
            var columns = Math.Max(1, IconsPerRow);
            var rows = Math.Max(1, Math.Ceiling(itemCount / (double)columns));

            // For 1-3 rows we respect any negative row spacing to keep height snug.
            // For 4+ rows we clamp spacing to 0 so scrolling range is consistent.
            var useRowSpacing = (rows > 3) ? Math.Max(0, _iconRowSpacing) : _iconRowSpacing;
            var singleRowHeight = scaledTileHeight + ExtraBottomPaddingPerRow + useRowSpacing;

            var totalContentHeight = (rows * singleRowHeight)
                                     + ContentVerticalPaddingTop
                                     + ContentVerticalPaddingBottom;

            const int MaxRowsWithoutScroll = 3;
            if (rows > MaxRowsWithoutScroll)
            {
                // Viewport height capped at ~3 rows so a 4th row has full scroll range.
                var viewportHeight = (MaxRowsWithoutScroll * singleRowHeight)
                                     + ContentVerticalPaddingTop
                                     + ContentVerticalPaddingBottom;
                // Force additional overflow so the 4th row can scroll fully into view and leave a small buffer underneath.
                const double ScrollBuffer = 24; // extra breathing room below the last row when scrolling
                var forcedOverflowHeight = viewportHeight - (singleRowHeight * 0.8) - ScrollBuffer;

            var maxContentHeight = Math.Max(200, SystemParameters.WorkArea.Height - _headerHeight - 48);
                var minHeight = singleRowHeight + ContentVerticalPaddingTop + ContentVerticalPaddingBottom;
                forcedOverflowHeight += _lastRowPaddingAdjust;
                return Math.Max(minHeight, Math.Min(forcedOverflowHeight, maxContentHeight));
            }

            const double NoScrollBottomBuffer = 32; // slightly more buffer under last row when no scrollbar
            var adjustedHeight = totalContentHeight + NoScrollBottomBuffer + _lastRowPaddingAdjust;
            var minimum = singleRowHeight + ContentVerticalPaddingTop + Math.Min(0, _lastRowPaddingAdjust);
            return Math.Max(minimum, adjustedHeight);
        }

        private void ApplyConfig(ConfigModel config)
        {
            _iconsPerRow = Math.Max(4, config.IconsPerRow);
            _icon_scale = config.IconScale;
            _isAlwaysOnTop = config.IsAlwaysOnTop;
            _isFloatingIconTopmost = config.IsFloatingIconTopmost;
            _showScrollButtons = config.ShowScrollButtons;
            _themeState.ApplyConfig(config);
            _startWithWindows = config.StartWithWindows;
            _uiScale = config.UiScale <= 0 ? 1.0 : Math.Max(0.8, Math.Min(1.0, config.UiScale));
            _showDesktopIcon = config.ShowDesktopIcon;
            _showDevOverlay = config.ShowDevOverlay;
            _iconRowSpacing = config.IconRowSpacing;
            _lastRowPaddingAdjust = config.LastRowPaddingAdjust;
            _enableSlideUpAnimation = config.EnableSlideUpAnimation;
            _enableContentScroll = config.EnableContentScroll;
            _windowAnimationDurationMs = config.WindowAnimationDurationMs;
            Language = string.IsNullOrWhiteSpace(config.Language) ? "da" : config.Language;
            _windowLeft = config.WindowLeft;
            _windowTop = config.WindowTop;
            _settingsWindowLeft = config.SettingsWindowLeft;
            _settingsWindowTop = config.SettingsWindowTop;
            _floatingLeft = config.FloatingIconLeft;
            _floatingTop = config.FloatingIconTop;
            _layoutState.ApplyConfig(config);
            NotifyLayoutCollectionsChanged();
            OnPropertyChanged(nameof(ShowDevOverlay));
        }

        // ---------- Tema (enkle brushes - kun WPF Media) ----------
        // Fully-qualified WPF types to avoid ambiguity with System.Drawing

        public System.Windows.Media.Brush WindowBackground => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 245, 246, 250));
        public System.Windows.Media.Brush TileBackground   => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(64,   0,   0,   0));

        // ---------- Commands / handling ----------

        [SupportedOSPlatform("windows")]
        private void TryUpdateStartupRegistration(bool enable)
        {
            if (!OperatingSystem.IsWindows())
                return;
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                    return;

                if (enable)
                {
                    StartupTaskManager.Register(exePath);
                }
                else
                {
                    StartupTaskManager.Unregister();
                }

                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key?.DeleteValue("IconGrid", throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to update startup registration: " + ex);
            }
        }

        public void AddTab(string name)
        {
            _tabsState.AddTab(name);
            SaveSettingsToConfig();
        }

        public void ClearCurrentCategory()
        {
            if (!_itemsManager.ClearCurrentCategory())
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        private List<LauncherItem> GetItemsForSelectedTabSnapshot()
        {
            return GetItemsForTabSnapshot(SelectedTab);
        }

        private List<LauncherItem> GetItemsForTabSnapshot(string? tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return new List<LauncherItem>();

            return Items
                .Where(i => string.Equals(i.Category, tabName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void RenameTab(string oldName, string newName)
        {
            if (!_tabsState.RenameTab(oldName, newName))
                return;

            foreach (var item in Items.Where(i => i.Category == oldName))
            {
                item.Category = newName;
            }

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            SaveSettingsToConfig();
        }

        public void RemoveTab(string tabName)
        {
            if (!_tabsState.RemoveTab(tabName))
                return;

            // Fjern alle items under det tab
            var toRemove = Items.Where(i => i.Category == tabName).ToList();
            foreach (var item in toRemove)
            {
                Items.Remove(item);
            }

            // Skift til første tab hvis nødvendigt
            if (!Tabs.Contains(SelectedTab) && Tabs.Any())
            {
                SelectedTab = Tabs[0];
            }

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            SaveSettingsToConfig();
        }

        public void RemoveItem(LauncherItem item)
        {
            if (!_itemsManager.RemoveItem(item))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void RenameItem(LauncherItem item, string newName)
        {
            if (!_itemsManager.RenameItem(item, newName))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        /// <summary>
        /// Updates the icon path/index for a launcher item and persists it.
        /// </summary>
        public void UpdateItemIcon(LauncherItem item, string iconPath, int iconIndex)
        {
            if (!_itemIconManager.UpdateItemIcon(item, iconPath, iconIndex))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        /// <summary>
        /// Håndterer filer droppet fra Explorer ind i content-området.
        /// </summary>
        public void MoveItemWithinCategory(LauncherItem source, LauncherItem? target, bool insertAfter)
        {
            if (!_itemsManager.MoveItemWithinCategory(source, target, insertAfter))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void HandleFileDrop(string[] files)
        {
            if (!_shortcutManager.HandleFileDrop(files, SelectedTab))
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void LaunchItem(LauncherItem item)
        {
            _itemLaunchManager.LaunchItem(item);
        }

        /// <summary>
        /// Creates and persists a custom shortcut entry.
        /// </summary>
        public LauncherItem CreateCustomShortcut(string displayName, string targetPath, string category, string? arguments = null, string? iconPath = null, int iconIndex = 0)
        {
            var item = _shortcutManager.CreateCustomShortcut(displayName, targetPath, category, arguments, iconPath, iconIndex);
            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
            return item;
        }

        /// <summary>
        /// Applies an icon and persists.
        /// </summary>
        public void SetIcon(LauncherItem item, string iconPath, int iconIndex = 0)
        {
            UpdateItemIcon(item, iconPath, iconIndex);
        }

        // ---------- Persistence (simple JSON) ----------

        private void SaveItemsToFile()
        {
            _itemsPersistence.Save(Items);
        }

        private void SaveSettingsToConfig()
        {
            if (_isInitializing || _tabsState == null || Items == null)
                return;

            var state = new MainViewModelSettingsState
            {
                ContentAreaHeight = CalculateContentAreaHeight(),
                IconsPerRow = _iconsPerRow,
                IconScale = _icon_scale,
                UiScale = _uiScale,
                ShowDesktopIcon = _showDesktopIcon,
                IsAlwaysOnTop = _isAlwaysOnTop,
                IsFloatingIconTopmost = _isFloatingIconTopmost,
                ShowScrollButtons = _showScrollButtons,
                IsLightTheme = _themeState.IsLightTheme,
                StartWithWindows = _startWithWindows,
                ShowDevOverlay = _showDevOverlay,
                IconRowSpacing = _iconRowSpacing,
                LastRowPaddingAdjust = _lastRowPaddingAdjust,
                TabNames = Tabs.ToList(),
                Language = _language,
                WindowLeft = _windowLeft,
                WindowTop = _windowTop,
                SettingsWindowLeft = _settingsWindowLeft,
                SettingsWindowTop = _settingsWindowTop,
                FloatingIconLeft = _floatingLeft,
                FloatingIconTop = _floatingTop,
                EnableSlideUpAnimation = _enableSlideUpAnimation,
                EnableContentScroll = _enableContentScroll,
                WindowAnimationDurationMs = _windowAnimationDurationMs
            };

            _layoutState.ApplyToSettingsState(state);
            _settingsPersistence.Save(_config, state);
        }

        private void LoadItemsFromFile()
        {
            var list = _itemsPersistence.Load();
            if (list.Count == 0)
                return;

            Items.Clear();
            foreach (var it in list)
            {
                it.RefreshIcon();
                Items.Add(it);
            }

            OnPropertyChanged(nameof(CurrentItems));
        }

        private void MaybeMigrateItemsFromLegacy()
        {
            _itemsPersistence.MigrateLegacyIfNeeded();
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

        private void ThemeHelper_ThemeChanged(object? sender, ThemeSnapshot e)
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

            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(CurrentItems));
            OnPropertyChanged(nameof(ContentAreaHeight));
            OnPropertyChanged(nameof(ContentHostHeight));
            OnPropertyChanged(nameof(WindowDesiredHeight));
            OnPropertyChanged(nameof(WindowDesiredHeightEffective));
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
            get => _iconRowSpacing;
            set
            {
                if (SetField(ref _iconRowSpacing, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(IconMargin));
                }
            }
        }

        /// <summary>
        /// Fine-tune bottom space under the last visible row when not scrolling.
        /// </summary>
        public double LastRowPaddingAdjust
        {
            get => _lastRowPaddingAdjust;
            set
            {
                if (SetField(ref _lastRowPaddingAdjust, value))
                {
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(ContentAreaHeight));
                    OnPropertyChanged(nameof(ContentHostHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeight));
                    OnPropertyChanged(nameof(WindowDesiredHeightEffective));
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
                    UpdatePawnIoLocalizationStrings();
                    RaiseLocalizationChanged();
                }
            }
        }

        // ---------- Localization bindings ----------
        public string SettingsTitle => LocalizationHelper.Get(Language, "SettingsTitle");
        public string IconsPerRowLabel => LocalizationHelper.Get(Language, "IconsPerRow");
        public string IconRowSpacingLabel => LocalizationHelper.Get(Language, "IconRowSpacing");
        public string StartWithWindowsLabel => LocalizationHelper.Get(Language, "StartWithWindows");
        public string LastRowPaddingLabel => LocalizationHelper.Get(Language, "LastRowPadding");
        public string IconSizeLabel => LocalizationHelper.Get(Language, "IconSize");
        public string UiScaleLabel => LocalizationHelper.Get(Language, "UiScale");
        public string AlwaysOnTopLabel => LocalizationHelper.Get(Language, "AlwaysOnTop");
        public string FloatingIconTopmostLabel => LocalizationHelper.Get(Language, "FloatingIconTopmost");
        public string ShowScrollButtonsLabel => LocalizationHelper.Get(Language, "ShowScrollButtons");
        public string ShowDesktopIconLabel => LocalizationHelper.Get(Language, "ShowDesktopIcon");
        public string LanguageLabel => LocalizationHelper.Get(Language, "Language");
        public string ResetDefaultsLabel => LocalizationHelper.Get(Language, "ResetDefaults");
        public string EnableSlideUpAnimationLabel => LocalizationHelper.Get(Language, "EnableSlideUpAnimation");
        public string WindowAnimationSpeedLabel => LocalizationHelper.Get(Language, "WindowAnimationSpeed");
        public string EnableIconScrollLabel => LocalizationHelper.Get(Language, "EnableIconScroll");
        public string SettingsPlaceholderLabel => LocalizationHelper.Get(Language, "SettingsPlaceholder");
        public string DevOverlayLabel => LocalizationHelper.Get(Language, "DevOverlayLabel");
        public string ChangeIconLabel => LocalizationHelper.Get(Language, "ChangeIcon");
        public string WindowsIconsLabel => LocalizationHelper.Get(Language, "WindowsIcons");
        public string OpenLabel => LocalizationHelper.Get(Language, "Open");
        public string RunAsAdminLabel => LocalizationHelper.Get(Language, "RunAsAdmin");
        public string OpenFileLocationLabel => LocalizationHelper.Get(Language, "OpenFileLocation");
        public string CopyPathLabel => LocalizationHelper.Get(Language, "CopyPath");
        public string ResetIconLabel => LocalizationHelper.Get(Language, "ResetIcon");
        public string RenameLabel => LocalizationHelper.Get(Language, "Rename");
        public string RemoveLabel => LocalizationHelper.Get(Language, "Remove");
        public string LayoutsLabel => LocalizationHelper.Get(Language, "MoreLayouts");
        public string MoreSettingsLabel => LocalizationHelper.Get(Language, "MoreSettings");
        public string HelpLabel => LocalizationHelper.Get(Language, "MoreHelp");
        public string AboutLabel => LocalizationHelper.Get(Language, "MoreAbout");
        public string MonitorNetworkLabel => LocalizationHelper.Get(Language, "MonitorNetworkLabel");
        public string MonitorDownloadLabel => LocalizationHelper.Get(Language, "MonitorDownloadLabel");
        public string MonitorUploadLabel => LocalizationHelper.Get(Language, "MonitorUploadLabel");
        public string MonitorCpuLabel => LocalizationHelper.Get(Language, "MonitorCpuLabel");
        public string MonitorGpuLabel => LocalizationHelper.Get(Language, "MonitorGpuLabel");
        private string _pawnIoMissingMessage = "CPU temperatures require PawnIO.";
        private string _pawnIoDownloadLink = "Download PawnIO";

        public string PawnIoMissingMessage
        {
            get => _pawnIoMissingMessage;
            private set => SetField(ref _pawnIoMissingMessage, value);
        }

        public string PawnIoDownloadLink
        {
            get => _pawnIoDownloadLink;
            private set => SetField(ref _pawnIoDownloadLink, value);
        }

        private void RaiseLocalizationChanged()
        {
            OnPropertyChanged(nameof(SettingsTitle));
            OnPropertyChanged(nameof(IconsPerRowLabel));
            OnPropertyChanged(nameof(IconRowSpacingLabel));
            OnPropertyChanged(nameof(StartWithWindowsLabel));
            OnPropertyChanged(nameof(LastRowPaddingLabel));
            OnPropertyChanged(nameof(IconSizeLabel));
            OnPropertyChanged(nameof(UiScaleLabel));
            OnPropertyChanged(nameof(AlwaysOnTopLabel));
            OnPropertyChanged(nameof(FloatingIconTopmostLabel));
            OnPropertyChanged(nameof(ShowScrollButtonsLabel));
            OnPropertyChanged(nameof(ShowDesktopIconLabel));
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(ResetDefaultsLabel));
            OnPropertyChanged(nameof(EnableSlideUpAnimationLabel));
            OnPropertyChanged(nameof(EnableIconScrollLabel));
            OnPropertyChanged(nameof(WindowAnimationSpeedLabel));
            OnPropertyChanged(nameof(SettingsPlaceholderLabel));
            OnPropertyChanged(nameof(DevOverlayLabel));
            OnPropertyChanged(nameof(ChangeIconLabel));
            OnPropertyChanged(nameof(WindowsIconsLabel));
            OnPropertyChanged(nameof(OpenLabel));
            OnPropertyChanged(nameof(RunAsAdminLabel));
            OnPropertyChanged(nameof(OpenFileLocationLabel));
            OnPropertyChanged(nameof(CopyPathLabel));
            OnPropertyChanged(nameof(ResetIconLabel));
            OnPropertyChanged(nameof(RenameLabel));
            OnPropertyChanged(nameof(RemoveLabel));
            OnPropertyChanged(nameof(LayoutsLabel));
            OnPropertyChanged(nameof(MoreSettingsLabel));
            OnPropertyChanged(nameof(HelpLabel));
            OnPropertyChanged(nameof(AboutLabel));
            OnPropertyChanged(nameof(MonitorNetworkLabel));
            OnPropertyChanged(nameof(MonitorDownloadLabel));
            OnPropertyChanged(nameof(MonitorUploadLabel));
            OnPropertyChanged(nameof(MonitorCpuLabel));
            OnPropertyChanged(nameof(MonitorGpuLabel));
        }

        private void UpdatePawnIoLocalizationStrings()
        {
            PawnIoMissingMessage = LocalizationHelper.Get(Language, "PawnIoMissingMessage");
            PawnIoDownloadLink = LocalizationHelper.Get(Language, "PawnIoDownloadLink");
        }

        /// <summary>
        /// Location where users can drop custom Windows 11 style icons.
        /// </summary>
        public string IconPackFolder => _iconPackFolder;

        private void ResetSettingsToDefaults()
        {
            // Apply default values without touching user tabs or items.
            IconsPerRow = 4;
            IconRowSpacing = -20;
            LastRowPaddingAdjust = -50;
            IconScale = 1.0;
            UiScale = 1.0;
            IsAlwaysOnTop = false;
            IsFloatingIconTopmost = true;
            ShowScrollButtons = true;
            EnableContentScroll = true;
            StartWithWindows = true;
            ShowDevOverlay = false;
            Language = "da";
            _themeState.SetIsLightTheme(true);
            _floatingLeft = null;
            _floatingTop = null;
            _layoutState.ResetToDefaults();
            WindowAnimationDurationMs = 250;

            NotifyAllLayoutPropertiesChanged();
            SaveSettingsToConfig();

            // Refresh bindings for theme-related brushes.
            NotifyThemePropertiesChanged();
            ApplyTheme(ThemeHelper.GetTheme());
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







