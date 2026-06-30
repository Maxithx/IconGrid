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

- [ ] Fase 5A: Udskil tab/category-relateret state og logik
- [ ] Fase 5B: Udskil launcher item-operations og shortcut-logik
- [ ] Fase 5C: Udskil layout-state og saved-layout logik
- [ ] Fase 5D: Udskil settings/config persistence
- [ ] Fase 5E: Udskil theme/appearance state
- [ ] Fase 5F: Udskil localization-labels og relateret hjælpe-logik

### Fase 5 noter

- [x] `MainViewModel.cs` er analyseret og opdelt i konkrete refactor-faser før kodeændringer
- [x] Første anbefalede startpunkt er tab/category-logik, fordi `LauncherTabsBar` allerede er modulariseret
- [x] `LauncherTabsState` er oprettet som første split af tab-state fra `MainViewModel`
- [x] `CurrentItems` og current-category filtrering bruger nu fælles hjælpe-metoder i `MainViewModel`
- [x] `MainViewModel` delegérer nu `Tabs`, `SelectedTab`, `AddTab`, `RenameTab` og `RemoveTab` til tabs-state

### Fase 6: Struktur-oprydning

- [ ] Planlæg undermapper for `Launcher`-relaterede controls/views/helpers
- [ ] Planlæg undermapper for `Settings`-relaterede views/pages/helpers
- [ ] Udfør mappeflytninger først efter stabil `MainViewModel`-refactor

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
