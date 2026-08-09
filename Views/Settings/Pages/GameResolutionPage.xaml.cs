using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using IconGrid.Helpers;
using IconGrid.Helpers.Launcher;
using IconGrid.Models;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    public partial class GameResolutionPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private const string DefaultLanguage = "da";
        private const string NoResolutionValue = "";
        private MainViewModel? _mainViewModel;
        private string _pageTitleText = "Game resolution";
        private string _pageIntroText = string.Empty;
        private string _restoreAfterExitTitleText = string.Empty;
        private string _restoreAfterExitDescriptionText = string.Empty;
        private string _resolutionListIntroText = string.Empty;
        private string _categoryFilterTitleText = string.Empty;
        private string _categoryFilterDescriptionText = string.Empty;
        private string _currentResolutionLabelText = string.Empty;
        private string _currentResolutionValue = string.Empty;
        private CategoryOption? _selectedCategory;
        private readonly ObservableCollection<LauncherItem> _allShortcuts = new();
        private readonly ObservableCollection<CategoryOption> _availableCategories = new();
        private IReadOnlyList<string> _supportedResolutions;

        public GameResolutionPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += GameResolutionPage_Loaded;
            Unloaded += GameResolutionPage_Unloaded;

            var resolutions = DisplayResolutionService.GetSupportedResolutions().ToList();
            resolutions.Insert(0, NoResolutionValue);
            _supportedResolutions = resolutions;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Shortcuts in the currently selected category.
        /// </summary>
        public ObservableCollection<LauncherItem> FilteredShortcuts { get; } = new();

        public ObservableCollection<CategoryOption> AvailableCategories => _availableCategories;

        public IReadOnlyList<string> SupportedResolutions => _supportedResolutions;

        public CategoryOption? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (ReferenceEquals(_selectedCategory, value))
                    return;

                _selectedCategory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCategory)));
                RefreshFilteredShortcuts();
            }
        }

        public string PageTitleText
        {
            get => _pageTitleText;
            private set => SetField(ref _pageTitleText, value);
        }

        public string PageIntroText
        {
            get => _pageIntroText;
            private set => SetField(ref _pageIntroText, value);
        }

        public string RestoreAfterExitTitleText
        {
            get => _restoreAfterExitTitleText;
            private set => SetField(ref _restoreAfterExitTitleText, value);
        }

        public string RestoreAfterExitDescriptionText
        {
            get => _restoreAfterExitDescriptionText;
            private set => SetField(ref _restoreAfterExitDescriptionText, value);
        }

        public string ResolutionListIntroText
        {
            get => _resolutionListIntroText;
            private set => SetField(ref _resolutionListIntroText, value);
        }

        public string CategoryFilterTitleText
        {
            get => _categoryFilterTitleText;
            private set => SetField(ref _categoryFilterTitleText, value);
        }

        public string CategoryFilterDescriptionText
        {
            get => _categoryFilterDescriptionText;
            private set => SetField(ref _categoryFilterDescriptionText, value);
        }

        public string CurrentResolutionLabelText
        {
            get => _currentResolutionLabelText;
            private set => SetField(ref _currentResolutionLabelText, value);
        }

        public string CurrentResolutionValue
        {
            get => _currentResolutionValue;
            private set => SetField(ref _currentResolutionValue, value);
        }

        private void GameResolutionPage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachMainViewModel();
            RefreshLocalizedText();
            RefreshShortcuts();
            RefreshCurrentResolution();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportedResolutions)));
        }

        private void GameResolutionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Items.CollectionChanged -= Items_CollectionChanged;
                _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
                _mainViewModel = null;
            }
        }

        private void AttachMainViewModel()
        {
            var next = Window.GetWindow(this)?.DataContext as MainViewModel;
            if (ReferenceEquals(_mainViewModel, next))
                return;

            if (_mainViewModel != null)
            {
                _mainViewModel.Items.CollectionChanged -= Items_CollectionChanged;
                _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            }

            _mainViewModel = next;

            if (_mainViewModel != null)
            {
                _mainViewModel.Items.CollectionChanged += Items_CollectionChanged;
                _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            }
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RefreshShortcuts();
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.Language), System.StringComparison.Ordinal))
            {
                RefreshLocalizedText();
                // Rebuild the category options so the ComboBox selection box
                // re-renders with the newly localized DisplayName.
                RefreshShortcuts();
            }
        }

        private void RefreshCurrentResolution()
        {
            CurrentResolutionValue = DisplayResolutionService.GetCurrentResolutionWithHz();
        }

        private void RefreshShortcuts()
        {
            if (_mainViewModel == null)
                return;

            foreach (var item in _allShortcuts)
                item.PropertyChanged -= Item_PropertyChanged;

            _allShortcuts.Clear();
            _availableCategories.Clear();

            var language = _mainViewModel.Language;
            if (string.IsNullOrWhiteSpace(language))
                language = DefaultLanguage;

            foreach (var item in _mainViewModel.Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
                _allShortcuts.Add(item);

                if (!_availableCategories.Any(c => string.Equals(c.Key, item.Category, System.StringComparison.OrdinalIgnoreCase)))
                {
                    _availableCategories.Add(new CategoryOption(
                        item.Category,
                        TabNameLocalizationConverter.LocalizeCategoryName(item.Category, language)));
                }
            }

            // Include external games detected via foreground/FPS detection
            // (games launched outside IconGrid — Battle.net, Steam, Ubisoft, etc.)
            // Always read from disk fresh so newly registered games appear immediately.
            var externalRegistry = new ExternalGameRegistry();
            var externalItems = externalRegistry.GetLauncherItems();
            if (externalItems.Count > 0)
            {
                var externalCategory = "External";
                if (!_availableCategories.Any(c => string.Equals(c.Key, externalCategory, System.StringComparison.OrdinalIgnoreCase)))
                {
                    _availableCategories.Add(new CategoryOption(
                        externalCategory,
                        TabNameLocalizationConverter.LocalizeCategoryName(externalCategory, language)));
                }

                foreach (var item in externalItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    _allShortcuts.Add(item);
                }
            }

            // Keep the selected category if it still exists; otherwise fall back to "Games".
            var selectedKey = _selectedCategory?.Key;
            _selectedCategory = _availableCategories.FirstOrDefault(c => string.Equals(c.Key, selectedKey, System.StringComparison.OrdinalIgnoreCase))
                                ?? _availableCategories.FirstOrDefault(c => string.Equals(c.Key, "Games", System.StringComparison.OrdinalIgnoreCase))
                                ?? _availableCategories.FirstOrDefault()
                                ?? new CategoryOption("Games", TabNameLocalizationConverter.LocalizeCategoryName("Games", language));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCategory)));

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableCategories)));
            RefreshFilteredShortcuts();
        }

        private void RefreshFilteredShortcuts()
        {
            foreach (var item in FilteredShortcuts)
                item.PropertyChanged -= Item_PropertyChanged;

            FilteredShortcuts.Clear();
            var selectedKey = _selectedCategory?.Key;
            if (selectedKey != null)
            {
                foreach (var item in _allShortcuts.Where(i => string.Equals(i.Category, selectedKey, System.StringComparison.OrdinalIgnoreCase)))
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    FilteredShortcuts.Add(item);
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredShortcuts)));
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(LauncherItem.GameResolution), System.StringComparison.Ordinal) &&
                _mainViewModel != null)
            {
                _mainViewModel.SaveItemsToFile();
            }
        }

        private void RefreshLocalizedText()
        {
            var language = _mainViewModel?.Language;
            if (string.IsNullOrWhiteSpace(language))
                language = DefaultLanguage;

            if (string.Equals(language, "da", System.StringComparison.OrdinalIgnoreCase))
            {
                PageTitleText = "Spilopløsning";
                PageIntroText = "Vælg hvilken skærmopløsning IconGrid skal skifte til, før et spil startes fra dine genveje.";
                RestoreAfterExitTitleText = "Gendan opløsning efter spil";
                RestoreAfterExitDescriptionText = "Når et spil lukkes (eller crasher), skifter Windows automatisk tilbage til den oprindelige opløsning.";
                ResolutionListIntroText = "Vælg en opløsning for hver genvej. Tom = skift ikke opløsning.";
                CategoryFilterTitleText = "Kategori";
                CategoryFilterDescriptionText = "Vis kun genveje fra den valgte kategori.";
                CurrentResolutionLabelText = "Nuværende opløsning";
            }
            else
            {
                PageTitleText = "Game resolution";
                PageIntroText = "Choose which display resolution IconGrid should switch to before launching a game from your shortcuts.";
                RestoreAfterExitTitleText = "Restore resolution after game";
                RestoreAfterExitDescriptionText = "When a game closes (or crashes), Windows automatically switches back to the original resolution.";
                ResolutionListIntroText = "Pick a resolution for each shortcut. Empty = do not change resolution.";
                CategoryFilterTitleText = "Category";
                CategoryFilterDescriptionText = "Only show shortcuts from the selected category.";
                CurrentResolutionLabelText = "Current resolution";
            }

            foreach (var category in _availableCategories)
            {
                category.DisplayName = TabNameLocalizationConverter.LocalizeCategoryName(category.Key, language);
            }
        }

        private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            if (string.Equals(field, value, System.StringComparison.Ordinal))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// A category/tab in the launcher with a localized display name.
    /// The Key is the raw category name used for filtering; DisplayName follows the active UI language.
    /// </summary>
    public sealed class CategoryOption : INotifyPropertyChanged
    {
        private string _displayName;

        public CategoryOption(string key, string displayName)
        {
            Key = key;
            _displayName = displayName;
        }

        public string Key { get; }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (string.Equals(_displayName, value, System.StringComparison.Ordinal))
                    return;

                _displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => DisplayName;
    }
}