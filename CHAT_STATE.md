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

## Latest commits (this session)
- `4c779aa` — feat: monitor row layout localization, save-as-default, MCP server architecture enforcer, and two-part memory structure (15 files)
- `90dbe81` — fix: persist monitor row layout settings and stabilize download/upload value widths (4 files)

- `7ac327a` — feat: live CPU voltage on HardwarePage, fixed Game Resolution dropdown alignment + cards, visible card borders in dark theme (8 files, 279 insertions / 79 deletions) — pushet til GitHub.

- `053bef5` — fix: rename 'Language and region' to 'Language' on the start page (2 files) — pushet til GitHub.

- `633e84d` — feat: make Element gaps, Dividers and CPU/GPU bars collapsible on Monitor Row Layout page (2 files, 152 insertions / 16 deletions) — pushet til GitHub.

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


