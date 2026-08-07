using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Launcher;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
    /// <summary>
    /// One row in the "default overlay scale per resolution" list.
    /// </summary>
    public sealed class ResolutionScaleEntry : INotifyPropertyChanged
    {
        private readonly Action<string, double>? _onScaleChanged;
        private double _scale;

        public string Resolution { get; }

        public double Scale
        {
            get => _scale;
            set
            {
                if (System.Math.Abs(_scale - value) < 0.005)
                    return;

                _scale = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Scale)));
                _onScaleChanged?.Invoke(Resolution, value);
            }
        }

        public ResolutionScaleEntry(string resolution, double scale, Action<string, double>? onScaleChanged)
        {
            Resolution = resolution;
            _scale = scale;
            _onScaleChanged = onScaleChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class GamingOverlayPage : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private const string DefaultLanguage = "da";
        private string _fpsSetupStatusText = "Checking...";
        private string _fpsSetupDetailsText = string.Empty;
        private bool _canRunFpsSetupFix;
        private string _currentUserDisplayName = string.Empty;
        private MainViewModel? _mainViewModel;
        private string _pageTitleText = "Gaming overlay";
        private string _pageIntroText = string.Empty;
        private string _overlayTransparentBackgroundTitleText = "Transparent background";
        private string _overlayTransparentBackgroundDescriptionText = string.Empty;
        private string _overlayAutoTransparentTitleText = "Transparent while in game";
        private string _overlayAutoTransparentDescriptionText = string.Empty;
        private string _overlayTextColorTitleText = "Text color";
        private string _overlayCustomColorButtonText = "Custom color...";
        private string _overlayScaleTitleText = string.Empty;
        private string _fpsSetupTitleText = string.Empty;
        private string _fpsSetupIntroText = string.Empty;
        private string _refreshFpsSetupButtonText = string.Empty;
        private string _runFpsSetupFixButtonText = string.Empty;
        private string _runFpsSetupFixHelpText = string.Empty;
        private string _technicalDetailsTitleText = string.Empty;
        private string _technicalDetailsIntroText = string.Empty;
        private string _readyBadgeText = "Ready";
        private string _resolutionDefaultsTitleText = string.Empty;
        private string _resolutionDefaultsIntroText = string.Empty;
        private string _overlayPositionTitleText = string.Empty;
        private string _overlayPositionIntroText = string.Empty;
        private string _gameAutoBehaviorTitleText = string.Empty;
        private string _gameAutoBehaviorIntroText = string.Empty;
        private string _gameAutoShowTitleText = string.Empty;
        private string _gameAutoShowIntroText = string.Empty;
        private string _gameAutoCloseTitleText = string.Empty;
        private string _gameAutoCloseIntroText = string.Empty;
        private string _restoreLauncherTitleText = string.Empty;
        private string _restoreLauncherIntroText = string.Empty;
        private string _gameLauncherBehaviorTitleText = string.Empty;
        private string _gameLauncherBehaviorIntroText = string.Empty;
        private List<KeyValuePair<string, string>> _overlayPositionPresetItems = new();
        private List<KeyValuePair<string, string>> _gameLauncherBehaviorItems = new();
        private readonly ObservableCollection<ResolutionScaleEntry> _resolutionScaleEntries = new();

        public GamingOverlayPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += GamingOverlayPage_Loaded;
            Unloaded += GamingOverlayPage_Unloaded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string FpsSetupStatusText
        {
            get => _fpsSetupStatusText;
            private set => SetField(ref _fpsSetupStatusText, value);
        }

        public string FpsSetupDetailsText
        {
            get => _fpsSetupDetailsText;
            private set => SetField(ref _fpsSetupDetailsText, value);
        }

        public bool CanRunFpsSetupFix
        {
            get => _canRunFpsSetupFix;
            private set => SetField(ref _canRunFpsSetupFix, value);
        }

        public bool IsFpsSetupReady => !CanRunFpsSetupFix;

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

        public string OverlayTransparentBackgroundTitleText
        {
            get => _overlayTransparentBackgroundTitleText;
            private set => SetField(ref _overlayTransparentBackgroundTitleText, value);
        }

        public string OverlayTransparentBackgroundDescriptionText
        {
            get => _overlayTransparentBackgroundDescriptionText;
            private set => SetField(ref _overlayTransparentBackgroundDescriptionText, value);
        }

        public string OverlayAutoTransparentTitleText
        {
            get => _overlayAutoTransparentTitleText;
            private set => SetField(ref _overlayAutoTransparentTitleText, value);
        }

        public string OverlayAutoTransparentDescriptionText
        {
            get => _overlayAutoTransparentDescriptionText;
            private set => SetField(ref _overlayAutoTransparentDescriptionText, value);
        }

        public string OverlayTextColorTitleText
        {
            get => _overlayTextColorTitleText;
            private set => SetField(ref _overlayTextColorTitleText, value);
        }

        public string OverlayCustomColorButtonText
        {
            get => _overlayCustomColorButtonText;
            private set => SetField(ref _overlayCustomColorButtonText, value);
        }

        public string OverlayScaleTitleText
        {
            get => _overlayScaleTitleText;
            private set => SetField(ref _overlayScaleTitleText, value);
        }

        public string FpsSetupTitleText
        {
            get => _fpsSetupTitleText;
            private set => SetField(ref _fpsSetupTitleText, value);
        }

        public string FpsSetupIntroText
        {
            get => _fpsSetupIntroText;
            private set => SetField(ref _fpsSetupIntroText, value);
        }

        public string RefreshFpsSetupButtonText
        {
            get => _refreshFpsSetupButtonText;
            private set => SetField(ref _refreshFpsSetupButtonText, value);
        }

        public string RunFpsSetupFixButtonText
        {
            get => _runFpsSetupFixButtonText;
            private set => SetField(ref _runFpsSetupFixButtonText, value);
        }

        public string RunFpsSetupFixHelpText
        {
            get => _runFpsSetupFixHelpText;
            private set => SetField(ref _runFpsSetupFixHelpText, value);
        }

        public string TechnicalDetailsTitleText
        {
            get => _technicalDetailsTitleText;
            private set => SetField(ref _technicalDetailsTitleText, value);
        }

        public string TechnicalDetailsIntroText
        {
            get => _technicalDetailsIntroText;
            private set => SetField(ref _technicalDetailsIntroText, value);
        }

        public string ReadyBadgeText
        {
            get => _readyBadgeText;
            private set => SetField(ref _readyBadgeText, value);
        }

        public ObservableCollection<ResolutionScaleEntry> ResolutionScaleEntries => _resolutionScaleEntries;

        public string ResolutionDefaultsTitleText
        {
            get => _resolutionDefaultsTitleText;
            private set => SetField(ref _resolutionDefaultsTitleText, value);
        }

        public string ResolutionDefaultsIntroText
        {
            get => _resolutionDefaultsIntroText;
            private set => SetField(ref _resolutionDefaultsIntroText, value);
        }

        public string OverlayPositionTitleText
        {
            get => _overlayPositionTitleText;
            private set => SetField(ref _overlayPositionTitleText, value);
        }

        public string OverlayPositionIntroText
        {
            get => _overlayPositionIntroText;
            private set => SetField(ref _overlayPositionIntroText, value);
        }

        public string GameAutoBehaviorTitleText
        {
            get => _gameAutoBehaviorTitleText;
            private set => SetField(ref _gameAutoBehaviorTitleText, value);
        }

        public string GameAutoBehaviorIntroText
        {
            get => _gameAutoBehaviorIntroText;
            private set => SetField(ref _gameAutoBehaviorIntroText, value);
        }

        public string GameAutoShowTitleText
        {
            get => _gameAutoShowTitleText;
            private set => SetField(ref _gameAutoShowTitleText, value);
        }

        public string GameAutoShowIntroText
        {
            get => _gameAutoShowIntroText;
            private set => SetField(ref _gameAutoShowIntroText, value);
        }

        public string GameAutoCloseTitleText
        {
            get => _gameAutoCloseTitleText;
            private set => SetField(ref _gameAutoCloseTitleText, value);
        }

        public string GameAutoCloseIntroText
        {
            get => _gameAutoCloseIntroText;
            private set => SetField(ref _gameAutoCloseIntroText, value);
        }

        public string RestoreLauncherTitleText
        {
            get => _restoreLauncherTitleText;
            private set => SetField(ref _restoreLauncherTitleText, value);
        }

        public string RestoreLauncherIntroText
        {
            get => _restoreLauncherIntroText;
            private set => SetField(ref _restoreLauncherIntroText, value);
        }

        public string GameLauncherBehaviorTitleText
        {
            get => _gameLauncherBehaviorTitleText;
            private set => SetField(ref _gameLauncherBehaviorTitleText, value);
        }

        public string GameLauncherBehaviorIntroText
        {
            get => _gameLauncherBehaviorIntroText;
            private set => SetField(ref _gameLauncherBehaviorIntroText, value);
        }

        /// <summary>
        /// Localized options for the launcher auto-behavior while a game runs.
        /// Key = GameAutoBehaviorMode value ("0"/"1"/"2"), Value = display text.
        /// </summary>
        public List<KeyValuePair<string, string>> GameLauncherBehaviorItems
        {
            get => _gameLauncherBehaviorItems;
            private set => SetField(ref _gameLauncherBehaviorItems, value);
        }

        /// <summary>
        /// Currently selected launcher behavior (forwards to MainViewModel).
        /// </summary>
        public string SelectedGameLauncherBehavior
        {
            get => _mainViewModel?.GameLauncherAutoBehavior.ToString() ?? "0";
            set
            {
                if (_mainViewModel != null && int.TryParse(value, out var parsed))
                {
                    _mainViewModel.GameLauncherAutoBehavior = parsed;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedGameLauncherBehavior)));
                }
            }
        }

        public bool AutoShowGamingOverlayOnGameStart
        {
            get => _mainViewModel?.AutoShowGamingOverlayOnGameStart ?? false;
            set
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.AutoShowGamingOverlayOnGameStart = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoShowGamingOverlayOnGameStart)));
                }
            }
        }

        public bool AutoCloseGamingOverlayOnGameEnd
        {
            get => _mainViewModel?.AutoCloseGamingOverlayOnGameEnd ?? false;
            set
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.AutoCloseGamingOverlayOnGameEnd = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoCloseGamingOverlayOnGameEnd)));
                }
            }
        }

        public bool RestoreLauncherAfterOverlayClosed
        {
            get => _mainViewModel?.RestoreLauncherAfterOverlayClosed ?? false;
            set
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.RestoreLauncherAfterOverlayClosed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RestoreLauncherAfterOverlayClosed)));
                }
            }
        }

        /// <summary>
        /// Localized options for the overlay's standard placement preset.
        /// Key = GamingOverlayPositionPreset enum name, Value = display text.
        /// </summary>
        public List<KeyValuePair<string, string>> OverlayPositionPresetItems
        {
            get => _overlayPositionPresetItems;
            private set => SetField(ref _overlayPositionPresetItems, value);
        }

        /// <summary>
        /// Currently selected standard placement preset (forwards to MainViewModel).
        /// </summary>
        public string SelectedOverlayPositionPreset
        {
            get => _mainViewModel?.GamingOverlayPositionPreset ?? "TopRight";
            set
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.GamingOverlayPositionPreset = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOverlayPositionPreset)));
                }
            }
        }

        private void GamingOverlayPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AttachMainViewModel();
            RefreshLocalizedText();
            RefreshResolutionScales();
            RefreshFpsSetupStatus();
        }

        private void RefreshResolutionScales()
        {
            if (_mainViewModel == null)
                return;

            _resolutionScaleEntries.Clear();
            foreach (var resolution in DisplayResolutionService.GetSupportedResolutions())
            {
                var parsed = DisplayResolutionService.ParseResolution(resolution);
                if (!parsed.HasValue)
                    continue;

                // Only show common gaming resolutions: 1920x1080 up to 4096x2160.
                var width = parsed.Value.Width;
                var height = parsed.Value.Height;
                if (width < 1920 || height < 1080 || width > 4096 || height > 2160)
                    continue;

                var entry = new ResolutionScaleEntry(
                    resolution,
                    _mainViewModel.GetGamingOverlayScaleForResolution(resolution),
                    (res, scale) => _mainViewModel?.SetGamingOverlayScaleForResolution(res, scale));
                _resolutionScaleEntries.Add(entry);
            }
        }

        private void GamingOverlayPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
                _mainViewModel = null;
            }
        }

        private void RefreshFpsSetupButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RefreshFpsSetupStatus();
        }

        private void OverlayColorSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element &&
                element.Tag is string hex &&
                _mainViewModel != null)
            {
                _mainViewModel.GamingOverlayTextColor = hex;
            }
        }

        private void OverlayCustomColorButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_mainViewModel == null)
                return;

            using var dialog = new System.Windows.Forms.ColorDialog();
            try
            {
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(_mainViewModel.GamingOverlayTextColor);
            }
            catch
            {
                dialog.Color = System.Drawing.Color.White;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _mainViewModel.GamingOverlayTextColor = ToHex(dialog.Color);
            }
        }

        private static string ToHex(System.Drawing.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void RunFpsSetupFixButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!CanRunFpsSetupFix || string.IsNullOrWhiteSpace(_currentUserDisplayName))
            {
                return;
            }

            var launched = EtwAccessRequirements.TryLaunchElevatedSetup(_currentUserDisplayName);
            if (!launched)
            {
                System.Windows.MessageBox.Show(
                    "Could not launch the elevated setup command. Try the suggested command manually from an administrator PowerShell window.",
                    "IconGrid",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            System.Windows.MessageBox.Show(
                "The admin setup command was launched. After the user has been added to Performance Log Users, sign out/in or restart Windows before testing FPS again.",
                "IconGrid",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RefreshFpsSetupStatus()
        {
            var status = EtwAccessRequirements.GetCurrentStatus();
            _currentUserDisplayName = status.UserDisplayName;
            CanRunFpsSetupFix = !status.IsReadyForEtwFps;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFpsSetupReady)));
            FpsSetupStatusText = status.Summary;
            FpsSetupDetailsText =
                $"User: {status.UserDisplayName}\n" +
                $"Group: {status.GroupDisplayName}\n" +
                $"Current process elevated: {status.IsCurrentProcessElevated}\n" +
                $"User in group: {status.IsUserInPerformanceLogUsers}\n" +
                $"ETW FPS ready: {status.IsReadyForEtwFps}\n\n" +
                $"{status.Guidance}\n\n" +
                $"Suggested command:\n{status.SuggestedAddCommand}";
        }

        private void AttachMainViewModel()
        {
            var next = Window.GetWindow(this)?.DataContext as MainViewModel;
            if (ReferenceEquals(_mainViewModel, next))
            {
                return;
            }

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

        private void RefreshLocalizedText()
        {
            var language = _mainViewModel?.Language;
            if (string.IsNullOrWhiteSpace(language))
            {
                language = DefaultLanguage;
            }

            if (string.Equals(language, "da", System.StringComparison.OrdinalIgnoreCase))
            {
                PageTitleText = "Gaming overlay";
                PageIntroText = "Dedikeret indstillingsside til gaming overlayet. Hold den adskilt fra de normale launcher-indstillinger.";
                OverlayTransparentBackgroundTitleText = "Transparent baggrund";
                OverlayTransparentBackgroundDescriptionText = "Gør gaming overlayets baggrund gennemsigtig, så kun tekst og indikatorer vises oven på spillet.";
                OverlayAutoTransparentTitleText = "Kun gennemsigtig under spil";
                OverlayAutoTransparentDescriptionText = "Gør baggrunden gennemsigtig kun mens et spil kører. Når spillet lukkes, kommer baggrunden automatisk tilbage.";
                OverlayTextColorTitleText = "Tekstfarve";
                OverlayCustomColorButtonText = "Brugerdefineret farve...";
                OverlayScaleTitleText = "Overlay størrelse";
                FpsSetupTitleText = "FPS setup status";
                FpsSetupIntroText = "IconGrid FPS via ETW afhænger af, at den aktuelle Windows-bruger har den rigtige tracing-adgang. Denne sektion tjekker det kendte krav om Brugere af ydelseslog.";
                RefreshFpsSetupButtonText = "Opdater FPS setup-status";
                RunFpsSetupFixButtonText = "Kør FPS setup-fix (Admin)";
                RunFpsSetupFixHelpText = "Kun nødvendig hvis den aktuelle bruger mangler medlemskab af Brugere af ydelseslog.";
                TechnicalDetailsTitleText = "Tekniske detaljer";
                TechnicalDetailsIntroText = "Vis den tekniske ETW-status og den foreslåede kommando.";
                ReadyBadgeText = "Klar";
                ResolutionDefaultsTitleText = "Standard scale per opløsning";
                ResolutionDefaultsIntroText = "Vælg den standard overlay-scale IconGrid bruger, når skærmen skifter til hver opløsning. Juster sliders her, eller træk i overlayets egen slider — begge steder gemmer som standard.";
                OverlayPositionTitleText = "Standard placering";
                OverlayPositionIntroText = "Vælg hvor gaming overlayet skal placeres på skærmen. Ved opløsningsskift (spil start/luk) springer overlayet tilbage til denne placering. 'Brugerdefineret' beholder den position du selv trækker overlayet til.";
                OverlayPositionPresetItems = new List<KeyValuePair<string, string>>
                {
                    new("TopLeft", "Top venstre"),
                    new("TopCenter", "Top midt"),
                    new("TopRight", "Top højre"),
                    new("BottomLeft", "Bund venstre"),
                    new("BottomCenter", "Bund midt"),
                    new("BottomRight", "Bund højre"),
                    new("Custom", "Brugerdefineret")
                };
                GameAutoBehaviorTitleText = "Automatisk under spil";
                GameAutoBehaviorIntroText = "Vælg hvad IconGrid gør med launcher'n og gaming overlayet, når et spil startes fra launcher'n.";
                GameAutoShowTitleText = "Vis gaming overlay automatisk ved spilstart";
                GameAutoShowIntroText = "Gaming overlayet åbner automatisk og placeres ved din valgte standardplacering, når et spil startes.";
                GameAutoCloseTitleText = "Luk gaming overlay når spillet lukkes";
                GameAutoCloseIntroText = "Gaming overlayet lukkes automatisk, når spillet afsluttes.";
                RestoreLauncherTitleText = "Genåbn launcher når overlayet lukkes";
                RestoreLauncherIntroText = "Launcher'n åbnes igen, hvis den blev skjult eller minimeret for spillet, når gaming overlayet lukkes.";
                GameLauncherBehaviorTitleText = "Launcher-adfærd under spil";
                GameLauncherBehaviorIntroText = "Vælg hvad der sker med launcher-vinduet, mens et spil kører.";
                GameLauncherBehaviorItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "Gør intet"),
                    new("1", "Auto-skjul (glider ned)"),
                    new("2", "Minimer til proceslinjen")
                };
            }
            else
            {
                PageTitleText = "Gaming overlay";
                PageIntroText = "Dedicated settings page for the gaming overlay. Keep this separate from the standard launcher settings.";
                OverlayTransparentBackgroundTitleText = "Transparent background";
                OverlayTransparentBackgroundDescriptionText = "Make the gaming overlay background transparent so only text and indicators show on top of the game.";
                OverlayAutoTransparentTitleText = "Transparent while in game";
                OverlayAutoTransparentDescriptionText = "Only make the background transparent while a game is running. When the game closes, the background automatically returns.";
                OverlayTextColorTitleText = "Text color";
                OverlayCustomColorButtonText = "Custom color...";
                OverlayScaleTitleText = "Overlay scale";
                FpsSetupTitleText = "FPS setup status";
                FpsSetupIntroText = "IconGrid FPS via ETW depends on the current Windows user having the right tracing access. This section checks the known Performance Log Users requirement.";
                RefreshFpsSetupButtonText = "Refresh FPS setup status";
                RunFpsSetupFixButtonText = "Run FPS setup fix (Admin)";
                RunFpsSetupFixHelpText = "Only needed when the current user is missing Performance Log Users membership.";
                TechnicalDetailsTitleText = "Technical details";
                TechnicalDetailsIntroText = "Show the technical ETW status and the suggested command.";
                ReadyBadgeText = "Ready";
                ResolutionDefaultsTitleText = "Default scale per resolution";
                ResolutionDefaultsIntroText = "Choose the default overlay scale IconGrid uses when the display switches to each resolution. Adjust any slider here, or drag the overlay's own slider — both save as the default.";
                OverlayPositionTitleText = "Default position";
                OverlayPositionIntroText = "Choose where the gaming overlay sits on screen. When the display resolution changes (game start/exit), the overlay snaps back to this placement. 'Custom' keeps the position you drag the overlay to.";
                OverlayPositionPresetItems = new List<KeyValuePair<string, string>>
                {
                    new("TopLeft", "Top left"),
                    new("TopCenter", "Top center"),
                    new("TopRight", "Top right"),
                    new("BottomLeft", "Bottom left"),
                    new("BottomCenter", "Bottom center"),
                    new("BottomRight", "Bottom right"),
                    new("Custom", "Custom")
                };
                GameAutoBehaviorTitleText = "Automatic while in game";
                GameAutoBehaviorIntroText = "Choose what IconGrid does with the launcher and gaming overlay when a game starts from the launcher.";
                GameAutoShowTitleText = "Show gaming overlay automatically on game start";
                GameAutoShowIntroText = "The gaming overlay opens automatically at your chosen default position when a game starts.";
                GameAutoCloseTitleText = "Close gaming overlay when the game exits";
                GameAutoCloseIntroText = "The gaming overlay closes automatically when the game ends.";
                RestoreLauncherTitleText = "Reopen launcher when the overlay closes";
                RestoreLauncherIntroText = "The launcher is restored if it was hidden or minimized for the game when the gaming overlay closes.";
                GameLauncherBehaviorTitleText = "Launcher behavior while in game";
                GameLauncherBehaviorIntroText = "Choose what happens to the launcher window while a game is running.";
                GameLauncherBehaviorItems = new List<KeyValuePair<string, string>>
                {
                    new("0", "Do nothing"),
                    new("1", "Auto-hide (slide down)"),
                    new("2", "Minimize to taskbar")
                };
            }
        }

        private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            if (string.Equals(field, value, System.StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
