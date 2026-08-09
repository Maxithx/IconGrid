# IconGrid

IconGrid is a Windows launcher and desktop overlay built with WPF and MVVM. It combines shortcut organization, layout presets, settings pages, live hardware telemetry, and a dedicated gaming overlay with real-time FPS — all in a launcher that can collapse into a 10px auto-hiding peek strip.

---

## Main Launcher

<!-- TODO: Screenshot — Full launcher with shortcuts and live monitor strip -->
<!-- Replace: Assets/git-img/IconGrid-full.png -->

- `Views/Launcher/MainWindow.xaml` is the main launcher shell containing the logo area, live monitor strip (CPU / GPU / ping / network), tab bar, shortcut grid, idle hide button, and settings entry point.
- Shortcut icons are fully drag-and-drop manageable with right-click context menus (rename, change icon, run as admin, copy path, remove).
- Closing the launcher hides it back to floating-icon mode instead of terminating the process.

### Floating icon

![IconGrid Floating icon Screenshot](Assets/git-img/Floatingicon.png)

- IconGrid can start in floating-icon mode, or go directly into the full launcher if "Start directly in launcher" is enabled on the Startside settings page.
- Left-click expands the main launcher. Right-click opens the localized exit menu.

### Launcher hide modes

Configured on the Startside settings page:

| Mode | Behavior |
|---|---|
| **Always visible** | Launcher stays open — never hides itself. |
| **Manual hide** | Click the mini-button in the icon area to toggle between visible and a 10px peek strip at the screen edge. Click the peek strip to show it again. |
| **Auto-hide** | After 1–10 seconds of mouse inactivity (configurable delay), the launcher slides up (top half of screen) or down (bottom half) to a 10px peek strip. Hover over the peek strip to reveal the launcher, or click it. The launcher re-hides automatically when the mouse leaves. |

- **Peek activation** — choose "Hover" (proximity detection — move mouse near the 10px strip) or "Click only".
- **Settings-open suppression** — auto-hide is automatically paused while the Settings window is open so you can configure without interruption.
- **Drag clamping** — after dragging the launcher, it snaps back into the work area so it can't be pulled entirely off-screen. Multi-monitor drag can be enabled via a toggle setting.

<!-- TODO: Screenshot — Launcher in auto-hide peek mode (10px strip visible at screen edge) -->
<!-- Replace: Assets/git-img/IconGrid-peek.png -->

### Collapsed tab view

IconGrid features a clean toggle between full and collapsed views. Clicking an active category tab hides the icon area, collapsing the UI to only show the top bar and system monitors. Clicking the tab again smoothly slides the icon area back into view.

![IconGrid collapsed state Screenshot](Assets/git-img/IconGrid-collapsed.png)

### Carousel & Grid view modes

Shortcut icons can be shown in two view modes, controlled by a toggle on the GenvejsIkoner (shortcut icons) settings page:

1. **Grid view** — icons laid out in multiple vertical rows with 4 icons per row, scrolled with a thin vertical scrollbar.
2. **Carousel view** — a single horizontal row with exactly 4 icons visible at a time. Scroll through the rest with the mouse wheel or a thin horizontal scrollbar. Same spacing as the grid.

<!-- TODO: Screenshot — Carousel view with 4 icons visible + horizontal scrollbar -->
<!-- Replace: Assets/git-img/IconGrid-carousel.png -->

### Settings window

![IconGrid Settings Screenshot](Assets/git-img/Settings.png)

- `Views/Settings/SettingsWindow.xaml` is the dedicated settings shell with sidebar navigation and modular pages.
- `Views/Settings/SettingsWindowCoordinator.cs` handles opening, reuse, placement, and shutdown.

### Live telemetry

- Live CPU, GPU, ping, and network telemetry in the launcher top bar powered by `LibreHardwareMonitorLib`.
- FPS capture via a passive ETW-based pipeline with a separate native FPS worker — no injection, no DLL detours, no graphics API hooking.

---

## Gaming Overlay

<!-- TODO: Screenshot — Gaming overlay in-game showing FPS, CPU, GPU, network -->
<!-- Replace: Assets/git-img/GamingOverlay-in-game.png -->

- `Views/Launcher/GamingOverlayWindow.xaml` is a compact always-on-top monitor designed for in-game use.
- The overlay shows live FPS, CPU, GPU, ping, and network stats in a single-row shell with subtle dividers for readability.
- `Download` / `Upload` bandwidth stats are intentionally removed from the overlay and remain only on the main launcher monitor row.

### Default position

Like classic overlay tools, the gaming overlay snaps to a user-chosen standard placement. Pick one on the Gaming Overlay settings page — **Top/Bottom × Left/Center/Right**, or **Custom**:

- **TopLeft / TopCenter / TopRight** — flush against the screen edge (no margin)
- **BottomLeft / BottomCenter / BottomRight** — flush against the bottom edge
- **Custom** — keep your drag position, never repositioned

When the overlay opens or the display resolution changes (game start/exit), it snaps to the chosen placement. Overlay is never locked — you can always drag it elsewhere, and the position is saved.

### Overlay scale

Slider ranges **100% to 150%** — the same percentage produces the same on-screen size regardless of resolution:

| Screen resolution | Slider 100% | Slider 150% |
|---|---|---|
| 3840×2160 (4K) | 100% | 150% |
| 2560×1440 (1440p) | 100% | 150% |
| 1920×1080 (1080p) | 100% | 150% |

IconGrid can remember a different default scale **per resolution**. When a game changes the display resolution, the overlay automatically adopts that resolution's saved scale, and scales back when the game exits.

### Transparent background

Two independent transparency modes:

- **Always transparent** — overlay background is always transparent. Text color is customizable (white, black, yellow, blue, green, pink, cyan, or a custom Windows color).
- **Auto transparent (while in game)** — overlay automatically becomes transparent only when a game is running. On the Windows desktop it keeps its normal opaque background — best of both worlds.

When either mode is active, overlay text switches to your chosen custom color for legibility against the transparent background. A color picker with 7 preset swatches (highlighted with accent-colored ring) and a "Custom color..." Windows dialog is available on the Gaming Overlay settings page.

### Game behavior (auto-hide + auto-show)

When a game is launched (from IconGrid or detected externally via the FPS pipeline):

- **Auto-hide** — the launcher hides or minimizes depending on the configured `GameLauncherAutoBehavior` (Do nothing / Auto-hide / Minimize to taskbar).
- **Auto-show overlay** — the gaming overlay opens automatically at its configured position and scale.
- **Auto-scale** — the overlay adopts the per-resolution scale for the resolution the game switches to.
- **Auto-close** — when the game exits, the overlay closes and the launcher is restored.

This applies to games launched both **from IconGrid** and **externally** (Steam, Uplay, EAC, etc.) — the elevated FPS agent detects the tracked process.

### Recommended game display mode

- Use **Fullscreen Windowed** (borderless) for the best overlay experience.
- **Exclusive fullscreen** can hide or mis-scale the overlay. The overlay does appear, but clicking it alt-tabs you out of the game — making it impractical. Switch to Fullscreen Windowed.

---

## Settings pages

| Page | Description |
|---|---|
| **Startside** | Startup mode, topmost behavior, UI scale, **hide mode** (Always visible / Manual / Auto), **auto-hide delay** (1–10 sec), **peek activation** (Hover / Click), language (Dansk / English) |
| **GenvejsIkoner** | Shortcut icon settings: **carousel/grid view toggle**, scrollbar on/off, icon rows & spacing |
| **Layout** | Layout presets, saved layouts, icon grid slot reservation, window arrangement |
| **Gaming Overlay** | Overlay **scale** (100–150%), **per-resolution defaults**, **position presets**, **transparent background** + auto-transparent while in game, **text color picker**, **Game Resolution** (per-game display resolution switching) |
| **Hardware** | CPU, GPU, RAM, motherboard diagnostics with real-time sensor data |
| **Hjælp** | Help and troubleshooting content |
| **About** | Version info, app description, credits |

<!-- TODO: Screenshot — Startside settings page showing hide mode dropdown + peek toggle -->
<!-- Replace: Assets/git-img/Settings-Startside.png -->

<!-- TODO: Screenshot — Gaming Overlay settings page showing scale slider + position preset dropdown + transparency toggles -->
<!-- Replace: Assets/git-img/Settings-GamingOverlay.png -->

---

## Game Resolution — per-game display resolution switching

> Configured from the **Gaming Overlay** settings page.

IconGrid can automatically switch your monitor to a lower resolution before launching a game, and restore the original resolution when the game exits.

**Why this exists:** When you run Windows at 4K, your GPU may struggle in games. Switching the game to Fullscreen Windowed at 1440p lets the overlay work correctly, and letting IconGrid change the monitor resolution at launch gives you smooth performance without compromising desktop sharpness.

**How it works:**
- Each shortcut can have a **Game Resolution** (3840×2160, 2560×1440, 1920×1080, etc.) configured on the Gaming Overlay settings page.
- IconGrid changes the primary monitor resolution before launch.
- The resolution is **sticky** — it only reverts when the game process actually exits (not when you alt-tab away).
- The overlay's scale and position automatically follow the new resolution.
- When the game exits, the original resolution is restored along with any desktop window layout.

---

## Architecture

### Models

- `Models/` contains shared data contracts and enums: `ConfigModel`, `LauncherItem`, `LauncherHideMode`, `GamingOverlayPositionPreset`, layout data, and startup mode definitions.

### ViewModels

- `ViewModels/MainViewModel.cs` — root launcher view model (partial class).
- `ViewModels/MainViewModel.*.cs` — modular partials: `.Settings`, `.Items`, `.Layout`, `.Localization`, `.Overlay`.
- `ViewModels/Launcher/` — launcher-specific state and managers.
- `ViewModels/Settings/` — config state and persistence (small state objects that keep `MainViewModel` from growing into a feature dump).

### Views

- `Views/Launcher/` — main launcher shell and gaming overlay window.
- `Views/Settings/` — settings shell, pages, and coordination helpers.
- `Views/StartsideStyles.xaml`, `Views/TemplateGuidelines.xaml` — shared view resources.

### Controls

- `Controls/Launcher/` — `LauncherTopBar`, `LauncherTabsBar`, `LauncherGrid`
- `Controls/Floating/` — `FloatingIconButton`
- `Controls/Common/` — shared controls (`LauncherLogo`, `SliderRow`)

### Helpers

- `Helpers/Launcher/` — `LauncherWindowModeController`, `FloatingIconController`, `SystemMonitor`, `DynamicIconHelper`, `WindowLayoutEngine`, `LauncherWindowInterop`, `DevOverlayController`, `LauncherDragDropHelper`, `LauncherShortcutActions`, `LayoutMenuController`, `PawnIoWarningController`, `MonitorPollingController`
- `Helpers/Settings/` — config, localization, startup, theme helpers
- `Helpers/Hardware/` — hardware monitor, ETW/FPS pipeline, native FPS agent runner, FPS smoothing
- `Helpers/Converters/` — shared WPF value converters
- `Helpers/Common/` — `RelayCommand`, `DevInspector`

---

## Security & Privilege Separation

IconGrid uses a multi-process architecture:

1. **Elevated Worker (Admin):** interfaces with `LibreHardwareMonitorLib` for low-level hardware telemetry (CPU/GPU temperatures, clocks).
2. **Standard Non-Elevated UI:** the main launcher, floating icon, and shortcut management run as a standard user.

**Benefits:**
- Drag-and-drop from Windows Explorer works without UIPI blocking.
- Any game, browser, or application launched inherits standard user privileges — prevents untrusted applications from gaining elevated access.

---

## FPS capture and ETW access

- IconGrid captures FPS through a passive ETW-based pipeline — no injection, no DLL detours, no graphics API hooking.
- A separate native FPS worker (`Native/FpsAgent/src/main.cpp`) is launched via `Helpers/Hardware/NativeFpsAgentRunner.cs`.
- ETW providers: `Microsoft-Windows-DxgKrnl`, `Microsoft-Windows-DXGI`, `Microsoft-Windows-D3D9`.

### Current FPS pipeline

1. Game renders frames
2. Native FPS worker captures present events through ETW
3. Native FPS worker publishes live FPS via shared memory (hottest path) + `native-fps-state.json` (fallback/diagnostics)
4. Elevated hardware agent reads native FPS state
5. `FpsMeter` maintains direct `live` FPS + smoothed `trend` FPS for fallback
6. Launcher / gaming overlay prefers shared-memory live FPS path, overlay shows a single live FPS number

### Important Windows requirement

The current Windows user may need to be added to `Performance Log Users` (Danish: `Brugere af ydelseslog`) for ETW to work. After adding, sign out/in or reboot.

### Currently tested games (2026-08-04)

| Game | FPS source | Status | Notes |
|------|-----------|--------|-------|
| **Path of Exile 1** | PrimaryApi (DXGI) | ✅ Perfect | Gold standard — holds target stably across all window switches |
| **Path of Exile 2** | PrimaryApi (DXGI) | ✅ Works | Real FPS fluctuates when unfocused — ETW shows correct render rate |
| **Call of Duty (cod22-cod.exe)** | PrimaryApi (DXGI) | ✅ Works | Intro >200 FPS, menu ~100 FPS. Unrelated present/display behavior when unfocused |
| **Tom Clancy's The Division 2** | PrimaryApi (DXGI) | ✅ Works | Render stops completely at de-focus (Nvidia confirms 0 FPS). Last known FPS shown via sticky-hold |
| **Stumble Guys** | PrimaryApi (DXGI) | ✅ Works | Tested 2026-08-04 |
| **RHYTHM SPROUT Demo** | PrimaryApi (DXGI) | ✅ Works | Tested 2026-08-04 |
| **Yuzu (emulator)** | DxgKrnlFallback | ⚠️ Overcount | Emulator normalization clamped to ~60 FPS |
| **Fireworks Mania** | PrimaryApi (DXGI) | ✅ Works | Direct .exe launch, fast lock |

---

## Hardware monitor startup

- Windows startup launches the launcher UI silently through Task Scheduler (`--startup-launch` at logon).
- Hardware monitor runs as a separate elevated scheduled task `IconGrid Monitor` with `--monitor-agent`.
- FPS worker is separate from the desktop-facing UI, launched through the monitor-side flow.
- If "Start directly in launcher" is enabled, the launcher opens straight into full UI on Windows sign-in.

---

## Local data folder

IconGrid stores user data in `%APPDATA%\IconGrid`:

| File | Content |
|------|---------|
| `config.json` | All settings: launcher config, window positions, layout data, language, theme, hide modes, gaming overlay settings |
| `items.json` | Launcher shortcuts grouped by category/tab (machine-specific) |
| `monitor-state.json` | Live hardware monitor state (CPU/GPU readings) |
| `fps-state.json` | Compatibility/fallback FPS state |
| `native-fps-state.json` | Raw native FPS worker state (diagnostics) |
| `trace.log` | App trace output (startup + runtime diagnostics) |
| `error.log` | Fatal error log |
| `IconPack\` | Optional cached icon pack assets |

---

## Developer notes

- Targets Windows, organized around small WPF shells + modular controls/state helpers.
- `ARCHITECTURE_RULES.md` defines file size and method count guardrails to keep `MainWindow` and `MainViewModel` from growing into feature dumps.
- All code comments and commit messages should be in English (see `ARCHITECTURE_RULES.md` "Code Comments Language").
- Local workflow/planning notes are kept in `.local-state/` (gitignored) — not part of the GitHub-facing docs.

### MCP notes server

- The `icongrid-notes` MCP server lives in `tools/mcp-notes-server/` — versioned with git, single source of truth.
- Fresh-clone setup: `cd tools/mcp-notes-server/ && npm install`, then register in Cline's MCP config.
- See `AGENT.md` ("Session memory") for full details.

---

## Versioning

IconGrid uses Semantic Versioning with beta builds during active refactor and feature work. See `VERSIONING.md` for the release flow.

Current version: `0.7.0-beta.1`