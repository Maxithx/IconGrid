# IconGrid

IconGrid is a Windows launcher and desktop overlay built with WPF and MVVM. It combines shortcut organization, layout presets, settings pages, and live hardware telemetry in a launcher that can collapse into a floating desktop button.

## User flow

### Floating icon

![IconGrid Floating icon Screenshot](Assets/git-img/Floatingicon.png)

- IconGrid can start in floating-icon mode, or go directly into the full launcher if the new "start directly in launcher" option is enabled on the Startside page.
- Left-click expands the main launcher window.
- Right-click opens the localized exit menu for fully closing the app.

### Main launcher

- `Views/Launcher/MainWindow.xaml` is the main launcher shell.
- The launcher contains the logo area, live monitor strip, tab bar, shortcut grid, and the `Indstillinger` entry point.
- Closing the launcher hides it back to floating-icon mode instead of terminating the process.

![IconGrid Screenshot](Assets/git-img/IconGrid.png)

## IconGrid collapsed state Toggle UI View

IconGrid features a clean and simple way to toggle between full and collapsed views. If a category is selected (for example, Games), clicking it once will hide the icon area, collapsing the UI to only show the top bar and system monitors. Clicking the category again will smoothly slide the icon area back down into full view.

![IconGrid collapsed state Screenshot](Assets/git-img/IconGrid-collapsed.png)

### Settings window

![IconGrid Settings Screenshot](Assets/git-img/Settings.png)

- `Views/Settings/SettingsWindow.xaml` is the dedicated settings shell.
- The window hosts sidebar navigation and loads modular pages from `Views/Settings/Pages/`.
- `Views/Settings/SettingsWindowCoordinator.cs` now handles launcher-side opening, reuse, placement, and shutdown of the settings window so `MainWindow` no longer owns that lifecycle directly.

### Gaming overlay monitor

- `Views/Launcher/GamingOverlayWindow.xaml` is the compact always-on-top gaming overlay monitor.
- The overlay reuses the live telemetry stack for network, CPU, GPU, and FPS status presentation in a dedicated single-row shell.
- The gaming overlay is intentionally more game-focused than the main launcher monitor row, so `Download` / `Upload` are currently excluded from the in-game strip.
- `Views/GamingOverlayWindowCoordinator.cs` owns opening, reuse, placement, and shutdown of the overlay window.
- The overlay now remembers its last on-screen position and restores it on the next open or after app restart.
- The overlay settings button opens a dedicated inline settings row with a mini overlay-scale slider and a direct link into the full Gaming Overlay settings page.

## Settings pages

- `StartsidePage.xaml`: startup, topmost behavior, UI scale, general launcher options, and the built-in `Dansk` / `English` language switcher.
- `GenvejsIkonerPage.xaml`: launcher shortcut and icon settings.
- `LayoutPage.xaml`: layout preset and saved-layout configuration.
- `HardwarePage.xaml`: hardware diagnostics and related status.
- `HjaelpPage.xaml`: help and troubleshooting content.
- `AboutPage.xaml`: version and project information.
- `GamingOverlayPage.xaml`: dedicated gaming overlay settings, including overlay scale and ETW/FPS setup status.

## Architecture

### Models

- `Models/` contains the shared data contracts and enums used across the app, including launcher config, layout data, and startup mode definitions.

### ViewModels

- `ViewModels/MainViewModel.cs` remains the root launcher view model.
- `ViewModels/Launcher/` contains launcher-specific state and managers such as tabs, items, layout, theme, overlay, localization, and item launch handling.
- `ViewModels/Settings/` contains config and persistence helpers used to translate and save launcher settings state.
- `ViewModels/Settings/` also contains the smaller state objects that keep `MainViewModel` from turning into a feature dump.

### Views

- `Views/Launcher/` contains the launcher shell.
- `Views/Launcher/GamingOverlayWindow.xaml` contains the dedicated gaming overlay monitor shell and inline settings row.
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
- `Helpers/Hardware/` now also contains the ETW/FPS pipeline, native FPS agent launch flow, shared-memory live FPS handoff, probe helpers, and FPS smoothing/fallback logic.
- `Helpers/Converters/` contains shared WPF value converters.
- `Helpers/Common/` contains shared infrastructure such as `RelayCommand` and `DevInspector`.

## Key features

- Drag-and-drop shortcut management in the launcher grid.
- Live CPU, GPU, ping, and network telemetry in the launcher top bar.
- A dedicated gaming overlay monitor with saved window position, inline quick settings, dedicated overlay settings page, dividers for readability, and live FPS via ETW.
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

## FPS capture and ETW access

- IconGrid now captures FPS through a passive ETW-based pipeline.
- The current implementation uses a separate native FPS worker:
  - `Native/FpsAgent/src/main.cpp`
  - launched via `Helpers/Hardware/NativeFpsAgentRunner.cs`
- The ETW path is passive only:
  - no injection
  - no DLL detours
  - no graphics API hooking
- Relevant Windows graphics providers are:
  - `Microsoft-Windows-DxgKrnl`
  - `Microsoft-Windows-DXGI`
  - `Microsoft-Windows-D3D9`

### Important Windows requirement

- On systems where ETW graphics access is restricted, the current Windows user may need to be added to:
  - `Performance Log Users`
  - Danish Windows name: `Brugere af ydelseslog`
- After adding the user, a sign-out/in or reboot may be required before FPS capture starts working.
- The Gaming overlay settings page now includes an FPS setup status section to help confirm whether this requirement is already satisfied.

### Current FPS pipeline

- game renders frames
- native FPS worker captures present events through ETW
- native FPS worker publishes live FPS through:
  - a shared-memory live channel for the hottest path
  - `native-fps-state.json` as fallback / diagnostics
- elevated hardware agent reads native FPS state
- `FpsMeter` still maintains:
  - a direct `live` FPS path
  - a smoothed `trend` FPS path for fallback / diagnostics
- launcher / gaming overlay prefers the shared-memory live FPS path
- `fps-state.json` remains as a compatibility / fallback handoff for broader monitor state
- overlay shows a single live FPS number tuned for faster response

### Current limitation

- ETW capture is now working, but the remaining work is still the last bit of display responsiveness for very small FPS changes.
- Shared memory removed a meaningful chunk of handoff latency, but an in-game counter can still feel slightly closer to the render loop.
- Near-term improvement work is expected to focus on:
  - validating the new shared-memory live path across more games
  - catching tiny FPS dips/spikes even earlier
  - optional frametime display
  - deciding whether the remaining live FPS fallback path should be simplified further

## Developer notes

- The project targets Windows and is organized around small WPF shells plus modular controls/state helpers.
- After the current refactor, launcher UI, settings UI, helpers, and feature-specific view-model code are grouped by responsibility rather than staying flat in a few root folders.
- See `ARCHITECTURE_RULES.md` for the guardrails we use to keep `MainWindow` and `MainViewModel` from growing into feature dumps again.
- Local workflow/planning notes such as Git workflow reminders are intentionally kept under `.local-state/` and are not part of the GitHub-facing repo documentation.

## Local data folder

IconGrid stores user data in `%APPDATA%\IconGrid`, which on a standard Windows profile resolves to `C:\Users\<user>\AppData\Roaming\IconGrid`.

Current files and folders in that directory:

- `config.json`: launcher settings, layout settings, window positions, language, theme, startup toggle, and saved layout data.
- `items.json`: launcher shortcuts grouped by category/tab. This is installation-specific and may not be portable to a new PC if shortcut paths change.
- `monitor-state.json`: temporary live state used by the hardware-monitor row to show current CPU/GPU readings.
- `fps-state.json`: compatibility/fallback live state used by the launcher and gaming overlay when the direct live path is unavailable.
- `native-fps-state.json`: raw native FPS worker state used by the elevated monitor path and diagnostics.
- `trace.log`: app trace output for startup and runtime diagnostics.
- `error.log`: fatal error log written after unhandled startup/runtime failures.
- `IconPack\`: optional cached icon pack assets used by the launcher. This folder can be empty if no pack has been migrated or installed yet.

The data folder is created automatically on startup by `ConfigManager`.
`config.json` is the main file for restoring a user's IconGrid setup after reinstall or migration. `items.json` can be useful on the same machine, but should be treated as machine-specific rather than guaranteed portable.

## Hardware monitor startup

IconGrid uses a split startup model:

- Windows startup launches the launcher UI silently through Task Scheduler.
- The launcher UI starts with `--startup-launch` when Windows logs in.
- The hardware monitor runs as a separate scheduled task named `IconGrid Monitor` with `--monitor-agent`.
- The monitor task is elevated so CPU/GPU telemetry can still work.
- The FPS worker is separate from the desktop-facing UI and is launched through the monitor-side flow when gaming overlay FPS is needed.
- Manual launcher starts still keep the normal UAC behavior for the monitor path.
- If "start directly in launcher" is enabled, the launcher opens straight into the full UI and skips the floating icon on Windows sign-in, logon, and restart.
- The startup-mode selector was removed from `StartsidePage.xaml` after the Task Scheduler vs. legacy test phase.
- If Windows startup ever launches a duplicate or elevated IconGrid instance again, the first thing to check is Task Scheduler for stale `IconGrid` or `IconGrid Monitor` tasks.

This keeps the UI non-elevated while preserving hardware telemetry access and avoiding the extra `Conhost` / `schtasks` startup chain.

## Versioning

IconGrid uses Semantic Versioning with beta builds during active refactor and feature work. See `VERSIONING.md` for the release flow.
