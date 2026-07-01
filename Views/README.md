# Views layout notes

## Structure

- `Views/Launcher/MainWindow.xaml` is the launcher shell and owns the transition between floating icon mode and the expanded dashboard.
- `Views/Settings/SettingsWindow.xaml` is the settings shell and hosts the shared sidebar navigation.
- `Views/Settings/Pages/` contains the settings content pages loaded into `SettingsWindow`.
- `Views/Settings/Dialogs/` contains settings-specific dialogs such as `PawnIoWarningWindow`.
- `Views/StartsideStyles.xaml` and `Views/TemplateGuidelines.xaml` remain shared view resources used across the settings pages.

## TemplatePage

- `Views/Settings/Pages/TemplatePage.xaml` provides the shared hero-plus-card shell used by the settings pages.
- `HeroContent` renders the top section inside a `StartsideSectionCardStyle` card.
- `CardsContent` renders the stacked follow-up cards and should keep using `StartsideSectionCardStyle` for consistent spacing.
- `TemplateContentWidthConverter` keeps the content column centered, applies gutters, and constrains the max width so `StartsidePage`, `LayoutPage`, and future pages stay visually aligned.

## Startside styles

- `Views/StartsideStyles.xaml` defines the shared brushes and styles used by the settings pages, including `StartsideSectionCardStyle`, `ToggleSwitchStyle`, and `ModernSliderStyle`.
- Reuse these styles instead of duplicating margins or card chrome inside individual pages.

## Settings shell

- `Views/Settings/SettingsWindow.xaml` owns the sidebar buttons and still swaps pages via `ShowPage(new YourPage(), yourNavButton)`.
- `Views/Settings/SettingsWindowCoordinator.cs` now owns launcher-side settings window orchestration: create, reuse, placement beside the main window, activation, and cleanup.
- `Views/Launcher/MainWindow.xaml.cs` should only trigger the coordinator instead of re-implementing settings-window lifecycle logic.
