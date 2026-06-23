# IconGrid: En letvægts launcher med hardware monitor

![IconGrid screenshot](Assets/git-img/IconGrid.png)

IconGrid is a lightweight WPF launcher for Windows with draggable shortcuts, flexible tabs/categories, and real‑time system monitoring in the title bar. Det tilpasser sig automatisk Windows-temaet, accentfarver og proceslinjens farve. Projektet indeholder et indbygget udvikler-overlay (`DevInspector.Metadata`), der gør det muligt at hover over UI-elementer for at se deres binding-kontekst.

## Quick overview
- **Interface:** Top bar med kategorier, "Mere"-menu til layouts/indstillinger, og et scrollbart grid af genveje.
- **Monitor strip:** Viser ping (`Net`), download/upload-hastigheder og CPU/GPU temperaturer.

## Hardware Overvågning & Temperaturmåling
Dette projekt benytter [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) til at læse sensorværdier.

- **Dynamisk Sensoraflæsning:** `HardwareSnapshotCollector` scanner dynamisk efter `SensorType.Temperature` og vælger højeste værdi.
- **Krav:** Kræver administratorrettigheder (PawnIO/LibreHardwareMonitor).

## Custom Controls (/Controls)
Genbrugelige UI-komponenter til standardiserede elementer. Disse filer befinder sig i mappen /Controls:

LauncherLogo.xaml / .cs: Viser applikationens logo (3x3 dot grid) med dynamisk accent-farve.

SliderRow.xaml / .cs: Kombineret label-slider-værdi komponent til brug i indstillings-menuer og layout-konfiguratorer.

## Helper Services
Nedenfor er en oversigt over hjælpeklasserne i `/Helpers`, opdelt efter deres ansvarsområde:

### 1. Hardware & System Monitor
- **HardwareInfoProvider.cs:** Læser hardware-info (CPU/GPU/RAM) via WMI og LibreHardwareMonitor.
- **HardwareMonitorAgent.cs:** Singleton, der kører hardware-opsamling i baggrunden.
- **HardwareMonitorSnapshot.cs:** Dataklasse for hardware-tilstand.
- **HardwareSnapshotCollector.cs:** Opsamler rå sensordata til snapshots.
- **SystemMonitor.cs:** Overordnet monitorering af netværk og hardware-pipeline.
- **StartupTaskManager.cs:** Håndterer applikationens autostart via `schtasks`.

### 2. UI & Converters
- **BooleanToAngleConverter.cs:** Mapper `true/false` til 180°/0°.
- **BoolToBrushConverter.cs:** Mapper boolean til farve-pensler.
- **BoolToVisibilityConverter.cs:** Mapper boolean til `Visibility.Visible/Collapsed`.
- **InverseBoolConverter.cs:** Inverterer boolean-værdier.
- **InverseBoolToVisibilityConverter.cs:** Inverterer logikken for Visibility.
- **PercentToHeightConverter.cs:** Konverterer procent til pixel-højde.
- **ScrollButtonsVisibilityConverter.cs:** Styrer synlighed af scroll-knapper.
- **LayoutSlotLabelConverter.cs:** Sætter labels på layout-slots.
- **PingSeverityToBrushConverter.cs:** Mapper ping-status til farver (grøn/orange/rød).
- **SelectedTabMatchConverter.cs:** Sammenligner tabs for valg-logik.
- **TabNameLocalizationConverter.cs:** Håndterer lokaliserings-nøgler for tabs.

### 3. Icons, Shell & Resources
- **DynamicIconHelper.cs:** Genererer dynamiske ikoner og konverterer dem til `System.Drawing.Icon`.
- **EmbeddedIconLibrary.cs:** Konstante værdier for indbyggede Windows-genvejsikoner.
- **IconHelper.cs:** Hjælpeværktøjer til `desktop.ini`, Base64-konvertering og shell-ikoner.
- **IconResourceUpdater.cs:** Opdaterer applikationens indlejrede ikon-ressourcer i EXE-filen.
- **ShellIconLabel.cs:** WinForms-kontrol til tegning af ikonteknst i transparente vinduer.
- **ShellIconTextBlock.cs:** WPF-element til tegning af tema-baseret ikonteknst.

### 4. Utilities & Infrastructure
- **ConfigManager.cs:** Central konfigurations-håndtering (JSON-læsning/skrivning).
- **DevInspector.cs:** Attached property til debugging og metadata-injektion.
- **LocalizationHelper.cs:** Håndterer sprog-filer (Engelsk/Dansk).
- **ShortcutHelper.cs:** Hjælper med at opløse og validere genveje (`.lnk`/`.exe`).
- **ThemeHelper.cs:** Synkroniserer appens tema med Windows.

## Views & UI Layer
Denne mappe indeholder alle XAML-grænseflader og deres tilhørende logik.

- **Main Views:**
    - `MainWindow.xaml`: Projektets hovedcontainer og vindueshus.
    - `SettingsWindow.xaml`: Centralt kontrolpanel for applikationens indstillinger.
- **Pages:**
    - `StartsidePage.xaml`: Konfiguration af systemopstart, UI-skalering og overlay-indstillinger.
    - `HardwarePage.xaml`: Detaljeret visning af system-hardware (CPU, GPU, RAM).
    - `GenvejsIkonerPage.xaml`: Visning og håndtering af software-genveje.
    - `LayoutPage.xaml`: Værktøj til at konfigurere layout af ikoner og presets.
    - `HjaelpPage.xaml`: Dokumentation og tastaturgenveje for brugeren.
    - `AboutPage.xaml`: Versionsoplysninger og licensinformation.
- **Components:**
    - `TemplatePage.xaml` & `TemplateGuidelines.xaml`: Definerede design-standarder og delkomponenter.
    - `PawnIoWarningWindow.xaml`: Dialogvindue til håndtering af Pawn.io-relaterede advarsler.

## Key features
- Drag-and-drop genveje.
- Tema-synkronisering.
- Layout-system til presets.
- Udvikler-overlay.

## Project structure
- `App.xaml.cs`: Bootstraps applikationen.
- `ViewModels/MainViewModel.cs`: Overordnet UI-state og kommandoer.
- `Views/`: Alle XAML-views.
- `Helpers/`: Hjælpe-services og convertere (opdelt i undermapper for bedre overblik).

## Developer Notes
- Projektet kører i et Windows PowerShell-miljø.
- Alle lokale AI-interaktioner skal overholde reglerne i `.clinerules`.

## Build & run
1. Installer .NET SDK (targets `net10.0-windows`).
2. Kør `dotnet build IconGrid.sln -c Debug` fra roden.
3. Konfiguration og logs gemmes under `%APPDATA%\IconGrid`.