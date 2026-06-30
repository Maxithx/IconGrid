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
        private bool _isLightTheme = true;

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
        private string _layoutPreset = "Auto";
        private bool _layoutSkipMinimized = true;
        private bool _layoutCurrentMonitorOnly = true;
        private bool _layoutReserveIconGridSlot = true;
        private int _layoutIconGridSlot = 0;
        private readonly Dictionary<string, int> _layoutIconGridSlots = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int[]> _layoutLinks = new();
        private readonly Dictionary<string, List<CustomLayoutSlot>> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);
        private List<CustomLayoutSlot> _favoriteLayoutSlots = new();
        private const double TileSlotWidth = 172;             // approximate width per icon tile including margin
        private const double BaseTileSlotHeight = 152;        // base row height (icon + label) before spacing
        private const double ContentVerticalPaddingTop = 36;  // upper padding portion (matches XAML padding)
        private const double ContentVerticalPaddingBottom = 0; // remove bottom padding to eliminate extra space
        private const double ExtraBottomPaddingPerRow = 0;    // no extra bottom padding per row
        private const double ContentHorizontalPadding = 40;   // content border padding left+right
        private const double WindowHorizontalPadding = 0;     // remove outer shell padding to keep full-mode window tight to content
        private double _headerHeight = 140;                   // measured height for top chrome + tabs
        private double _iconRowSpacing = -20;                 // adjustable extra spacing between rows (default tightened)
        private WMedia.Brush _accentBrush = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(37, 99, 235));
        private WMedia.Brush _topBarBackground = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(243, 243, 243));
        private WMedia.Brush _topBarForeground = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(15, 23, 42)); // default light foreground
        private WMedia.Brush _settingsWindowBackground = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(244, 245, 247));
        private WMedia.Brush _settingsCardBackground = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(255, 255, 255));
        private WMedia.Brush _settingsCardBorderBrush = new WMedia.SolidColorBrush(WMedia.Color.FromArgb(96, 15, 23, 42));
        private WMedia.Brush _settingsSubtextForeground = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(63, 63, 70));
        private WMedia.Color _settingsShadowColor = WMedia.Color.FromArgb(60, 0, 0, 0);

        private readonly string _dataFolder;
        private readonly string _legacyDataFolder;
        private readonly string _itemsFilePath;
        private readonly string _legacyItemsFilePath;
        private readonly string _iconPackFolder;
        private readonly ConfigManager _configManager;
        private readonly SystemMonitor _systemMonitor = new();
        private readonly LauncherTabsState _tabsState;
        private readonly LauncherItemsManager _itemsManager;
        private readonly LauncherItemIconManager _itemIconManager;
        private ConfigModel _config;

        public SystemMonitor SystemMonitor => _systemMonitor;
        private const double SettingsMinWindowHeight = 620;
        private const double FixedSettingsHeight = 680;
        private const int MaxSavedLayouts = 10;

        // ---------- Constructor ----------

        public MainViewModel()
        {
            _configManager = new ConfigManager();
            _config = _configManager.LoadConfig();
            _config.EnsureTabNames();
            ApplyConfig(_config);

            _dataFolder = _configManager.BaseDirectory;
            _legacyDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IconGrid");
            _itemsFilePath = Path.Combine(_dataFolder, "items.json");
            _legacyItemsFilePath = Path.Combine(_legacyDataFolder, "items.json");
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
        }

        // ---------- Public properties ----------

        public ObservableCollection<string> Tabs => _tabsState.Tabs;

        public ObservableCollection<LauncherItem> Items { get; }

        public ICommand SelectTabCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public string SelectedTab
        {
            get => _tabsState.SelectedTab;
            set => _tabsState.SelectedTab = value;
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
            get => _isLightTheme;
            set
            {
                if (SetField(ref _isLightTheme, value))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                    SaveSettingsToConfig();
                }
            }
        }

        public bool IsDarkTheme => !IsLightTheme;

        public WMedia.Brush AccentBrush
        {
            get => _accentBrush;
            private set => SetField(ref _accentBrush, value);
        }

        public WMedia.Brush TopBarBackground
        {
            get => _topBarBackground;
            private set => SetField(ref _topBarBackground, value);
        }

        public WMedia.Brush TopBarForeground
        {
            get => _topBarForeground;
            private set => SetField(ref _topBarForeground, value);
        }

        public WMedia.Brush SettingsWindowBackground
        {
            get => _settingsWindowBackground;
            private set => SetField(ref _settingsWindowBackground, value);
        }

        public WMedia.Brush SettingsCardBackground
        {
            get => _settingsCardBackground;
            private set => SetField(ref _settingsCardBackground, value);
        }

        public WMedia.Brush SettingsCardBorderBrush
        {
            get => _settingsCardBorderBrush;
            private set => SetField(ref _settingsCardBorderBrush, value);
        }


        public WMedia.Brush SettingsSubtextForeground
        {
            get => _settingsSubtextForeground;
            private set => SetField(ref _settingsSubtextForeground, value);
        }

        public WMedia.Color SettingsShadowColor
        {
            get => _settingsShadowColor;
            private set => SetField(ref _settingsShadowColor, value);
        }

        public string LayoutPreset
        {
            get => _layoutPreset;
            set
            {
                if (SetField(ref _layoutPreset, value))
                {
                    // Switch to the slot that belongs to this preset (if any).
                    LayoutIconGridSlot = GetSlotForPreset(_layoutPreset);
                    SaveSettingsToConfig();
                    OnPropertyChanged(nameof(LayoutPresetToolTip));
                }
            }
        }

        public string LayoutPresetToolTip
        {
            get
            {
                if (string.Equals(_layoutPreset, "Auto", StringComparison.OrdinalIgnoreCase))
                    return "Kør auto layout";

                if (TryGetSavedLayout(_layoutPreset, out _, out var canonical) || string.Equals(_layoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase))
                    return $"Kør {canonical ?? _layoutPreset} layout";

                return "Kør standard layout";
            }
        }

        public bool LayoutSkipMinimized
        {
            get => _layoutSkipMinimized;
            set
            {
                if (SetField(ref _layoutSkipMinimized, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool LayoutCurrentMonitorOnly
        {
            get => _layoutCurrentMonitorOnly;
            set
            {
                if (SetField(ref _layoutCurrentMonitorOnly, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public bool LayoutReserveIconGridSlot
        {
            get => _layoutReserveIconGridSlot;
            set
            {
                if (SetField(ref _layoutReserveIconGridSlot, value))
                {
                    SaveSettingsToConfig();
                }
            }
        }

        public int LayoutIconGridSlot
        {
            get => _layoutIconGridSlot;
            set
            {
                // Clamp to first 4 slots.
                var clamped = Math.Max(0, Math.Min(3, value));
                if (SetField(ref _layoutIconGridSlot, clamped))
                {
                    SaveSlotForPreset(_layoutPreset, clamped);
                    SaveSettingsToConfig();
                }
            }
        }

        public IReadOnlyDictionary<string, int[]> LayoutLinks => _layoutLinks;

        public IReadOnlyDictionary<string, List<CustomLayoutSlot>> SavedLayouts => _savedLayouts;

        public IEnumerable<string> SavedLayoutNames => _savedLayouts.Keys.OrderBy(k => k);

        public IEnumerable<string> LayoutPresetChoices => new[] { "Auto", "Grid2x2", "TwoUp", "ThreePane", "ThreePaneMirror" }.Concat(SavedLayoutNames).Distinct(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CustomLayoutSlot> FavoriteLayoutSlots => _favoriteLayoutSlots;

        private int GetSlotForPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return _layoutIconGridSlot;

            if (_layoutIconGridSlots.TryGetValue(preset, out var slot))
                return slot;

            return _layoutIconGridSlot;
        }

        private void SaveSlotForPreset(string preset, int slot)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return;

            _layoutIconGridSlots[preset] = slot;
        }

        public void SetLayoutLink(string preset, int[]? slots)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return;

            if (slots == null || slots.Length == 0)
            {
                if (_layoutLinks.ContainsKey(preset))
                    _layoutLinks.Remove(preset);
            }
            else
            {
                _layoutLinks[preset] = slots;
            }

            SaveSettingsToConfig();
        }

        public IReadOnlyList<CustomLayoutSlot> GetSavedLayoutSlots(string layoutName)
        {
            if (TryGetSavedLayout(layoutName, out var slots, out _))
                return slots;

            if (string.Equals(layoutName, "Favorite", StringComparison.OrdinalIgnoreCase) && _favoriteLayoutSlots.Any())
                return _favoriteLayoutSlots;

            return Array.Empty<CustomLayoutSlot>();
        }

        public bool TryGetSavedLayout(string layoutName, out List<CustomLayoutSlot> slots, out string? canonicalName)
        {
            slots = new List<CustomLayoutSlot>();
            canonicalName = null;

            if (string.IsNullOrWhiteSpace(layoutName))
                return false;

            foreach (var kvp in _savedLayouts)
            {
                if (string.Equals(kvp.Key, layoutName, StringComparison.OrdinalIgnoreCase))
                {
                    slots = kvp.Value;
                    canonicalName = kvp.Key;
                    return true;
                }
            }

            return false;
        }

        public void SaveLayout(string layoutName, IEnumerable<CustomLayoutSlot> slots)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return;

            _savedLayouts[layoutName] = CloneSlots(slots);
            _favoriteLayoutSlots = _savedLayouts[layoutName];
            // Preserve the current slot selection for this saved layout.
            SaveSlotForPreset(layoutName, _layoutIconGridSlot);

            TrimSavedLayouts();

            OnPropertyChanged(nameof(FavoriteLayoutSlots));
            OnPropertyChanged(nameof(SavedLayouts));
            OnPropertyChanged(nameof(SavedLayoutNames));
            OnPropertyChanged(nameof(LayoutPresetChoices));
            SaveSettingsToConfig();
        }

        public bool RenameLayout(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return false;

            if (string.Equals(oldName, "Auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newName, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            // No change needed
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!_savedLayouts.ContainsKey(oldName))
                return false;

            // Avoid duplicate name (case-insensitive dictionary already handles this)
            if (_savedLayouts.ContainsKey(newName))
                return false;

            var slots = _savedLayouts[oldName];
            _savedLayouts.Remove(oldName);
            _savedLayouts[newName] = slots;

            if (_layoutLinks.TryGetValue(oldName, out var link))
            {
                _layoutLinks.Remove(oldName);
                _layoutLinks[newName] = link;
            }

            if (string.Equals(_layoutPreset, oldName, StringComparison.OrdinalIgnoreCase))
            {
                _layoutPreset = newName;
                OnPropertyChanged(nameof(LayoutPreset));
                OnPropertyChanged(nameof(LayoutPresetToolTip));
            }

            OnPropertyChanged(nameof(SavedLayouts));
            OnPropertyChanged(nameof(SavedLayoutNames));
            OnPropertyChanged(nameof(LayoutPresetChoices));
            SaveSettingsToConfig();
            return true;
        }

        public bool DeleteLayout(string layoutName)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return false;

            if (string.Equals(layoutName, "Auto", StringComparison.OrdinalIgnoreCase))
                return false;

            var removed = _savedLayouts.Remove(layoutName);
            if (_layoutLinks.ContainsKey(layoutName))
            {
                _layoutLinks.Remove(layoutName);
            }

            if (removed && string.Equals(_layoutPreset, layoutName, StringComparison.OrdinalIgnoreCase))
            {
                _layoutPreset = _savedLayouts.Keys.FirstOrDefault() ?? "Auto";
                OnPropertyChanged(nameof(LayoutPreset));
                OnPropertyChanged(nameof(LayoutPresetToolTip));
            }

            if (removed)
            {
                OnPropertyChanged(nameof(SavedLayouts));
                OnPropertyChanged(nameof(SavedLayoutNames));
                OnPropertyChanged(nameof(LayoutPresetChoices));
                SaveSettingsToConfig();
            }

            return removed;
        }

        public void SetFavoriteLayoutSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            _favoriteLayoutSlots = CloneSlots(slots);

            SaveSettingsToConfig();
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
        }

        private static List<CustomLayoutSlot> CloneSlots(IEnumerable<CustomLayoutSlot> slots)
        {
            return slots?
                .Where(s => s != null && s.Width > 0 && s.Height > 0)
                .Select(s => new CustomLayoutSlot
                {
                    X = s.X,
                    Y = s.Y,
                    Width = s.Width,
                    Height = s.Height
                })
                .ToList() ?? new List<CustomLayoutSlot>();
        }

        private void TrimSavedLayouts()
        {
            while (_savedLayouts.Count > MaxSavedLayouts)
            {
                var oldest = _savedLayouts.Keys.FirstOrDefault();
                if (oldest == null)
                    break;
                _savedLayouts.Remove(oldest);
            }
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
            _isLightTheme = config.IsLightTheme;
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
            _layoutPreset = string.IsNullOrWhiteSpace(config.LayoutPreset) ? "Auto" : config.LayoutPreset;
            _layoutSkipMinimized = config.LayoutSkipMinimized;
            _layoutCurrentMonitorOnly = config.LayoutCurrentMonitorOnly;
            _layoutReserveIconGridSlot = config.LayoutReserveIconGridSlot;
            _layoutIconGridSlots.Clear();
            if (config.LayoutIconGridSlots != null)
            {
                foreach (var kvp in config.LayoutIconGridSlots)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                        continue;
                    _layoutIconGridSlots[kvp.Key] = Math.Max(0, Math.Min(3, kvp.Value));
                }
            }
            // Fallback to legacy single slot if no per-profile entry exists.
            _layoutIconGridSlot = GetSlotForPreset(_layoutPreset);
            if (_layoutIconGridSlot == 0 && config.LayoutIconGridSlot > 0)
            {
                _layoutIconGridSlot = Math.Max(0, Math.Min(3, config.LayoutIconGridSlot));
                SaveSlotForPreset(_layoutPreset, _layoutIconGridSlot);
            }
            _layoutLinks = config.LayoutLinks ?? new Dictionary<string, int[]>();
            _savedLayouts.Clear();
            if (config.SavedLayouts != null)
            {
                foreach (var kvp in config.SavedLayouts)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
                        continue;

                    _savedLayouts[kvp.Key] = CloneSlots(kvp.Value);
                }
            }

            if (_savedLayouts.Count == 0 && config.FavoriteLayoutSlots != null && config.FavoriteLayoutSlots.Any())
            {
                _savedLayouts["Favorit"] = CloneSlots(config.FavoriteLayoutSlots);
            }

            if (string.Equals(_layoutPreset, "Favorite", StringComparison.OrdinalIgnoreCase) && _savedLayouts.Count > 0)
            {
                _layoutPreset = _savedLayouts.Keys.First();
            }

            var hasSavedMatch = _savedLayouts.Keys.Any(name => string.Equals(name, _layoutPreset, StringComparison.OrdinalIgnoreCase));
            var isBuiltInPreset = string.Equals(_layoutPreset, "Auto", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(_layoutPreset, "Grid2x2", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(_layoutPreset, "TwoUp", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(_layoutPreset, "ThreePane", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(_layoutPreset, "ThreePaneMirror", StringComparison.OrdinalIgnoreCase);

            if (!hasSavedMatch && !isBuiltInPreset)
            {
                _layoutPreset = _savedLayouts.Keys.FirstOrDefault() ?? "Auto";
            }

            _favoriteLayoutSlots = _savedLayouts.Values.FirstOrDefault() ?? new List<CustomLayoutSlot>();
            OnPropertyChanged(nameof(FavoriteLayoutSlots));
            OnPropertyChanged(nameof(SavedLayouts));
            OnPropertyChanged(nameof(SavedLayoutNames));
            OnPropertyChanged(nameof(LayoutPresetChoices));
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
            if (files == null || files.Length == 0)
                return;

            var added = false;
            foreach (var file in files)
            {
                if (!ShortcutHelper.IsSupportedLauncherFile(file))
                    continue;

                var launcher = ShortcutHelper.CreateLauncherItemFromFile(file, SelectedTab);
                if (launcher != null)
                {
                    launcher.RefreshIcon();
                    _itemIconManager.EnsureItemIconLoaded(launcher);
                    Items.Add(launcher);
                    added = true;
                }
            }

            if (!added)
                return;

            OnPropertyChanged(nameof(CurrentItems));
            SaveItemsToFile();
        }

        public void LaunchItem(LauncherItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return;

            try
            {
                var psi = new ProcessStartInfo(item.Path)
                {
                    UseShellExecute = true
                };

                var workingDirectory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                if (!string.IsNullOrWhiteSpace(item.Arguments))
                {
                    psi.Arguments = item.Arguments;
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {item.Path}: {ex}");
            }
        }

        /// <summary>
        /// Creates and persists a custom shortcut entry.
        /// </summary>
        public LauncherItem CreateCustomShortcut(string displayName, string targetPath, string category, string? arguments = null, string? iconPath = null, int iconIndex = 0)
        {
            var item = new LauncherItem
            {
                DisplayName = displayName,
                Path = targetPath,
                Arguments = arguments,
                Category = category,
                IconPath = iconPath ?? targetPath,
                IconIndex = iconIndex
            };

            Items.Add(item);
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
            try
            {
                Directory.CreateDirectory(_dataFolder);
                // Persist only serializable properties in LauncherItem (Icon/Image ignored)
                var options = new JsonSerializerOptions { WriteIndented = true };
                var list = Items.ToList();
                var json = JsonSerializer.Serialize(list, options);
                File.WriteAllText(_itemsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save items: " + ex);
            }
        }

        private void SaveSettingsToConfig()
        {
            try
            {
            _config.ContentAreaHeight = CalculateContentAreaHeight();
            _config.IconsPerRow = _iconsPerRow;
            _config.IconScale = _icon_scale;
            _config.UiScale = _uiScale;
            _config.ShowDesktopIcon = _showDesktopIcon;
            _config.IsAlwaysOnTop = _isAlwaysOnTop;
            _config.IsFloatingIconTopmost = _isFloatingIconTopmost;
            _config.ShowScrollButtons = _showScrollButtons;
            _config.IsLightTheme = _isLightTheme;
            _config.StartWithWindows = _startWithWindows;
            _config.ShowDevOverlay = _showDevOverlay;
            _config.IconRowSpacing = _iconRowSpacing;
            _config.LastRowPaddingAdjust = _lastRowPaddingAdjust;
            _config.TabNames = Tabs.ToList();
            _config.Language = _language;
            _config.WindowLeft = _windowLeft;
            _config.WindowTop = _windowTop;
            _config.SettingsWindowLeft = _settingsWindowLeft;
            _config.SettingsWindowTop = _settingsWindowTop;
            _config.FloatingIconLeft = _floatingLeft;
            _config.FloatingIconTop = _floatingTop;
            _config.LayoutPreset = _layoutPreset;
            _config.LayoutSkipMinimized = _layoutSkipMinimized;
            _config.LayoutCurrentMonitorOnly = _layoutCurrentMonitorOnly;
            _config.LayoutIconGridSlot = _layoutIconGridSlot;
            _config.LayoutReserveIconGridSlot = _layoutReserveIconGridSlot;
            _config.LayoutIconGridSlots = new Dictionary<string, int>(_layoutIconGridSlots, StringComparer.OrdinalIgnoreCase);
            _config.LayoutLinks = _layoutLinks;
            _config.SavedLayouts = _savedLayouts.ToDictionary(kvp => kvp.Key, kvp => CloneSlots(kvp.Value));
            _config.FavoriteLayoutSlots = _favoriteLayoutSlots;
            _config.EnableSlideUpAnimation = _enableSlideUpAnimation;
            _config.EnableContentScroll = _enableContentScroll;
            _config.WindowAnimationDurationMs = _windowAnimationDurationMs;

                _configManager.SaveConfig(_config);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save settings: " + ex);
            }
        }

        private void LoadItemsFromFile()
        {
            try
            {
                if (!File.Exists(_itemsFilePath))
                    return;

                var json = File.ReadAllText(_itemsFilePath);
                var list = JsonSerializer.Deserialize<LauncherItem[]?>(json);
                if (list == null) return;

                Items.Clear();
                foreach (var it in list)
                {
                    // Ensure icon is (re)loaded from icon path or cached base64 (survives deleted source files)
                    it.RefreshIcon();
                    Items.Add(it);
                }

                OnPropertyChanged(nameof(CurrentItems));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load items: " + ex);
            }
        }

        private void MaybeMigrateItemsFromLegacy()
        {
            try
            {
                if (File.Exists(_itemsFilePath))
                {
                    return;
                }

                if (!File.Exists(_legacyItemsFilePath))
                {
                    return;
                }

                Directory.CreateDirectory(_dataFolder);
                File.Copy(_legacyItemsFilePath, _itemsFilePath, overwrite: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to migrate legacy items: " + ex);
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

        private void ThemeHelper_ThemeChanged(object? sender, ThemeSnapshot e)
        {
            ApplyTheme(e);
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
            IsLightTheme = snapshot.IsLightTheme;
            AccentBrush = new WMedia.SolidColorBrush(snapshot.AccentColor);
            TopBarBackground = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromRgb(243, 243, 243))
                : new WMedia.SolidColorBrush(WMedia.Color.FromRgb(32, 32, 32));
            TopBarForeground = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromRgb(15, 23, 42))
                : new WMedia.SolidColorBrush(WMedia.Color.FromRgb(229, 229, 229));

            SettingsWindowBackground = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromRgb(244, 245, 247))
                : new WMedia.SolidColorBrush(WMedia.Color.FromRgb(9, 12, 24));
            SettingsCardBackground = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromRgb(255, 255, 255))
                : new WMedia.SolidColorBrush(WMedia.Color.FromRgb(15, 20, 38));
            SettingsCardBorderBrush = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromArgb(96, 15, 23, 42))
                : new WMedia.SolidColorBrush(WMedia.Color.FromArgb(110, 255, 255, 255));
            SettingsSubtextForeground = snapshot.IsLightTheme
                ? new WMedia.SolidColorBrush(WMedia.Color.FromRgb(63, 63, 70))
                : new WMedia.SolidColorBrush(WMedia.Color.FromRgb(148, 163, 184));
            SettingsShadowColor = snapshot.IsLightTheme
                ? WMedia.Color.FromArgb(60, 0, 0, 0)
                : WMedia.Color.FromArgb(120, 0, 0, 0);
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
            _isLightTheme = true;
            _floatingLeft = null;
            _floatingTop = null;
            LayoutPreset = "Auto";
            LayoutSkipMinimized = true;
            LayoutCurrentMonitorOnly = true;
            LayoutIconGridSlot = 0;
            _layoutLinks.Clear();
            _favoriteLayoutSlots.Clear();
            WindowAnimationDurationMs = 250;

            SaveSettingsToConfig();

            // Refresh bindings for theme-related brushes.
            OnPropertyChanged(nameof(IsLightTheme));
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






