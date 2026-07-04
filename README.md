# IconGrid

IconGrid is a Windows launcher and desktop overlay built with WPF and MVVM. It combines shortcut organization, layout presets, settings pages, and live hardware telemetry in a launcher that can collapse into a floating desktop button.

## User flow

### Floating icon

![IconGrid Floating icon Screenshot](Assets/git-img/Floatingicon.png)

- IconGrid starts in floating-icon mode.
- Left-click expands the main launcher window.
- Right-click opens the localized exit menu for fully closing the app.

### Main launcher

- `Views/Launcher/MainWindow.xaml` is the main launcher shell.
- The launcher contains the logo area, live monitor strip, tab bar, shortcut grid, and the `Indstillinger` entry point.
- Closing the launcher hides it back to floating-icon mode instead of terminating the process.

![IconGrid Screenshot](Assets/git-img/IconGrid.png)

## IconGrid collapsed state
![IconGrid collapsed state Screenshot](Assets/git-img/IconGrid-collapsed.png)

### Settings window

![IconGrid Settings Screenshot](Assets/git-img/Settings.png)

- `Views/Settings/SettingsWindow.xaml` is the dedicated settings shell.
- The window hosts sidebar navigation and loads modular pages from `Views/Settings/Pages/`.
- `Views/Settings/SettingsWindowCoordinator.cs` now handles launcher-side opening, reuse, placement, and shutdown of the settings window so `MainWindow` no longer owns that lifecycle directly.

## Settings pages

- `StartsidePage.xaml`: startup, topmost behavior, UI scale, general launcher options, and the built-in `Dansk` / `English` language switcher.
- `GenvejsIkonerPage.xaml`: launcher shortcut and icon settings.
- `LayoutPage.xaml`: layout preset and saved-layout configuration.
- `HardwarePage.xaml`: hardware diagnostics and related status.
- `HjaelpPage.xaml`: help and troubleshooting content.
- `AboutPage.xaml`: version and project information.

## Architecture

### ViewModels

- `ViewModels/MainViewModel.cs` remains the root launcher view model.
- `ViewModels/Launcher/` contains launcher-specific state and managers such as tabs, items, layout, theme, overlay, localization, and item launch handling.
- `ViewModels/Settings/` contains config and persistence helpers used to translate and save launcher settings state.

### Views

- `Views/Launcher/` contains the launcher shell.
- `Views/Settings/` contains the settings shell, its pages, dialogs, and coordination helpers.
- `Views/StartsideStyles.xaml` and `Views/TemplateGuidelines.xaml` are shared view resources.

### Controls

- `Controls/Launcher/` contains launcher UI modules such as `LauncherTopBar`, `LauncherTabsBar`, and `LauncherGrid`.
- `Controls/Floating/` contains `FloatingIconButton`.
- `Controls/Common/` contains shared controls such as `LauncherLogo` and `SliderRow`.

### Helpers

- `Helpers/Launcher/` contains launcher-facing infrastructure such as `FloatingIconController`, `SystemMonitor`, `DynamicIconHelper`, and shortcut/icon helpers.
- `Helpers/Settings/` contains config, localization, startup, PawnIO, and theme helpers used by the settings/configuration flow.
- `Helpers/Hardware/` contains hardware-monitor integration types.
- `Helpers/Converters/` contains shared WPF value converters.
- `Helpers/Common/` contains shared infrastructure such as `RelayCommand` and `DevInspector`.

## Key features

- Drag-and-drop shortcut management in the launcher grid.
- Live CPU, GPU, ping, and network telemetry in the launcher top bar.
- Layout presets and saved desktop layout restoration.
- Theme synchronization with Windows accent and dark/light mode.
- Built-in developer overlay via `DevInspector`.

## Security & Privilege Separation

To ensure both maximum security and compatibility with Windows User Interface Privilege Isolation (UIPI), IconGrid utilizes a multi-process architecture:

1. **Elevated Worker Process (Admin):** Runs with administrator privileges exclusively to interface with `LibreHardwareMonitorLib` and fetch low-level hardware telemetry (CPU/GPU temperatures, clocks).
2. **Standard Non-Elevated UI Process:** The main launcher grid, floating desktop icon, and shortcut management run as a standard user. 

### Benefits of this Architecture:
- **Flawless Drag-and-Drop:** Because the UI runs without admin rights, users can freely drag and drop shortcuts from Windows Explorer into the launcher grid without being blocked by UIPI.
- **No Admin Contamination:** Any game, browser, or application launched from within IconGrid correctly inherits standard user privileges, preventing untrusted applications from gaining elevated system access.

## Hardware monitoring

IconGrid uses `LibreHardwareMonitorLib` for telemetry collection. Hardware access may require administrator privileges at startup because PawnIO and hardware driver access are part of the monitoring flow.

## Developer notes

- The project targets Windows and is organized around small WPF shells plus modular controls/state helpers.
- After the current refactor, launcher UI, settings UI, helpers, and feature-specific view-model code are grouped by responsibility rather than staying flat in a few root folders.
- See [ARCHITECTURE_RULES.md](/E:/IconGrid/ARCHITECTURE_RULES.md) for the guardrails we use to keep `MainWindow` and `MainViewModel` from growing into feature dumps again.
- See [GIT_WORKFLOW.md](/E:/IconGrid/GIT_WORKFLOW.md) for the recommended branch and merge workflow after the completed `refactor-mainwindow` phase.

## Versioning

IconGrid uses Semantic Versioning with beta builds during active refactor and feature work. See `VERSIONING.md` for the release flow.