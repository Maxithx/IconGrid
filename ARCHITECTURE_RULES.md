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

## Preferred Extension Pattern

When adding a feature:

1. Add persisted values to `ConfigModel` only if the feature truly needs saved state.
2. Add focused state handling in the relevant state class, such as `LauncherLayoutState`.
3. Expose only the minimal properties needed through `MainViewModel`.
4. Build dedicated UI in a settings page or separate control.
5. Keep Win32/runtime window placement logic isolated in the launcher shell where necessary.

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

## Goal

Better modular structure should make features easier to maintain, safer to change, and less likely to regress unrelated launcher behavior.
