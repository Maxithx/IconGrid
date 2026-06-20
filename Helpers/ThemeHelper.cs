using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;

namespace IconGrid.Helpers
{
    public record ThemeSnapshot(bool IsLightTheme, MediaColor AccentColor);

    public static class ThemeHelper
    {
        public static event EventHandler<ThemeSnapshot>? ThemeChanged;

        private static ThemeSnapshot _last = GetTheme();
        private static readonly object? UiSettingsInstance = InitializeUiSettings();
        private static readonly MethodInfo? UiSettingsGetColorValueMethod = UiSettingsInstance?.GetType().GetMethod("GetColorValue");
        private static readonly object UiColorTypeAccentValue = GetUiColorTypeAccentValue();

        static ThemeHelper()
        {
            SystemEvents.UserPreferenceChanged += (_, __) => CheckForChanges();
        }

        public static ThemeSnapshot GetTheme()
        {
            return new ThemeSnapshot(
                IsLightTheme: ReadIsLightTheme(),
                AccentColor: ReadAccentColor());
        }

        private static void CheckForChanges()
        {
            var current = GetTheme();
            if (current.IsLightTheme != _last.IsLightTheme || current.AccentColor != _last.AccentColor)
            {
                _last = current;
                ThemeChanged?.Invoke(null, current);
            }
        }

        public static void ForceRefresh() => CheckForChanges();

        private static bool ReadIsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int i)
                    return i != 0;
            }
            catch
            {
                // ignore and fallback
            }

            return true;
        }

        private static MediaColor ReadAccentColor()
        {
            var uiAccent = TryReadUiSettingsAccent();
            if (uiAccent.HasValue)
            {
                return uiAccent.Value;
            }

            // Preferred: ask DWM for the current colorization color (matches accent used by the shell).
            try
            {
                if (DwmGetColorizationColor(out uint colorizationColor, out _ ) == 0)
                {
                    byte a = (byte)(colorizationColor >> 24);
                    byte r = (byte)((colorizationColor >> 16) & 0xFF);
                    byte g = (byte)((colorizationColor >> 8) & 0xFF);
                    byte b = (byte)(colorizationColor & 0xFF);
                    if (a == 0) a = 255;
                    return MediaColor.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // ignore and try other sources
            }

            // Fallback: Explorer accent palette (first entry is active accent) when available.
            var explorerAccent = TryReadExplorerAccent();
            if (explorerAccent.HasValue)
                return explorerAccent.Value;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                var value = key?.GetValue("AccentColor");
                if (value is int i)
                {
                    // Registry stores accent as AABBGGRR (BGR order). Swap to ARGB for WPF.
                    uint color = unchecked((uint)i);
                    byte a = (byte)(color >> 24);
                    byte b = (byte)((color >> 16) & 0xFF);
                    byte g = (byte)((color >> 8) & 0xFF);
                    byte r = (byte)(color & 0xFF);
                    if (a == 0) a = 255;
                    return MediaColor.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // ignore and fallback
            }

            // Default accent (Windows 11 blue)
            return MediaColor.FromRgb(37, 99, 235);
        }

        private static MediaColor? TryReadExplorerAccent()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 4)
                {
                    // AccentPalette is BGRA little-endian; first color is the active accent.
                    byte b = palette[0];
                    byte g = palette[1];
                    byte r = palette[2];
                    byte a = palette[3];
                    if (a == 0) a = 255;
                    return MediaColor.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // ignore and fall back
            }

            return null;
        }

        private static MediaColor? TryReadUiSettingsAccent()
        {
            var instance = UiSettingsInstance;
            var method = UiSettingsGetColorValueMethod;
            if (instance == null || method == null)
                return null;

            try
            {
                var colorValue = method.Invoke(instance, new[] { UiColorTypeAccentValue });
                if (colorValue == null)
                    return null;

                dynamic windowsColor = colorValue;
                return MediaColor.FromArgb((byte)windowsColor.A, (byte)windowsColor.R, (byte)windowsColor.G, (byte)windowsColor.B);
            }
            catch
            {
                return null;
            }
        }

        private static object? InitializeUiSettings()
        {
            try
            {
                var uiSettingsType = Type.GetType("Windows.UI.ViewManagement.UISettings, Windows, ContentType=WindowsRuntime");
                if (uiSettingsType == null)
                    return null;

                var instance = Activator.CreateInstance(uiSettingsType);
                if (instance == null)
                    return null;

                var eventInfo = uiSettingsType.GetEvent("ColorValuesChanged");
                if (eventInfo is { } info &&
                    typeof(ThemeHelper).GetMethod(nameof(UiSettings_ColorValuesChanged), BindingFlags.Static | BindingFlags.NonPublic) is MethodInfo methodInfo &&
                    info.EventHandlerType is Type handlerType)
                {
                    var handler = Delegate.CreateDelegate(handlerType, methodInfo);
                    info.AddEventHandler(instance, handler);
                }

                return instance;
            }
            catch
            {
                return null;
            }
        }

        private static object GetUiColorTypeAccentValue()
        {
            try
            {
                var type = Type.GetType("Windows.UI.ViewManagement.UIColorType, Windows, ContentType=WindowsRuntime");
                var field = type?.GetField("Accent");
                if (field != null)
                {
                    var value = field.GetValue(null);
                    if (value != null)
                        return value;
                }
            }
            catch
            {
            }

            return 0;
        }

        private static void UiSettings_ColorValuesChanged(object? sender, object? args)
        {
            CheckForChanges();
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);
    }
}
