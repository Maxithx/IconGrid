# IconGrid

IconGrid is a Windows launcher and desktop overlay built with WPF and MVVM. It combines shortcut organization, layout presets, settings pages, live hardware telemetry, and a dedicated gaming overlay with real-time FPS — all in a launcher that can collapse into a 10px auto-hiding peek strip.

---

## Main Launcher

<!-- TODO: Screenshot — Full launcher with shortcuts and live monitor strip -->
<!-- Replace: Assets/git-img/IconGrid-full.png -->

- `Views/Launcher/MainWindow.xaml` is the main launcher shell containing the logo area, live monitor strip (CPU / GPU / ping / network), tab bar, shortcut grid, idle hide button, and settings entry point.
- Shortcut icons are fully drag-and-drop manageable with right-click context menus (rename, change icon, run as admin, copy path, remove).
- Closing the launcher follows the same rule as the in-app close button: with **Start directly in launcher** enabled it exits the app completely (which also stops the elevated hardware agent and the native FPS worker); otherwise it drops back to floating-icon mode. A second launch never spawns a duplicate instance — it brings the running one to the front and exits, so a stale FPS target can never be inherited from a previous session (single-instance guard).

### Categories (tabs)

Shortcuts are organized into category tabs at the top of the launcher (Games, Apps, Video, etc.). Each tab is shown as a clickable pill-shaped button:

- **Add tab** — click the `+` button next to the tabs to create a new category.
- **Rename / Remove tab** — right-click any tab for rename and remove options.
- **Reorder tabs** — drag any tab horizontally and drop it before or after another to change the order. The selected tab stays selected.
- **Move shortcut to another tab** — drag any shortcut icon from the grid and drop it onto a different category tab. The shortcut moves to that category and appears at the end of its list. Drop on the same tab does nothing (no duplicates).
- Tab order and category assignments are persisted across restarts.

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

Shortcut icons can be shown in two view modes, controlled by a toggle on the GenvejsIkoner (shortcut icons) settings page. The settings page is split into dedicated sections so it is always clear which options apply to which view:

1. **Grid view** — icons laid out in multiple vertical rows, scrolled with a thin vertical scrollbar. Grid-only settings: **icons per row**, **icon row spacing**, and **bottom padding (last row)**.
2. **Carousel view** — a single horizontal row, scrolled with the mouse wheel or a thin horizontal scrollbar. Carousel-only setting: **visible icons** (1–12, default 4) — controls how many icons are visible at once; fewer icons = more space between them. The horizontal spacing in carousel mode is independent of the grid's row settings.

A single shared **icon size** slider (82%–150%) applies to both views. The 82% floor guarantees carousel icons never clip, since the viewport height is derived from the scale.

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
| **GenvejsIkoner** | Shortcut icon settings split by view: **Grid view** (icons per row, row spacing, bottom padding), **Carousel view** (visible icons 1–12), shared **icon size** (82–150%), scrollbar on/off |
| **Layout** | Layout presets, saved layouts, icon grid slot reservation, window arrangement |
| **Gaming Overlay** | Overlay **scale** (100–150%), **per-resolution defaults**, **position presets**, **transparent background** + auto-transparent while in game, **text color picker**, **game auto-behavior** (auto-show, restore launcher, auto-close on game end, launcher behavior), **FPS/ETW setup status**, **Game Resolution** (per-game display resolution switching) |
| **Hardware** | CPU, GPU, RAM, motherboard diagnostics with real-time sensor data |
| **Monitor row layout** | Element gaps/dividers/bar gaps for the live monitor strip, gaming overlay background height, and the **ping target** for the "Net" value (Auto → Cloudflare/Google/router, router only, fixed provider, or custom host) |
| **Fast USB Copy** | High-performance file copying to/from any drive (USB/HDD/SSD) with an optimized buffer pipeline, live speed/ETA, structured logs and a benchmark suite (port/read/write/buffer/stability/Windows baseline + CSV export) |
| **Test** | Diagnostics and maintenance: FPS/ETW setup status, trace-log size + **Clean trace log** |
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

## Fast USB Copy

> Configured from the **Fast USB Copy** settings page (sidebar: "USB Copy").

Fast USB Copy is a high-performance file copy tool that copies to/from any drive (USB, HDD, SSD) using a buffer-optimized pipeline instead of Windows' default path.

- **Buffered pipeline** — buffer 64 KB – 2 MB, with `SequentialScan` on read and `WriteThrough` on write.
- **Live performance** — live MiB/s, average, peak, ETA, pipeline status (read/flush per file).
- **Structured logging** — from second 0 with device/port, buffer, throughput, file names/sizes, timestamps, I/O errors, pipeline events. `%APPDATA%\IconGrid\logs\fastusbcopy\` (+ `benchmark\`).
- **Benchmark suite** — port/read/write/buffer/stability tests + a **Windows baseline test** (copies via `File.Copy`, same API as Explorer) so you can compare against Explorer. Export results to CSV.

### Architecture

Logic lives in `Helpers/UsbCopy/` (engine, detector, logger, benchmark runner, state) with a dedicated view model (`ViewModels/Settings/UsbCopyViewModel.cs`) and settings page (`Views/Settings/Pages/UsbCopyPage.xaml`) — `MainViewModel` stays untouched.

---

## Architecture

> Full folder/file reference: [`PROJECT_STRUCTURE.md`](PROJECT_STRUCTURE.md).

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

- `Helpers/Launcher/` — `LauncherWindowModeController`, `FloatingIconController`, `SingleInstanceGuard`, `SystemMonitor`, `DynamicIconHelper`, `WindowLayoutEngine`, `LauncherWindowInterop`, `DevOverlayController`, `LauncherDragDropHelper`, `LauncherShortcutActions`, `LayoutMenuController`, `PawnIoWarningController`, `MonitorPollingController`
- `Helpers/Settings/` — config, localization, startup, theme helpers
- `Helpers/Hardware/` — hardware monitor, ETW/FPS pipeline, native FPS agent runner, FPS smoothing, `GameProcessClassifier` (the single "is this a game?" policy), `GpuProcessMemory` (per-process dedicated VRAM)
- `Helpers/Logging/` — `AppTrace`, the size-capped shared trace writer
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
4. The worker publishes a target PID **only once that target is confirmed by evidence** — until then it publishes `targetPid = 0`, so "in game" can never be claimed by a process that is not actually rendering
5. Elevated hardware agent reads native FPS state and owns target selection and retargeting
6. `FpsMeter` maintains direct `live` FPS + smoothed `trend` FPS for fallback
7. Launcher / gaming overlay prefers shared-memory live FPS path, overlay shows a single live FPS number

### Game detection policy — evidence, not a blocklist

IconGrid deliberately does **not** decide "is this a game?" from a list of program names. A name list always goes stale: a new shell element, utility or overlay appears that nobody added, and it then gets detected as a game. That is exactly how `TextInputHost`, `PowerToys.Peek.UI`, the Windows Command Palette (`Microsoft.CmdPal.UI`) and `explorer.exe` were each mis-detected in turn — and a mis-detected process becomes a *sticky* target that then blocks the real game from ever being picked up.

Instead, a process counts as a game only when there is real evidence:

| Signal | Weight | Notes |
|---|---|---|
| **DXGI / D3D9 present events** | Strong | Application-level presents. A game always emits these. |
| **Dedicated VRAM ≥ 300 MB** | Strong | Read per process from the Windows `GPU Process Memory` → `Dedicated Usage` counter. A shell/desktop process holds only a few tens of MB; a real game holds hundreds of MB to several GB. Independent of the ETW providers. |
| **DxgKrnl kernel presents** | **Never classifies** | The coarse kernel fallback may only supply an FPS *number* for an already-confirmed target. DWM-composited shell apps emit stray kernel presents while producing zero application presents — which is what created the false positives. |
| **`--trusted-launch`** | Trusted | Set only when IconGrid itself started the game. Such a session is trusted by identity, which keeps emulators and older titles working when they only expose the kernel path. |

Structural rules — deliberately *not* name lists, so they cannot go stale:

- **Windows system directories** (`SystemApps`, `system32`, `SysWOW64`) are never a game. No real game ships from inside them.
- **Windows shell window classes** (`Progman`, `WorkerW`, `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `MultitaskingViewFrame`, `TaskListThumbnailWnd`, `XamlExplorerHostIslandWindow`) are never a game window — regardless of which process owns them. This is what keeps the desktop, taskbar and task view out.
- **Store / MSIX packages** (`Program Files\WindowsApps`) must show application-level presents to be trusted, because that folder also hosts shell companions.

Two supporting behaviours keep a wrong target from ever sticking:

- **Challenger override** — a confirmed target is normally held while it is alive (so alt-tabbing does not retarget), but if a *different* valid foreground game candidate stays stable for 20 seconds, the old target is released and the real game is acquired.
- **Orphan safety** — the elevated agent verifies its launcher's PID *and* start time, and exits when they are gone, so a leftover agent with a stale target cannot survive into the next session.

The result: `IsInGame` — and with it overlay transparency and overlay auto-show — follows only a confirmed game, and a stale or non-game target can no longer make the overlay appear at startup.

### Per-process VRAM readout

Because dedicated VRAM is both an evidence signal and a useful diagnostic, every `Snapshot:` line in `trace.log` reports it for the current target:

```
Snapshot: FPS=118 GPU=97,0% Source=PrimaryApi Vram=1842MB
```

A game shows hundreds of MB to several GB while it is focused, and collapses to a small figure (for example `Vram=73MB`) the moment it loses focus — because most games stop rendering when they are not in the foreground. That is expected behaviour, not a dropped target: the overlay intentionally holds the last known FPS instead of blanking.

### Important Windows requirement

The current Windows user may need to be added to `Performance Log Users` (Danish: `Brugere af ydelseslog`) for ETW to work. After adding, sign out/in or reboot.

### Currently tested games (2026-09-13)

| Game | FPS source | Status | Notes |
|------|-----------|--------|-------|
| **Path of Exile 1** | PrimaryApi (DXGI) | ✅ Perfect | Gold standard — holds target stably across all window switches. Overlay appears almost instantly |
| **Path of Exile 2** | PrimaryApi (DXGI) | ✅ Works | Real FPS fluctuates when unfocused — ETW shows correct render rate |
| **Call of Duty (cod22-cod.exe)** | PrimaryApi (DXGI) | ✅ Works | Intro >200 FPS, menu ~100 FPS. Dedicated VRAM rises to ~5.4 GB while playing and drops to ~70 MB when unfocused |
| **Tom Clancy's The Division 2** | PrimaryApi (DXGI) | ✅ Works | Render stops completely at de-focus (Nvidia confirms 0 FPS). Last known FPS shown via sticky-hold |
| **Stumble Guys** | PrimaryApi (DXGI) | ✅ Works | Tested 2026-08-04 |
| **RHYTHM SPROUT Demo** | PrimaryApi (DXGI) | ✅ Works | Tested 2026-08-04 |
| **Yuzu (emulator)** | DxgKrnlFallback | ⚠️ Overcount | Emulator normalization clamped to ~60 FPS. Works via `--trusted-launch` when started from IconGrid |
| **Fireworks Mania** | PrimaryApi (DXGI) | ✅ Works | Direct .exe launch, fast lock |

**Verified false positives eliminated (2026-09-13):** `explorer.exe` (desktop/taskbar), Windows Command Palette (`Microsoft.CmdPal.UI`), PowerToys `Peek.UI`, `TextInputHost`, Task Manager, and browsers/editors all stay out of detection without being named in any list. `trace.log` shows the structural rejection instead, for example:

```
Rejecting foreground PID because the window is a Windows shell window. Name=explorer Class=Progman
Skipping foreground PID 18284 (brave) — module scan confirmed no graphics API DLLs.
```

A note on FPS drops: when a game loses foreground focus — opening the Start menu, another window, or the overlay — its FPS legitimately drops, because most games throttle or pause rendering when unfocused. With **Fullscreen Windowed** the game is always DWM-composited, so there is no display-mode transition involved; the drop is the game's own background behaviour plus the extra compositor work. The pipeline holds the last known FPS rather than blanking, and does not retarget.

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
| `native-fps-state.json` | Raw native FPS worker state (diagnostics, incl. `gameConfirmed` / `trustedLaunch`) |
| `external-games.json` | Games detected as started outside IconGrid (auto-registered so they can get their own resolution) |
| `trace.log` | App trace output (startup + runtime diagnostics). Size-capped at 10 MB — the oldest half is trimmed automatically, and it can be cleared from the **Test** settings page |
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
- Fresh-clone setup: `cd Tools/mcp-notes-server/ && npm install`, then register in Cline's MCP config.
- See `AGENT.md` ("Session memory") for full details.

---

## Versioning

IconGrid uses Semantic Versioning with beta builds during active refactor and feature work. See `VERSIONING.md` for the release flow.

Current version: `0.7.0-beta.1`