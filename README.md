# IconGrid

IconGrid is a lightweight WPF launcher for Windows with draggable shortcuts, flexible tabs/categories, and real‑time system monitoring in the title bar. It adapts to the current Windows theme, accent, and even taskbar color so icons, tabs, toggles, and accent brushes always match the OS palette. There is also a built-in developer overlay that explains every UI element via `DevInspector.Metadata`, so contributors can hover over most controls to learn the responsible View/VM methods.

## Quick overview
- **Interface:** Top bar with category tabs, quick “More” menu for layouts/settings/help, and a scrollable grid of shortcuts. Right-click the grid to add new shortcuts (Windows/PowerShell variants, “Add all Windows shortcuts”, clear current category, etc.).
- **Monitor strip:** Shows ping (`Net`), download/upload rates, and CPU/GPU temperatures. Colors follow the light/dark theme and ping values automatically highlight in amber/red once thresholds are breached.

## Hardware Overvågning & Temperaturmåling
Dette projekt benytter [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) til at læse sensorværdier.

- **Dynamisk Sensoraflæsning:** For at sikre kompatibilitet med en bred vifte af hardware (herunder Intel N150 SoC og andre moderne arkitekturer), benytter applikationen en dynamisk tilgang. I stedet for at lede efter specifikke sensornavne, scanner `HardwareSnapshotCollector` alle tilgængelige sensorer af typen `SensorType.Temperature` og vælger den højeste værdi for henholdsvis CPU og GPU.
- **Opdatering af biblioteket:**
    1. Opdater `Version` i `IconGrid.csproj` under `<PackageReference Include="LibreHardwareMonitorLib" ... />`.
    2. Kør `dotnet restore` efterfulgt af `dotnet build`.
    3. Test altid på den specifikke hardware (især hvis det er SoC-arkitektur), da sensor-mapping kan variere.
- **Krav:** Da temperaturmåling kræver adgang til hardware-registre via PawnIO/LibreHardwareMonitor, skal applikationen køres med administratorrettigheder for at fungere korrekt.

## Hero card design and theming
- The settings/startside hub is composed with `TemplatePage` and `StartsideSectionCardStyle` (defined in `Views/StartsideStyles.xaml`). That style sets `CornerRadius=6`, `Padding=28`, `Margin=0,0,0,5`, and inherits from the shared `StartsideCardStyle`.
- Card backgrounds swap between `StartsideCardFill=#323232` for dark theme and `StartsideLightCardFill=#F3F4F7` for light theme via the `IsLightTheme` `DataTrigger`.
- Typography is aligned with the rest of the UI: hero titles bind to `TopBarForeground` and supporting text binds to `SettingsSubtextForeground`.

## Help & Tips
1. **Drag & drop rules:** Dragging shortcuts requires IconGrid to run without administrator privileges. UIPI prevents Explorer from sending drag messages to a high-privileged window.
2. **CPU temperature visibility:** Requires elevation via admin rights and PawnIO installation.
3. **“More” menu:** Hosts Settings, Layouts, Help, and About.

## Key features
- Drag-and-drop `.lnk`/`.exe` files directly to the active category.
- Theme-sync with Windows through `ThemeHelper`.
- Customizable UI state managed by `MainViewModel`.
- Layout system for arranging windows by preset.
- Developer overlay for surfacing metadata and binding contexts.

## Project structure
- `App.xaml.cs`: Bootstraps the application, ensures DPI-awareness.
- `Models/LauncherItem.cs`: Represents each shortcut.
- `ViewModels/MainViewModel.cs`: Holds tab/category state, command implementations.
- `Views/MainWindow.xaml(.cs)`: Hosts the entire UI.
- `Helpers/`: `ThemeHelper`, `DynamicIconHelper`, `ShortcutHelper`, `DevInspector`.
- `Assets/`: App icons and artwork.
- `Controls/`: Custom WPF controls.

## Build & run
1. Install the .NET SDK (targets `net10.0-windows`).
2. From the repo root: `dotnet build IconGrid.sln -c Debug`.
3. Config, shortcut lists, and logs persist under `%APPDATA%\IconGrid`.

## Developer notes
- Dev overlay metadata strings are intentionally detailed—extend them when you add new UI paths.
- Product version is driven by `AssemblyInfo.cs`.
- **DPI & elevation:** Manifest requests `requireAdministrator` plus `PerMonitorV2` awareness.
- Always create a timestamped backup under `backups/` when modifying files.