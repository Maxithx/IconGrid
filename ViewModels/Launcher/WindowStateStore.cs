using IconGrid.ViewModels.Settings;

namespace IconGrid.ViewModels.Launcher
{
    /// <summary>
    /// Owns window-position persistence for the main window, settings window, gaming overlay,
    /// and floating icon. Previously lived directly in MainViewModel.
    /// </summary>
    public class WindowStateStore
    {
        private double? _windowLeft;
        private double? _windowTop;
        private double? _settingsWindowLeft;
        private double? _settingsWindowTop;
        private double? _gamingOverlayWindowLeft;
        private double? _gamingOverlayWindowTop;
        private double? _floatingLeft;
        private double? _floatingTop;

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
        }

        public bool TryGetSavedGamingOverlayWindowPosition(out double left, out double top)
        {
            if (_gamingOverlayWindowLeft.HasValue && _gamingOverlayWindowTop.HasValue)
            {
                left = _gamingOverlayWindowLeft.Value;
                top = _gamingOverlayWindowTop.Value;
                return true;
            }

            left = 0;
            top = 0;
            return false;
        }

        public void SaveGamingOverlayWindowPosition(double left, double top)
        {
            _gamingOverlayWindowLeft = left;
            _gamingOverlayWindowTop = top;
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
        }

        public void ApplyConfig(double? windowLeft, double? windowTop, double? settingsWindowLeft, double? settingsWindowTop,
            double? gamingOverlayWindowLeft, double? gamingOverlayWindowTop, double? floatingLeft, double? floatingTop)
        {
            _windowLeft = windowLeft;
            _windowTop = windowTop;
            _settingsWindowLeft = settingsWindowLeft;
            _settingsWindowTop = settingsWindowTop;
            _gamingOverlayWindowLeft = gamingOverlayWindowLeft;
            _gamingOverlayWindowTop = gamingOverlayWindowTop;
            _floatingLeft = floatingLeft;
            _floatingTop = floatingTop;
        }

        public void ResetFloatingPosition()
        {
            _floatingLeft = null;
            _floatingTop = null;
        }

        public void ApplyToSettingsState(MainViewModelSettingsState state)
        {
            state.WindowLeft = _windowLeft;
            state.WindowTop = _windowTop;
            state.SettingsWindowLeft = _settingsWindowLeft;
            state.SettingsWindowTop = _settingsWindowTop;
            state.GamingOverlayWindowLeft = _gamingOverlayWindowLeft;
            state.GamingOverlayWindowTop = _gamingOverlayWindowTop;
            state.FloatingIconLeft = _floatingLeft;
            state.FloatingIconTop = _floatingTop;
        }
    }
}