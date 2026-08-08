using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    public partial class StartsidePage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private const string DefaultLanguage = "da";
        private MainViewModel? _mainViewModel;
        private string _launcherVisibilityTitleText = "Launcher visibility";
        private string _launcherVisibilityDescriptionText = string.Empty;
        private string _launcherVisibilityModeDescriptionText = string.Empty;
        private bool _autoHideDelayVisible;
        private string _autoHideDelayLabelText = "Auto-hide delay";
        private string _autoHideDelayDescriptionText = string.Empty;
        private string _peekActivationLabelText = "Peek activation";
        private string _peekActivationDescriptionText = string.Empty;
        private List<KeyValuePair<string, string>> _launcherHideModeItems = new();
        private List<KeyValuePair<string, string>> _autoHideDelayItems = new();
        private List<KeyValuePair<string, string>> _peekActivationItems = new();

        public StartsidePage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += StartsidePage_Loaded;
            Unloaded += StartsidePage_Unloaded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string LauncherVisibilityTitleText
        {
            get => _launcherVisibilityTitleText;
            private set => SetField(ref _launcherVisibilityTitleText, value);
        }

        public string LauncherVisibilityDescriptionText
        {
            get => _launcherVisibilityDescriptionText;
            private set => SetField(ref _launcherVisibilityDescriptionText, value);
        }

        public string LauncherVisibilityModeDescriptionText
        {
            get => _launcherVisibilityModeDescriptionText;
            private set => SetField(ref _launcherVisibilityModeDescriptionText, value);
        }

        public bool AutoHideDelayVisible
        {
            get => _autoHideDelayVisible;
            private set => SetField(ref _autoHideDelayVisible, value);
        }

        public string AutoHideDelayLabelText
        {
            get => _autoHideDelayLabelText;
            private set => SetField(ref _autoHideDelayLabelText, value);
        }

        public string AutoHideDelayDescriptionText
        {
            get => _autoHideDelayDescriptionText;
            private set => SetField(ref _autoHideDelayDescriptionText, value);
        }

        public string PeekActivationLabelText
        {
            get => _peekActivationLabelText;
            private set => SetField(ref _peekActivationLabelText, value);
        }

        public string PeekActivationDescriptionText
        {
            get => _peekActivationDescriptionText;
            private set => SetField(ref _peekActivationDescriptionText, value);
        }

        public List<KeyValuePair<string, string>> LauncherHideModeItems
        {
            get => _launcherHideModeItems;
            private set => SetField(ref _launcherHideModeItems, value);
        }

        public List<KeyValuePair<string, string>> AutoHideDelayItems
        {
            get => _autoHideDelayItems;
            private set => SetField(ref _autoHideDelayItems, value);
        }

        public List<KeyValuePair<string, string>> PeekActivationItems
        {
            get => _peekActivationItems;
            private set => SetField(ref _peekActivationItems, value);
        }

        public string SelectedLauncherHideMode
        {
            get => _mainViewModel?.LauncherHideMode.ToString() ?? "0";
            set
            {
                if (_mainViewModel != null && int.TryParse(value, out var parsed))
                {
                    _mainViewModel.LauncherHideMode = parsed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLauncherHideMode)));
                    RefreshModeDescription();
                }
            }
        }

        public string SelectedAutoHideDelay
        {
            get => _mainViewModel?.IdleAutoHideDelaySeconds.ToString() ?? "2";
            set
            {
                if (_mainViewModel != null && int.TryParse(value, out var parsed))
                {
                    _mainViewModel.IdleAutoHideDelaySeconds = parsed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedAutoHideDelay)));
                }
            }
        }

        public string SelectedPeekActivationMode
        {
            get => _mainViewModel?.PeekActivationMode.ToString() ?? "0";
            set
            {
                if (_mainViewModel != null && int.TryParse(value, out var parsed))
                {
                    _mainViewModel.PeekActivationMode = parsed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPeekActivationMode)));
                }
            }
        }

        private void StartsidePage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachMainViewModel();
            RefreshLocalizedText();
            RefreshModeDescription();
        }

        private void StartsidePage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
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
                _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            }

            _mainViewModel = next;

            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.Language), System.StringComparison.Ordinal))
            {
                RefreshLocalizedText();
            }
        }

        private void RefreshModeDescription()
        {
            var mode = _mainViewModel?.LauncherHideMode ?? 0;
            var delay = _mainViewModel?.IdleAutoHideDelaySeconds ?? 2;
            var language = _mainViewModel?.Language;
            if (string.IsNullOrWhiteSpace(language))
                language = DefaultLanguage;

            var isDanish = string.Equals(language, "da", System.StringComparison.OrdinalIgnoreCase);

            AutoHideDelayVisible = mode == 2;

            LauncherVisibilityModeDescriptionText = mode switch
            {
                0 => isDanish
                    ? "Launcheren forbliver altid synlig på skrivebordet"
                    : "The launcher stays always visible on the desktop",
                1 => isDanish
                    ? "Launcheren skjules manuelt via ▼-knappen. Klik på den synlige stribe i toppen af skærmen for at vise den igen"
                    : "The launcher is hidden manually via the ▼ button. Click the visible strip at the top of the screen to show it again",
                2 => isDanish
                    ? $"Launcheren skjules automatisk efter {delay} sekunder uden mus. Før musen til toppen af skærmen for at vise den"
                    : $"The launcher auto-hides after {delay} seconds without mouse activity. Move the mouse to the top of the screen to show it",
                _ => string.Empty
            };
        }

        private void RefreshLocalizedText()
        {
            var language = _mainViewModel?.Language;
            if (string.IsNullOrWhiteSpace(language))
                language = DefaultLanguage;

            if (string.Equals(language, "da", System.StringComparison.OrdinalIgnoreCase))
            {
                LauncherVisibilityTitleText = "Launcher synlighed";
                LauncherVisibilityDescriptionText = "Vælg om launcheren skal skjules automatisk når den ikke bruges, eller manuelt via en knap. Peek-striben i toppen af skærmen signalerer at launcheren er skjult.";
                AutoHideDelayLabelText = "Auto-skjul forsinkelse";
                AutoHideDelayDescriptionText = "Antal sekunder før launcheren automatisk skjules efter musen forlader vinduet";
                PeekActivationLabelText = "Peek aktivering";
                PeekActivationDescriptionText = "Vælg om launcheren skal vises når musen nærmer sig toppen af skærmen, eller kun når der klikkes på den synlige stribe";
                LauncherHideModeItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "Altid synlig"),
                    new("1", "Manuel skjul"),
                    new("2", "Auto-skjul")
                };
                AutoHideDelayItems = new List<KeyValuePair<string, string>>
                {
                    new("1", "1 sekund"),
                    new("2", "2 sekunder"),
                    new("3", "3 sekunder"),
                    new("5", "5 sekunder"),
                    new("10", "10 sekunder")
                };
                PeekActivationItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "Ved hover (mus nær topkant)"),
                    new("1", "Ved klik på striben")
                };
            }
            else
            {
                LauncherVisibilityTitleText = "Launcher visibility";
                LauncherVisibilityDescriptionText = "Choose whether the launcher hides automatically when idle, or manually via a button. The peek strip at the top of the screen signals that the launcher is hidden.";
                AutoHideDelayLabelText = "Auto-hide delay";
                AutoHideDelayDescriptionText = "Number of seconds before the launcher automatically hides after the mouse leaves the window";
                PeekActivationLabelText = "Peek activation";
                PeekActivationDescriptionText = "Choose whether the launcher shows when the mouse approaches the top of the screen, or only when clicking the visible strip";
                LauncherHideModeItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "Always visible"),
                    new("1", "Manual hide"),
                    new("2", "Auto-hide")
                };
                AutoHideDelayItems = new List<KeyValuePair<string, string>>
                {
                    new("1", "1 second"),
                    new("2", "2 seconds"),
                    new("3", "3 seconds"),
                    new("5", "5 seconds"),
                    new("10", "10 seconds")
                };
                PeekActivationItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "On hover (mouse near top edge)"),
                    new("1", "On click on the strip")
                };
            }

            // Notify the ComboBox to re-read its SelectedValue after rebuilding items.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLauncherHideMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedAutoHideDelay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPeekActivationMode)));
            RefreshModeDescription();
        }

        private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            if (string.Equals(field, value, System.StringComparison.Ordinal))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}