# IconGrid — Project Structure

Complete folder/file reference for the repository. Verified against the actual working tree
(`E:\IconGrid-GitHub`) on **2026-09-15**. Build output (`bin/`, `obj/`) and ignored local folders
are intentionally left out — see [Ignored / local-only paths](#ignored--local-only-paths).

> Related docs: `README.md` (feature overview), `ARCHITECTURE_RULES.md` (where new code belongs),
> `AGENT.md` (agent workflow), `CHAT_STATE.md` (session memory), `VERSIONING.md` (release flow).

## Legend

| Marker | Meaning |
|---|---|
| `# …` | Short description of the file/folder |
| `gitignored` | Exists on disk but is not tracked by git (see `.gitignore`) |
| `generated` | Produced by build/publish — never edit by hand |

---

## Repository root

```
E:\IconGrid-GitHub\
├── .clinerules                        # Agent runtime rules (shell + WPF/MVVM guardrails)
├── .editorconfig                      # Editor/formatting conventions
├── .gitattributes                     # Line-ending normalization
├── .gitignore                         # Ignore rules (build output, secrets, local state)
├── .vscode\
│   └── tasks.json                     # VS Code build/deploy tasks (launch/settings.json gitignored)
├── .local-state\                      # Local agent notes + technical references  (gitignored)
├── .dotnet\                           # Local .NET toolchain state (sentinel files are tracked in git)
├── .nuget\                            # NuGet cache                                              (gitignored)
│
├── AGENT.md                           # Entry point for any agent working in the repo
├── ARCHITECTURE_RULES.md              # Modularity guardrails, refactor workflow, security checklist
├── CHAT_STATE.md                      # Tracked short-term memory (current status + session findings)
├── COLLABORATOR_GUIDE.md              # Guide to the agent memory system (notes/CHAT_STATE/MCP)
├── PROJECT_STRUCTURE.md               # This file
├── README.md                          # Feature documentation and user-facing overview
├── TODO.md                            # Open work items
├── VERSIONING.md                      # Semantic versioning / release flow
│
├── App.xaml, App.xaml.cs              # Application entry, startup branching, single-instance guard
├── app.manifest                       # Windows manifest (DPI awareness PerMonitorV2, elevation info)
├── AssemblyInfo.cs                    # Assembly metadata — canonical SemVer lives here
├── IconGrid.csproj, IconGrid.sln      # WPF app project (net10.0-windows10.0.22621.0) + solution
├── IconGrid.msix                      # Packaged MSIX artifact (repo root copy)
├── IconGrid-workspace.code-workspace  # VS Code multi-root workspace file
├── nuget.config                       # NuGet source configuration
├── deploy-test.cmd                    # Deploy build output to C:\icongrid and start it
├── generated-icon.png                 # Icon source asset used for generated shortcuts
└── kommandoer.txt                     # Local command scratchpad                          (gitignored)
```

**NuGet packages** (`IconGrid.csproj`): `LibreHardwareMonitorLib`, `Newtonsoft.Json`,
`System.Management`, `System.ServiceProcess.ServiceController`.

**Content copied to the output/publish folder** (`IconGrid.csproj`): `Assets\WindowsShortcuts\**`,
`Assets\Hw-logo\**`, selected `figma-icons\*.png`, `Tools\PresentMon\PresentMon-*.exe`, and the
native FPS worker `Native\FpsAgent\bin\$(Configuration)\IconGridFpsAgent.exe`
(→ `Tools\FpsAgent\IconGridFpsAgent.exe`).

---

## Source folders at a glance

```
├── Assets\            # Icons, logos and bundled shortcuts (copied to output)
├── certificate\       # Signing certificates                                        (gitignored)
├── Controls\          # Reusable WPF user controls (launcher + shared + floating)
├── figma-icons\       # Design-exported PNG/Figma icon sources
├── Helpers\           # Services, agents, converters, logging (no UI)
├── IconGrid.Package\  # MSIX packaging project (wapproj, manifest, images, AppPackages)
├── Models\            # Data contracts, config model, enums
├── Native\            # C++ ETW/FPS worker (FpsAgent)
├── publish\           # dotnet publish / MSIX output                                (gitignored)
├── tools\             # Standalone helper projects (benchmark runner, MCP notes server)
├── ViewModels\        # MVVM view models and state objects
├── Views\             # WPF windows, settings shell and settings pages
└── _backups\          # Manual backups                                              (gitignored)
```

---

## Assets\

```
Assets\
├── dlb-icon.ico                                  # Application icon (also <ApplicationIcon> + <Resource>)
├── git-img\                                      # Screenshots embedded in README.md
│   ├── Floatingicon.png, IconGrid-collapsed.png, IconGrid.png, Settings.png
├── Hw-logo\                                      # Hardware vendor logos shown on the Hardware page
│   ├── AMDlogo-darktheme.png, AMDlogo-lighttheme.png
│   ├── Asuslogo-dark-theme.png, Asuslogo-light-theme.png
│   ├── Intel-logo.png, Nvidia-GTX-logo.png, Nvidia-RTX-logo.png
├── LinuxShortcuts\                               # Bundled Linux shortcut (.lnk)
└── WindowsShortcuts\                             # Bundled Windows shortcuts shipped with the app (.lnk)
```
`WindowsShortcuts\` and `Hw-logo\` are copied to the output/publish directory by `IconGrid.csproj`.

## Controls\

```
Controls\
├── Common\        # Shared, feature-agnostic UI
│   ├── LauncherLogo.xaml (.cs)        # App logo/wordmark block
│   └── SliderRow.xaml (.cs)           # Label + slider + value row used by settings pages
├── Floating\      # Floating desktop icon mode
│   └── FloatingIconButton.xaml (.cs)  # Draggable floating icon window content
└── Launcher\      # Launcher shell composition controls
    ├── LauncherGrid.xaml (.cs)         # Shortcut grid/carousel host (drag & drop)
    ├── LauncherLogoArea.xaml (.cs)     # Logo area + collapsed/peek affordances
    ├── LauncherMonitorRow.xaml (.cs)   # Live telemetry strip (ping/net/download/upload/CPU/GPU)
    ├── LauncherTabsBar.xaml (.cs)      # Category pills (+ add/rename/remove/reorder)
    ├── LauncherTopBar.xaml (.cs)       # Title bar, settings entry, window-button host
    └── LauncherWindowButtons.xaml (.cs)# Minimize/maximize/close buttons
```

## Helpers\ (no UI — services, agents, converters)

```
Helpers\
├── Common\
│   ├── AppVersion.cs                 # Version helpers (reads AssemblyInfo)
│   ├── DevInspector.cs               # Attached property feeding the dev overlay metadata
│   ├── RelayCommand.cs               # ICommand implementation used across view models
│   └── UpdateVisitor.cs              # Visual-tree visitor helper
├── Converters\                       # WPF IValueConverter implementations (one type per file)
│   ├── BooleanToAngleConverter.cs        InverseBoolConverter.cs
│   ├── BoolToBrushConverter.cs           InverseBoolToVisibilityConverter.cs
│   ├── BoolToVisibilityConverter.cs      LayoutSlotLabelConverter.cs
│   ├── DoubleToLeftMarginConverter.cs    LayoutSlotMatchConverter.cs
│   ├── PercentToHeightConverter.cs       PingSeverityToBrushConverter.cs
│   ├── ScrollButtonsVisibilityConverter.cs  SelectedTabMatchConverter.cs
│   ├── SidebarWidthConverter.cs          StringEqualsToVisibilityConverter.cs
│   ├── TabNameLocalizationConverter.cs   TemplateContentWidthConverter.cs
│   └── ZeroToAutoWidthConverter.cs
└── Hardware\                         # Telemetry, ETW/FPS pipeline, elevation
    ├── ElevatedEtwProbeRunner.cs         # Runs the ETW probe elevated
    ├── EtwAccessRequirements.cs          # ETW/privilege requirement checks
    ├── EtwFpsProvider.cs, FpsEtwProbeAgent.cs
    ├── FpsMeter.cs, FpsNormalizer.cs     # FPS measurement + smoothing
    ├── GameProcessClassifier.cs          # Single "is this a game?" evidence policy
    ├── GpuProcessMemory.cs               # Per-process dedicated VRAM readout
    ├── HardwareInfoProvider.cs, HardwareMonitorSnapshot.cs
    ├── HardwareMonitorAgent.cs           # Elevated worker (--monitor-agent)
    ├── HardwareMonitorTaskManager.cs     # Scheduled task lifecycle ("IconGrid Monitor")
    ├── HardwareSnapshotCollector.cs
    ├── NativeFpsAgentRunner.cs, NativeFpsAgentState.cs, NativeFpsSharedMemory.cs
    ├── PresentMonFpsProvider.cs          # Optional PresentMon-backed path
    └── SmBiosMemoryParser.cs
├── Launcher\                         # Launcher runtime behaviour (window/state/telemetry)
│   ├── DevOverlayController.cs           LayoutMenuController.cs
│   ├── DisplayResolutionService.cs       MonitorPollingController.cs
│   ├── DynamicIconHelper.cs              PawnIoWarningController.cs
│   ├── EmbeddedIconLibrary.cs            ShellIconLabel.cs, ShellIconTextBlock.cs
│   ├── ExternalGameRegistry.cs           ShortcutHelper.cs
│   ├── FloatingIconController.cs         SingleInstanceGuard.cs
│   ├── IconHelper.cs                     SystemMonitor.cs
│   ├── IconResourceUpdater.cs            WindowLayoutEngine.cs
│   ├── LauncherDragDropHelper.cs         WindowLayoutSnapshotService.cs
│   ├── LauncherShortcutActions.cs        WindowTrackingService.cs
│   ├── LauncherWindowInterop.cs, LauncherWindowModeController.cs
│   └── PresentMonFpsProvider.cs
├── Logging\
│   └── AppTrace.cs                   # Size-capped shared trace writer (trace.log)
├── Settings\                         # Config, localization, startup, theming
│   ├── ConfigManager.cs                  # config.json read/write
│   ├── LocalizationHelper.cs             # da/en string dictionaries (single source of UI text)
│   ├── PawnIoHelper.cs                   # PawnIO driver helper (hardware sensor access)
│   ├── StartupTaskManager.cs             # Windows startup / scheduled task registration
│   └── ThemeHelper.cs                    # Light/dark theme resolution
└── UsbCopy\                          # Fast Copy feature (engine, IO, benchmark, state)
    ├── FileBrowserEntry.cs               # Drive/folder/file entry model for the dual-pane browser
    ├── FileBrowserPane.cs                # Pane logic (enumeration, refresh, create/delete)
    ├── MultiWorkerCopyService.cs         # Multi-worker copy engine (buffers, chunk/ZIP strategy)
    ├── OverwritePolicy.cs                # Overwrite/skip decisions + session-scoped resolver
    ├── UsbBenchmarkRunner.cs             # Port/read/write/buffer/stability/Windows-baseline tests
    ├── UsbCopyEngine.cs                  # High-level orchestration of a copy job
    ├── UsbCopyLogger.cs                  # Structured logging to %APPDATA%\IconGrid\logs
    ├── UsbCopyState.cs                   # Observable copy progress/labels
    ├── UsbDeviceDetector.cs              # Drive/port capability detection
    └── UsbDeviceInfo.cs
```

## Models\ (data contracts and enums)

```
Models\
├── ConfigModel.cs                    # Persisted settings contract (config.json shape)
├── CustomLayoutSlot.cs               # Custom layout slot definition
├── GameAutoBehaviorMode.cs           # Overlay/launcher auto behaviour when a game runs
├── GamingOverlayPositionPreset.cs    # Top/Bottom × Left/Center/Right + Custom presets
├── LauncherHideMode.cs               # AlwaysVisible / ManualHide / AutoHide
├── LauncherItem.cs                   # Shortcut: DisplayName, Path, Category, GameResolution, icon
├── MonitorLayoutDefaultsSnapshot.cs  # "My default" snapshot for the monitor row layout page
├── StartupLaunchMode.cs              # Startup behaviour (floating icon vs. launcher)
└── WindowTrackingRecord.cs           # Tracked external window (layout restore)
```

## Native\ (C++ ETW/FPS worker)

```
Native\FpsAgent\
├── FpsAgent.vcxproj      # MSBuild project (toolset v145, x64) — built separately from the WPF app
├── README.md             # Build notes for the native worker
├── src\main.cpp          # ETW/PresentMon-style FPS capture, target lock, shared-memory output
├── bin\Debug|Release\    # Build output (IconGridFpsAgent.exe)                (gitignored)
└── obj\                  # Intermediate build files                           (gitignored)
```
The WPF app only consumes the produced binary (`Native\FpsAgent\bin\<Config>\IconGridFpsAgent.exe`
→ `Tools\FpsAgent\IconGridFpsAgent.exe`). Build it with the MSBuild command in
`.local-state/regex-commands-cheatsheet.md` §9.

## ViewModels\ (MVVM)

```
ViewModels\
├── MainViewModel.cs                  # Root composition view model (partial class)
├── MainViewModel.Items.cs            # Shortcut items/tabs/categories
├── MainViewModel.Layout.cs           # Launcher layout presets and measurements
├── MainViewModel.Localization.cs     # Localized string properties + OnPropertyChanged fan-out
├── MainViewModel.Overlay.cs          # Gaming overlay state bridging
├── MainViewModel.Settings.cs         # Settings apply/save/defaults plumbing
├── Launcher\                         # Launcher-focused state/managers (keep MainViewModel lean)
│   ├── LauncherItemIconManager.cs        # Icon resolution/change for shortcuts
│   ├── LauncherItemLaunchManager.cs      # Launch flow (args, admin, resolution switch)
│   ├── LauncherItemsManager.cs           # Items collection operations
│   ├── LauncherItemsPersistence.cs       # items.json read/write
│   ├── LauncherLayoutMeasurements.cs     # Grid/carousel metrics
│   ├── LauncherLayoutState.cs            # Layout/preset state
│   ├── LauncherLocalizationState.cs      # Localization lookup used by the view model
│   ├── LauncherOverlayState.cs           # Overlay-related observable state
│   ├── LauncherShortcutManager.cs        # Shortcut creation/import
│   ├── LauncherTabsState.cs              # Tabs/categories state + ordering
│   ├── LauncherThemeCoordinator.cs       # Theme switching coordination
│   ├── LauncherThemeState.cs             # Theme state (light/dark, brushes)
│   └── WindowStateStore.cs               # Saved window positions (launcher/settings/overlay/floating)
└── Settings\                         # Config state + persistence
    ├── MainViewModelConfigState.cs       # Config → observable state mapping (+ normalization)
    ├── MainViewModelSettingsPersistence.cs # Save mapping to config.json
    ├── MainViewModelSettingsState.cs     # Editable settings state
    └── UsbCopyViewModel.cs               # Dedicated view model for the Fast Copy page
```

## Views\ (windows, settings shell and settings pages)

```
Views\
├── AboutWindow.xaml (.cs)              # About dialog
├── BenchmarkRunWindow.xaml (.cs)       # Fast Copy benchmark progress/results window
├── DeleteConfirmWindow.xaml (.cs)      # Explorer-style delete confirmation
├── FileCopyProgressWindow.xaml (.cs)   # Non-modal copy progress window (topmost, drag-movable)
├── NewFolderDialogWindow.xaml (.cs)    # "New folder" name prompt
├── OverwritePromptWindow.xaml (.cs)    # Overwrite conflict prompt (Yes / Yes all / No / No all)
├── OverwritePromptResolver.cs          # Shows the prompt from worker threads (Dispatcher marshalling)
├── GamingOverlayWindowCoordinator.cs   # Overlay window lifetime + placement coordination
├── StartsideStyles.xaml                # Shared styles: cards, toggles, sliders, scrollbars
├── TemplateGuidelines.xaml             # Settings-page style set (hero/card/section/body styles)
├── Launcher\
│   ├── MainWindow.xaml (.cs)           # Launcher shell (composition only — no feature UI)
│   └── GamingOverlayWindow.xaml (.cs)  # Always-on-top in-game overlay
└── Settings\
    ├── SettingsWindow.xaml (.cs)       # Settings shell: sidebar navigation + page host
    ├── SettingsWindowCoordinator.cs    # Reuse / placement / close of the settings window
    ├── Dialogs\
    │   └── PawnIoWarningWindow.xaml (.cs)   # PawnIO driver warning dialog
    └── Pages\                          # One page per sidebar entry (DataContext = MainViewModel)
        ├── StartsidePage.xaml (.cs)         # Startup, hide modes, UI scale, language
        ├── GenvejsIkonerPage.xaml (.cs)     # Grid/carousel icon settings
        ├── LayoutPage.xaml (.cs)            # Layout presets and saved layouts
        ├── HardwarePage.xaml (.cs)          # CPU/GPU/RAM/motherboard diagnostics
        ├── MonitorRowLayoutPage.xaml (.cs)  # Monitor row gaps/dividers, overlay height, ping target
        ├── GamingOverlayPage.xaml (.cs)     # Overlay scale, position, transparency, FPS setup
        ├── GameResolutionPage.xaml (.cs)    # Per-game display resolution switching
        ├── UsbCopyPage.xaml (.cs)           # Fast Copy: dual pane, copy job, benchmark
        ├── TestPage.xaml (.cs)              # Diagnostics: FPS/ETW status, trace-log cleanup
        ├── HjaelpPage.xaml (.cs)            # Help / troubleshooting
        ├── AboutPage.xaml (.cs)             # Version info and credits
        └── TemplatePage.xaml (.cs)          # Page template: HeroContent + CardsContent
```

Settings pages inherit `MainViewModel` as DataContext from `SettingsWindow`, and bind all
user-facing text to localized properties (see `AGENT.md` → "Localization design pattern (da/en)").

> History: `MonitorPingPage.xaml` (its own sidebar entry, "MP") was merged into
> `MonitorRowLayoutPage.xaml` on 2026-09-15 — the ping target is now a card on that page.

---

## Packaging, tooling and local folders

```
IconGrid.Package\                     # MSIX packaging project
├── IconGrid.Package.wapproj          # Windows Application Packaging project
├── Package.appxmanifest              # Capabilities, identity, tile definitions
├── IconGrid.Package.assets.cache     # Packaging cache
├── Images\                           # MSIX package logos/tiles (scale-200 variants)
│   ├── StoreLogo.png, SplashScreen.scale-200.png, LockScreenLogo.scale-200.png
│   ├── Square44x44Logo*.png, Square150x150Logo*.png, Wide310x150Logo.scale-200.png
└── AppPackages\                      # Generated test packages 1.0.0.0 – 1.0.0.3_x64_Test (generated)

figma-icons\                          # Design-exported icon sources
├── figma-logo-x4.png, figma-genvejs-ikon-x4.png, figma-Layout-ikon-x4.png
├── figma-About-ikon-x4.png, help-icon-x4.png, Resolution.png, collaps-ikon.png
└── *.fig                             # Original Figma files (genvejs, Layout, Startside-logo)

tools\                                # Standalone helper projects
├── benchmark-runner\                 # Console benchmark harness
│   ├── BenchmarkRunner.csproj, Program.cs
│   └── bin\, obj\                    # (generated)
└── mcp-notes-server\                 # Local MCP server "icongrid-notes"
    ├── package.json, package-lock.json, .gitignore, TESTING.md
    ├── src\index.js                  # Note tools + project checks (run_all_checks etc.)
    ├── test\e2e.mjs                  # End-to-end test
    └── node_modules\                 # (gitignored via nested .gitignore)

certificate\                          # Signing material — never commit
├── icongrid-dev.pfx                  # (gitignored)
└── vs-icongrid.pfx                   # (gitignored)

.local-state\                         # Gitignored agent memory + technical references
├── project-structure.md              # Local stub pointing to this file
├── current-focus.md, documentation-plan.md, session-history.md
├── color-system.md, ui-design-guidelines.md, ui-launcher.md, gaming-overlay.md
├── fast-copy.md, fps-etw.md, GIT_WORKFLOW.md, ICONGRID_REMOTE_PLAN.md
├── next-session-*.md                 # Open follow-up notes
├── regex-commands-cheatsheet.md      # Verified shell command patterns (read before commands)
├── sessions\                         # Per-day session logs (2026-08-09 / 11 / 12.md)
└── PresentMon-reference\             # Upstream PresentMon sources used as ETW reference

.vscode\
└── tasks.json                        # Build/deploy tasks (launch.json, settings.json are gitignored)

publish\                              # dotnet publish / MSIX output                     (gitignored)
_backups\                             # Manual file backups                              (gitignored)
```

---

## Where new code goes (see `ARCHITECTURE_RULES.md` for the full rules)

| You are adding… | Put it in |
|---|---|
| Persisted setting value | `Models/ConfigModel.cs` + the matching state class under `ViewModels/Settings/` |
| Feature state/logic (no UI) | `Helpers/<Feature>/` or a focused state class under `ViewModels/Launcher/` / `ViewModels/Settings/` |
| Settings editing UI | `Views/Settings/Pages/` (one page per sidebar entry) or a dedicated control |
| Reusable interactive UI | `Controls/Common/`, `Controls/Launcher/` or `Controls/Floating/` |
| WPF value converter | `Helpers/Converters/` |
| New localized text | `Helpers/Settings/LocalizationHelper.cs` (**both** `en` and `da`) + a property + `OnPropertyChanged` in `ViewModels/MainViewModel.Localization.cs`, then bind it in XAML |
| Background worker / elevation | `Helpers/Hardware/` (or a separate process/project — never the launcher shell) |
| Shared styles | `Views/StartsideStyles.xaml`, `Views/TemplateGuidelines.xaml` |

Hard rules kept short: **no** new feature UI or feature state in `MainWindow.xaml(.cs)` or
`MainViewModel.cs`; comments and commit messages in **English**; never commit certificates/secrets.

## Build, run and deploy

| Task | Command |
|---|---|
| Build the WPF app | `dotnet build E:\IconGrid-GitHub\IconGrid.csproj --nologo -v m` |
| Deploy for testing | `cmd /c E:\IconGrid-GitHub\deploy-test.cmd` → copies to `C:\icongrid` and starts it |
| Verify a fresh deploy | `Get-FileHash "<build>\IconGrid.dll","C:\icongrid\IconGrid.dll" -Algorithm SHA256` (hashes must match) |
| Rebuild the native FPS worker | MSBuild on `Native\FpsAgent\FpsAgent.vcxproj` (see `.local-state/regex-commands-cheatsheet.md` §9) |
| MCP notes server | `cd tools\mcp-notes-server; npm install` |

Always read `.local-state/regex-commands-cheatsheet.md` before running shell commands — it lists the
verified good/bad patterns for this machine.

## Runtime data (not in the repo)

User data lives in `%APPDATA%\IconGrid`: `config.json`, `items.json`, `monitor-state.json`,
`fps-state.json`, `native-fps-state.json`, `external-games.json`, `trace.log`, `error.log`, `IconPack\`
and `logs\fastusbcopy\` (+ `benchmark\`).

## Ignored / local-only paths

| Path | Why |
|---|---|
| `bin\`, `obj\`, `Native\**\bin\`, `Native\**\obj\` | Build output |
| `publish\`, `_backups\`, `logs\` | Generated / local backups |
| `.local-state\` | Local agent memory and technical references |
| `.vscode\launch.json`, `.vscode\settings.json` | Machine-specific editor config |
| `certificate\*.pfx`, `*.key`, `*.pem`, `.env*` | Secrets / signing material — never commit |
| `Tools\FpsAgent\`, `Tools\PresentMon\*.exe` | Runtime copies of native/external binaries (not present before a build) |
| `kommandoer.txt`, `trace.log`, `config.json`, `items.json` | Local scratch/log files |

## Keeping this file up to date

- Update it whenever folders/files are added, moved or deleted (e.g. a settings page change or a new
  `Helpers\` sub-folder) — it is meant to mirror the working tree, not an aspiration.
- Keep the descriptions short and factual: one line per file/folder.
- Cross-check with `README.md` ("Architecture" + "Settings pages") and `ARCHITECTURE_RULES.md`
  ("Feature Placement Rules") so the three documents never contradict each other.

