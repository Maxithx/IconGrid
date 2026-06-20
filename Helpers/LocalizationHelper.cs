using System.Collections.Generic;

namespace IconGrid.Helpers;

public static class LocalizationHelper
{
    private static readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["en"] = new()
        {
            ["SettingsTitle"] = "Settings",
            ["IconsPerRow"] = "Icons per row",
            ["IconRowSpacing"] = "Icon row spacing",
            ["LastRowPadding"] = "Bottom padding (last row)",
            ["IconSize"] = "Icon size",
            ["UiScale"] = "UI size",
            ["AlwaysOnTop"] = "Always on top",
            ["FloatingIconTopmost"] = "Floating icon always on top",
            ["ShowScrollButtons"] = "Show scroll buttons",
            ["ShowDesktopIcon"] = "Show desktop icon",
            ["StartWithWindows"] = "Start with Windows",
            ["DevOverlayLabel"] = "Show developer overlays",
            ["Language"] = "Language",
            ["ResetDefaults"] = "Reset to defaults",
            ["SettingsPlaceholder"] = "Settings page placeholder.\nHere we will add real options later.",
            ["AddShortcut"] = "Add shortcut...",
            ["AddPowerShellShortcut"] = "Add PowerShell shortcut",
            ["Custom"] = "Custom...",
            ["AddWindowsShortcut"] = "Add Windows shortcut",
            ["AddAll"] = "Add all",
            ["ChangeIcon"] = "Change icon",
            ["WindowsIcons"] = "Windows icons (modern)",
            ["ClearCategory"] = "Clear category",
            ["ClearCategoryConfirm"] = "Are you sure you want to remove all icons in this category?",
            ["Open"] = "Open",
            ["RunAsAdmin"] = "Run as administrator",
            ["OpenFileLocation"] = "Open file location",
            ["CopyPath"] = "Copy path",
            ["ResetIcon"] = "Reset icon",
            ["Rename"] = "Rename",
            ["Remove"] = "Remove",
            ["EnableSlideUpAnimation"] = "Enable slide-up animation",
            ["WindowAnimationSpeed"] = "Window animation speed",
            ["EnableIconScroll"] = "Enable icon scroll",
            ["PawnIoMissingMessage"] = "CPU temperatures require PawnIO.",
            ["PawnIoDownloadLink"] = "Download PawnIO",
            ["MoreLayouts"] = "Layouts",
            ["MoreSettings"] = "Settings",
            ["MoreHelp"] = "Help",
            ["MoreAbout"] = "About",
            ["TabGames"] = "Games",
            ["TabSoftware"] = "Software",
            ["TabDevelop"] = "Develop",
            ["MonitorNetworkLabel"] = "Net",
            ["MonitorDownloadLabel"] = "↓ Download",
            ["MonitorUploadLabel"] = "↑ Upload",
            ["MonitorCpuLabel"] = "CPU",
            ["MonitorGpuLabel"] = "GPU"
        },
        ["da"] = new()
        {
            ["SettingsTitle"] = "Indstillinger",
            ["IconsPerRow"] = "Ikoner pr. række",
            ["IconRowSpacing"] = "Mellemrum mellem ikonrækker",
            ["LastRowPadding"] = "Luft under sidste række",
            ["IconSize"] = "Ikonstørrelse",
            ["UiScale"] = "UI-størrelse",
            ["AlwaysOnTop"] = "Altid øverst",
            ["FloatingIconTopmost"] = "Flydende ikon altid øverst",
            ["ShowScrollButtons"] = "Vis rulleknapper",
            ["ShowDesktopIcon"] = "Vis skrivebordsikon",
            ["StartWithWindows"] = "Start med Windows",
            ["DevOverlayLabel"] = "Vis udvikler overlay",
            ["Language"] = "Sprog",
            ["ResetDefaults"] = "Nulstil til standarder",
            ["SettingsPlaceholder"] = "Indstillingsside placeholder.\nHer tilføjer vi rigtige valg senere.",
            ["AddShortcut"] = "Tilføj genvej...",
            ["AddPowerShellShortcut"] = "Tilføj PowerShell-genvej",
            ["Custom"] = "Brugerdefineret...",
            ["AddWindowsShortcut"] = "Tilføj Windows-genvej",
            ["AddAll"] = "Tilføj alle",
            ["ChangeIcon"] = "Skift ikon",
            ["WindowsIcons"] = "Windows-ikoner (moderne)",
            ["ClearCategory"] = "Ryd kategori",
            ["ClearCategoryConfirm"] = "Vil du fjerne alle ikoner i denne kategori?",
            ["Open"] = "Åbn",
            ["RunAsAdmin"] = "Kør som administrator",
            ["OpenFileLocation"] = "Åbn filplacering",
            ["CopyPath"] = "Kopier sti",
            ["ResetIcon"] = "Nulstil ikon",
            ["Rename"] = "Omdøb",
            ["Remove"] = "Fjern",
            ["EnableSlideUpAnimation"] = "Aktiver slide-op animation",
            ["WindowAnimationSpeed"] = "Vindues animationshastighed",
            ["EnableIconScroll"] = "Aktiver scroll",
            ["PawnIoMissingMessage"] = "CPU-temperaturer kræver PawnIO.",
            ["PawnIoDownloadLink"] = "Hent PawnIO",
            ["MoreLayouts"] = "Layouts",
            ["MoreSettings"] = "Indstillinger",
            ["MoreHelp"] = "Hjælp",
            ["MoreAbout"] = "Om",
            ["TabGames"] = "Spil",
            ["TabSoftware"] = "Software",
            ["TabDevelop"] = "Udvikling",
            ["MonitorNetworkLabel"] = "Net",
            ["MonitorDownloadLabel"] = "↓ Hent",
            ["MonitorUploadLabel"] = "↑ Send",
            ["MonitorCpuLabel"] = "CPU",
            ["MonitorGpuLabel"] = "GPU"
        }
    };

    public static string Get(string language, string key)
    {
        if (string.IsNullOrWhiteSpace(language)) language = "en";
        if (_resources.TryGetValue(language.ToLowerInvariant(), out var dict) && dict.TryGetValue(key, out var value))
        {
            return value;
        }

        // fallback to en
        if (_resources["en"].TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }
}
