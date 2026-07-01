# IconGrid TODO

## Status

- Floating icon refactor phase 1 er færdig
- `FloatingIconButton` er udskilt fra `MainWindow`
- `FloatingIconController` er udskilt fra `MainWindow`
- `LauncherTopBar` er udskilt fra `MainWindow`
- README er opdateret med vigtige `Helpers`

## Næste fokus

- Start refactor af `ViewModels/MainViewModel.cs` i små, sikre faser
- Holde hardware/temperature-forskellen som separat spor

## MainWindow refactor

### Fase 2: Main Launcher Interface

- [x] Udskil `TopBar` fra `Views/MainWindow.xaml`
- [x] Flyt logo-område til separat control
- [x] Flyt monitor-row (`SystemMonitor`) til separat control
- [x] Flyt minimize/close-knapper til separat control eller topbar-del
- [x] Afklar om `...`-knappen skal omdøbes fra `More` til `Indstillinger`
- [ ] Behold åbning af `SettingsWindow`, men gør triggeren mere modulær

### Fase 2 noter

- [x] Fix startup resource-scope i `LauncherTopBar`
- [x] Fix defensiv shutdown når startup fejler tidligt
- [x] `LauncherLogoArea` er oprettet, så layout/logo-adfærden er adskilt fra resten af topbaren
- [x] `LauncherMonitorRow` er oprettet, så live monitor-strippen er adskilt fra resten af topbaren
- [x] `LauncherWindowButtons` er oprettet, så minimize/close-knapperne er adskilt fra resten af topbaren

### Fase 3: Kategori-navigation

- [x] Udskil kategori-tabs til separat control
- [x] Flyt rename/remove category menu ud med tab-komponenten
- [x] Flyt `+`-knappen til samme kategori-komponent
- [x] Flyt pil-knapper/scroller-logik hvis den hører til tabs
- [x] Flyt `More`-knappen ind i tabs-komponenten

### Fase 3 noter

- [x] `LauncherTabsBar` er oprettet som separat control
- [x] Tab-rækkens UI er flyttet ud af `MainWindow.xaml`
- [x] Tab-adfærden går stadig via `MainWindow.xaml.cs` for en sikker trinvis refactor
- [x] Add/rename/remove category-logik kører nu direkte i `LauncherTabsBar`
- [x] `More` er omdøbt i UI til `Indstillinger`
- [x] Overflødige tab-handlers er fjernet fra `MainWindow.xaml.cs`

### Fase 4: Launcher-område

- [x] Udskil selve ikon-området til separat control
- [x] Udskil launcher-ikon item template/control
- [x] Flyt højreklik-menu på genvej til launcher-komponenten
- [x] Flyt højreklik-menu på ikon-området til launcher-komponenten

### Fase 4 noter

- [x] `LauncherGrid` er oprettet som separat control
- [x] Launcher-gridets UI er flyttet ud af `MainWindow.xaml`
- [x] Drag/drop og genvejsmenu-events forwardes stadig til `MainWindow.xaml.cs`
- [x] Genvejsmenuens labels bindes nu korrekt i `LauncherGrid`
- [x] Empty-state teksten i `LauncherGrid` er rettet til korrekt dansk

### Fase 5: `MainViewModel` oprydning

- [x] Fase 5A: Udskil tab/category-relateret state og logik
- [x] Fase 5B: Afslut restoprydning efter udskilning af launcher item-operations og shortcut-logik
- [x] Fase 5B.1: `LauncherItemsManager` håndterer nu category/item-mutationer
- [x] Fase 5B.2: `LauncherItemIconManager` håndterer nu ikon-opdatering og ikon-fallback
- [x] Fase 5B.3: `LauncherShortcutManager` håndterer nu file-drop og custom shortcut-oprettelse
- [x] Fase 5C: Afslut restoprydning efter udskilning af layout-state og saved-layout logik
- [x] Fase 5C.1: `LauncherLayoutState` håndterer nu layout-state, saved layouts og layout-links
- [x] Fase 5D: Afslut restoprydning efter udskilning af settings/config persistence
- [x] Fase 5D.1: `LauncherItemsPersistence` håndterer nu items save/load/migrering
- [x] Fase 5D.2: settings-save er flyttet til `MainViewModelSettingsPersistence`
- [x] Fase 5E: Udskil theme/appearance state
- [x] Fase 5F: Udskil localization-labels og relateret hjælpe-logik

### Fase 5 noter

- [x] `MainViewModel.cs` er analyseret og opdelt i konkrete refactor-faser før kodeændringer
- [x] Første anbefalede startpunkt er tab/category-logik, fordi `LauncherTabsBar` allerede er modulariseret
- [x] `LauncherTabsState` er oprettet som første split af tab-state fra `MainViewModel`
- [x] `CurrentItems` og current-category filtrering bruger nu fælles hjælpe-metoder i `MainViewModel`
- [x] `ClearCurrentCategory`, `RemoveItem`, `RenameItem` og `MoveItemWithinCategory` delegérer nu til `LauncherItemsManager`
- [x] `MainViewModel` delegérer nu `Tabs`, `SelectedTab`, `AddTab`, `RenameTab` og `RemoveTab` til tabs-state
- [x] `UpdateItemIcon` og ikon-fallback er flyttet til `LauncherItemIconManager`
- [x] `HandleFileDrop` bruger nu fælles ikon-initialisering via `LauncherItemIconManager`
- [x] `HandleFileDrop` og `CreateCustomShortcut` delegérer nu til `LauncherShortcutManager`
- [x] `LaunchItem` delegérer nu til `LauncherItemLaunchManager`
- [x] Restvurdering afsluttet: launcher item action-flow ligger ikke længere i `MainViewModel`, så der er ikke et yderligere split at tage her
- [x] Items save/load/migrering er flyttet til `LauncherItemsPersistence`
- [x] `SaveSettingsToConfig` bygger nu et state-snapshot og delegérer save til `MainViewModelSettingsPersistence`
- [x] Layout preset, saved layouts, favorite layout og layout-links delegérer nu til `LauncherLayoutState`
- [x] Layout-mutationer, defaults-reset og settings-snapshot delegérer nu primært til `LauncherLayoutState`
- [x] Layout-related property notifications i `MainViewModel` bruger nu fælles hjælpe-metoder i stedet for gentagne notify-blokke
- [x] Restvurdering afsluttet: overlay-/layout-triggerlogik er nu reduceret til små fælles hjælpe-metoder i `MainViewModel`
- [x] Overlay-state (`IsSettingsOpen`, `IsLayoutsOpen`, `IsHelpOpen`) delegérer nu til `LauncherOverlayState`
- [x] Overlay-property notifications og content-height refresh bruger nu fælles hjælpe-metoder i `MainViewModel`
- [x] Tab-skifte og row-spacing/last-row-height notifications bruger nu fælles content-height helpers i `MainViewModel`
- [x] Layout property-/collection-mutationer bruger nu fælles persist-hjælpere i `MainViewModel`
- [x] Restvurdering afsluttet: `load/apply-config/startup` er nu delt i små orkestrerende trin, og yderligere split vurderes ikke at give reel værdi
- [x] Basis config-oversættelse fra `ConfigModel` delegérer nu til `MainViewModelConfigState`
- [x] Konstruktørens startup-sekvens er opdelt i små init-metoder, så config-load, appearance, managers, commands og persisted item-load er tydeligt separeret
- [x] `ApplyConfig` bruger nu direkte state-assignments for language/config-load i stedet for at trigge property-setter sideeffekter under init
- [x] `ResetSettingsToDefaults` bruger nu et samlet default-state/apply-flow i stedet for en lang kæde af property-setter sideeffekter
- [x] Theme-state og theme-brushes delegérer nu til `LauncherThemeState`
- [x] `ThemeHelper` subscription og current-theme opslag delegérer nu til `LauncherThemeCoordinator`
- [x] Localization-lookups og PawnIO-tekster delegérer nu til `LauncherLocalizationState`
- [x] `Language`-flow og localization-notifications bruger nu fælles lokalization-koordinering i `MainViewModel`

### Fase 6: Struktur-oprydning

- [x] Planlæg undermapper for `Launcher`-relaterede controls/views/helpers
- [x] Planlæg undermapper for `Settings`-relaterede views/pages/helpers
- [ ] Udfør mappeflytninger først efter stabil `MainViewModel`-refactor

### Fase 6 plan

- `Controls/Launcher/`
- Flyt `LauncherGrid`, `LauncherLogoArea`, `LauncherMonitorRow`, `LauncherTabsBar`, `LauncherTopBar` og `LauncherWindowButtons` hertil
- [x] Næste flyttefase gennemført: launcher-controls er flyttet til `Controls/Launcher/`
- Behold `Controls/Floating/` som næste naturlige hjem for `FloatingIconButton`
- [x] Næste flyttefase gennemført: `FloatingIconButton` er flyttet til `Controls/Floating/`
- Behold `Controls/Common/` som næste naturlige hjem for `SliderRow` og evt. `LauncherLogo`, hvis den fortsat bruges bredt
- [x] Næste flyttefase gennemført: `SliderRow` og `LauncherLogo` er flyttet til `Controls/Common/`

- `Views/Launcher/`
- Behold `MainWindow` som launcher-shell og flyt det hertil sammen med evt. launcher-specifikke dialoger, hvis de opstår senere

- `Views/Settings/Pages/`
- Flyt `AboutPage`, `GenvejsIkonerPage`, `HardwarePage`, `HjaelpPage`, `LayoutPage`, `StartsidePage` og `TemplatePage` hertil
- [x] Næste flyttefase gennemført: settings-sider er flyttet til `Views/Settings/Pages/`
- Behold `Views/Settings/SettingsWindow.xaml` som settings-shell
- Behold `Views/Settings/Dialogs/` som næste naturlige hjem for `PawnIoWarningWindow`, hvis den fortsat kun hører til settings-flowet

- `ViewModels/Launcher/`
- Flyt `LauncherTabsState`, `LauncherItemsManager`, `LauncherItemIconManager`, `LauncherItemLaunchManager`, `LauncherShortcutManager`, `LauncherItemsPersistence`, `LauncherLayoutState`, `LauncherOverlayState`, `LauncherThemeState`, `LauncherThemeCoordinator` og `LauncherLocalizationState` hertil
- [x] Næste flyttefase gennemført: launcher-viewmodels er flyttet til `ViewModels/Launcher/`
- Behold `MainViewModel` i roden af `ViewModels/`, så launcherens samlede entry-viewmodel stadig er let at finde

- `ViewModels/Settings/`
- Flyt `MainViewModelSettingsState`, `MainViewModelSettingsPersistence` og `MainViewModelConfigState` hertil, fordi de nu primært beskriver settings/config-flow
- [x] Næste flyttefase gennemført: settings-viewmodels er flyttet til `ViewModels/Settings/`

- `Helpers/Launcher/`
- Flyt `FloatingIconController`, `DynamicIconHelper`, `IconHelper`, `IconResourceUpdater`, `ShortcutHelper`, `ShellIconLabel`, `ShellIconTextBlock`, `SystemMonitor` og evt. `EmbeddedIconLibrary` hertil
- [x] Næste flyttefase gennemført: launcher-helpers er flyttet til `Helpers/Launcher/`

- `Helpers/Settings/`
- Flyt `LocalizationHelper`, `PawnIoHelper`, `ThemeHelper`, `StartupTaskManager` og `ConfigManager` hertil hvis de fortsat primært bruges af settings/configuration
- [x] Næste flyttefase gennemført: settings-helpers er flyttet til `Helpers/Settings/`

- `Helpers/Hardware/`
- Flyt `HardwareInfoProvider`, `HardwareMonitorAgent`, `HardwareMonitorSnapshot` og `HardwareSnapshotCollector` hertil som separat spor før eventuel hardware-refactor
- [x] Næste flyttefase gennemført: hardware-helpers er flyttet til `Helpers/Hardware/`

- `Helpers/Converters/`
- Saml UI-converters her: `BooleanToAngleConverter`, `BoolToBrushConverter`, `BoolToVisibilityConverter`, `InverseBoolConverter`, `InverseBoolToVisibilityConverter`, `LayoutSlotLabelConverter`, `LayoutSlotMatchConverter`, `PercentToHeightConverter`, `PingSeverityToBrushConverter`, `ScrollButtonsVisibilityConverter`, `SelectedTabMatchConverter`, `TabNameLocalizationConverter` og `TemplateContentWidthConverter`
- [x] Første flyttefase gennemført: UI-converters er flyttet til `Helpers/Converters/`

- `Helpers/Common/`
- Behold `RelayCommand`, `DevInspector` og `UpdateVisitor` her eller flyt dem til en fælles mappe, da de ikke er launcher- eller settings-specifikke

## Hardware TODO

### Observation

- Launcher topbar viser live `CPU`/`GPU` temperaturer
- `SettingsWindow` → `Hardware` side viser kun hardware-information
- Begge områder bruger relateret hardware-infrastruktur (`LibreHardwareMonitor` / `PawnIO`)

### Afklaring

- [ ] Kortlæg hvor launcher-topbaren får live temp-data fra
- [ ] Kortlæg hvilke services/models `Hardware`-siden bruger nu
- [ ] Beslut om `Hardware`-siden også skal vise live temperaturer
- [ ] Beslut om launcher og hardware-side skal dele samme monitor-service
- [ ] Undersøg om `PawnIO`-krav/admin-flow skal vises tydeligere i `SettingsWindow`

## Dokumentation

- [ ] Hold `README.md` opdateret efter hver større refactor-fase
- [ ] Dokumentér nye controls/helpers når de bliver arkitektonisk vigtige

## Arbejdsregel

- Lav små sikre refactors
- Build efter hver større fase
- Manuel test af UI-flow efter hver større fase
- Commit/push ved stabile checkpoints
