# IconGrid TODO

## Status

- Floating icon refactor phase 1 er færdig
- `FloatingIconButton` er udskilt fra `MainWindow`
- `FloatingIconController` er udskilt fra `MainWindow`
- README er opdateret med vigtige `Helpers`

## Næste fokus

- Refactor `Main Launcher Interface` i mindre moduler
- Holde hardware/temperature-forskellen som separat spor

## MainWindow refactor

### Fase 2: Main Launcher Interface

- [ ] Udskil `TopBar` fra `Views/MainWindow.xaml`
- [ ] Flyt logo-område til separat control
- [ ] Flyt monitor-row (`SystemMonitor`) til separat control
- [ ] Flyt minimize/close-knapper til separat control eller topbar-del
- [ ] Afklar om `...`-knappen skal omdøbes fra `More` til `Indstillinger`
- [ ] Behold åbning af `SettingsWindow`, men gør triggeren mere modulær

### Fase 3: Kategori-navigation

- [ ] Udskil kategori-tabs til separat control
- [ ] Flyt rename/remove category menu ud med tab-komponenten
- [ ] Flyt `+`-knappen til samme kategori-komponent
- [ ] Flyt pil-knapper/scroller-logik hvis den hører til tabs

### Fase 4: Launcher-område

- [ ] Udskil selve ikon-området til separat control
- [ ] Udskil launcher-ikon item template/control
- [ ] Flyt højreklik-menu på genvej til launcher-komponenten
- [ ] Flyt højreklik-menu på ikon-området til launcher-komponenten

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
