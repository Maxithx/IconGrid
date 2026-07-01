namespace IconGrid.ViewModels.Launcher
{
    public class LauncherOverlayState
    {
        public bool IsSettingsOpen { get; private set; }

        public bool IsLayoutsOpen { get; private set; }

        public bool IsHelpOpen { get; private set; }

        public bool IsOverlayOpen => IsSettingsOpen || IsLayoutsOpen || IsHelpOpen;

        public bool SetSettingsOpen(bool value, out bool layoutsChanged, out bool helpChanged)
        {
            layoutsChanged = false;
            helpChanged = false;

            if (IsSettingsOpen == value)
                return false;

            IsSettingsOpen = value;
            if (!value)
                return true;

            layoutsChanged = IsLayoutsOpen;
            helpChanged = IsHelpOpen;
            IsLayoutsOpen = false;
            IsHelpOpen = false;
            return true;
        }

        public bool SetLayoutsOpen(bool value, out bool settingsChanged, out bool helpChanged)
        {
            settingsChanged = false;
            helpChanged = false;

            if (IsLayoutsOpen == value)
                return false;

            IsLayoutsOpen = value;
            if (!value)
                return true;

            settingsChanged = IsSettingsOpen;
            helpChanged = IsHelpOpen;
            IsSettingsOpen = false;
            IsHelpOpen = false;
            return true;
        }

        public bool SetHelpOpen(bool value, out bool settingsChanged, out bool layoutsChanged)
        {
            settingsChanged = false;
            layoutsChanged = false;

            if (IsHelpOpen == value)
                return false;

            IsHelpOpen = value;
            if (!value)
                return true;

            settingsChanged = IsSettingsOpen;
            layoutsChanged = IsLayoutsOpen;
            IsSettingsOpen = false;
            IsLayoutsOpen = false;
            return true;
        }
    }
}
