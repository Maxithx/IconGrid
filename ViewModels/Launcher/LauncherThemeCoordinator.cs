using System;
using IconGrid.Helpers.Settings;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherThemeCoordinator
    {
        public event EventHandler<ThemeSnapshot>? ThemeChanged;

        public LauncherThemeCoordinator()
        {
            ThemeHelper.ThemeChanged += ThemeHelper_ThemeChanged;
        }

        public ThemeSnapshot GetCurrentTheme()
        {
            return ThemeHelper.GetTheme();
        }

        private void ThemeHelper_ThemeChanged(object? sender, ThemeSnapshot e)
        {
            ThemeChanged?.Invoke(this, e);
        }
    }
}
