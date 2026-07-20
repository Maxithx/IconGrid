# Architecture Rules For IconGrid

This document defines the guardrails we should follow when extending IconGrid so `MainWindow` and `MainViewModel` do not become large, fragile integration points again.

## Core Principle

Keep feature logic close to the feature that owns it.

Do not let launcher-shell code become the default place for new behavior.

## MainWindow Rules

- `Views/Launcher/MainWindow.xaml` is the launcher shell, not the home for feature-specific UI.
- `Views/Launcher/MainWindow.xaml.cs` may coordinate shell-level behavior, window interop, and composition of modules.
- Do not add new feature UI directly to `MainWindow` when it can live in a dedicated control or settings page.
- Do not place feature-specific state in `MainWindow.xaml.cs` unless it is strictly window-lifetime or Win32 integration state.

## MainViewModel Rules

- `ViewModels/MainViewModel.cs` is the root composition view model, not the storage place for all business logic.
- `MainViewModel` should expose properties and delegate work to focused state/helper classes.
- New feature logic should prefer dedicated classes under `ViewModels/Launcher/`, `ViewModels/Settings/`, `Helpers/`, or a feature-specific control/view model.
- If a feature needs multiple related properties, persistence rules, or calculations, move them into a focused state class instead of expanding `MainViewModel` directly.

## Feature Placement Rules

- Launcher runtime behavior belongs in launcher-focused modules.
- Settings-specific editing UI belongs in `Views/Settings/Pages/` or dedicated controls used by those pages.
- Reusable interactive UI should become a control under `Controls/`.
- Shared calculations, transforms, or persistence helpers should become focused helper/state classes.
- WPF converters stay in `Helpers/Converters/`.
- Background or worker-process responsibilities should stay outside the launcher shell and UI view models.
- Cross-process state handoff should prefer dedicated helper/agent classes instead of leaking process concerns into `MainWindow` or `MainViewModel`.

## Preferred Extension Pattern

When adding a feature:

1. Add persisted values to `ConfigModel` only if the feature truly needs saved state.
2. Add focused state handling in the relevant state class, such as `LauncherLayoutState`.
3. Expose only the minimal properties needed through `MainViewModel`.
4. Build dedicated UI in a settings page or separate control.
5. Keep Win32/runtime window placement logic isolated in the launcher shell where necessary.

## Recent Good Examples

- Gaming overlay UI stayed in `Views/Launcher/GamingOverlayWindow.xaml` instead of being folded into `MainWindow`.
- Overlay-side FPS display behavior stayed in `Helpers/Launcher/SystemMonitor.cs` instead of turning `MainViewModel` into a realtime telemetry loop.
- Native ETW FPS capture stayed in `Native/FpsAgent/src/main.cpp` and `Helpers/Hardware/NativeFpsAgentRunner.cs` instead of bleeding native/worker concerns into launcher UI files.
- Shared-memory live FPS handoff was added as a focused helper (`Helpers/Hardware/NativeFpsSharedMemory.cs`) instead of spreading IPC details across multiple view or shell files.
- Hardware and FPS background flow remained in `Helpers/Hardware/HardwareMonitorAgent.cs` rather than pushing worker orchestration into `MainWindow.xaml.cs`.

## Coordinator / Helper Rule

- If a feature opens, owns, or reuses a separate window, prefer a focused coordinator/helper instead of growing shell code.
- If a feature needs cross-process orchestration, state polling, or IPC, prefer a focused helper/agent class instead of adding that logic to a view model.
- If a feature has a dedicated hot path, such as live FPS updates, isolate that path in the smallest responsible module and keep fallback/diagnostic behavior separate.

## When To Create A Separate Control

Create a dedicated control when any of these are true:

- The UI has its own interaction model, such as drag, preview, selection, or custom rendering.
- The XAML block is large enough to obscure the surrounding page structure.
- The feature will likely be reused or evolved independently.
- The code-behind would otherwise grow inside a page or shell file.

## Refactor Trigger

If a change adds:

- many new bindings to one existing page
- several new persisted properties
- custom pointer or drag logic
- non-trivial preview rendering

then stop and ask whether the feature should become its own module or control before continuing.

Also stop and ask the same question if a change starts adding:

- worker-process launch/restart logic to UI files
- IPC/shared-memory details to view models
- realtime polling loops to `MainViewModel`
- feature-specific window ownership logic directly in the launcher shell

## Goal

Better modular structure should make features easier to maintain, safer to change, and less likely to regress unrelated launcher behavior.
