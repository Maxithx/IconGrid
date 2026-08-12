# IconGrid — Short-term Memory

> **Full session history:** `.local-state/session-history.md` (gitignored, searchable via `search_notes`)

## Current date
Saturday, August 9, 2026 → Sunday, August 10, 2026

## Current status
- Monitor Row Layout-siden er færdig: alle 18 slider/toggle-properties justerbare med full persistence.
- CPU/GPU bar gaps defaults sænket til 0, hardcoded Widths fjernet, value-width lock TextAlignment fixet.
- "Save current as default" / "Reset to my default" knapper implementeret.
- Alle tekster på MonitorRowLayoutPage er nu da/en-lokaliserede (første fuldt centraliserede lokaliserede side).
- MCP-server opgraderet til 12 værktøjer inkl. `run_all_checks`, `check_localization_completeness`, `check_git_security`, `check_xaml_hardcoded_danish`.
- AGENT.md opdateret med localization design pattern (4-step guide) + architecture rules link.
- `.clinerules` opdateret: kræver `run_all_checks` ved session-afslutning.
- Memory: CHAT_STATE.md = korttidshukommelse. `.local-state/session-history.md` = index. `.local-state/sessions/*.md` = per-date logs (max ~500 linjer).

## Architecture status
- `run_all_checks`: 3 pre-existing VIOLATIONs — MainWindow 1062, MainViewModel 1840, HardwareMonitorAgent 1772. Alle dokumenterede tolerancer.
- Version: 0.7.0-beta.1 (konsistent)
- Localization: 168 en / 168 da — synkroniseret
- Security: ingen staged secrets
- XAML: 6 pre-existing hardcoded Danish linjer

## Working tree status
- **Alt committed og pushet** (2 commits: `4c779aa` + `90dbe81`)
- Build: 0 fejl, 0 advarsler
- Deploy: C:\IconGrid

## Good next steps
- Opdater ARCHITECTURE_RULES.md "Recent Good Examples" med MonitorRowLayoutPage + MonitorLayoutDefaultsSnapshot (valgfrit polish)
- Evt. lokaliser de 6 resterende hardcodede XAML-strenge


- AFVENTER BRUGER-VERIFIKATION (2026-08-11): Fast USB Copy-siden (settings → 'USB Copy') — test enhedsdetektion + port-type, kopiering og benchmark. Når godkendt: commit + push (spørg brugeren først, jf. AGENT.md).
- Evt. forbedringer: IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX for eksakt negotiated port-linkspeed (i stedet for controller-heuristic), benchmark-resultat-kurve (buffer vs throughput) i UI.

## Latest commits (this session)
- `4c779aa` — feat: monitor row layout localization, save-as-default, MCP server architecture enforcer, and two-part memory structure (15 files)
- `90dbe81` — fix: persist monitor row layout settings and stabilize download/upload value widths (4 files)

- `7ac327a` — feat: live CPU voltage on HardwarePage, fixed Game Resolution dropdown alignment + cards, visible card borders in dark theme (8 files, 279 insertions / 79 deletions) — pushet til GitHub.

- `053bef5` — fix: rename 'Language and region' to 'Language' on the start page (2 files) — pushet til GitHub.

- `633e84d` — feat: make Element gaps, Dividers and CPU/GPU bars collapsible on Monitor Row Layout page (2 files, 152 insertions / 16 deletions) — pushet til GitHub.

- `18bfaab` — feat: monitor row layout tuned defaults + 3-button reset/save design (11 files, 134 insertions / 93 deletions) — pushet til GitHub.

- `4814666` — feat: shortcut icons grid/carousel view settings split + visible icons slider + 82% icon floor + page scrollbar (13 files, 250 insertions / 51 deletions) — pushet til GitHub. Inkluderer README.md opdatering.

## Session findings (2026-08-10)

- HardwarePage.xaml: ALLE 4 hero-cards (Motherboard, CPU, GPU, Memory) gjort collapsible med Expander (samme mønster som GamingOverlayPage).
  - 3-kolonne header: Title (*) | Logo (Auto, SharedSizeGroup) | Chevron (Auto)
  - `Grid.IsSharedSizeScope="True"` + `SharedSizeGroup="HardwareLogoColumn"` — perfekt vertikal logo-alignment
  - Nøgle-info synlig i header ved foldet tilstand:
    - Motherboard: Board model
    - CPU: Modelnavn @ Live clock (MultiBinding)
    - GPU: Modelnavn
    - Memory: Layout Type @ Speed (MultiBinding)
  - Intro-tekst flyttet fra header til Content (kun synlig når ekspanderet)
  - Logo-størrelser: ASUS 80×34, AMD/Intel/NVIDIA 100×34, alle `Stretch=Uniform` + `HorizontalAlignment=Center`
  - Styles: `HardwareHeroExpanderStyle`, `HardwareHeroExpanderToggleButtonStyle`. Bruger eksisterende `BooleanToAngleConverter`.
- Build: 0 fejl, 0 advarsler. Deploy: C:\IconGrid.
- Commit: `9087441` — pushed til GitHub.

- CPU card på HardwarePage: tilføjet live Voltage tile (Spænding). Vises via DataContext.SystemMonitor.CpuVoltage (fx "1.181 V" for undervoltet CPU).
- Ny data-flow: HardwareSnapshotCollector.CaptureCpuVoltage() → HardwareMonitorSnapshot.CpuVoltage → monitor-state.json → SystemMonitor.CpuVoltage → HardwarePage.xaml.
- Sensor-prioritet i CaptureCpuVoltage: VID > Vcore/Core Voltage > "CPU ... Voltage" > "Voltage". Formateret F3 + " V".
- Genbrugte eksisterende lokaliseringsnøgle HardwareVoltageLabel (en: Voltage, da: Spænding) — ingen nye nøgler nødvendigt.
- Build: 0 fejl. check_architecture_rules: ingen nye violations (kun de 3 kendte).

- CPU voltage: CaptureCpuVoltage hærdet med motherboard-fallback — Vcore på AMD ligger typisk på Motherboard-noden (Super I/O), ikke CPU-noden. Prioritet: VID > Vcore/Core Voltage > "CPU ... Voltage" > "Voltage" (kun CPU-relevante navne filtreres på board-node).
- FormatVoltage øget til F4 (fx "1.1813 V") for at vise undervolt-præcision.
- Deployet via deploy-test.cmd til C:\icongrid + IconGrid genstartet — afventer bruger-verifikation af Spænding-tile på CPU-kortet.

- ✅ BRUGER-VERIFICERET (03:32): CPU-volten vises nu korrekt på HardwarePage CPU-kort (“Spænding”-tile). Løsning: motherboard-fallback + F4-præcision + deploy til C:\icongrid.

- Margin-fix: Spænding-tilen på CPU-kortet fik `Margin="0,10,0,0"` for luft over den på sin egen WrapPanel-række. Build + deploy + restart udført.

- Alignment-fix: GameResolutionPage (Game resolution i GamingOverlayPage) — Category/spil/External games dropdowns deler nu SharedSizeGroup="GameResComboBoxColumn" via Grid.IsSharedSizeScope=True på roden. External games' slet-knap fik egen delt kolonne (GameResRemoveColumn). Dropdowns: MinWidth=160 + HorizontalAlignment=Stretch i stedet for hardcodede Width=180/160.
- Ny TemplateComboBoxStyle i StartsideStyles.xaml (erstatning for GamerResComboBoxStyle-duplikatet).
- TemplateGuidelines.xaml opdateret med Alignment-kontrakt: Grid.IsSharedSizeScope ved roden, SharedSizeGroup på kontrol-kolonner, action-knapper i egen delt kolonne, MinWidth+Stretch i stedet for fixed Width, TemplateComboBoxStyle. Reference: GameResolutionPage.
- Review: GenvejsIkonerPage/MonitorRowLayoutPage/LayoutPage bruger allerede konsistente mønstre (SliderRow, ToggleSwitchStyle, TemplatePage).
- Build 0 fejl, deployet til C:\icongrid + IconGrid startet — afventer bruger-verifikation af alignment.

- Game Resolution dropdowns (KUN den sektion, per bruger-besked): nu fast fælles grid. TemplateComboBoxStyle fik fast Width=MinWidth=220 (SettingsDropdownWidth), Height=MinHeight=32, VerticalContentAlignment=Center. Alle 3 dropdowns (Category, spil, External games) bruger TemplateComboBoxStyle + samme Margin=12,0,0,0 + SharedSizeGroup=GameResComboBoxColumn. Slet-knap i egen delte kolonne — dropdowns kan aldrig flytte sig.
- KUN GameResolutionPage + StartsideStyles ændret (LayoutPage/GamingOverlayPage/StartsidePage dropdowns rørt IKKE).
- Build 0 fejl, deployet C:\icongrid, IconGrid startet — afventer bruger-verifikation.

- X-knap alignment fikset: ALLE tre grids (Category, spil-liste, External games) har nu samme 3-kolonne-struktur med GameResRemoveColumn (tom i Category/spil) — SharedSizeGroup tvinger tom kolonne til X-knappens bredde (12px margin + 28px knap = 40px). Dropdowns uden X-knap får dermed præcis samme højre-plads som dem med, og ingen dropdown skubbes. Deployet C:\icongrid, IconGrid startet.

- Game Resolution opdelt i egne cards: Category-filter fik eget StartsideSectionCardStyle card (Padding 16, samme som spil/External cards) så Category-dropdownen nu aligner X-mæssigt med spil- og External-dropdowns. External games-titlen flyttet ind i sit eget card (titel + liste i samme Border, Padding 16). Alle tre grids deler stadig GameResComboBoxColumn + GameResRemoveColumn via SharedSizeGroup. Names ExternalGamesSection + ExternalGamesTitle bevaret (code-behind afhænger af dem). Build 0 fejl, deployet C:\icongrid, IconGrid startet.

- Mørk-tema fix: StartsideSectionCardStyle fik nu default BorderThickness=1 + BorderBrush=#3A3A3A (mørk), så ALLE cards (inkl. de nye Game Resolution cards) har synlig ramme i mørkt tema. Lyst tema skifter stadig til #D1D5DB. Gælder app-wide da stilen er delt. Build 0 fejl, deployet C:\icongrid, IconGrid startet.

- Startside: 'Language and region' → 'Language' (en). DK er allerede 'Sprog'. Lokaliseringsnøgle LanguageSectionTitle ændret i LocalizationHelper.cs. Build 0 fejl, deployet C:\icongrid, IconGrid startet.

- Monitor Row Layout: de 3 cards (Element Gaps, Dividers, CPU/GPU bars) er nu collapsible expanders med chevron (samme mønster som GamingOverlay/Hardware): MonitorRowExpanderStyle + MonitorRowExpanderAngleConverter. Titles er 16/SemiBold og indholdet er uændret. Build 0 fejl, deployet C:\icongrid, IconGrid startet.

- Deploy 05:29: HardwarePage card-overskrifter 20→16 deployet via deploy-test.cmd til C:\icongrid (DLL verificeret frisk). IconGrid startet 05:36.

- Deploy 05:45: Del 2 typografi-centralisering deployet via deploy-test.cmd til C:\icongrid (DLL verificeret frisk, 969728 bytes). IconGrid startet 05:45.
- Del 2 færdig: ALLE 7 settings-sider bruger nu Template*Style-styles fra TemplateGuidelines.xaml:
  - HardwarePage: card-titler + metrics (TemplateCardTitleStyle), header-subtitler (TemplateSectionTitleStyle), intro (TemplateHeroBodyStyle), labels (TemplateSmallTextStyle). Chevron(18)/white badges beholdt lokalt.
  - StartsidePage: hero (TemplateHeroTitleStyle), card-titler (TemplateCardTitleStyle), sektions-titler (TemplateSectionTitleStyle), intro (TemplateHeroBodyStyle), små tekster (TemplateSmallTextStyle).
  - GenvejsIkonerPage: hero + intro + animation card titel/intro centraliseret.
  - AboutPage, HjaelpPage, LayoutPage, TestPage: hero/card/section/body/small alle centraliseret.
- KONSISTENS-GEVINST: Én ændring i fx TemplateCardTitleStyle (16) i TemplateGuidelines.xaml → alle settings-sider følger med automatisk.
- Bevidst bevaret lokalt: Segoe Fluent Icons chevron (18), video-badges med Foreground=White (CPU/GPU/Memory), LAYOUT slot-knap style (16), TestPage buttons (14), ComboBox FontSize=14 (LayoutPage).
- Build: 0 fejl, 0 advarsler. check_architecture_rules: ingen nye violations (kun de 3 kendte). run_all_checks: alt OK (version 0.7.0-beta.1 konsistent, 168 en/168 da, ingen secrets, 6 pre-existing hardcoded danske linjer uændret).















## Næste session — PLAN & PROMPT (overskriftskonsistens + central typografi)

## Status lige nu (gemt 2026-08-10 05:31, session afsluttes)
- HardwarePage.xaml: alle 4 card-overskrifter (Motherboard, CPU, GPU, Memory) ændret FontSize 20 → 16. Build ✅ 0 fejl. MEN: **ikke deployet til C:\icongrid og ikke committet** (u-committed working tree).
- Seneste pushede commit: `633e84d`.

## Plan for næste session (en ad gangen, small steps)

### Del 1 — hardware-fix færdiggøres
1. Deploy til C:\icongrid (`& .\deploy-test.cmd`) + start IconGrid.
2. Manuel test: Hardware-siden skal nu have card-overskrifter i 16 (samme som GamingOverlay/Startside).
3. Commit + push når brugeren bekræfter: `git add Views/Settings/Pages/HardwarePage.xaml CHAT_STATE.md` → commit → push.

### Del 2 — Centraliser typografi (brugerens ønske: ændr ét sted → virker alle sider)
- Skift alle hardcodede FontSize på settings-siderne til de delte styles i `TemplateGuidelines.xaml`:
  - `TemplateHeroTitleStyle` (20, side-titel) — brugt af side-titler på Startside/GenvejsIkoner/Layout/About/Hjaelp/MonitorRowLayout.
  - `TemplateCardTitleStyle` (16, card-titel) — GamingOverlay bruger 16 på collapsible headers; Hardware bruger nu 16 (skal også bindes til style).
  - `TemplateSectionTitleStyle` (14), `TemplateHeroBodyStyle` (13), `TemplateSmallTextStyle` (12).
- Berørte filer (har stadig hardcodede FontSize): HardwarePage.xaml, StartsidePage.xaml, GenvejsIkonerPage.xaml, AboutPage.xaml, HjaelpPage.xaml, LayoutPage.xaml, TestPage.xaml.
- Når alle sider bruger styles, kan man ændre fx TemplateCardTitleStyle ét sted → hele appen følger med.

### Del 3 — Verifikation
- Build + deploy + manuel gennemgang af ALLE sider (overskrifter ens).
- `run_all_checks` + CHAT_STATE.md opdateres.

## FÆRDIG PROMPT til næste session (kopiér dette)
"Vi arbejder videre på overskriftskonsistens. Færdiggør Del 1: HardwarePage card-overskrifter er allerede ændret 20→16 i working tree (bygget 0 fejl, men ikke deployet/committet) — deploy, test, commit og push. Fortsæt derefter med Del 2: centraliser al settings-typografi ved at erstatte hardcodede FontSize med Template*Style-styles fra TemplateGuidelines.xaml på alle settings-sider (HardwarePage, StartsidePage, GenvejsIkonerPage, AboutPage, HjaelpPage, LayoutPage, TestPage), så én ændring i en delt style virker på alle sider. Afslut med Del 3: build, deploy, manuel gennemgang + run_all_checks."



## Session findings (2026-08-11)

- Monitor row layout: de 'gode' justeringer (Ping→Net 4, Net→Download 4, Down→Up 4, Up→CPU 4, CPU→GPU 4, DividerGap 16, CPU/GPU BarGap 8/8, label→value 4×2, value→unit 4×2, value-widths 20/20) er nu de nye fabriksdefaults.
- Opdateret alle 6 steder med hardcodede defaults: ConfigModel.cs, MonitorLayoutDefaultsSnapshot.cs, MainViewModel.cs (backing fields), MainViewModelConfigState.cs, MainViewModelSettingsState.cs, MonitorRowLayoutPage.xaml.cs (reset-fallback) + MainWindow.xaml.cs (legacy reset, dead code konsistens).
- Build: 0 fejl, 0 advarsler. Deployet via deploy-test.cmd til C:\icongrid (DLL verificeret frisk, 969728 bytes, 18:15). IconGrid startet.
- check_architecture_rules: ingen nye violations (kun de 3 kendte).
- IKKE committet/pushet — afventer bruger-approval.

- Knap-tekst rettet: 'Reset to my default' → 'Reset to default' (en) og 'Nulstil til min standard' → 'Nulstil til standard' (da) i LocalizationHelper.cs. Build 0 fejl, deployet C:\icongrid (18:23).
- Bruger overvejer 3 save-profiler for monitor row layout — svaret: foreslået 3 foruddefinerede presets (Kompakt/Normal/Luftig) i stedet for 3 brugerdefinerede lagrede profiler.

- Monitor Row Layout fik nyt 3-knap design efter brugerønske:
  1. **Reset to default** — ALTID fabriksværdier (kan ikke ændres af brugerens gemte default)
  2. **Reset to my default** — gendanner brugerens gemte layout (ny knap; falder tilbage til fabriksværdier hvis ingen er gemt)
  3. **Save current to my default** — gemmer nuværende som brugerens personlige default
  - Ny lokaliseringsnøgle: MonitorRowResetToMyDefaultButton (en: 'Reset to my default', da: 'Nulstil til min standard')
  - Opdateret MonitorRowSaveAsDefaultButton (en: 'Save current to my default', da: 'Gem nuværende som min standard') + ny description på begge sprog.
  - Filer ændret: LocalizationHelper.cs, MainViewModel.Localization.cs, MonitorRowLayoutPage.xaml (3. knap + Click=ResetToMyDefaultButton_Click), MonitorRowLayoutPage.xaml.cs (split reset-logik: ResetDefaultsButton_Click = altid fabrik, ResetToMyDefaultButton_Click = saved-snapshot med fallback).
  - Build 0 fejl, deployet C:\icongrid (18:29) — afventer bruger-verifikation.

- ✅ BRUGER-VERIFICERET + COMMITTET + PUSHEt (18:36): `18bfaab` — monitor row layout tuned defaults + 3-button reset/save design. 
- run_all_checks ved session-afslutning: Architecture 3 kendte violations (uændret), version 0.7.0-beta.1 konsistent, localization 169 en/169 da synkroniseret, ingen secrets, XAML 6 kendte hardcodede danske linjer (uændret).
- Working tree: ren (alt commit + push).

- GenvejsIkonerPage.xaml omstruktureret i tydelige sektioner efter brugerønske (Grid view vs Carousel view indstillinger):
  - Hero: Carousel-view toggle + Enable icon scroll toggle.
  - Card 1 'Shared': Icon size slider (fælles for BEGGE views — enig med bruger om at ikonstørrelse skal deles).
  - Card 2 'Grid view (vertical)': Icons per row, Icon row spacing, Bottom padding (last row) — tydeligt markeret som kun-vertikal. IconsPerRow er nu flyttet ind her (var i MainWindow.xaml settings overlay).
  - Card 3 'Carousel view (horizontal)': NY indstilling 'Visible icons' (1-12, default 4) — styrer hvor mange ikoner der er synlige i carousel-viewporten (= horisontal afstand).
  - Card 4: Animation card (uændret).
- Ny backend: CarouselVisibleIcons (int, default 4, clamp 1-12) gennem hele kæden: ConfigModel → ConfigState → SettingsState → Persistence → MainViewModel → LauncherLayoutMeasurements.CarouselCellWidth() (bruger nu CarouselVisibleIcons i stedet for IconsPerRow som slots i carousel).
- Nye lokaliseringsnøgler (en+da): ShortcutsGridTitle, ShortcutsGridDescription, ShortcutsCarouselTitle, ShortcutsCarouselDescription, ShortcutsCarouselVisibleIconsLabel, ShortcutsCarouselVisibleIconsDescription.
- Build 0 fejl, deployet C:\icongrid (18:54, DLL 976384 bytes) — afventer bruger-verifikation.

- IconScale (Icon size) fik nyt gulv: minimum er nu 82% / 0.82 (tidligere 0.5 på GenvejsIkonerPage, 0.8 i MainWindow overlay). Årsag: i carousel bliver viewport-højden beregnet som 96 × scale, mens selve tile altid fylder ~96px → under ~84% klippes ikonerne. 82% er nu det nye laveste punkt (brugerens ønske).
- Ændringer: GenvejsIkonerPage.xaml slider Minimum 0.5→0.82, MainWindow.xaml settings-overlay Minimum 0.8→0.82, MainViewModel.IconScale setter clamp Math.Max(0.82, value), MainViewModelConfigState.FromConfig clamp Math.Max(0.82, config.IconScale).
- Build 0 fejl, deployet C:\icongrid (19:06, DLL 976384 bytes) — afventer bruger-verifikation.

- GenvejsIkonerPage.xaml fik det samme ScrollViewer-mønster som GamingOverlayPage (ScrollViewer Margin=12, VerticalScrollBarVisibility=Auto, PanningMode=VerticalOnly, SettingsPageScrollBarStyle i Resources) så siden har scrollbar når indholdet overstiger vinduet. TemplatePage Margin ændret 12→0 (ScrollViewer tager nu margenen).
- Build 0 fejl, deployet C:\icongrid (19:11, DLL 976896 bytes) — afventer bruger-verifikation.

- ✅ SESSION AFSLUTTET (19:14): Alt commit + push (`4814666`). README.md opdateret med nye GenvejsIkoner beskrivelser.
- run_all_checks ved session-afslutning: Architecture 3 kendte violations (MainWindow 1062, MainViewModel 1862 — voksede fra 1840 pga. CarouselVisibleIcons + clamp, stadig kun de 3 kendte filer), version 0.7.0-beta.1 konsistent, localization 175 en/175 da synkroniseret, ingen secrets, XAML 6 kendte hardcodede danske linjer (uændret).


- NY FEATURE: Fast USB Copy-side implementeret (C#/.NET, WPF). Sidebar-nav 'USB Copy' (UC-badge) efter Monitor Row Layout. Siden bruger GamingOverlayPage-struktur + central lokaliseringsmodel (LocalizationHelper → MainViewModel.Localization → bindings), TemplatePage + TemplateGuidelines.xaml + StartsideSectionCardStyle.
- Backend: Helpers/UsbCopy/ — UsbDeviceInfo+UsbPortType-enum, UsbDeviceDetector (WMI Win32_DiskDrive/USBControllerDevice, port-type USB 2.0/3.0/3.2/4.0), UsbCopyEngine (buffer-pipeline, SequentialScan+WriteThrough, events), UsbCopyLogger (AppData\Roaming\IconGrid\logs\fastusbcopy\ + benchmark\), UsbBenchmarkRunner (Port/Read/Write/BufferStress 64KB-2MB/Stability 30s), UsbCopyState (INotifyPropertyChanged).
- ViewModels/Settings/UsbCopyViewModel.cs ejer feature-logik (MainViewModel uberørt, jf. ARCHITECTURE_RULES). Views/Settings/Pages/UsbCopyPage.xaml(.cs) — 5 cards: Enheder, Kopiering, Performance, Log, Benchmark. Lokalisering: 39 nye nøgler (UsbCopy*) = 214 en/214 da.
- Build: 0 fejl, 0 advarsler. Deployet via deploy-test.cmd til C:\icongrid (DLL verificeret 20:33:18). IconGrid startet — afventer bruger-verifikation af siden + enhedsdetektion + benchmark.
- check_architecture_rules/run_all_checks: KUN de 3 kendte violations uændret (MainWindow 1062, MainViewModel 1862, HardwareMonitorAgent 1772). Ingen nye violations.
- IKKE committet/pushet — afventer bruger-approval.


- FIX (brugerrapporteret crash): XamlParseException 'LayoutActionButtonStyle could not be found' i UsbCopyPage.InitializeComponent. Årsag: TemplateActionButtonStyle i den DELTE Views/TemplateGuidelines.xaml var BasedOn={StaticResource LayoutActionButtonStyle}, men LayoutActionButtonStyle er KUN defineret lokalt i LayoutPage.xaml — så alle sider der brugte TemplateActionButtonStyle ville crash (ikke kun UsbCopyPage). Fix: gjort TemplateActionButtonStyle selvstændig (Padding 12,6 + AccentBrush-setters direkte, uden BasedOn).
- Build 0 fejl, deployet via deploy-test.cmd (DLL verificeret 20:37:05), IconGrid kører nu (2 processer: launcher PID + agent).
- NY PROCES-REGEL: .clinerules + AGENT.md opdateret med 'COMMAND CHEATSHEET (REQUIRED)' — konsulter E:\IconGrid-GitHub\.local-state\regex-commands-cheatsheet.md FØR enhver shell-kommando, og opdater den ved hver fejl/forbedring. Cheatsheetet er oprettet med gode/dårlige mønstre (PowerShell $-variabler i oneliner = forbudt, && ikke gyldigt i PS 5.1, dir med flere stier, pipelines til Select-String).


- NYE BENCHMARK-FUNKTIONER (brugerønske: test vs Windows + logge alt): UsbBenchmarkRunner.WindowsBaselineTestAsync (kopierer 32 MiB inkompressibel fil via File.Copy = samme API som Stifinder, markeret BaselineMethod='File.Copy (Explorer)'), UsbCopyLogger.ExportBenchmarkCsv (TestName,BaselineMethod,BufferSize,Bytes,AvgMiBS,PeakMiBS,Stalls,FlushSeconds,ElapsedSeconds), UsbCopyViewModel WindowsBaselineCommand + ExportCsvCommand, 3 nye lokaliseringsnøgler (en+da: UsbCopyWindowsBaselineButton, UsbCopyWindowsBaselineDescription, UsbCopyExportCsvButton), 2 nye knapper i UsbCopyPage benchmark-card. Så man kan sammenligne Fast USB Copy-pipeline vs Windows Stifinder side om side og analysere buffer-kurven i CSV/Excel.
- Deploy 20:51:47: baseline+CSV-version deployet via deploy-test.cmd (DLL verificeret frisk), IconGrid startet. Build 0 fejl.
- IKKE committet/pushet — afventer bruger-approval.


- ✅ COMMITTET + PUSHEt (21:18): `f3d1e09` — 'feat: Fast USB Copy tool with optimized buffer pipeline, benchmark suite, Windows baseline, and CSV export' (18 filer, 2268 insertions / 3 deletions). Push: 4814666..f3d1e09 main -> main.
- README.md opdateret i samme commit: Fast USB Copy i Settings-tabellen + dedikeret '## Fast USB Copy'-sektion (pipeline, live performance, logging, benchmark suite, arkitektur).
- Next: .local-state/fast-copy.md indeholder den bruger-godkendte Fast Copy-plan (omdøb → dual-pane → multi-worker → skalerings-benchmark) + færdig prompt til næste session.














## Næste session — PLAN & PROMPT (Fast Copy: omdøb + dual-pane + multi-worker)


## Status lige nu (gemt 2026-08-11 21:14)
- 'Fast USB Copy' feature er bygget + deployet (Helpers/UsbCopy, UsbCopyViewModel, UsbCopyPage, sidebar-knap UC, lokalisering 214 en/214 da). TemplateActionButtonStyle-fixet er deployet (var BasedOn=LayoutActionButtonStyle som kun findes i LayoutPage.xaml → crash). WindowsBaseline + ExportCsv knapper tilføjet.
- IKKE committet/pushet — afventer bruger-approval. FastCopy-repo med teknikker: E:\ExternalTools\FastCopy-master (JAVA-implementering, principper oversættes til C#).
- FULDE detaljer + godkendt plan + færdig prompt står i `.local-state/fast-copy.md` (læs med read_note 'fast-copy').

## FÆRDIG PROMPT til næste session (kopiér dette)
'Læs .local-state/fast-copy.md + CHAT_STATE.md først (Fast Copy-plan er godkendt af brugeren 2026-08-11 21:08). Udfør i rækkefølge med build + auto-deploy (deploy-test.cmd) efter hvert trin og verificér DLL-friskhed:
1. Omdøb 'USB Copy' → 'Fast Copy' (en)/'Hurtig kopiering' (da): sidebar-nav, side-titel 'Fast Copy — HDD · SSD · USB', vis ALLE drevtyper (HDD/SSD/NVMe/USB) via DriveInfo.GetDrives() + Win32_DiskDrive MediaType/InterfaceType (ikke kun Removable). Ændr UsbCopy*-lokaliseringsnøgler til FastCopy* (en+da synkroniseret).
2. Dual-pane fil-browser (Norton Commander-stil) på Fast Copy-siden — erstatter 'Vælg filer...': venstre 'Fra' + pil + højre 'Til'; begge paneler frit navigable (drev-dropdown, mapper/filer navn/størrelse/dato, dobbeltklik = ind, Op-knap, markering mellemrum/checkbox, status 'X filer · Y MB'); WorkerCount-slider (1–16) + buffer-dropdown her.
3. Multi-worker engine (FastCopy-teknikker): WorkerCount + 20-fil-tærskel (<20 → sekventiel); små filer (≤20 KB) pakkes i temp-ZIP → én kopi → unzip; store filer (>64 MB) chunk-splittes parallelt (File.SetLength + positioned write). Log fil + worker-id + chunk + pakke-events.
4. Skalerings-benchmark (1, 2, 4, 8 workers) + WorkerCount i CSV.
5. Commit/push kun ved eksplicit godkendelse.
Referencer: .local-state/fast-copy.md, Helpers/UsbCopy/, ViewModels/Settings/UsbCopyViewModel.cs, Views/Settings/Pages/UsbCopyPage.xaml(.cs), command-cheatsheet.'


## Session findings (2026-08-11) — Fast Copy Trin 1

- Fast Copy Trin 1 færdig + deployet (DLL 21:29:38, matcher build): 'USB Copy' → 'Fast Copy'/'Hurtig kopiering', side-titel 'Fast Copy — HDD · SSD · USB', alle drevtyper (Fixed+Removable) vises med medietype HDD/SSD/NVMe/USB via Win32_DiskDrive InterfaceType+MediaType/Model. Sidebar-badge 'UC'→'FC'. UsbCopy*-nøgler → FastCopy* (en+da, +FastCopyMediaTypeLabel). DriveMediaType-enum tilføjet. Build 0 fejl.
- LÆRE (deploy): deploy-test.cmd gav 'Sharing violation' første gang fordi IconGrid.exe blev startet af scriptet mens den gamle DLL stadig var låst — C:\icongrid DLL var derfor stale (20:51:47). Fix: verificér at INGEN IconGrid.exe/IconGridFpsAgent.exe kører FØR deploy (tasklist), kør deploy igen, verificér DLL-timestamp matcher build.
- IKKE committet/pushet — afventer bruger-approval.

## Session findings (2026-08-11) — Fast Copy Trin 2

- Fast Copy Trin 2 (dual-pane fil-browser) færdig + deployet (DLL 21:52:37, matcher build). Nye filer: Helpers/UsbCopy/FileBrowserEntry.cs + FileBrowserPane.cs. UsbCopyState fik WorkerCount (default 8). UsbCopyEngine fik CopyPathsAsync (relativ sti-bevaring + rekursiv mapper). UsbCopyViewModel fik LeftPane/RightPane/ActivePane/TargetPane + SwapSource + ToggleEntry/EnterDirectory/GoUp/SelectAll/ClearSelection kommandoer. UsbCopyPage.xaml fik dual-pane card (Fra | ⇄ | Til) + WorkerCount-slider (1-16) + buffer. 8 nye FastCopy*-nøgler (en+da synkroniseret). Build 0 fejl.
- Næste: Trin 3 multi-worker engine (WorkerCount + 20-fil-tærskel, små filer ZIP-pakkes, store filer chunk-splittes) + Trin 4 skalerings-benchmark (1/2/4/8 workers + WorkerCount i CSV).
- IKKE committet/pushet — afventer bruger-approval.

## Next steps

Bruger-test er nødvendig før commit/push (spørg eksplicit, jf. AGENT.md).

## Session findings (2026-08-11) — Fast Copy UI-fix: dropdown tekst hvid-på-hvid

- Brugerrapporteret: dropdown-menuer på Fast Copy-siden havde hvid tekst på hvid baggrund (ulæseligt). To fixes bygget + deployet (DLL 22:25:28, matcher build):
  1) Views/StartsideStyles.xaml → TemplateComboBoxStyle fik eksplicit ItemContainerStyle (ComboBoxItem Foreground=Black, Padding 6,4) så dropdown-listen er sort-på-hvid i begge temaer (gælder ALLE sider der bruger style'en, fx Game Resolution).
  2) Views/Settings/Pages/UsbCopyPage.xaml → Fra/Til drev-dropdowns fik eksplicit ComboBox.ItemTemplate med TextBlock Foreground=Black, så både det viste felt-indhold og listen er sort.
- AFVENTER bruger-verifikation. IKKE committet/pushet.

## Session findings (2026-08-12) — Farvepalet/design-tokens + dropdown-fix

- Brugerrapporteret: i Files From/To dropdowns er det IKKE teksten der er hvid, men WPF's DEFAULT ComboBox-popup der hardcoder hvid baggrund → tekst (mørk nok) bliver usynlig på hvid. Løsning:
  1) DESIGN TOKENS (farvepalet) tilføjet øverst i Views/StartsideStyles.xaml: SurfaceFill*, SurfaceBorder*, Scroll*, ComboBox* (ComboBoxSurface #F3F4F7 (lyst, IKKE hvid), ComboBoxText #1F2937, border/hover/highlight).
  2) TemplateComboBoxStyle omskrevet til custom ControlTemplate + PART_Popup med Border der bruger ComboBoxSurfaceBrush — så den udvidede liste er lysegå og læsbar i BEGGE temaer (gælder alle sider der bruger style'en). ComboBoxItemContainerStyle m. highlight token.
  3) Farvepaletten dokumenteret i Views/TemplateGuidelines.xaml (ny 'COLOR PALETTE'-sektion) — én ændring i token-filen propagerer app-wide.
- Build 0 fejl, deployet via deploy-test.cmd (DLL 01:38:58 verificeret frisk; første deploy gav transient Sharing violation — retry lykkedes).
- IKKE committet/pushet — afventer bruger-approval.

## Session findings (2026-08-12) — From/To dropdowns virkede ikke (drev lister tomme)

- Brugerrapporteret: Files From/To dropdowns kunne ikke åbne / ingen drev, mens Buffer Size virkede fint. Rodårsager:
  1) FileBrowserPane.DriveRoots var en {get; private set;}-auto property UDEN PropertyChanged-notification → ComboBox ItemsSource blev aldrig opdateret → tom liste i dropdownen.
  2) From/To SelectedItem bandt Mode=TwoWay til CurrentDirectory som er getter-only → binding-fejl.
  3) x:Name på ComboBox under TemplatePage gav namescope-fejl (MC3093) — løst med Tag="Left"/"Right" i stedet.
- Fiks: DriveRoots fik INotifyPropertyChanged (OnPropertyChanged), SelectedItem→Mode=OneWay (CurrentDirectory er getter-only, opdateres via NavigateTo), fjernet band-aid ItemTemplate, ny DriveComboBox_SelectionChanged handler i code-behind der navigerer korrekt panel via Tag.
- Build 0 fejl (13.2s), deployet via deploy-test.cmd, DLL verificeret frisk (01:57:53) — IconGrid kører.
- IKKE committet/pushet — afventer bruger-approval. Farvepalet/design-tokens fra tidligere fix ligger fortsat i StartsideStyles.xaml + TemplateGuidelines.xaml.

## Session findings (2026-08-12) — Dropdown-popup lå bag indhold + viste kun en stribe

- Brugerrapporteret: From/To dropdown-menuer viste kun meget lidt og 'lå ikke et lag over knapperne nedenunder'. Rodårsager i TemplateComboBoxStyle (Views/StartsideStyles.xaml):
  1) Popup havde AllowsTransparency=True → renderes i et transparent kompositions-lag uden eget HWND → kan ligge BAG andet indhold/knapper.
  2) Border MaxHeight={TemplateBinding MaxDropDownHeight} → MaxDropDownHeight er NaN som standard → Border måles til ~0 højde → kun en lille stribe af menuen vises.
- Fiks: fjernet AllowsTransparency (popup får eget HWND → ligger ALTID øverst) + fast MaxHeight=320 på popup-Border.
- Build 0 fejl (12.0s), deployet via deploy-test.cmd, DLL verificeret frisk (02:11:02) — IconGrid kører. AFVENTER bruger-verifikation.
- IKKE committet/pushet — afventer bruger-approval.

## Session findings (2026-08-12) — TOMT dropdown fix (IReadOnlyList → ObservableCollection + tidlig init)

- Brugerrapporteret: From/To + Drives dropdowns er TOME (vises korrekt via standard WPF-popup, men ingen items). Buffer-dropdown virker (statiske XAML-items). Ekstern analyse (WPF-binding):
  1) ItemsSource som IReadOnlyList<T> opdaterer IKKE UI ved indholdsændring — Kræver ObservableCollection<T> eller at hele property-referencen skiftes + OnPropertyChanged.
  2) Data populert i Loaded-eventen kan nå at være 'for sent' ift. binding-etablering.
- Fiks (alle 4):
  1) FileBrowserPane.DriveRoots: IReadOnlyList<string> → ObservableCollection<string> (Clear/Add i LoadDrives).
  2) UsbCopyState.Devices: IReadOnlyList<UsbDeviceInfo> → ny ObservableDeviceCollection (ObservableCollection<UsbDeviceInfo>).
  3) UsbCopyViewModel.RefreshDevices(): Clear() + Add() i stedet for hele-listen-assignment + SelectedDevice = Devices[0].
  4) UsbCopyPage.xaml.cs: RefreshDevices() kaldt i constructoren (tidlig init) + Loaded kører opfriskning igen.
- ElementName-bindinger bibeholdt (sidens DataContext er MainViewModel; UsbCopyViewModel er child — et fuldt DataContext-skift ville bryde lokaliseringen). ObservableCollection giver UI notifikation uanset ElementName-binding.
- Build 0 fejl (12.4s), deployet via deploy-test.cmd, DLL verificeret frisk (02:59:46). AFVENTER bruger-verifikation.
- IKKE committet/pushet — afventer bruger-approval.

## Næste session — PLAN & PROMPT (dropdown-problem ULØST — ingen commit, ingen kodning mere i denne session)

## STATUS (2026-08-12 03:10) — dropdown-problem IKKE løst
- Sagen: Fast Copy-siden (Files From/To + Drives) viser ikke drev i dropdownsene. Popuppen åbner, men listen er tom (Drives + From/To). Buffer-dropdown virker (statiske XAML-items).
- Vi HAR lavet mange fixes der IKKE løste det: (1) hvid-tekst fix, (2) light-surface popup, (3) standard WPF popup (fjerne custom ControlTemplate), (4) ObservableCollection for DriveRoots + Devices + tidlig init i constructor + Clear/Add. Build 0 fejl hver gang, deployet via deploy-test.cmd. Intet af dette fik drev til at vises.
- Vigtig lære: ekstern analyse sagde IReadOnlyList+sen populering, men det løste det ikke. Næste skridt skal DEBUG datakilden: hvad returnerer UsbDeviceDetector.DetectDevices() og DriveInfo.GetDrives() PÅ DENNE MASKINE (evt. skriv count til trace.log), og verificér at ItemsSource-bindingerne faktisk får data (binding-fejl i Output).
- INGEN commit/push — hele Fast Copy-arbejdet + dropdown-fixene er u-committet i working tree. AFVENTER bruger-approval + løsning.

## FÆRDIG PROMPT til næste session
'Fast Copy dropdown-problem er ULØST. Debug på data-niveau, ikke UI-styling: (1) Kontrollér hvad UsbDeviceDetector.DetectDevices() faktisk returnerer på systemet (skriv fil-/drev-count til trace.log i AppData/Roaming/IconGrid/trace.log under RefreshDevices() og driv-dropdown LoadDrives()) — er listen tom, eller er det bindingen der fejler? (2) Tjek Output-vinduet for WPF binding-fejl (System.Windows.Data Error) når siden åbnes. (3) Behold standard WPF ComboBox-styling (ingen custom ControlTemplate). (4) Når rodårsagen er fundet: ret, build 0 fejl, deploy via deploy-test.cmd, verificér DLL-timestamp. (5) Commit/push kun ved eksplicit bruger-approval. Referencer: Helpers/UsbCopy/UsbDeviceDetector.cs, Helpers/UsbCopy/FileBrowserPane.cs (DriveRoots ObservableCollection), Helpers/UsbCopy/UsbCopyState.cs (ObservableDeviceCollection), ViewModels/Settings/UsbCopyViewModel.cs (RefreshDevices), Views/Settings/Pages/UsbCopyPage.xaml(.cs), Views/StartsideStyles.xaml (TemplateComboBoxStyle — brug kun style/ItemContainerStyle, ikke custom template).'

## Session findings (2026-08-12) — Ultimo session
- Dropdown-problem: efter standard WPF-popup + ObservableCollection + tidlig init er dropdownene stadig tomme (brugerrapport 03:08). Problem ULØST. Ingen mere kodning i denne session (bruger-besked). Intet committet/pushet. CHAT_STATE opdateret. Next: se prompt ovenfor.

## Næste session — PLAN & PROMPT (dropdown-problemet LØST 2026-08-12 03:58)

## STATUS (2026-08-12 04:02) — dropdown-problemet er LØST
- RODÅRSAG (data-niveau debug): trace.log beviste data ER til stede (DetectDevices=5, LoadDrives=5+5). Dropdowns var tomme pga. BINDING-fejl: alle ViewModel.*-bindinger brugte ElementName=Root, som ikke resolver henover TemplatePage (UserControl/namescope-grænse med ContentPresenter). Fix: RelativeSource AncestorType={x:Type views:UsbCopyPage} → drev viste.
- Yderligere fixes: (2) DriveRootItem(Path,DisplayName) → drevnavne i dropdown; (3) string.Equals(Tag,"Left"/"Right",Ordinal) i stedet for ReferenceEquals → drevskift navigerer; (4) dobbeltklik på mappe: EventSetter sender=ListBoxItem → ItemsControlFromItemContainer + ListBox Tag → mappe-navigation virker; (5) langsommere opstart: global DataBindingSource-listener fjernet; undermappe-dropdown: ny FileBrowserPane.DriveRootPath (drev-roden).
- ✅ BRUGER-VERIFICERET (03:58): dropdowns viser drevnavne, drevskift viser filer/mapper, mappe-navigation virker, undermappe-dropdown viser aktivt drev, opstart normal igen. Kopi-test OK.
- NYE ØNSKER (ikke implementeret endnu): (1) UI fryser under kopiering → gør CopyPathsAsync/kopiering async med UI-marsalling; (2) destination pane opdateres dynamisk under kopiering (vis kopierede mapper efterhånden); (3) kopi-status-indikator med tid (samlet størrelse, tid brugt, resterende, filer tilbage) som normale kopiprogrammer.
- LÆRE (MCP): icongrid-notes append_to_note afviser 'Invalid JSON argument' ved content med \n/specialtegn — brug korte kompakte kald eller File API. Noteret i regex-commands-cheatsheet.md.
- Build 0 fejl hver gang. Deploy: C:\icongrid (seneste DLL 03:47:26). IKKE committet/pushet — afventer bruger-approval.

