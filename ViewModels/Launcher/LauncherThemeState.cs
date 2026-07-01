using IconGrid.Helpers;
using IconGrid.Models;
using WMedia = System.Windows.Media;

namespace IconGrid.ViewModels.Launcher
{
    public class LauncherThemeState
    {
        public bool IsLightTheme { get; set; } = true;
        public WMedia.Brush AccentBrush { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(37, 99, 235));
        public WMedia.Brush TopBarBackground { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(243, 243, 243));
        public WMedia.Brush TopBarForeground { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(15, 23, 42));
        public WMedia.Brush SettingsWindowBackground { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(244, 245, 247));
        public WMedia.Brush SettingsCardBackground { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(255, 255, 255));
        public WMedia.Brush SettingsCardBorderBrush { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromArgb(96, 15, 23, 42));
        public WMedia.Brush SettingsSubtextForeground { get; private set; } = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(63, 63, 70));
        public WMedia.Color SettingsShadowColor { get; private set; } = WMedia.Color.FromArgb(60, 0, 0, 0);

        public bool SetIsLightTheme(bool value)
        {
            if (IsLightTheme == value)
                return false;

            IsLightTheme = value;
            return true;
        }

        public void ApplyConfig(ConfigModel config)
        {
            IsLightTheme = config.IsLightTheme;
        }

        public void ApplyThemeSnapshot(ThemeSnapshot snapshot)
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
    }
}
