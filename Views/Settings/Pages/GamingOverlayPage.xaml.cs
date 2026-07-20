using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;
using IconGrid.ViewModels;

namespace IconGrid.Views
{
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
        private string _overlayScaleTitleText = string.Empty;
        private string _fpsResponsivenessTitleText = string.Empty;
        private string _fpsResponsivenessIntroText = string.Empty;
        private string _fpsSetupTitleText = string.Empty;
        private string _fpsSetupIntroText = string.Empty;
        private string _refreshFpsSetupButtonText = string.Empty;
        private string _runFpsSetupFixButtonText = string.Empty;
        private string _runFpsSetupFixHelpText = string.Empty;
        private string _technicalDetailsTitleText = string.Empty;
        private string _technicalDetailsIntroText = string.Empty;
        private string _plannedOverlaySettingsTitleText = string.Empty;
        private string _plannedOverlaySettingsIntroText = string.Empty;
        private string _readyBadgeText = "Ready";

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

        public string OverlayScaleTitleText
        {
            get => _overlayScaleTitleText;
            private set => SetField(ref _overlayScaleTitleText, value);
        }

        public string FpsResponsivenessTitleText
        {
            get => _fpsResponsivenessTitleText;
            private set => SetField(ref _fpsResponsivenessTitleText, value);
        }

        public string FpsResponsivenessIntroText
        {
            get => _fpsResponsivenessIntroText;
            private set => SetField(ref _fpsResponsivenessIntroText, value);
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

        public string PlannedOverlaySettingsTitleText
        {
            get => _plannedOverlaySettingsTitleText;
            private set => SetField(ref _plannedOverlaySettingsTitleText, value);
        }

        public string PlannedOverlaySettingsIntroText
        {
            get => _plannedOverlaySettingsIntroText;
            private set => SetField(ref _plannedOverlaySettingsIntroText, value);
        }

        public string ReadyBadgeText
        {
            get => _readyBadgeText;
            private set => SetField(ref _readyBadgeText, value);
        }

        private void GamingOverlayPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AttachMainViewModel();
            RefreshLocalizedText();
            RefreshFpsSetupStatus();
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
                OverlayScaleTitleText = "Overlay storrelse";
                FpsResponsivenessTitleText = "FPS opdateringshastighed";
                FpsResponsivenessIntroText = "Juster hvor hurtigt FPS-visningen reagerer paa nye ETW-data. Hojejere vaerdi foeles mere realtime, lavere vaerdi giver roligere tal.";
                FpsSetupTitleText = "FPS setup status";
                FpsSetupIntroText = "IconGrid FPS via ETW afhaenger af, at den aktuelle Windows-bruger har den rigtige tracing-adgang. Denne sektion tjekker det kendte krav om Brugere af ydelseslog.";
                RefreshFpsSetupButtonText = "Opdater FPS setup-status";
                RunFpsSetupFixButtonText = "Koer FPS setup-fix (Admin)";
                RunFpsSetupFixHelpText = "Kun noedvendig hvis den aktuelle bruger mangler medlemskab af Brugere af ydelseslog.";
                TechnicalDetailsTitleText = "Tekniske detaljer";
                TechnicalDetailsIntroText = "Vis den tekniske ETW-status og den foreslaaede kommando.";
                PlannedOverlaySettingsTitleText = "Planlagte overlay-indstillinger";
                PlannedOverlaySettingsIntroText = "Placeholder-side til overlay-specifikke valg.";
                ReadyBadgeText = "Klar";
            }
            else
            {
                PageTitleText = "Gaming overlay";
                PageIntroText = "Dedicated settings page for the gaming overlay. Keep this separate from the standard launcher settings.";
                OverlayScaleTitleText = "Overlay scale";
                FpsResponsivenessTitleText = "FPS update responsiveness";
                FpsResponsivenessIntroText = "Adjust how quickly the FPS display reacts to new ETW data. Higher values feel more realtime, lower values smooth the numbers more.";
                FpsSetupTitleText = "FPS setup status";
                FpsSetupIntroText = "IconGrid FPS via ETW depends on the current Windows user having the right tracing access. This section checks the known Performance Log Users requirement.";
                RefreshFpsSetupButtonText = "Refresh FPS setup status";
                RunFpsSetupFixButtonText = "Run FPS setup fix (Admin)";
                RunFpsSetupFixHelpText = "Only needed when the current user is missing Performance Log Users membership.";
                TechnicalDetailsTitleText = "Technical details";
                TechnicalDetailsIntroText = "Show the technical ETW status and the suggested command.";
                PlannedOverlaySettingsTitleText = "Planned overlay settings";
                PlannedOverlaySettingsIntroText = "Placeholder page for overlay-specific options.";
                ReadyBadgeText = "Ready";
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
    }
}
