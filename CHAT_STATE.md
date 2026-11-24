# IconGrid — Short-term Memory

> **Full session history:** `.local-state/session-history.md` (gitignored, searchable via `search_notes`)

## Current date
October 23, 2026

> **Seneste session:** se nederst — "Session findings (2026-10-27)" + "Test-siden flyttet ind i About + Test-fane fjernet" + "Rettelse (27-10 01:36) — tomme About-kort fjernet + mørk-tema-tekst i Debug/Test". Disks sorteret C: → D: → E: ..., Voltage-tile + switches aligned, `TestPage` → `TestSection` nederst på About, og About har ikke længere tomme kort (DLL 27-10 01:35:55).

## Current status
- Fast Copy-siden (Settings → 'Fast Copy') er implementeret + committet/pushet: dual-pane fil-browser (Fra | Til), alle drevtyper (HDD/SSD/NVMe/USB), drev-dropdowns med navne, mappe-navigation, DriveRootPath, MultiWorkerCopyService (WorkerCount 1-16, 20-fil-tærskel, små filer ZIP-pakkes, store filer chunk-splittes), skalerings-benchmark + CSV.
- Dropdown-problemet er LØST (root cause: ElementName=Root resolverede ikke henover TemplatePage → RelativeSource AncestorType). Bruger-verificeret 03:58.
- ✅ BRUGERENS 3 ØNSKER IMPLEMENTERET (04:16): (1) UI-fryser fixet — `ExpandToFiles()` kører nu via `Task.Run` (baggrundstråd); (2) destination-pane opdateres dynamisk under kopiering (throttlet `TargetPane.Refresh()` ~1 sek i OnEngineProgress); (3) kopi-status-indikator — Performance-card viser nu Tid brugt (Elapsed), Samlet størrelse (TotalSizeLabel "kopieret/total") og Filer tilbage (FilesRemainingLabel). 3 nye lokaliseringsnøgler (en+da): FastCopyElapsedLabel/FastCopyTotalSizeLabel/FastCopyFilesRemainingLabel.
- ✅ FILE COPY PROGRESS DIALOG IMPLEMENTERET (04:34): Nyt `Views/FileCopyProgressWindow.xaml(.cs)` — selvstændigt, ikke-modal, topmost, frit flytbart via DragMove på titel (Windows-file-kopi-stil). Vises automatisk når `IsCopying=true` (abonneret i UsbCopyPage.xaml.cs `OnCopyStatePropertyChanged`) og lukkes når kopieringen slutter. Binder lokaliserede labels til MainViewModel DataContext + live-tal til `UsbCopyState` via DependencyProperty `State` (RelativeSource). Annullér-knap → `CancelCopyCommand`. Ny lok nøgle FastCopyProgressTitle (en+da). Lokalisering: 233 en/233 da.
- ✅ FIX (04:39): From/To-drevfelterne var TOMT ved siden-åbning (C:\ ikke vist), selvom dropdown-listen havde alle drev. Rodårsag: WPF re-evaluerer ikke SelectedValue når ObservableCollection genopbygges (Clear+Add) med uændret værdi. Fix: `LoadDrives()` kalder nu `OnPropertyChanged(nameof(DriveRootPath))` efter gen-populering → ComboBox'erne re-selecter aktivt drev. Deployet (DLL 04:38:45 matcher build).
- ✅ FRYSER-FIX PARTIEL (05:04): `TargetPane.Refresh()` → `FileBrowserPane.RefreshAsync()` (enum i `Task.Run` + serial-guard `_refreshSerial`) + `RefreshTargetPaneAsync()` fire-and-forget. Hjalp under selve kopieringen, men **LØSTE IKKE fryseren ved START** (se ULØST nedenfor).
- ✅ FIX 1 FULDT IMPLEMENTERET (16:01, næste session): UI-fryser LØST i hele kopi-kæden — `.ConfigureAwait(false)` i HELE `MultiWorkerCopyService` (sequential + medium + large + CopyFileChunkedAsync), ZIP-pakning og -udpakning kører i `Task.Run`, og `UsbCopyEngine.CopyPathsAsync` kører HELE MultiWorkerCopyService via `Task.Run` så kopi-IO aldrig fanger UI SynchronizationContext. `StartCopyAsync` overgik til `TargetPane.RefreshAsync()` efter kopi.
- ✅ FIX 2 (16:01, næste session): OVERSKRIV-VALG implementeret — `OverwritePolicy.cs` (OverwriteDecision: Overwrite/OverwriteAll/Skip/SkipAll + `IOverwriteConflictResolver`), `MultiWorkerCopyService` bruger session-scoped OverwriteSession (Yes-to-all/No-to-all huskes pr. kopi) på ALLE stier (sequential, medium, large, ZIP-unpack), `OverwritePromptResolver` + `OverwritePromptWindow` (modal Ja/Ja alle/Nej/Nej alle, lokaliseret) vises via Dispatcher.Invoke fra worker-tråde.
- ✅ FIX 3 (16:01, næste session): OPRET MAPPE + SLET (Stifinder-stil) implementeret — `FileBrowserPane.CreateDirectory(name)` + `DeleteSelectedEntries()` (Directory.Delete recursive / File.Delete), `NewFolderDialogWindow` (navn-input) + `DeleteConfirmWindow` (bekræftelse med lokaliseret "Slet '{0}'?"), knapper "Ny mappe"/"Slet" i BEGGE paneler, kommandoer via `UsbCopyViewModel.NewFolderCommand`/`DeleteCommand` + events.
- ✅ LOKALISERING: 18 nye nøgler (en+da) — Overwrite-titel/spørgsmål/Ja/Ja alle/Nej/Nej alle, Ny mappe (knap/titel/prompt/Opret), Slet (knap/titel/bekræftelse/Slet/Annullér), ugyldigt navn, mappe findes. 250 en / 250 da (synkroniseret via run_all_checks 16:02).
- ✅ UI-OMLÆGNING (05:09, brugerens ønske): "Drives"-card SLETTET — drev-info (Medietype/Porttype/Kapacitet/Ledig plads) vises nu som From/To-paneler under fil-browseren (`LeftPaneDevice`/`RightPaneDevice` i UsbCopyViewModel, synkroniseret via PropertyChanged). Performance (Live/Gen/Peak/ETA + Tid/Samlet/Filer + ProgressBar) er flyttet IND i Files-cardet under Start/Cancel-knapperne. Benchmark-card har fået egen kompakt mål-enhed-vælger (ComboBox SelectedDevice + Opdater). Deployet (DLL 05:09:48 matcher build).
- ✅ ALLE Fast Copy-fixes siden 08-11 er COMMITTET + PUSHET (19:47) som `dbf7f83` — "feat: Fast Copy - stabil kopi-pipeline, overskriv-dialog, fil-manager, benchmark-system + robuste dialoger".

## Architecture status
- `run_all_checks` (sidste kørsel ved 79ff8f6): 3 pre-existing VIOLATIONs — MainWindow 1062, MainViewModel 1862, HardwareMonitorAgent 1772. Alle dokumenterede tolerancer.
- Version: 0.7.0-beta.1 (konsistent)
- Localization: 233 en / 233 da — synkroniseret (run_all_checks 04:34)
- Security: ingen staged secrets
- XAML: 6 pre-existing hardcoded Danish linjer

## Working tree status
- **Seneste commit:** `dbf7f83` — "feat: Fast Copy - stabil kopi-pipeline, overskriv-dialog, fil-manager, benchmark-system + robuste dialoger" (27 filer, +2460/−428) — ✅ pushet til origin/main (19:47).
- ✅ ALLE Fast Copy-fixes siden 08-11 er nu committet + pushet: FIX 1-14 + benchmark-system (CLI runner, MCP-tool run_copy_benchmark, live-vindue 100/300/500/1GB).
- Build: 0 fejl, 0 advarsler (18:50:05).
- Deploy: C:\IconGrid (18:50:05, DLL-timestamp matcher build — verificeret).

## ULØSTE ISSUES fra bruger-test (05:11) — PRIORITERET til næste session
1. **UI fryser i STARTEN under kopiering + kopieringen går i stå ved KOPIERING AF FLERE MAPPER.**
   - **ROOT CAUSE (analyseret, høj sikkerhed):** `MultiWorkerCopyService` await'er **UDEN `ConfigureAwait(false)`** (CopyAsync, CopySmallBatchAsync, CopyLargeFilesAsync, CopyFileChunkedAsync). Da `StartCopyAsync` await'er `CopyPathsAsync`, fortsætter koden efter hvert await på **UI-tråden** (SynchronizationContext).
   - **`CopySmallBatchAsync` pakker tusindvis af små filer i en ZIP med `ZipFile.Open` + `CreateEntryFromFile` (Fx 2742 filer ≈ 30+ sek) PÅ UI-TRÅDEN** → UI fryser ved start. Samtidig bliver progress-events (`RunOnUi` → `Dispatcher.BeginInvoke`) sat i kø bagved → hele siden "går i stå" indtil pack er færdig. Ved flere store mapper bliver dette ekstremt tydeligt.
   - **FIX (næste session):** Tilføj `.ConfigureAwait(false)` i HELE `MultiWorkerCopyService` (CopyAsync, CopySmallBatchAsync, CopyLargeFilesAsync, CopyFileChunkedAsync) så AL kopi-IO + ZIP-pakning kører på worker-tråde. Alternativt/derudover: kør `await Task.Run(() => service.CopyAsync(...), ct)` i `UsbCopyEngine.CopyPathsAsync`. Også: `StartCopyAsync`'s endelige `TargetPane.Refresh()` → overgå til `RefreshAsync()`.
2. **Manglende OVERSKRIV-valg:** Hvis filen allerede eksisterer på destinationen, kan man i dag IKKE vælge at overskrive. **Næste session:** Tilføj overwrite-politik (OverwriteAll / SkipAll / Ask pr. fil) i UsbCopyEngine/MultiWorkerCopyService + en lille bruger-dialog eller inline-valg "Overskriv? [Ja] [Ja alle] [Nej] [Nej alle]" (da/en lokaliseret), før kopieringen skriver til eksisterende filer.
3. **Manglende FIL/OPSÆTNINGS-OPERATIONER (Stifinder-stil, brugerens ønske 15:40):** Kunne **oprette ny mappe** og **slette filer og mapper** direkte i de to paneler. **Næste session:** tilføj kommandoer/knapper i hvert panel — Ny mappe (prompt navn via lille input-dialog), Slet fil/mappe (bekræftelse + `Directory.Delete(recursive:true)` / `File.Delete`, bevæg til papirkurv evt.) — da/en lokaliseret. Integrer i FileBrowserPane (enumerate → opdater) + UsbCopyPage-knapper pr. panel.

1. ✅ LØST (16:01): UI fryser i starten + kopiering går i stå ved flere mapper — se FIX 1 ovenfor.
2. ✅ LØST (16:01): Manglende overskriv-valg — se FIX 2 ovenfor.
3. ✅ LØST (16:01): Opret mappe + slet filer/mapper (Stifinder-stil) — se FIX 3 ovenfor.

## Good next steps (næste session)
- **Afventer bruger-verifikation** (17:12): test i C:\IconGrid at (1) kopiering starter hurtigere (default Workers nu 4 — benchmark-log viste at 8 workers var ~9× langsommere på H:\), (2) Files remaining/Tid/Filer tæller nu live ned allerede under ZIP-pakningen, (3) benchmark-udskrift er hvid/læsbar, (4) ingen dublet Start/Cancel-knapper under Buffer Size, (5) layout: File manager // Copy setup (Workers+Buffer) // Performance // Log // Benchmark i 5 adskilte cards.
- Buffer til hjemmeside-filer: **512 KB eller 1 MB** anbefales (small-fil ZIP-pakning kører uanset; Workers 4 er ny default — se "FIX 8"-note).
- ✅ Commit + push GENNEMFØRT (19:47) som `dbf7f83` — intet ligger u-committet længere.
- **Næste session (brugerønsker):** (1) LM Studio-forbindelse — tilføj "Vælg AI model / tilslut LM Studio"-UI på Fast Copy-siden (brugeren gør dette i sit andet program `E:\Maxithx projekter\AI-wordpress-scanner` og vil have det integreret); (2) kør benchmark-serien 100/300/500/1000 MB via nye knapper + optimér yderligere.
- Referencer: `.local-state/fast-copy.md`, `Helpers/UsbCopy/MultiWorkerCopyService.cs`, `Helpers/UsbCopy/UsbCopyEngine.cs`, `Helpers/UsbCopy/OverwritePolicy.cs`, `Helpers/UsbCopy/FileBrowserPane.cs`, `ViewModels/Settings/UsbCopyViewModel.cs`, `Views/Settings/Pages/UsbCopyPage.xaml(.cs)`, `Views/OverwritePromptWindow.xaml(.cs)`, `Views/NewFolderDialogWindow.xaml(.cs)`, `Views/DeleteConfirmWindow.xaml(.cs)`, `Helpers/Settings/LocalizationHelper.cs`, `ViewModels/MainViewModel.Localization.cs`.
- Session-log: `.local-state/sessions/2026-08-12.md` skal udvides med denne session (FIX 1-3 + rodårsager + bruger-verifikation).


## FIX 4-5 (16:27, brugerrapport 16:20)

- ✅ FIX 4 (16:27): SELECT ALL virkede ikke i det ikke-aktive panel (og Slet/Ny mappe ramte forkert panel). Rodårsag: pane-knapperne bandt til `ActivePane`-kommandoer uden pane-tag. Fix: `UsbCopyViewModel` kommandoer tager nu `CommandParameter` ("Left"/"Right") → `ResolvePane(tag)`, `UsbCopyPage.xaml` sender tag fra hver panel-knap, `UsbCopyPage.xaml.cs` handlerne (`OnNewFolderRequested`/`OnDeleteRequested`) modtager tag. Hver panel-knappe betjener nu DET panel (Explorer-stil).
- ✅ FIX 5 (16:27): SLET crashede IconGrid + monitor-blink ved åbning af siden. Ændring: fjernede `AllowsTransparency="True"` fra alle 3 nye modal-dialoger (OverwritePromptWindow, NewFolderDialogWindow, DeleteConfirmWindow) og gav dem solid `Background="#F2F3F6"` — `AllowsTransparency` + `ToolWindow` + modal `ShowDialog()` er en kendt ustabil WPF-kombination (kan give DWM/blink og endda procescrash). Visuelt uændret (StartsideSectionCardStyle på Border). Logger evt. eventuelle crash-dumps i Event Viewer.
- ⚠️ MONITOR-BLINK: forsvandt efter genstart af IconGrid — men årsage mistænkes at være AllowsTransparency-dialogerne (FIX 5 adresserer det proaktivt). Hvis det vender tilbage: tjek Windows Event Viewer → Application → .NET Runtime / Application Error efter `IconGrid.exe`.
- ✅ Build 0 fejl/0 advarsler (16:27:51) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering: 250 en/250 da (run_all_checks 16:28).

## FIX 6 (16:36, brugerrapport 16:34)

- ✅ FIX 6 (16:36): FILE COPY PROGRESS DIALOG gik i stå under kopiering — men filerne blev kopieret korrekt. Rodårsag: `FileCopyProgressWindow` brugte `AllowsTransparency="True"` (samme ustabile WPF-kombination som FIX 5). Fix: `AllowsTransparency` fjernet + solid `Background="#F2F3F6"` — Topmost/ToolWindow/drag-bevarret. Deployet C:\IconGrid (DLL 16:36:14 matcher build).
- ℹ️ BUFFER-ANBEFALING (16:36, hjemmeside-filer): web-filer består typisk af MASSER af små filer (CSS/JS/HTML/billeder ≤20 KB) + få store. MultiWorkerCopyService pakker allerede al ≤20 KB i én ZIP (én stream), så buffer-størrelsen betyder LIDT for små filer — 64–256 KB er fint. For billeder/medier >64 MB bruges chunk-split uanset buffer. **Anbefaling: 512 KB eller 1 MB** som god generel balance. VIGTIGST: hold Workers ≥4–8 så ZIP-pakning + medium-filer parallelliseres; sænk IKKE Workers til 1 (så bliver det sekventielt). Hvis højest gennemstrømning til store filer: 2 MB. Benchmark-kortet (Worker scaling / Buffer stress test) kan bekræfte kurven på dit drev.

## FIX 7a-7b (16:45, brugerrapport 16:43)

- ✅ FIX 7a (16:45): CANCEL-KNAPPEN i FileCopyProgressWindow blev KILPET i bunden (vinduehøjde 300 var for lav til 6-rækket grid + knap + ToolWindow-titlebar). Fix: højden øget til 360.
- ✅ FIX 7b (16:45): OVERWRITE-VINDUET lå UNDER "Copying"-vinduet (Copying er Topmost, Overwrite var ikke). Fix: `Topmost="True"` på OverwritePromptWindow + højden justeret til 260 så knaprækken ikke klippes.
- Deployet C:\IconGrid (DLL 16:45:15 matcher build).

## FIX 8 (17:11, brugerrapport 16:59 — 6 punkter)

- ✅ #1 LANGSOMMELIG START: Benchmark-loggens Worker Scaling på H:\ viste at FLERE workers = MEGET langsommere (1: 21,58 MiB/s | 2: 6,62 | 4: 4,04 | 8: 2,36) — typisk for HDD/USB (disk-thrashing ved parallel skrivning). Fix: default WorkerCount 8 → 4.
- ✅ #2 BENCHMARK-UDSTRIKT SORT: rodårsag — `TemplateSmallTextStyle` bruger `{Binding SettingsSubtextForeground}` som fejler inde i benchmark-DataTemplate (DataContext = benchmark-resultat, ikke MainViewModel) → sort standardfarve. Fix: eksplicit `Foreground="{Binding DataContext.SettingsSubtextForeground, RelativeSource=AncestorType=UsbCopyPage}"` på de 2 benchmark-TextBlocks.
- ✅ #3 DUBLET-KNAPPER: de 2 ekstra "Start copy"+"Cancel"-knapper under Buffer Size FJERNET (kun Start/Cancel i midterste kolonne mellem panelerne + dialog-vindue).
- ✅ #4+#5 LAYOUT: siden er nu opdelt i egne hero-cards — (1) Files/File manager (paneler + drev-info), (2) Copy setup (Workers + Buffer + PipelineStatus), (3) Performance (kun live-tal + ProgressBar), (4) Log, (5) Benchmark. Workers ligger ikke længere direkte under file manager.
- ✅ #6 FILES REMAINING GÅR I STÅ: ZIP-pakningen af tusindvis af små filer rapporterede først fremdrift NÅR hele pakningen var færdig. Fix: `state.AddSmallBatch(file.Size, 1, rel)` pr. pakket fil inde i pakke-loopen → Files remaining tæller nu ned live under hele pakningen.
- Build 0 fejl/0 advarsler (17:11:57) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering stadig 250 en/250 da.

## FIX 9 (17:23, brugerrapport 17:20)

- ✅ FIX 9 (17:23): PROGRESS-BAREN FRØS PÅ ~5%. Rodårsag: `MarkFileCompleted()` tællede kun fil-antallet (`_filesCompleted++`), men tilføjede ALDRIG filstørrelsen til `_totalCopied`. Billeder er typisk >20 KB (medium-filer) → de kopieres med no-op onProgress, så `TotalBytesCopied` kun steg under ZIP-pakningen (~5% af totalen). Fix: `MarkFileCompleted(fileName, bytes)` tilføjer nu `_totalCopied += bytes` på ALLE stier (sequential, medium, large-skip). Store filer (>64 MB) tæller allerede live via chunk-bytes. Progress-baren løber nu til 100% i takt med kopieringen.
- Build 0 fejl/0 advarsler (17:23:38) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering stadig 250 en/250 da.

## FIX 10 + LOG-ANALYSE (17:32, brugerrapport 17:28)

- 📊 LOG-BEKRÆFTET HASTIGHED (copy-20260812-172507.log): 13.958 filer / 599.672.875 bytes (~572 MB) kopieret på ~20 s (17:25:08→17:25:28) USB→SSD med workers=4, buffer=1 MB. ZIP-pakning af 6.818 små filer tog kun 1,5 s (var 30+ s før). `MULTI COPY DONE bytes=599672875` = 100% korrekt. Kæmpe forbedring ✅.
- ✅ FIX 10 (17:32): PERFORMANCE-SEKTIONEN viste "Files remaining 41" + status ~98% selvom alt var kopieret. Rodårsag: progress-events er throttlet ~100 ms + sendt via Dispatcher.BeginInvoke; kopien slutter lynhurtigt → IsCopying=false lukker vinduet før de sidste events rappes. Fix: efter `CopyPathsAsync` returnerer, sætter StartCopyAsync eksplicit `State.Progress = 100` og `State.FilesRemainingLabel = "0"` FØR vinduet lukkes.
- Build 0 fejl/0 advarsler (17:32:29) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering stadig 250 en/250 da.

## FIX 11 (17:46, brugerrapport 17:44)

- ✅ FIX 11 (17:46): READ TEST viste 1169 MB/s på USB — umuligt. Rodårsag: `RunReadTestAsync` læste med almindeligt FileStream, så Windows' PAGE CACHE (RAM) serverede den frisklavede 8 MB probe-fil → målte RAM-hastighed, ikke USB. Fix: `FILE_FLAG_NO_BUFFERING` (0x20000000) via `File.OpenHandle` + FileStream — alle reads går nu direkte til enheden (buffer 1 MB + 8 MB probe er multipla af 512-sektor, så flaget er gyldigt). Loggen logger nu også `READ TEST file_bytes=`.
- ⚠️ DEPLOY-TRIVIA (17:46): `deploy-test.cmd` gav "Sharing violation" x2 første gang (filer låst). Fix ses i session: `taskkill /f /im IconGrid.exe` + `IconGridFpsAgent.exe` (begge "not found" trods lås — deployet skal bare køres IGEN) → andet kørs resultat vellykket. DLL-timestamp 17:46:03 matcher build. Noter: kør deployet en ekstra gang hvis det melder Sharing violation.
- Brugerrapport: kopiering nu PÅ NIVEAU med Windows-kopiering (H:\ USB → E:\ SSD), statuslinje slutter 100%/0 korrekt.

## FIX 12 (17:56, brugerrapport 17:50)

- ✅ FIX 12 (17:56): SELECT ALL virkede ikke + ønske om Stifinder-markering. Rodårsag: `SelectAllFiles()` markerede kun FILER, ikke mapper (og almindelige klik opdaterede ikke status). Fix: (1) `SelectAllFiles()` markerer nu ALT (filer+mapper) som Windows Ctrl+A; (2) `FileBrowserPane` har nu `AnchorEntry` + `SetAnchor` + `SelectRange` (interval) + `RefreshSelectionStatus`; (3) `UsbCopyPage`'s ListBox'er har `PreviewMouseLeftButtonDown` → almindelig klik = enkelt-markering + nyt anker, Ctrl+klik = toggle, Shift+klik = interval fra anker. CheckBox-klik er beskyttet mod dobbelt-håndtering.
- Build 0 fejl/0 advarsler (17:56:19) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering stadig 250 en/250 da.

## FIX 13 + BREAK-ANALYSE (18:11, brugerrapport 18:03)

- 📊 BREAK-ANALYSE (copy-20260812-180011.log): 18.490 filer / 1.177 MB, workers=4. ZIP-pakningen af 8.936 små filer tog kun ~2 s (18:00:11.5→18:00:13.6), men UNPACK-fasen tog ~7,5 s (PACK UNPACK 18:00:13.9 → PACK DONE 18:00:21.4) — det er brugerens "break"/stoppet.
- ✅ FIX 13 (18:11): UNPACK var SEKVENTIEL (`ExtractToFile` én ad gangen for 8.936 filer = tusindvis af små random-writes = samme bottleneck som ZIP-pakningen undgår). Fix: `UnpackZipAsync` bruger nu `Parallel.ForEachAsync` (MaxDegreeOfParallelism = max(2, ProcessorCount/2)) med samme OverwriteSession-politik. ~7,5 s breaket forventes fjernet.
- Build 0 fejl/0 advarsler (18:11:40) — deployet C:\IconGrid (DLL-timestamp matcher).

## FIX 14 + BENCHMARK-SYSTEM (18:50, brugerønsker 18:27-18:34)

- ✅ FIX 14 (17:46→18:21): ZIP-UDPAKNING trådsikker. Rodårsag til "kopi fejl" i log (PACK ERROR unsupported compression method): parallel ExtractToFile delte ÉN ZipArchive-stream (ikke trådsikker). Fix: hver worker åbner sit EGET arkiv pr. entry (ZipFile.OpenRead); UnpackZipAsync returnerer bool + `CopySmallFallbackAsync` kopierer små filer enkeltvis hvis ZIP fejler (kopien går ALDRIG ned pga. én dårlig entry). OverwriteSession er nu låst (lock) så "Overskriv alle" ikke spørger igen (race fixed).
- ✅ BENCHMARK-SYSTEM (Del 1-3, 18:50): (1) `tools/benchmark-runner/BenchmarkRunner.csproj` — CLI der skaber incompressible web-lignende payload (70% små filer) + kører ægte CopyPathsAsync-pipeline, skriver benchmark-live.log + benchmark-summary.json. 100MB-test kørte: 180 filer, 4,28 MiB/s, 23,4s på H:\. (2) MCP-tool `run_copy_benchmark` i icongrid-notes-serveren — kan kaldes af MIG og af lokal AI i LM Studio (via OpenAI-compatibel API): `--size 100|300|500|1000`, optional drive/workers/buffer, returnerer stdout + JSON-summary. (3) Appen: `RunSyntheticBenchmarkCommand` + `BenchmarkRunWindow` (live-log + progress + Stop/Close) + knapper 100MB/300MB/500MB/1GB på Benchmark-card. Ny lok: FastCopyBenchmarkPreparing, FastCopyCloseButton. IconGrid.csproj ekskluderer nu tools/ + Native/ fra compile-globs.
- ℹ️ LM Studio-ide: IKKE dum — det er en rigtig god idé. LLM'en kan selv kalde run_copy_benchmark (100/300/500/1000), læse benchmark-summary.json og optimere workers/buffer, og skrive konklusioner til CHAT_STATE.md som jeg læser næste session. Kræver: LM Studio server + OpenAI-kompatibelt function calling + MCP-serveren kørende.
- Build 0 fejl/0 advarsler (18:50:05) — deployet C:\IconGrid (DLL-timestamp matcher). Lokalisering 252 en/252 da; XAML hardcoded dansk tilbage på 6 pre-existing.

## Session findings (2026-08-14)

- FPS bug POE1 LØST (kode-fix implementeret): stale FpsTarget efter spil-lukning forårsagede restart-spiral (Configurered FPS target changed... hver ~10s) + 'FindAnyGameProcess gave up after 60s'. Fix: TryClearStaleFpsTargetRuntimeMetadata rydder nu HELE FpsTarget når spillet er fuldt lukket (ingen proces matcher ExecutableName); ny helper AnyProcessMatchesConfigTarget. Byg 0 fejl 22:32. Docs: .local-state/fps-etw.md. Deploy til C:\IconGrid venter.

## Session findings (2026-08-14) - VERIFICERET

VERIFICERET 22:34: IconGrid startet fra C:\IconGrid (ny DLL 22:33:00). trace.log linje 66754: 'Cleared stale FpsTarget from config.json.' config.json FpsTarget nu tom (alle null). NativeFpsAgentRunner log: HasConfigTarget=False, TargetExe=null. Fixet virker: IconGrid starter nu i foreground-first mode uden stale POE1-target. Brugertest: start POE1 fra IconGrid -> FPS skal vises i gaming overlay.

## Session findings (2026-08-14) - VERIFICERET FPS virker

VERIFICERET LIVE 22:47: FPS vist korrekt i POE1 (58->62 FPS, Source=PrimaryApi, Target=PathOfExile.exe). Ingen restart-spiral ('Locked config target is active... Suppressing foreground retarget'). POE1-luk -> FpsTarget ryddet -> POE2-start -> ny FpsTarget oprettet korrekt. 2 fixes: (1) stale FpsTarget ryddes fuldt ved spil-luk (TryClearStaleFpsTargetRuntimeMetadata -> TryClearStaleFpsTargetConfig + AnyProcessMatchesConfigTarget), (2) HardwareMonitorAgent venter pa mutex-overtagelse (15s) i stedet for at give op. Build 0 fejl 22:45, deploy C:\IconGrid DLL 22:45:42. Fuldt beskrevet i .local-state/fps-etw.md.

## Session findings (2026-08-14) - Resolution updatering fix

FIX 3 deployet 23:05 (DLL 23:05:47 matcher): DisplayResolutionService.WatchProcess folger nu heLE launch-sessionen (exe-navn), ikke en enkelt PID. Spil bekræftet startet (synligt game-vindue) -> exit = restore straks. Spil aldrig set startet (opdaterings-check) -> hold opløsningen uanset hvor lang tid opdateringen tager, kun 30-min sikkerhedsventil. LauncherItemLaunchManager sender exe-navn explicit. Alle 3 fixes (stale FpsTarget, mutex-race, resolution-session) forklaret i .local-state/fps-etw.md.

## Session findings (2026-08-14) - FIX 4 StarCraft + hvid kant

FIX 4 deployet 23:22 (DLL 23:22:56): Filter mod shell/non-game processer (SystemSettings, XboxGameBarWidgets, GameBar, Battle.net, steam, launchers, browsers) i FindAnyGameProcess + HasVisibleGameWindow + FindHandoffProcess - forhindrer at opløsningen gendannes for tidligt mens spillet starter via Battle.net/Steam-kæde. HVID KANT: AllowsTransparency=True på MainWindow + opløsningsskift efter spil-luk = DWM fallback-ramme under drag (kendt Windows-begrænsning, jf. FIX 5/6). Afventer bruger-verifikation: StarCraft via Battle.net skal nu holde opløsningen indtil spillet LUKKER.

## Session findings (2026-08-14) - FIX 5 launcher hide-mode

FIX 5 deployet 23:31 (DLL 23:31:01): MainWindow.OnGameExited() kalder nu ALTID RestoreLauncherFromGame() - forhindrer periodisk 'launcher forbliver i hide mode' efter spil-lukning. Rodårsag: launcher-gendannelse afhang af overlay-close-eventet. StarCraft verificeret fint af bruger (opløsning+nedslag+overlay+launcher). Afventer POE1-verifikation.

## Session findings (2026-08-14) - FIX 7 false-positive external games + session klar

FIX 7 deployet 23:53 (DLL 23:53:36): PERMANENT anti-false-positive løsning i TryAutoRegisterExternalGame — en proces registreres kun som external game når native FPS agent har set ETW GPU present events (DXGI/D3D9/DXGKRNL > 0) for PID'en. Discord/WindowsTerminal/taskmgr/Outlook/KeePassXC/qBittorrent/dwm/LockApp/SnippingTool præsenterer ALDRIG frames til vores ETW-session → de registreres aldrig. TryAutoRegisterExternalGame kaldes nu KUN via AttemptExternalGameRegistration (med nativeState med events), ikke direkte i TryUpdateForegroundGameTarget. run_all_checks: 3 pre-existing violations (MainWindow 1071, MainViewModel 1862, HardwareMonitorAgent 1888), version/lokalisering/security GRØN, XAML 6 pre-existing danske linjer. Build 0 fejl 23:53. Alle 7 fixes beskrevet i .local-state/fps-etw.md. Klar til commit+push.

## Session findings (2026-08-18) - Gaming overlay position fix

BUG RAPPORT: Brugeren sagde at gaming overlay var sat til 'TopRight' på siden, men viste 'TopCenter' når spillet startede. RODÅRSAG: config.json indeholdt faktisk 'GamingOverlayPositionPreset': 'TopCenter' (ikke 'TopRight' som brugeren troede). GamingOverlayPage.xaml.cs SelectedOverlayPositionPreset getter returnerede 'TopRight' som FALLBACK når _mainViewModel var null. AttachMainViewModel() udløste IKKE PropertyChanged efter viewmodel-tilknytning → ComboBox'en viste stale 'TopRight' mens overlayet korrekt brugte 'TopCenter'. FIX: (1) AttachMainViewModel() udløser nu PropertyChanged for SelectedGameLauncherBehavior, AutoShowGamingOverlayOnGameStart, AutoCloseGamingOverlayOnGameEnd, RestoreLauncherAfterOverlayClosed + SelectedOverlayPositionPreset; (2) MainViewModel_PropertyChanged reagerer nu også på ændringer i disse VM-properties → siden holdes synkroniseret. Build 0 fejl 19:50, deploy C:\IconGrid DLL 19:50:15 matcher. Brugeren skal selv vælge 'TopRight' på siden hvis det ønskes — siden viser nu KORREKT den faktiske config-værdi.

## Session findings (2026-08-18) - Launcher hide-mode fix

BUG RAPPORT 2: Efter spil-lukning kom main-launcheren ikke ud af hide mode (efter gaming overlay var lukket). RODÅRSAG (trace.log-analyse): RestoreLauncherFromGame() returnerede tidligt fordi _isGameHideActive OG _isHidden begge var false, selvom launcher var FYSISK skjult (Top=-205). Sekvens: (1) auto-hide/peek skjuler launcher → _isHidden=true, _originalTop=-205; (2) SlideToVisible() kaldes → _isHidden=false men _originalTop er stadig -205 → launcher forbliver fysisk skjult; (3) spil starter → HideForGame(1) → _isHidden=false → _preGameTop=-204.8 (skjult position!); (4) spil lukker → RestoreLauncherFromGame → _isGameHideActive && _isHidden false → RETURNER TIDLIGT → launcher forbliver skjult for evigt. FIX (LauncherWindowModeController.cs): (1) Ny IsPhysicallyHidden() - tjekker faktisk rentderet position via GetWindowRect mod WorkArea; (2) RestoreLauncherFromGame returnerer nu KUN tidligt hvis launcher IKKE er fysisk skjult; (3) HideForGame gemmer nu null i _preGameTop hvis launcher allerede er off-screen (undgår at gemme -205 som restore-target); (4) SlideToPreGameTop falder tilbage til WorkArea.Top hvis ingen gyldig position er gemt. Build 0 fejl 20:12, deploy C:\IconGrid DLL 20:12:58 matcher. AFVENTER BRUGER-VERIFIKATION: start POE1, luk spil + overlay → launcher skal glider tilbage.

## Session findings (2026-09-04) - Robust ping + Monitor Ping settings page

BRUGER-RAPPORT: Monitor row viste "Net: 33ms" og ind imellem "ingen data". Brugerens faktiske ping (m�lt via online speedtest): ~2ms. Dansk Kabel TV internet.

ROD�RSAG (3 problemer i gammel SystemMonitor.CaptureNetworkSnapshot):
1. **Hardcoded 8.8.8.8** � Google anycast er langt v�k fra danske kabelnet-brugere, derfor 33ms i stedet for det brugeren faktisk oplever.
2. **`new Ping().Send(...)` per tick** � l�kker ICMP sockets, ingen instans-cache.
3. **Ingen retry/fallback** � hvis �t target fejler vises "--ms" med det samme, ingen EMA-smoothing.

FIX (7 filer �ndret + 2 nye):
- **Helpers/Launcher/SystemMonitor.cs** � ny robust ping-arkitektur:
  - `PingTargetMode` enum (Auto/Gateway/Cloudflare/Google/Custom)
  - Cached `Ping`-instans med `_pingLock` (ingen socket-l�kage)
  - `ResolvePingTargets()` med prioriteret fallback-liste (gateway ? 1.1.1.1 ? 8.8.8.8)
  - `GetActiveGatewayAddress()` auto-detekterer brugerens router (IPv4 foretr�kkes, IPv6 fallback)
  - EMA-smoothing (`PingEmaAlpha = 0.3`) s� spikes ikke viser 33ms n�r sandheden er 2ms
  - Stale-markering (10 sek): viser `--ms (NN)` hvis alle targets fejler men vi har en nylig god v�rdi
  - `PingTargetLabel` og `IsPingStale` properties til tooltip
  - `ConfigurePingTarget(mode, custom)` public API
  - `Dispose()` opdateret til at dispose den shared Ping
- **Models/ConfigModel.cs** � nye properties `MonitorPingTargetMode` (string "Auto"/"Gateway"/"Cloudflare"/"Google"/"Custom") + `MonitorPingCustomTarget`
- **ViewModels/Settings/MainViewModelConfigState.cs** � samme + `NormalizePingMode()` whitelist
- **ViewModels/Settings/MainViewModelSettingsState.cs** � samme
- **ViewModels/Settings/MainViewModelSettingsPersistence.cs** � save-mapping
- **ViewModels/MainViewModel.cs** � backing fields, public properties, `MonitorPingTargetItems` ComboBox-kilde (KeyValuePair), kalder `ConfigurePingTarget` p� settings-apply
- **ViewModels/MainViewModel.Settings.cs** � kalder `ConfigurePingTarget` fra `ApplyConfig()` s� settings tager effekt med det samme
- **Helpers/Converters/StringEqualsToVisibilityConverter.cs** (NY) � Visible/Collapsed baseret p� string-match
- **Views/Settings/Pages/MonitorPingPage.xaml + .cs** (NY underside) � TemplatePage med hero + card med ComboBox + TextBox (kun synlig n�r "Custom" valgt)
- **Helpers/Settings/LocalizationHelper.cs** � 12 nye n�kler en+da
- **ViewModels/MainViewModel.Localization.cs** � 12 nye properties + OnPropertyChanged for Items ved sprogskift
- **Controls/Launcher/LauncherMonitorRow.xaml** � KUN en enkelt ToolTip-binding tilf�jet p� net-teksten (viser aktivt target). **INGEN layout�ndring, INGEN ny visuel styling.**
- **Views/Settings/SettingsWindow.xaml + .xaml.cs** � ny sidebar-knap "MP" mellem Monitor Layout og Usb Copy.

FORVENTET OUTPUT EFTER FIX:
- Default "Auto" ? pinger gateway f�rst ? brugeren ser ~1-5ms (gr�n)
- Hvis gateway fejler ? fallback til 1.1.1.1 (~5-15ms)
- Hvis ALT fejler i >10s ? viser `--ms (NN)` (sidste kendte v�rdi, markeret stale)
- Spike til 80ms ? EMA udj�vner til n�sten ingenting
- Tooltip p� net-teksten viser aktivt target (f.eks. "192.168.1.1" eller "1.1.1.1")

BYGGESTATUS:
- F�rste build: 5 fejl (alle i SystemMonitor.cs) � `HasValue`/`Value` p� `IPAddress?` (Nullable reference type) + type-mismatch `double?` vs `long?`. Fixet: brugt `!= null` p� IPAddress (det er Nullable Reference Type, ikke Nullable<T>) + cast `displayMs` til `long?` med `Math.Max(0, Math.Round(...))`.
- Andet build: SUCCESS (12,9s).
- Deploy: SUCCESS (DLL 04-09-2026 21:01:29, 1196032 bytes matcher build).

HVAD BRUGEREN SKAL TESTE:
1. �bn IconGrid fra C:\IconGrid � Net-tallet b�r vise ~1-5ms (gr�n) i stedet for 33ms.
2. Hover over "Net: NNms" ? tooltip viser aktivt target (f.eks. "192.168.1.1").
3. �bn Settings ? ny "Monitor Ping"-side (mellem Monitor Layout og Fast Copy). ComboBox b�r vise "Auto (router ? Cloudflare ? Google)" som default.
4. Skift til "Router only" / "Cloudflare" / "Google" / "Custom" � �ndring tager effekt med det samme (n�ste tick).
5. V�lg "Custom" ? TextBox vises ? indtast IP eller hostname.
6. Skift sprog da/en ? alle labels opdateres (12 nye n�kler).

L�ST/UF�RDIGT:
- ? Robust ping-logik med fallback + EMA + stale-markering.
- ? Settings-side + persistence + lokalisering.
- ? Build + deploy verificeret.
- ? Afventer bruger-verifikation (ping skal vise ~1-5ms i stedet for 33ms).
- ? Ikke commitet/pushet endnu (kr�ver separat godkendelse per AGENT.md).

REFERENCER:
- `.local-state/regex-commands-cheatsheet.md` � `dotnet build IconGrid.csproj --nologo -v m` (ingen pipe).
- `cmd /c E:\IconGrid-GitHub\deploy-test.cmd` � non-destructive deploy.
- `AGENT.md` linje 25: deploy ? approval til commit/push.
- `AGENT.md` linje 110-118: lokalisering-pattern (key ? property ? OnPropertyChanged ? XAML binding).

## Session findings (2026-09-04) - FPS agent reset + ping minimum 1ms

To follow-ups pa ping-session:

1. **Min ping 1ms clamp** � Windows kan rapportere 0ms pa grund af clock-tick resolution (~15.6ms). 0ms er fysisk umuligt. `Helpers/Launcher/SystemMonitor.cs`: ny `MinPingMs = 1.0` konstant, clampes pa raw sample + EMA + display. Severity threshold uendret (1ms <= 30ms = Good).

2. **FPS agent reset i IconGrid-logo-menu** � bruger rapporterede at gaming overlay ikke altid ser et spil der allerede korer naar IconGrid starter. Fix: ny menu-item "Nulstil FPS-agent" i `LauncherLogoArea` context menu (ikke floating-ikonet). Klik ? `MainViewModel.ResetFpsAgent()`:
   - Dr�ber alle k�rende `IconGridFpsAgent.exe` processer (`Process.GetProcessesByName`)
   - Sletter `fps-state.json`
   - Rydder `FpsTarget` i config.json via eksisterende `SaveSettingsToConfig()`
   - MessageBox viser resultat (success / no-agent-running / failed)
   - HardwareMonitorAgent (separat elevated process) genstarter agenten naeste tick

Filer aendret (kun dem der er nye i denne session, ekskl. ping):
- `Controls/Floating/FloatingIconButton.xaml` + `.cs` + `Views/Launcher/MainWindow.xaml` + `MainWindow.xaml.cs` � INGEN aendringer (fjernet FPS-reset igen efter bruger-feedback at den skulle vaere i launcher-logo-menuen, ikke floating)
- `Views/Launcher/MainWindow.xaml.cs` � `LayoutContextMenu_Opened` tilfoejer separator + "Nulstil FPS-agent" menu-item EFTER layout-menuen er udfyldt; ny `LogoMenuResetFpsAgent_Click` handler
- `ViewModels/MainViewModel.cs` � `ResetFpsAgent()` public metode
- `ViewModels/MainViewModel.Localization.cs` � 3 nye properties (ResetFpsAgentMenuLabel, ResetFpsAgentSuccessMessage, ResetFpsAgentNoAgentMessage)
- `Helpers/Settings/LocalizationHelper.cs` � 3 nye noekler en + da

Build 0 fejl, deploy C:\IconGrid DLL 22:45:57 matcher build. Afventer bruger-verifikation: start IconGrid ? h�jreklik pa IconGrid-logo ? "Nulstil FPS-agent" ? start spil ? FPS vises.

Ekstra filer aendret i working copy (ikke fra denne session, med i commit):
- `Helpers/Launcher/LauncherWindowModeController.cs`
- `Views/Settings/Pages/GamingOverlayPage.xaml.cs`

Klar til commit + push (bruger har givet eksplicit godkendelse).

## Session findings (2026-09-12) — Gaming overlay: spil vs. program-detektion hærdet

BRUGERPROBLEM: Gaming overlay starter ikke ved COD MW; falske positiver aktiverer overlayet for programmer.

RODARSAG (trace.log): Agentens "sticky game target" låste forkert på `TextInputHost.exe` (PID 19080) via DxgKrnl-fallback FPS (2026-09-11T13:54:02, linje 423382-423393). Så længe den levende shell-proces var target, ignorerede agenten COD: "Ignoring foreground PID 7216 because sticky game target PID 19080 is still alive." (linje 439500 ff. + 443015-443024 + 444074). Da IsInGame = native TargetPid>0 og allerede stod på "sand" (forkert target), skete der ingen false->true-overgang da COD startede -> GameLaunched fyrede ikke -> overlayet auto-vistes ikke. POE virker fordi den startes direkte og bliver target. external-games.json var fyldt med falske positiver (TextInputHost, LockApp, taskmgr, SearchHost, XboxGameBarWidgets, SnippingTool, WindowsTerminal, Photos, MediaPlayer, Photoshop m.fl.).

FIX (bygget 0 fejl + DEPLOYET 2026-09-12 21:19 til C:\IconGrid — DLL 1.200.640 bytes matcher build 1:1):
1. NY `Helpers/Hardware/GameProcessClassifier.cs` — central "spil vs. program"-politik: navne-blocklist (shell/system/launchers/browsers/editors/media/utility) + Windows-systemstier (SystemApps/system32/SysWOW64) + evidens (DXGI/D3D9 = stærk; DxgKrnl = svag).
2. `HardwareMonitorAgent.cs`: system-proces-filter (navn+sti) i forgrundsdetektion; sticky-bekræftelse + game-signal afviser shell-processer (TargetProcessName); `HasUsableFrameSignal` -> classifier; registrerings-guard kræver nu troværdig evidens + ikke-system-proces.
3. Sticky escape-hatch: `StickyChallengerOverrideDelay` (20s) — en stabil ny forgrunds-kandidat frigiver et forkert sticky-target og retargeter. Et dårligt target kan aldrig blokere det rigtige spil for evigt.
4. Unificeret: `DisplayResolutionService` + `LauncherItemLaunchManager` bruger nu GameProcessClassifier.NonGameProcessNames (én kilde).

HVAD BRUGEREN SKAL TESTE (efter deploy):
1. Start COD MW -> overlay skal komme frem (og FPS vises hvis native agenten får DXGI-events).
2. Start POE -> skal stadig virke.
3. Åbn taskmgr/TextInputHost/fullscreen-browser -> overlay må IKKE aktivere; external-games.json må ikke få nye falske positiver.
4. Verificér at `%APPDATA%\IconGrid\external-games.json` ikke vokser med system-processer.

UFARDIGT:
- Gammel forurening i external-games.json (fra før fixet) er ikke ryddet — kandidat til oprydning.
- Ikke committet/pushet (kræver separat godkendelse).
- Afventer bruger-verifikation (COD-overlay + ingen falske positiver for programmer).

DEPLOY-NOTE: Første deploy-forsøg fejlede med 'Sharing violation' fordi IconGrid kørte forhøjet (Adgang nægtet på taskkill). Efter brugeren lukkede IconGrid kørte deploy-test.cmd rent, og `cmd /c dir` viste C:\icongrid\IconGrid.dll = 12-09-2026 21:19 (1.200.640 bytes) = build. (PowerShell Get-Item viste fejlagtigt en cachet/gammel timestamp — brug `cmd /c dir` til deploy-verifikation.)
- VIGTIGT: Kør deploy og `dir`-verifikation i SEPARATE tool-kald — flere kommandoer i samme kald kører parallelt, så `dir` kan nå at læse FØR kopien er færdig (gav falsk "stale DLL").

## Session findings (2026-09-12, fortsat) — Trace-log auto-clean + UI på TestPage

BRUGERØNSKE: Ryd trace.log (var 112 MB) + UI til manuel clean + automatisk oprydning så den ikke vokser, vist i UI.

FIX (bygget 0 fejl + DEPLOYET 12-09-2026 21:29 til C:\IconGrid — DLL 1.205.248 bytes matcher build):
1. NY `Helpers/Logging/AppTrace.cs` — central trace-writer. Fast loft `MaxBytes = 10 MB`; ved overskridelse trimmes ældste halvdel automatisk (linje-justeret) på hver Write + én gang ved App-start (`AppTrace.EnforceSizeLimit()` i OnStartup). `Clear()` til manuel rydning; `GetSizeBytes/GetSizeText/FormatSize/GetLastWriteTime` til UI. Cross-process-robust (retry ved IOException, da launcher + forhøjet monitor-agent skriver samtidigt).
2. `App.xaml.cs` WriteTrace + `DisplayResolutionService.cs` WriteTrace bruger nu AppTrace.Write (én kilde, capped). HardwareMonitorAgent/FpsEtwProbeAgent logger via App.WriteTrace → også capped.
3. `Views/Settings/Pages/TestPage.xaml(.cs)` — nyt "Trace log"-kort: viser sti, størrelse/cap og sidste skrivning + knapperne "Refresh" og "Clean trace log" (statustekst).
4. Nuværende `%APPDATA%\IconGrid\trace.log` (112 MB) er SLETTET manuelt.

AFVIGELSE: TestPage er en debug-side og bruger (som resten af siden) hardcoded engelsk tekst, ikke det centraliserede da/en-system. Dokumenteret tolerance — hvis TestPage skal lokaliseres, tag hele siden i ét hug.

HVAD BRUGEREN SKAL TESTE:
1. Åbn Settings -> Debug/Test -> "Trace log"-kortet viser størrelse og cap.
2. Klik "Clean trace log" -> størrelse bliver 0 B.
3. Lad IconGrid køre -> trace.log må aldrig overstige 10 MB (trimmes automatisk).

UFARDIGT / NÆSTE:
- Ikke committet/pushet (kræver separat godkendelse).
- Overvej at gøre cap konfigurerbar (persisteret) hvis ønsket.
- Gammel forurening i external-games.json (fra før game-detektion-fixet) er ikke ryddet.

## VERIFIKATION (2026-09-12, bruger-test POE + COD MW2) — GRØN

trace.log (24 KB, capped) 21:30-21:33 bekræfter at spil/program-pipelinen virker:
- POE (PID 28360): native agent låste `Target=PathOfExile.exe`, `DXGI=3`, FPS 58-59, `Source=PrimaryApi`. "Locked config target is active ... Suppressing foreground retarget." (korrekt sticky). Overlay vist.
- COD MW2 (cod22-cod, PID 22072): COD HQ-vinduet (843x480) blev korrekt AFVIST ("window is too small"); derefter `Foreground candidate detected: PID=22072 Name=cod22-cod` -> agenten låste `Target=cod22-cod.exe`, `DXGI=5`, FPS 93-128, `Source=PrimaryApi`. Stale POE FpsTarget ryddet. Overlay vist.
- INGEN falske positiver: ingen TextInputHost/LockApp/taskmgr-låsninger; `external-games.json` uændret (sidst ændret 11-09 13:54 — ingen nye registreringer).
- INGEN "Ignoring foreground ... because sticky ... still alive"-blokering længere.

MINDRE OBSERVATIONER (ikke blokerende):
1. `LauncherItemLaunchManager` mærkede COD-PID 22072 som `PathOfExile.exe` under resolution-lock-handoff (linje 100-109), fordi POE-sessionen stadig var aktiv da COD kom. Harmløst (rigtig opløsning anvendt), men log-støj + konceptuelt mismatch. Kandidat til oprydning.
2. Efter COD-luk viste monitor-rækken `FPS=105 Source=FpsMeter` (hold-last) i ~1 min. Formodentlig bevidst hold-last; bør bekræftes at det er ønsket.
3. "FindAnyGameProcess gave up after 60s for PathOfExile" (linje 136) — harmløs støj fra POE-sessionen der reelt blev afløst af COD.

## RODÅRSAG FUNDET: overlay vises ikke ved COD når en shell-proces er låst (2026-09-13) — FIKSET, AFVENTER DEPLOY

Bruger startede COD MW igen 13-09 ~20:39 og gaming overlayet kom IKKE frem. trace.log (6,1 MB) analyseret.

TO UAFHÆNGIGE BUGS:

Bug 1 — identitets-hul (shell-proces låst som spil ved startup):
- `15:51:26 Foreground candidate detected: PID=2760 Name=Microsoft.CmdPal.UI` -> `Initial foreground game PID detected: 2760` -> `Auto-registered external game: Microsoft.CmdPal.UI.exe at C:\Program Files\WindowsApps\...\Microsoft.CmdPal.UI.exe`.
- Windows Command Palette slap gennem ALLE filtre: navnet var ikke i blocklisten, og stien ligger i `C:\Program Files\WindowsApps\` som klassificereren IKKE dækkede (kun SystemApps/system32/SysWOW64).
- Låst via svag `Source=DxgKrnlFallback` (DXGI=0, DXGKRNL=2). Samme mønster tidligere på dagen med `PowerToys.Peek.UI` (07:19).

Bug 2 — escape-hatchen var DØD KODE (den strukturelle killer):
- `TryUpdateForegroundGameTarget` havde `if (currentForegroundGamePid.HasValue && ProcessIsAlive(...) && IsCurrentTargetStillOwned(...)) return;` FØR challenger-logikken.
- `IsCurrentTargetStillOwned` er sand for CmdPal (DXGKRNL>=2 -> HasUsableFrameSignal), så loopet returnerede hver gang og evaluerede ALDRIG forgrundsvinduet. Derfor 0 `cod22`-linjer efter 05:05. Challenger-frigivelsen (20s) lå efter return og var derfor unåelig.

Symptom-mekanik: `IsInGame = TargetPid > 0` (SystemMonitor.cs:723-724). CmdPal satte TargetPid=2760 -> IsInGame=true fra 15:51. Da COD startede var IsInGame allerede true -> ingen false->true-overgang -> `GameLaunched` fyrer ikke -> `MainViewModel.OnSystemMonitorPropertyChanged` (1876) viser ikke overlayet. POE virker fordi intet dårligt target blev låst først.

FIKS (3 lag, defense-in-depth) — BUILD OK (0 fejl), DEPLOY BLOKERET:
1. Identitet: `GameProcessClassifier.NonGameProcessNames` + `Microsoft.CmdPal.UI`, `CmdPal`, `PowerToys.Peek.UI`, `PowerToys.PowerLauncher`, `PowerToys`, `PowerToys.Runner`. Ny `IsWindowsStoreAppPath` (Program Files\WindowsApps) + `RequiresPrimaryGraphicsEvidence`.
2. Evidens: ny `HardwareMonitorAgent.HasAcquisitionEvidence` — Store/WindowsApps-processer kan IKKE bekræftes/ejes/registreres på svag DxgKrnl alene; de kræver ægte DXGI/D3D9 (Game Pass-spil udsender DXGI, så de virker fortsat). Anvendes i `IsStickyTargetConfirmed`, `IsCurrentTargetStillOwned`, `HasAnyGameSignal`, `TryAutoRegisterExternalGame`. `IsStickyTargetConfirmed` afviser nu ogsaa via sti (ikke kun navn).
3. Struktur: challenger-frigivelsen flyttet FØR early-return i `TryUpdateForegroundGameTarget` — et forkert target kan ikke blokere det rigtige spil i mere end 20s. Bootstrap-target sætter nu `currentForegroundPidObservedAtUtc`, så non-game-afvisningen (5s) ogsaa gælder ved startup. `currentForegroundPidObservedAtUtc` flyttet op før bootstrap-blokken (CS0841-fix).

STATUS:
- Build: `IconGrid.dll` 13-09 20:50 (1.205.760 bytes), 0 fejl.
- trace.log: RYDDET (0 B) for frisk test.
- DEPLOY BLOKERET: kørende `IconGrid.exe` (PID 10356) + `IconGridFpsAgent.exe` (PID 13244) kører FORHØJET -> `taskkill` giver "Adgang nægtet". `C:\icongrid\IconGrid.dll` er derfor stadig 12-09 21:29. Bruger skal lukke IconGrid via Task Manager, derefter køres `cmd /c E:\IconGrid-GitHub\deploy-test.cmd` igen.
- Ikke committet/pushet (kræver separat godkendelse).

NÆSTE TEST (frisk log): start COD MW -> overlay skal frem; POE -> skal stadig virke; CmdPal/PowerToys/taskmgr/browser -> maa IKKE aktivere. Verificér at der ikke længere kommer `Auto-registered external game: Microsoft.CmdPal.UI.exe`.

## ARKITEKTUR-OMLÆGNING: evidens-baseret spil-klassificering (2026-09-13) — bruger-godkendt

Bruger afviste blocklist-tilgangen: "det er så forkert, for så vil der altid være problemer fremover med at programmer som ikke er et spil, detectes som et spil". Korrekt — vi er ramt 3 gange (TextInputHost -> PowerToys.Peek -> CmdPal), hver gang et nyt program der ikke stod på listen.

RODÅRSAG I PIPELINEN (native FPS-agent):
- `PollLockedTarget()` (main.cpp): `g_targetPid = candidate->pid` låses UBETINGET naar `FindTargetProcess()` returnerer en kandidat — dvs. 0 beviser kraevet. Valget sker paa identitet (navn/sti/root/score).
- `WriteSharedFpsState()`: `snapshot.targetPid = g_targetPid` publiceres ubetinget.
- `SystemMonitor.FpsTimer_Tick`: `SetInGame(trackedGamePid > 0)`.
=> "in game" betoed "agenten foelger en PID", ikke "PID'en renderer frames". Beviser blev KUN brugt til at beregne FPS-tallet.
- `EtwCallback`: DxgKrnl kernel-fallback (`dxgKrnlCount>=2 && dxgiCount==0 && d3d9Count==0`) blev behandlet som gyldig FPS-kilde. Kernel-presents attribueres til den PID kernen rapporterer — for DWM-komponerede shell-apps er det appens PID. Derfor fik CmdPal/Peek/TextInputHost "FPS" via DxgKrnlFallback med DXGI=0 D3D9=0.

DATA DER BEVISER DET: alle falske positiver havde DXGI=0 D3D9=0; alle aegte spil havde DXGI>=2 (POE DXGI=3, COD DXGI=5). Signalet var der hele tiden — vi brugte det bare ikke til klassificering.

NY DESIGN (implementeret):
- Native `main.cpp`: nyt `--trusted-launch` (saettes KUN naar IconGrid selv startede spillet). Nyt atomar `g_lastPrimaryEvidenceTicksUtc` saettes i `EtwCallback` naar `dxgiCount>=2 || d3d9Count>=2`. `WriteSharedFpsState` publicerer nu `targetPid` KUN naar `g_trustedLaunch || frisk primary-evidens (<5s hold)`; nyt shared-flag `kSharedFlagGameConfirmed=0x4`. JSON-state faar `gameConfirmed` + `trustedLaunch`. Nulstilles ved pid-skift.
- C#: `NativeFpsAgentRunner` sender `--trusted-launch` i config-target-grenen (launch-session), IKKE ved foreground/ekstern detektion. `NativeFpsAgentState` + `NativeFpsSharedMemory` + `ReadSharedMemoryState` faar `GameConfirmed`/`TrustedLaunch`.
- `GameProcessClassifier` opdelt: `SelfProcessNames` (kun IconGrid/IconGridFpsAgent) til FPS-pipelinen; `LaunchInfrastructureProcessNames` + `NonGameProcessNames` (self + launchers + Windows shell hosts) bruges KUN af launch/resolution-disambiguering (DisplayResolutionService, LauncherItemLaunchManager). Den store app-blocklist (browsere/editors/media/utilities/CmdPal/PowerToys) er FJERNET. `HardwareMonitorAgent` bruger nu `IsSelfProcessName` + strukturelle sti-regler + evidens.
- Beholdt (principielt, ikke en blocklist): `NonGamePathPrefixes` (SystemApps/system32/SysWOW64) og `IsWindowsStoreAppPath` -> WindowsApps-processer kraever primaer-evidens.
- `--trusted-launch` bevarer emulatorer/aeldre titler der kun eksponerer kernel-pathen, naar de startes FRA IconGrid.

BUILD/VERIFIKATION:
- Native: MSBuild `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` "E:\IconGrid-GitHub\Native\FpsAgent\FpsAgent.vcxproj" /p:Configuration=Debug /p:Platform=x64 -> `Native\FpsAgent\bin\Debug\IconGridFpsAgent.exe` 13-09 20:57 (OK).
- C#: `dotnet build IconGrid.csproj` 0 fejl; `IconGrid.dll` 13-09 20:59; ny FpsAgent exe kopieret til `bin\Debug\...\Tools\FpsAgent\` (20:57).
- DEPLOY STADIG BLOKERET: `C:\icongrid\IconGrid.dll` er 12-09 21:29 og `C:\icongrid\Tools\FpsAgent\IconGridFpsAgent.exe` er 23-07. IconGrid.exe PID 10356 kører forhøjet -> taskkill "Adgang nægtet". Bruger skal lukke IconGrid helt.

TESTPLAN (frisk trace.log): COD (ekstern, fra Battle.net) -> overlay skal frem; POE (fra IconGrid) -> uændret; CmdPal/PowerToys/taskmgr/Steam/Discord/browser -> IsInGame maa forblive false og INGEN `Auto-registered external game`. Forventet i log: `gameConfirmed` false indtil DXGI/D3D9-evidens, derefter true.

OPFOELGNING (bevidst udskudt): hvis en Chromium-baseret app (Steam/Discord/Electron) viser sig at udsende app-level DXGI-present og dermed klassificeres som spil, skal vi stramme EVIDENS-reglen (fx kraev vedvarende cadence / flip-model fullscreen), IKKE tilfoeje navne til en liste.

## BRUGER-RAPPORT "overlay vises ved opstart uden spil" (2026-09-13 ~21:04) — IKKE NY BUG, GAMMEL BUILD + STALE AGENT

Symptom: naar IconGrid startes vises gaming-overlayet med det samme, selvom intet spil koerer.

Diagnose (bevist med procesdata):
- `C:\icongrid\IconGrid.dll` = 12-09 21:29 og `C:\icongrid\Tools\FpsAgent\IconGridFpsAgent.exe` = 23-07. **DEPLOY ER ALDRIG GENNEMFOERT** — brugeren tester den GAMLE build. Hele evidens-omlaegningen er ikke i drift.
- Stale forhoejede processer fra i eftermiddags koerer endnu: `IconGrid.exe` PID 10356 startet 13-09 15:50:57 (agent-mode, forhoejet, 31 MB) og `IconGridFpsAgent.exe` PID 13244 startet 15:51:26.
- `Microsoft.CmdPal.UI.exe` PID 2760 (startet 15:51:17) er stadig den laaste target.
- `native-fps-state.json` uaendret i 3+ timer: `targetPid=2760 Target=Microsoft.CmdPal.UI.exe matchedDxgiEventCount=0 matchedD3D9EventCount=0 matchedDxgKrnlEventCount=2` — praecis den gamle false-positive-signatur.
- `config.json` FpsTarget: `ExecutableName=null`, `RootProcessId=null` -> intet config-target.
- Ved 21:03:25 kunne den NYE agent-instans ikke starte: "Hardware monitor agent is already running. Waiting for the previous instance to exit..." -> den gamle agent (PID 10356) holder mutex'en.

Mekanik: den gamle native agent holder `TargetPid=2760` i shared memory (`Local\IconGrid.NativeFps.Live`). `SystemMonitor.FpsTimer_Tick` laeser kun `targetPid > 0` -> `SetInGame(true)` -> overlayet vises. Ingen spil involveret.

KONKLUSION: evidens-gaten (native `targetPid` publiceres kun ved DXGI/D3D9-evidens eller trusted-launch) FIK SER netop dette; men den skal deployes foerst. En stale ikke-spil-target kan derefter ikke laengere saette `targetPid > 0`.

ROBUSTHEDS-HULL (kandidat, ikke implementeret): den forhoejede monitor-agent overlevede at launcher'en blev lukket. `ParentIsAlive` (HardwareMonitorAgent.cs:1896) validerer KUN PID — ved PID-genbrug kan en fremmed proces holde agenten i live, og en ny launcher kan ikke tage over (mutex). Anbefalet fiks: send ogsaa foraelderens start-tid med (`--parent-start-filetime`) og validér PID+start-tid, alternativt lad launcher'en aktivt stoppe en stale agent ved opstart.

IKKE committet/pushet. Naeste skridt: bruger skal lukke ALLE `IconGrid.exe` (baade launcher og agent-mode) + `IconGridFpsAgent.exe` med forhoejet rettigheder, hvorefter deploy koeres og verificeres.

## FIX (2026-09-13 sen aften): X-knappen efterlod headless proces + foraeldreloes agent

Bruger-diagnose var korrekt: "naar icongrid bliver lukket fra knappen X, lukker den ikke alt i taskmanager".

BEVIS:
- `IconGridFpsAgent.exe` PID 13244 havde `ParentProcessId = 10356` -> PID 10356 ER den forhoejede monitor-agent (IconGrid.exe i `--monitor-agent` mode).
- 10356's foraelder er `svchost.exe` PID 2268 (UAC-host). Launcher'en fra 15:50 er VAEK, men agenten levede 5+ timer senere.
- `CommandLine` var tom for 10356 (forhoejet -> kan ikke laeses uden elevation), men parent/child-forholdet er entydigt.

ROD (to huller):

1. `MainWindow.Window_Closing` (Views/Launcher/MainWindow.xaml.cs) lukkede bare vinduet. Fordi `App.xaml.cs:35 ShutdownMode = OnExplicitShutdown`, drabte det IKKE processen -> launcher-traaden blev haengende headless, og den forhoejede agent + native FPS-agent fortsatte med et stale target. Den eksisterende `CloseButton_Click` (in-app luk-knap) gjorde det RIGTIGE (`ExitApplication()` naar `StartDirectlyInLauncher`, ellers `EnterFloatingMode()`), men OS-vinduets X gik udenom.
   FIX: `Window_Closing` ruter nu X gennem praecis samme adfaerd som `CloseButton_Click` (`e.Cancel = true` + `ExitApplication()` eller `EnterFloatingMode()`), styret af et nyt `_isShuttingDown`-flag som `ExitApplication()` saetter foerst, saa den rigtige exit ikke blokeres.

2. `HardwareMonitorAgent.ParentIsAlive` validerede KUN PID. Windows genbruger PID'er, saa en foraeldreloes agent kan finde en fremmed proces med "sin" PID og aldrig afslutte.
   FIX: `HardwareMonitorTaskManager.StartAgent` sender nu ogsaa `--parent-start-filetime <FILETIME>` (launcherens starttid). Agenten parser den (`TryReadParentStartFileTime`) og `ParentIsAlive(int? parentPid, long? parentStartFileTimeUtc)` verificerer nu PID **+ starttid** (tolerance 20.000.000 = 2 sek). Falder tilbage til PID-only hvis starttiden ikke kan laeses.

3. YDERLIGERE HUL (IKKE fixet endnu): launcher'en har INGEN single-instance-beskyttelse. Kun agenten har en mutex (`AgentMutexName`). En ny launcher kan derfor starte mens en gammel (floating/headless) instans koerer -> den nye kan ikke tage agent-mutex'en ("Hardware monitor agent is already running. Waiting for the previous instance to exit...") og arver det stale target. Det er praecis sekvensen kl. 21:03. Anbefalet fiks: single-instance mutex + signalér den koerende instans om at komme i forgrunden (fx named event), og afslut den nye proces.

BUILD: `dotnet build IconGrid.csproj` 0 fejl efter alle tre aendringer (1 + 2).
DEPLOY: stadig blokeret af forhoejet `IconGrid.exe` PID 10356 + `IconGridFpsAgent.exe` PID 13244.

## DEPLOY GENNEMFOERT + DEPLOY-SCRIPT BUG FUNDET (2026-09-13 ~21:15)

Bruger lukkede de forhoejede processer; `taskkill` var ikke laengere noedvendigt. Deploy koert.

KRITISK FUND: `deploy-test.cmd` havde ALDRIG deployet den native FPS-agent.
- Scriptet kopierede kun `%SRC%\*.exe` (roden). FpsAgent ligger i UNDERMAPPEN `Tools\FpsAgent\`, og `xcopy` uden `/s` rekurserer IKKE -> `C:\icongrid\Tools\FpsAgent\IconGridFpsAgent.exe` stod stille paa 23-07-2026.
- Fejlen var usynlig fordi linjen slutter med `>nul`.
- FIX: tilfoejet eksplicit `xcopy /y /q /i "%SRC%\Tools\FpsAgent\IconGridFpsAgent.exe" "%DST%\Tools\FpsAgent\" >nul` i `deploy-test.cmd` (linje ~21).
- KONSEKVENS: alle tidligere "native agent"-aendringer i repoet er aldrig naaet ud til `C:\IconGrid`. Den nu udrullede agent er den FOERSTE opdaterede nogensinde (23-07 -> 13-09).

VERIFIKATION (hash, ikke timestamp!):
- `IconGrid.dll`: DEPLOYED = BUILD = `188614CD14A8F8C9004EC42721A2199A6BDC59551628599EDCEE93CB1C6DC22F` (13-09 21:09:43, 1.206.784 bytes).
- `IconGridFpsAgent.exe`: DEPLOYED = BUILD = `39D1C1E8A4DF4CE4DCCB49D7828B41D2CE2F9BDA796BBD2F6B87FF9BE30E9FF9` (13-09 20:57:14, 1.000.448 bytes).

VIGTIGT: `cmd /c dir` gav FORAELDEDE resultater i denne session (viste 12-09 21:29 for en fil der reelt var 13-09 21:09). Brug ALTID `Get-FileHash <dst>,<src>` og sammenlign hashes. Dette boer ind i AGENT.md's deploy-verifikationsinstruks, som i dag siger "verify the DLL timestamp".

TESTPLAN (frisk trace.log, ryddet):
1. Start IconGrid -> overlay skal IKKE vise noget (intet spil, ingen stale target).
2. COD MW (fra Battle.net) -> overlay skal frem; forvent `gameConfirmed` true foerst efter DXGI/D3D9-evidens.
3. POE (fra IconGrid, trusted-launch) -> uaendret.
4. CmdPal/PowerToys/taskmgr/Steam/Discord/browser -> IsInGame skal forblive false, ingen `Auto-registered external game`.
5. Luk IconGrid med X -> INGEN `IconGrid.exe` eller `IconGridFpsAgent.exe` maa staa tilbage i Task Manager (medmindre StartDirectlyInLauncher=false, hvor den gaar til floating-ikon).

## SINGLE-INSTANCE GUARD IMPLEMENTERET (2026-09-13 ~21:20)

Bruger godkendte at fixe det tredje hul: to launcher-instanser kunne koere samtidigt (kun agenten havde en mutex), saa en ny launcher ikke kunne overtage agent-mutex'en og arvede et stale FPS-target.

NY FIL: `Helpers/Launcher/SingleInstanceGuard.cs`
- Named mutex `Local\IconGrid.Launcher.SingleInstance` (ejes i processens levetid).
- Named event `Local\IconGrid.Launcher.Activate` (AutoReset).
- `TryAcquire()` -> true for foerste instans; false hvis en anden launcher ejer mutex'en.
- `SignalExistingInstance()` -> statisk; aabner activate-eventet (best-effort) og saetter det.
- `ListenForActivation(Dispatcher, Action)` -> `ThreadPool.RegisterWaitForSingleObject` der dispatcher til UI-traaden.
- `Dispose()` rydder RegisteredWaitHandle/event/mutex.

AENDRINGER:
- `App.xaml.cs`: nyt felt `_singleInstanceGuard`. I launcher-grenen (efter `WriteTrace("Starting launcher UI...")`, FOER agent-start): hvis `!TryAcquire()` -> log, `SignalExistingInstance()`, dispose, `Shutdown(0)` og return. Ellers `ListenForActivation(Dispatcher, ActivateRunningLauncher)`. Ny metode `ActivateRunningLauncher()` kalder `MainWindow.ActivateFromSecondInstance()`. Nyt `using IconGrid.Helpers.Launcher;`.
- `Views/Launcher/MainWindow.xaml.cs`: ny public `ActivateFromSecondInstance()` — gendanner vinduet hvis minimeret (`WindowState = Normal`, `Show()`), kalder `EnterFullMode()` (som allerede kalder `_window.Activate()`), derefter `Activate()`.

VERIFIKATION: `dotnet build` 0 fejl. Deploy koert SEPARAT efter build. Hash-verifikation OK:
- `IconGrid.dll`: DEPLOYED = BUILD = `2C0E2CF8E56493A3...`
- `IconGridFpsAgent.exe`: DEPLOYED = BUILD = `39D1C1E8A4DF4CE4...`

NY LAERDOMME (cheatsheet afsnit 11): `dotnet build ... | Select-String` returnerede FOER buildet var faerdigt (~20 sek senere), saa en deploy i samme batch kopierede en ufaerdig fil. Koer ALTID build og deploy som separate tool-kald + verificér med hash.

TEST TILFOEJET: start IconGrid to gange -> anden opstart maa IKKE give en ekstra `IconGrid.exe`; den koerende instans skal komme i forgrunden, og den nye proces skal afslutte.

## REGRESSION: explorer.exe blev klassificeret som spil + VRAM-EVIDENS (2026-09-13 ~21:30)

Bruger-test: COD OK men langsom; POE perfekt; **Division 2 viser INTET overlay**.

RODÅRSAG (fundet i trace.log):
- `21:20:21 Foreground candidate detected: PID=10776 Name=explorer` -> `Foreground game PID changed from 19532 to 10776. Restarting native FPS agent.`
- `21:20:49 Auto-registered external game: Explorer.EXE at C:\WINDOWS\Explorer.EXE` (leak!)
- Derefter sad agenten fast paa `explorer.exe` med `DXGI=0 D3D9=0 DXGKRNL=1-2` og falsk FPS (290/37/60) via `DxgKrnlFallback` indtil shutdown.
- **Division 2 (PID 25656)** blev fundet af `LauncherItemLaunchManager.FindAnyGameProcess` og fik resolution-lock, men FPS-pipelinen opdagede den ALDRIG, fordi den var laast paa explorer. Ingen `Foreground candidate detected` for Division 2 i hele loggen.

HVORFOR (min regression): da blocklisten blev fjernet (efter brugerens oenske), forsvandt `explorer` fra FPS-pipelinens filter. Explorers skrivebordsvindue er skaermstort + synligt -> bestod `IsLikelyGameForegroundWindow`, og `HasAcquisitionEvidence` accepterede den svage DxgKrnl-fallback (DXGKRNL=2) -> bekræftet sticky target.

FIX 1 — STRUKTUREL shell-vindue-regel (ikke en navneliste):
- Nyt P/Invoke `GetClassNameW` + `ShellWindowClasses` = Progman, WorkerW, Shell_TrayWnd, Shell_SecondaryTrayWnd, MultitaskingViewFrame, TaskListThumbnailWnd, XamlExplorerHostIslandWindow.
- `IsShellWindowClass(hwnd, out className)` kaldes i `IsLikelyGameForegroundWindow` -> afviser skrivebord/taskbar/opgavvisning uanset procesnavn. Log: `Rejecting foreground PID because the window is a Windows shell window.`
- Bevidst IKKE `Windows.UI.Core.CoreWindow` (bruges ogsaa af UWP/Game Pass-spil).

FIX 2 — VRAM SOM BEVIS (brugerens idé, verificeret mulig):
- NY FIL `Helpers/Hardware/GpuProcessMemory.cs`: laeser per-proces dedikeret VRAM via PDH-kategorien `GPU Process Memory` -> `Dedicated Usage` (instanser `pid_<pid>_...` summmeres). Genbruger samme PerformanceCounter-infrastruktur som FpsMeter allerede bruger. Cache m. 2s refresh; returnerer null hvis kategorien mangler.
- `GameProcessClassifier.MinimumGameVramBytes = 300 MB`.
- `HasAcquisitionEvidence` er nu 3-trins: (1) DXGI/D3D9 app-presents -> spil; (2) maalbar VRAM >= 300MB -> spil; (3) VRAM ikke maalbar -> fald tilbage til DxgKrnl-tærsklen, men ALDRIG for Store/shell-processer. En shell-proces har maalbar, lav VRAM -> afvises.
- Synlighed: `Snapshot:`-linjen i trace.log har nu `Vram=XXMB` for target-PID'en (brugeren kan se om VRAM bliver brugt).

COD-LANGSOMMELIGHED (analyse): 21:18:43 COD-HQ-vinduet (843x480) afvist -> 21:18:53 kandidat -> 21:18:56 resolution + native agent start -> 21:19:00 lock -> 21:19:03 `ETW events but no usable frame count` (DXGI=20) -> 21:19:10 FPS=187. Dvs. ~17s fra kandidat til FPS. Elementer: COD-HQ-vinduet afvises indtil det rigtige spil-vindue kommer; native agent-process + ETW-session skal startes (~3-5s); evidens-gaten kraever DXGI>=2 i 50ms-vinduet. Mulig optimering (IKKE implementeret): genbrug native agent i stedet for Restart, eller start den ved launch-session-start i stedet for ved foreground-skift.

BUILD + DEPLOY: `dotnet build` 0 fejl. Deploy koert 2x (1. gang ramte build-skrivningsracet). Hash-verifikation OK:
- `IconGrid.dll`: DEPLOYED = BUILD = `A6123DC44F...` (1.212.416 bytes)
- `IconGridFpsAgent.exe`: DEPLOYED = BUILD = `39D1C1E8A4...`

## BRUGER-TEST EFTER VRAM/SHELL-FIX (2026-09-13 21:31-21:38) — ALT PERFEKT

Bruger: "alt kørte perfekt". COD + Division 2 virker; FPS falder naar Start-menu/vinduer aabnes (forventet).

Verifikation fra trace.log (55 KB):
- **Shell-vindue-reglen virker:** `Rejecting foreground PID because the window is a Windows shell window. Name=explorer Class=Progman` (gentagne gange). Ingen explorer-laasning, ingen `Auto-registered external game: Explorer.EXE`.
- **Strukturelle filtre virker:** `Skipping foreground PID 18284 (brave) — module scan confirmed no graphics API DLLs`, samme for `21268 (Code)` og `5932 (Battle.net)`; `Rejecting ... Name=EACLaunch Size=800x450` (for lille); `Skipping 11704 (ApplicationFrameHost) — process lives in a Windows system directory`.
- **VRAM-synlighed virker:** `Snapshot: ... Vram=` stiger 978MB -> 1,40GB -> 1,83GB -> 1,96GB -> 3,50GB -> 4,50GB -> 5,38GB -> 5,39GB for COD (PID 27400). Da brugeren aabnede Start-menuen faldt den til `Vram=73MB` og FPS blev holdt: `Holding COD background FPS during ambiguous ETW sample: holdFps=63`.
- **Ingen fejl-retarget:** COD (PID 27400) forblev target gennem alle vindues-aabninger; ingen `Releasing sticky`, ingen `Foreground game PID changed`.
- COD FPS varierede 55-200 under spil (normalt: afhaenger af scene og fokus).

FPS-FALD VED START-MENU/VINDUER — NORMALT, ikke en IconGrid-fejl:
1. Start-menu/andet vindue tager spillet ud af exclusive fullscreen -> spillet praesenterer gennem DWM-kompositoren (ekstra kopi/komposition).
2. Spillet mister forgrundsfokus -> mange spil (COD isaer) throttler eller pauser rendering naar de ikke er i fokus ("background FPS").
3. Derfor kollapser VRAM (5,39GB -> 73MB) og FPS falder. Det er Windows/spillets adfaerd, ikke pipelinen.
IconGrid gjorde det RIGTIGE: holdt COD som target, holdt sidste FPS, retargetede ikke.

RISIKO NOTERET (ikke et problem nu): `HasAnyGameSignal` bruger nu `HasAcquisitionEvidence` (DXGI/D3D9 ELLER VRAM>=300MB). Et spil der bruger >5s (NonGameProbeTimeout) paa at naa DXGI>=2/300MB VRAM kan i teorien afvises + faa 45s cooldown. Et allerede bekræftet target er beskyttet af sustain-branchen (`confirmed && EtwRunning`). Hvis et langsomt-startende spil nogensinde rammes, er fixet at forlaenge probe-timeout naar VRAM er maalbar og > 0.

IKKE committet/pushet.

IKKE committet/pushet.

## Session findings (2026-09-13, sen aften) — Gaming overlay baggrundshøjde + ping-fix

### 1. Gaming overlay baggrundshøjde (px) — ✅ bruger-verificeret ("spiller super godt")
- Ny persisteret `GamingOverlayBackgroundHeight` (20-120 px, default 44) med slider i et NYT kort "Gaming overlay" på `MonitorRowLayoutPage.xaml` (centraliseret lokalisering, en+da).
- Værdien er design-højden FØR overlay-skalering (44 px @ 100% = 66 px @ 150%).
- `GamingOverlayWindow.xaml`: `<Grid Height="{Binding GamingOverlayBackgroundHeight}">` (var hardcodet 44). Code-behind: `const BaseOverlayHeight` → property der læser VM-værdien live. `MainViewModel.GamingOverlayWindowHeight` bruger samme felt.
- Indgår i `MonitorLayoutDefaultsSnapshot` som **nullable** `double?` — gamle snapshots (uden feltet) rører IKKE brugerens nuværende højde ved "Nulstil til min standard".
- Config-kæden opdateret alle 5 steder: ConfigModel → ConfigState (normaliserer 20-120, ellers 44) → SettingsState → MainViewModel.Settings (apply/default/save) → SettingsPersistence.
- 3 nye lok-nøgler (en+da): MonitorRowOverlayCardTitle / MonitorRowOverlayHeightLabel / MonitorRowOverlayHeightDescription.

### 2. FIX: gear/luk-knap stak ud af baren ved lav højde (brugerrapport)
- Rodårsag: `SettingsMenuButton` + `CloseButton` havde fast `Height="32"`. Ved bar-højde < 32 (fx 20 px) blev knapperne højere end baren og klippet / ikke centreret — alt andet i rækken er `VerticalAlignment="Center"` uden fast højde.
- Fix: `ActionButtonHeight => Math.Min(32, GamingOverlayBackgroundHeight)` anvendes i BÅDE `ApplyWindowSize()` og `ApplyVisualScaleOnly()` (samme steder som margins sættes). Knapperne skrumper nu med baren og forbliver centreret.

### 3. FIX: "Net: 1ms" altid — ping målte routeren (brugerrapport "virker ikke korrekt")
- Rodårsag: `PingTargetMode.Auto` pingede gateway FØRST. Brugerens router (192.168.0.1) svarer `time<1ms` → `PingReply.RoundtripTime = 0` → clampet til `MinPingMs = 1.0` → ALTID "1ms". Bevist på maskinen: gateway = 0 ms, Cloudflare 1.1.1.1 = 12 ms.
- Kommentaren på `MinPingMs` ("clock-tick ~15.6ms") var en fejlslutning — 0 ms er en reelt sub-millisekund LAN-roundtrip. Kommentaren er rettet.
- Fix (bruger valgte "Rettelse A"):
  1. `Auto` = internet-first: **Cloudflare → Google → router (sidste udkald)**. Værdien flytter sig nu med den reelle forbindelse.
  2. Sub-millisekund-samples vises som **`<1ms`** i stedet for et fastlåst `1ms` (nyt felt `_lastSampleSubMillisecond`); stale viser `--ms (<1)`.
- UI-tekster opdateret (en+da): MonitorPingTargetAuto, MonitorPingTargetDescription, MonitorPingPageIntro.
- "Router only (gateway)" er bevaret som eksplicit valg for LAN-latency.

### Verifikation
- Build 0 fejl begge gange; deploy til C:\icongrid med SHA256 BUILD == DEPLOYED hver gang.
- Lokalisering: 269 en / 269 da (paritet) efter ændringerne.
- ✅ COMMITTET + PUSHET: `b5c44db` → origin/main — "feat: adjustable gaming overlay background height and internet-first ping" (14 filer, +201/-21).
- Bruger-feedback: "det spiller perfekt".

### Næste skridt
- Ingen kendte åbne punkter fra denne session.
- Mulig opfølgning: nu hvor Auto er internet-first, kan "Kun router (gateway)" evt. relabeles/omtales klarere — bevaret uændret indtil videre.


## Session findings (2026-09-15) — Monitor Ping flettet ind i Monitor row layout

### Ændring (bruger-ønske: "MonitorPingPage bør ligge inden i MonitorRowLayoutPage")
- **`Views/Settings/Pages/MonitorRowLayoutPage.xaml`**: Nyt expander-kort "Monitor Ping" (samme `StartsideSectionCardStyle` + `MonitorRowExpanderStyle` som sidens øvrige kort, `IsExpanded="False"`) med ping-mål ComboBox (`MonitorPingTargetItems` / `MonitorPingTargetMode`) + Custom-target TextBox (`MonitorPingCustomTarget`). Kortet ligger efter "Gaming overlay background height" og før Reset/Save-knapperne. `StringEqualsToVisibilityConverter` tilføjet til sidens ressourcer (Custom-input vises kun ved mode = "Custom").
- **`Views/Settings/SettingsWindow.xaml`**: `MonitorPingNavButton` ("MP") fjernet fra sidebaren → siden har ikke længere en separat ping-underside.
- **`Views/Settings/SettingsWindow.xaml.cs`**: `MonitorPingNavButton_Click` fjernet.
- **Slettet (dead code efter merge)**: `Views/Settings/Pages/MonitorPingPage.xaml` + `.xaml.cs`.
- **Lokalisering**: `MonitorPingNavTitle` fjernet fra BEGGE dictionaries (en+da) + property + `OnPropertyChanged` (kun sidebaren brugte den, og teksten var identisk med `MonitorPingPageTitle`). Ingen nye nøgler nødvendige — `MonitorPingPageTitle`/`MonitorPingPageIntro` genbruges nu som kort-titel/beskrivelse. Paritet en/da bevaret.
- **`ViewModels/MainViewModel.cs`**: XML-doc på `MonitorPingTargetItems` opdateret til at pege på det nye kort.

### Verifikation
- `dotnet build IconGrid.csproj` → **Build succeeded, 0 fejl** (frisk BAML for begge XAML-filer).
- Grep: 0 resterende referencer til `MonitorPingPage`/`MonitorPingNavButton` i .cs/.xaml (uden for obj).
- **DEPLOY ✅ (15-09-2026 19:34, efter brugeren lukkede IconGrid)**: `deploy-test.cmd` uden "Sharing violation"; SHA256 BUILD == DEPLOYED (`66C784F0...4226E1F5`). Brugeren kan teste fra `C:\icongrid`.
- Ikke committet/pushet (kræver separat bruger-godkendelse).

### Åbent spørgsmål (ikke implementeret)
- Ping-målet indgår IKKE i `MonitorLayoutDefaultsSnapshot`, så "Nulstil til standard"/"Gem nuværende som min standard" på siden rører ikke ping-indstillingen. Kan tilføjes hvis brugeren ønsker det.

### Dokumentation (2026-09-15)
- **NY `PROJECT_STRUCTURE.md`** i repo-roden — komplet mappe-/filoversigt (verificeret mod arbejdstræet), "Where new code goes"-tabel, build/deploy-kommandoer, runtime-data-placering og liste over gitignored stier. Engelsk, som README/ARCHITECTURE_RULES.
- `README.md`: "Monitor ping"-rækken i Settings pages-tabellen fjernet og "Monitor row layout"-beskrivelsen opdateret til at nævne ping-målet; link til `PROJECT_STRUCTURE.md` tilføjet i "Architecture"; `Tools/mcp-notes-server` → `tools/mcp-notes-server` (case-fix).
- `AGENT.md`: `PROJECT_STRUCTURE.md` tilføjet som punkt 4 i "Read first".
- `.local-state/project-structure.md` (gitignored) gjort til kort stub der peger på det nye sporede dokument (den gamle kopi var forældet).



## Session findings (2026-09-19) — FPS-agent: COD-genstart-blindhed + hængende monitor-agent (fixet, deployet)

### Brugerens to rapporter
1. COD bliver ikke fanget (igen) efter at spillet selv genstarter (fx efter en opdatering, ~5 s inde).
2. IconGrid har kørt længe og har fanget falske positiver; desuden **"de 2 processer hænger når programmet lukkes"**.

### A. RODÅRSAG 1 — 12 minutters blindhed efter COD-genstart (trace.log 2026-09-19)
Beviskæde fra `%APPDATA%\IconGrid\trace.log`:
```
02:05:20  Foreground candidate detected: PID=28952 Name=cod
02:05:20  Ignoring foreground PID 28952 because sticky game target PID 8748 (explorer.exe) is still alive
02:05:20  Foreground game PID changed from 8748 to 28952 → agent genstart → FPS=120, overlay 02:05:23 OK
02:05:50  Rejecting foreground PID ... Name=cod Size=854x480   +   Current game PID 28952 has exited. Clearing.
02:06:25  Visible game fallback candidate detected ... PID=14088 Name=cod Area=3686400
02:06:28  [NativeFpsAgentRunner] start requested ... ForegroundGamePid=14088 -> laaser 14088, starter ETW,
          skriver EN state-fil (02:06:28.766, etwRunning=true, ALLE counters=0, gameConfirmed=false)
   -> INTET derefter i 12 minutter (02:07:02-02:11:27 kun "native FPS state was unavailable during probe timeout")
02:18:45  foerst HER genstartes agenten (fordi Taskmgr blev "visible game fallback")
```
- Agent-processen VAR væk (ca. 02:16 viste `tasklist` ingen `IconGridFpsAgent.exe`, mens `native-fps-state.json` stod stille). Ingen WER-hændelse 1000/1001 -> ikke et managed crash; proces afsluttet uden oprydning. **Sandsynlig kode-aarsag (ikke endeligt bevist):** `g_etwThread` er ubeskyttet — `StopEtwSession()` (`main.cpp:1493`, `g_etwThread.join()`) kaldes fra target-traaden samtidig med at main-loekkens `StartEtwSession()` tildeler `g_etwThread = std::thread(...)` (`main.cpp:1484`) -> race der kan give `std::terminate()` uden WER-spor.
- `SystemMonitor.FpsTimer_Tick` (SystemMonitor.cs:745) saetter `IsInGame = nativeFpsState?.TargetPid > 0`, og `WriteSharedFpsState` (main.cpp:345-388) publicerer `targetPid` KUN naar `gameConfirmed` (DXGI/D3D9>=2). Uden agent-output -> `targetPid=0` -> **overlayet vises aldrig** (ingen `[GamingOverlay]`-linjer efter 02:05:23).
- **Ingen watchdog:** C# genstarter kun agenten naar game-PID'en aendrer sig -> en doed/haengt agent holdt pipelinen nede i 12 min.

### B. RODÅRSAG 2 — "de 2 processer hænger" (bevist)
- Arkitekturen starter agenten to steder fra: (1) launcheren (`HardwareMonitorTaskManager.StartAgent`, med `--parent-pid` + `--shutdown-event`), (2) **Task Scheduler `\IconGrid Monitor` -> `C:\IconGrid\IconGrid.exe --monitor-agent`** (verificeret Enabled, At logon, Highest) — sidstnaevnte **uden foraelder og uden shutdown-event**. `ParentIsAlive(null)` = altid true og `TryOpenShutdownEvent` = null -> agenten kunne hverken afslutte sig selv eller signaleres; dens barn `IconGridFpsAgent.exe` fulgte med.
- **Ekstra defekt fundet under testen:** `StartAgent` havde `using var shutdownEvent = ...` -> event-handlen blev frigivet **foer** den netop spawnede agent naaede at `OpenExisting`, saa agenten loggede `Shutdown event was not found: Local\IconGrid.HardwareMonitorAgent.Stop.<launcherPid>` (set 02:28:59). Ogsaa launcher-startede agenter var derfor ustopbare.
- Tracebevis fra den oprindelige session: `02:09:47/02:14:12 Hardware monitor shutdown event was not present.` + `02:10:14 Timed out waiting for the previous hardware monitor agent to release the mutex.`
- **ETW-laek:** `logman query -ets` viste `IconGridFpsAgent_ETW = Running` efter at ALLE IconGrid-processer var draebt (og efter at `Local\IconGrid.NativeFps.Live` var vaek). `DisposeProcess()` bruger `Process.Kill()` -> `StopEtwSession()` blev aldrig koert (ETW real-time sessions overlever processen). Ryddet manuelt med `logman stop "IconGridFpsAgent_ETW" -ets`.


### C. IMPLEMENTERET (trin 1-3, bruger-godkendt plan)
1. **Diagnostik:** native `main.cpp` skriver nu exit-aarsag til state-JSON (`"Exiting: worker loop finished (parent gone or stop requested)."`); `NativeFpsAgentRunner.TryGetProcessExitCode()` rapporterer barnets exit-kode, saa trace kan skelne graceful exit fra kill/crash.
2. **Livscyklus (ny fil `Helpers/Hardware/MonitorAgentLifecycle.cs`, 167 linjer):** velkendt `Local\IconGrid.HardwareMonitorAgent.RequestStop`-event (ikke PID-bundet) + `LauncherPresenceTracker` (1 s probe-interval). Brugt i:
   - `HardwareMonitorAgent.Run`: mutex-timeout -> signalér RequestStop -> vent 15 s igen (handover i stedet for "ingen agent tilbage"); agenten venter ogsaa paa RequestStop i loekken; `Reset()` efter mutex-overtagelse; **uden `--parent-pid`** afslutter agenten naar ingen IconGrid-launcher har vaeret der i 60 s (`LauncherAbsenceGracePeriod`).
   - `HardwareMonitorTaskManager.SignalCurrentAgentToStop`: falder tilbage til RequestStop naar per-PID-eventet mangler.
   - `HardwareMonitorTaskManager.StartAgent`: `_launcherShutdownEvent` holdes i live for processens levetid (ikke laengere `using`).
3. **Watchdog (ny fil `Helpers/Hardware/FpsAgentWatchdog.cs`, 93 linjer):** hvis en game-PID holdes og native state er vaek/stale i >5 s, eller barnet er exited, genstartes den native agent (min. 10 s mellem restarts, `Restart(parentPid, LastForegroundGamePid)` saa trusted-launch/config-target-tilstand bevares).

### D. VERIFIKATION
- Native: MSBuild `FpsAgent.vcxproj` -> `IconGridFpsAgent.exe` SHA256 `8AA06E3A...` (identisk i `Native\...\bin\Debug`, `bin\Debug\...\Tools\FpsAgent` og `C:\icongrid\Tools\FpsAgent`).
- C#: `dotnet build IconGrid.csproj` -> **0 advarsler / 0 fejl**. `IconGrid.dll` SHA256 `5D05D021...` i baade `bin\Debug` og `C:\icongrid` (foerste deploy gav "Sharing violation" = gammel hash; koert igen jf. cheatsheet §11 -> hash matcher).
- **Livetest 1 (request-stop):** signallede eventet eksternt -> trace: `Exiting because a monitor agent stop was requested.` + `Hardware monitor agent exited gracefully.` -> **den elevated agent OG FPS-agenten forsvandt** (kun launcheren tilbage).
- **Livetest 2 (Task Scheduler-vejen):** startede `IconGrid.exe --monitor-agent` (uden foraelder), draebte launcheren -> trace: `Exiting because no IconGrid launcher process has been present for 62s.` + `Hardware monitor agent exited gracefully.` -> **0 IconGrid-processer tilbage**.
- **Opstartsverifikation:** den nye build logger ikke laengere `Shutdown event was not found` (den gamle build gjorde kl. 02:28:59).
- `deploy-test.cmd` **starter ikke** appen (sidste linje er kun `echo Start: ...`) — start manuelt med `Start-Process 'C:\icongrid\IconGrid.exe'`.

### E. ARKITEKTUR-NOTE (violation, dokumenteret)
- `Helpers/Hardware/HardwareMonitorAgent.cs` er nu **2156 linjer mod graensen 1500** (var 2096 foer denne session; +~60 for mutex/loekke/watchdog-hook). Nye features blev derfor lagt i nye filer ovenfor. **Foreslaaet udtraek ved naeste lejlighed:** flyt evidens-helperne `HasUsableFrameSignal` / `HasAcquisitionEvidence` / `IsStickyTargetConfirmed` / `IsCurrentTargetStillOwned` / `HasAnyGameSignal` (~130 linjer) til `Helpers/Hardware/GameTargetEvidence.cs` (mekanisk flytning, ingen logikaendring).

### F. IKKE LØST ENDNU (naeste skridt)
- **COD-test mangler:** start COD (gerne med den selv-genstart/opdatering) -> forvent i trace: `FPS agent watchdog: restarting the native FPS worker. Reason=...` hvis agenten falder ud, i stedet for 12 minutters blindhed.
- **Falske positiver (bekraeftet, ikke fikset):** (a) `explorer.exe` var "spil" i ~3,5 t med opdigtet `FPS=81 Source=DxgKrnlFallback` — `HasAcquisitionEvidence` trin 3 falder tilbage til `IsGameEvidence` naar PDH-VRAM ikke kan maales, og `RequiresPrimaryGraphicsEvidence` daekker KUN Store/WindowsApps-stier (ikke `C:\Windows`); (b) `TryGetVisibleGamePidFallback` (HardwareMonitorAgent.cs:637) mangler `IsNonGameProcessPath`-filteret som forgrundsstien har -> **Taskmgr.exe blev target 02:18:45**; (c) `PollLockedTarget`'s "grace expired" re-scanner ikke reelt (kommentar/kode-uoverensstemmelse, `main.cpp:1094-1112`) -> et levende-ikke-renderende target holdes i det uendelige.
- **ETW-laek:** FPS-agentens kill-sti rydder stadig ikke ETW-sessionen (RequestStop daekker monitor-agenten, ikke FPS-agenten).
- **WER LocalDumps er ikke aktiveret** paa maskinen (`HKLM\...\Windows Error Reporting\LocalDumps` findes ikke) — kraever elevation hvis vi vil have crash-dumps af FPS-agenten.
- Ikke committet/pushet (kraever separat bruger-godkendelse).

## Session findings (2026-09-19, del 2) — VRAM gjort til AUTORITET for spil-detektion (ingen navne-/stilister)

### Bruger-beslutning
"vi skal ikke bruge nogen filter fordi der findes så mange programmer, vi skal derimod bruge VRAM use for at detekte spil" → bekraeftet med data fra den koerende session. Jeg valgte den bedste loesning: VRAM er nu den primaere klassificering, kernel-present klassificerer aldrig.

### Data der grundlagde reglen (live trace 19-09 02:46-02:53)
- `cod.exe`: 4,69-5,42 GB (peak 5,42 GB). Under load/alt-tab: 790 MB, 835 MB, 838 MB, **85 MB**.
- `explorer.exe`: **166 MB** peak (havde vaeret "spil" i ~3,5 t med opdigtet FPS=81 via DxgKrnl-fallback).
- `Taskmgr.exe`: **12 MB** → blev target 02:47:44-02:48:37 mens COD koerte (5,29 GB, DXGI=11). Aarsag: `TryGetVisibleGamePidFallback` valgte efter vinduesareal/score uden VRAM.
- `brave`, `Code`: 0 MB. `TextInputHost`: 12 MB.
- VRAM var maalbar i 9180 af 9457 snapshots (97 %).

### NY REGEL (ét sted: `Helpers/Hardware/GameVramEvidence.cs`)
```
SPIL      = peak(VRAM) >= 500 MB            ELLER  app-presents (DXGI/D3D9) >= 2
IKKE SPIL = peak(VRAM) < 250 MB   OG  ingen app-presents   (maalt, ikke gættet)
UNKNOWN   = derimellem, eller VRAM ikke maalbar
```
- **Peak-hold pr. PID (15 min)** fordi et spil frigiver VRAM under load/alt-tab (COD: 85 MB midt i en transition) — en momentan graense ville droppe spillet. PID-reuse er begraenset af hold-vinduet.
- **Kernel-present (DXGKRNL) klassificerer ALDRIG.** Det var netop kernel-reglen der lukkede explorer ind. Den bruges fortsat kun til at *beregne* et FPS-tal for et allerede bekræftet target.
- **Afvisning kraever maalt bevis:** `IsMeasurablyNotGame` returnerer false naar VRAM er ikke-målbar → et target afvises aldrig pga. manglende data (kun hvis maalt lav VRAM + ingen presents).
- **Ansigt udad:** et target kan derfor vaere "UNKNOWN" (hverken bekræftet eller afvist) og holdes under probe-vinduet i stedet for at faa en 45 s cooldown paa falsk grundlag.

### Fjernede filtre (jf. brugerens princip)
- `GameProcessClassifier.RequiresPrimaryGraphicsEvidence` (Store-sti-krav) og `MinimumGameVramBytes` (300 MB, nu 500 MB i den nye regel) er FJERNET.
- `IsNonGameProcessPath` er fjernet fra FPS-klassificeringen: forgrundsstien (gammel linje 617), `IsStickyTargetConfirmed`, `TryAutoRegisterExternalGame` og `HasAnyGameSignal` (helt slettet som doed kode).
- `NonGameProcessNames`/`IsNonGameProcessPath` bruges nu KUN af `DisplayResolutionService` (opløsningslaas ved spil-start) — ikke af FPS/spil-detektion.
- Tilbage er kun strukturelle, ikke-navnebaserede guards: self-proces (`IconGrid*`), shell-vindues-klasser (Progman/taskbar — desktoppen er skaermstor), overlay-vindue-stile, og modul-scan (læser processen dxgi/d3d11 ind?) som optimistisk skip. Modul-scannet kaldes nu KUN for kandidater der allerede har spil-lignende VRAM (rækkefølgen er byttet), hvilket samtidig fjerner hang-risikoen ved `CreateToolhelp32Snapshot` på beskyttede processer.

### Implementerede aendringer
| Fil | Aendring |
|---|---|
| **NY** `Helpers/Hardware/GameVramEvidence.cs` (204 linjer) | Den nye regel + peak-hold + `IsMeasurablyNotGame` + `GetPeakBytes` |
| `GpuProcessMemory.cs` | Permanent `_categoryUnavailable`-latch FJERNET → retry hver 30 s (`UnavailableRetryInterval`) + ny `LastUnavailableReason` (synlig i trace). Foer kunne én transient PDH-fejl slaa VRAM-evidensen fra for resten af processens levetid og dermed lydloest nedgradere detektion til kernel-reglen |
| `HardwareMonitorAgent.cs` | `HasAcquisitionEvidence` → den nye regel; VRAM-gate + trace-log i `TryGetVisibleGamePidFallback`; afvisning kraever maalt ikke-spil; `Peak=` tilfoejet snapshot-linjen; filtre fjernet (se ovenfor) |
| `GameProcessClassifier.cs` | To nu-ubrugte medlemmer fjernet (én regel ét sted) |
| `SystemMonitor.cs` | `IsInGame` foelger nu samme regel (native `GameConfirmed` ELLER VRAM-evidens for target-PID'en) — bevarer eksisterende adfaerd og tllfoejer VRAM-vejen |

### Verifikation
- `dotnet build IconGrid.csproj` → **0 advarsler / 0 fejl** (2 builds). Deploy hash-verificeret: `IconGrid.dll` `6ED9EA61...` i baade build og `C:\icongrid` (foerste deploy gav "Sharing violation" fordi den elevated agent stadig lukkede ned → koert igen jf. cheatsheet §11).
- **Live-bevis efter deploy (03:00):**
```
Visible window candidate PID=8748 (explorer)   skipped: dedicated VRAM peak 166MB < 250MB and no application-level presents.
Visible window candidate PID=10144 (TextInputHost) skipped: dedicated VRAM peak 12MB < 250MB and no application-level presents.
Visible window candidate PID=15896 (Code)      skipped: dedicated VRAM peak 0MB < 250MB and no application-level presents.
Visible window candidate PID=4816 (brave)      skipped: dedicated VRAM peak 0MB < 250MB and no application-level presents.
```
→ Samme fire vinduer som foer blev kandidater; nu afvises de paa deres EGEN maalte VRAM. Taskmgr-scenariet (12 MB mod spillets 5,3 GB) kan ikke gentages.

### Status / naeste
- Appen koerer fra `C:\icongrid` med den nye regel. **COD-test mangler** (brugeren): forvent `Foreground candidate detected ... Name=cod` og snapshot med `Vram=4,69GB Peak=4,72GB`, samt `Visible window candidate ... skipped` for alt andet.
- Arkitektur-note opdateret: `HardwareMonitorAgent.cs` er nu **2125 linjer** (graense 1500) — stadig en dokumenteret violation; udtraek af evidens-helperne til `GameTargetEvidence.cs` er fortsat den anbefalede oprydning.
- Ikke committet/pushet.


## Session findings (2026-09-19, del 3) — Klokke i gaming overlayet

### Bruger-ønske
"i vores gaming overlay lige før vores ping 12MS. kan du ikke vise klokken." → implementeret.

### Ændringer
- **`Helpers/Launcher/SystemMonitor.cs`**: ny `ClockText`-property (lokal tid) + `UpdateClock()` kaldt fra `FpsTimer_Tick`. Formateres med brugerens EGET Windows-mønster (`CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern`, fx `03:12` i DK / `3:12 AM` i US) — ingen hardcodet format. `PropertyChanged` rejses kun når teksten faktisk ændrer sig, så overlayet ikke invalideres hver 250 ms. Lagt i `SystemMonitor` fordi det er overlayets live-telemetri (ARCHITECTURE_RULES forbyder realtids-polling i `MainViewModel`).
- **`Views/Launcher/GamingOverlayWindow.xaml`**: klokke-`TextBlock` (genbrug af `MonitorTextStyle` + `MonitorDividerStyle`, `MinWidth="40"`, højrestillet) indsat som FØRSTE element i monitor-rækken, dvs. umiddelbart før ping-prikken og "Ping: 12ms". Ingen ny styling/effekt, ingen nye lokaliseringsnøgler (et klokkeslæt er ikke oversættelig tekst).
- Layout nu: `[03:08] │ ● Ping: 12ms │ CPU: xx° │ ▮ │ GPU: xx° │ ▮ │ FPS: xx`

### Verifikation
- `dotnet build IconGrid.csproj` → **0 advarsler / 0 fejl** (XAML/BAML kompileret).
- Deploy hash-verificeret: `IconGrid.dll` `7D10F355...` i baade build og `C:\icongrid`. Appen genstartet og koerer.
- Ping vises kun ét sted i XAML (`GamingOverlayWindow.xaml:246`), så der er kun denne række at holde ved lige.
- **Bruger-verifikation mangler:** se at klokken staar til venstre for ping-prikken i overlayet. Ønskes sekunder, er det ét format-skift i `UpdateClock`.

### Bonus-bekraeftelse fra brugeren
"det ser ud til at virke super godt, og gaming overlayet starter også hurtigere op i spillet nu." → VRAM-reglen (del 2) bekreaftet i praksis.


### Opfoelgning — afstand omkring klokken (bruger-feedback)
Bruger: "for stor mellemrum i forhold til resten af elementerne".
- **Aaarsag:** overlayets spaceringsmodel er at divideren `|` (`MonitorDividerStyle`) har `Margin="0,0,12,0"` — dvs. **0 px venstre-margin**. Afstanden FOER en divider kommer derfor udelukkende fra det foregaaende elements hoejre-margin (CPU/GPU-teksterne bruger `Margin="0,0,9,0"`). Mit foerste forsoeg gav klokken `MinWidth="40"` + `TextAlignment="Right"`, hvilket lagde ~7-10 px luft i boksen ud over teksten.
- **Fix:** fjernet `MinWidth` og `TextAlignment` fra klokke-TextBlock; beholdt `Margin="0,0,9,0"`. Klokken har nu praecis samme rytme som de oevrige elementer: `03:08` + 9 px + `|` + 12 px + ping-prik.
- Byg 0 fejl, deploy hash-verificeret (`AB68E648...`), app genstartet.
- Hvis brugeren stadig synes det er for luftigt: naeste skridt er at fjerne divideren efter klokken (=> `03:08 ● Ping: 12ms`).


### Retteise 2 — ensartet afstand i HELE monitor-raekken (bruger: "den boer saa have samme mellemrum som resten")
Brugeren praeciserede at problemet var luften EFTER divideren der foelger "Net: 12ms", altsaa foer `CPU:`.

**Maalt med WPF FormattedText (Segoe UI 12, samme som overlayet):**
`Ping: 12ms`=57,7 · `CPU: 45°`=45,7 · `CPU: 100°`=52,2 · `GPU: 45°`=46,5 · `03:08`=28,5.

**Aarsag:** `Width="75"` (CPU) og `Width="64"` (GPU) + `TextAlignment="Right"` betoed at teksten blev skubbet til HOEJRE i boksen → **29 px / 17 px tom luft til venstre**, dvs. praecis efter divideren. Tilsvarende gav `MinWidth="70"` paa net-teksten ~12 px luft FOER dens divider.

**Fix (ensartet rytme: 9 px foer hver divider, 12 px efter):**
| Element | Foer | Efter |
|---|---|---|
| Klokke | `MinWidth=40` + Right | `Margin="0,0,9,0"` (hugger teksten) |
| Net-tekst | `MinWidth="70"` | `Margin="0,0,9,0"` |
| CPU-tekst | `Width="75"` + Right | `Margin="0,0,9,0"` (ingen fast bredde) |
| CPU-bar | `Margin="0,0,10,0"` | `Margin="0,0,9,0"` |
| GPU-tekst | `Width="64"` + Right | `Margin="0,0,9,0"` (ingen fast bredde) |
| GPU-bar | `Margin="0,0,10,0"` | `Margin="0,0,9,0"` |
Alle `MonitorDividerStyle`-divider har fortsat `Margin="0,0,12,0"` (0 venstre / 12 hoejre) — uændret, saa rytmen er nu identisk hele vejen. `TextAlignment="Right"` er beholdt (no-op uden fast bredde, saa en fremtidig bredde opfoerer sig som foer).

**Konsekvens:** overlayet bliver ~40 px smallere. Ved 3-cifrede temperaturer (100°) vokser CPU/GPU-teksterne ~6 px (teksten hugger altid, ingen luft) — acceptabelt og ensartet. FPS-gruppen (`Width="54"`, Margin 10) er bevidst UROERT (sidste element, moeder knapperne, ikke en divider).

- Byg 0 fejl · deploy hash-verificeret (`C55F36F7...`) · app genstartet.


## Session findings (2026-09-19, del 4) — ETW-laek FIXET (graceful stop af FPS-worker)

Det tidligere noterede aabne punkt "ETW-sessionen laekker hvis FPS-agenten draebes" er nu loest.

**Aarsag:** `NativeFpsAgentRunner.DisposeProcess()` brugte udelukkende `Process.Kill()`. En draebt proces naar aldrig at koere `StopEtwSession()`, og en ETW real-time session overlever processen -> `IconGridFpsAgent_ETW` blev efterladt "Running" (verificeret flere gange, fx lige efter et deploy).

**Loesning (samme moenster som monitor-agenten fik):**
- Native `main.cpp`: nyt `--stop-event <navn>`. Agenten aabner eventet (`OpenEventW(SYNCHRONIZE)`) og tjekker det som foerste i hovedloekken -> saetter `stopRequested`, bryder ud, og den normale exit-sti koerer `StopEtwSession()` + skriver en sidste state-fil. Exit-aarsag skelnes nu: `"Exiting: graceful stop requested (ETW session stopped)."` vs `"Exiting: parent gone or runner loop ended (ETW session stopped)."`
- `NativeFpsAgentRunner.cs`: holder et velkendt event `Local\IconGrid.NativeFpsAgent.Stop` (oprettes i ctor), sender `--stop-event` med, `Reset()` foer hver start, og `DisposeProcess()` goer nu: **signalér -> `WaitForExit(900)` -> kun hvis den ikke stopper: `Kill()` (med trace-linje) -> `Reset()`**. Eventet frigives i `Dispose()`.

**Verifikation (isoleret test, ikke gætværk):** startede agenten manuelt med `--root-pid <explorer> --stop-event Local\IconGrid.FpsAgent.StopTest`, lod den laase target og starte ETW, satte derefter eventet:
| | Foer stop | Efter stop |
|---|---|---|
| Proces | kørte (PID 28208) | **væk** (afsluttede sig selv) |
| ETW | `IconGridFpsAgent_ETW` **Running** | **ingen session** |
| State | `etwRunning=true` | `etwRunning=false` + `"Exiting: graceful stop requested (ETW session stopped)."` |
- Byg: native MSBuild + `dotnet build` = 0 fejl. Deploy hash-verificeret.
- Restrisiko (dokumenteret): draebes agenten haardt (Task Manager) eller crasher den, kan sessionen stadig efterlades — men naeste agent-start stopper den by-name (`ControlTraceA(0, kSessionName, STOP)` i `StartEtwSession`), og det er nu den eneste vej til en orphan.



## Session findings (2026-09-19, del 5) — spil i baggrunden blev IKKE fundet (FIXET)

**Bruger-rapport:** "COD har hele tiden koert i baggrund ... da du startede IconGrid, saa startede launcher fint op, men gaming overlayet kom IKKE, selvom COD er open." (+ praecisering: "COD er ikke minimeret, det koerer fullscreen window eller borderless".)

**Aarsag (bevist):** Fallback'en `TryGetVisibleGamePidFallback` havde `if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;` som ALLERFOERSTE filter — og **Windows skjuler vinduet paa et fullscreen-spil naar det mister fokus** (`IsWindowVisible=false`). Spillet blev derfor filtreret vaek foer VRAM-maalingen, helt tavst (ingen log), og `cod.exe` optraadte ikke i kandidatlisten (kun Code, brave, SystemSettings, explorer, TextInputHost, codCrashHandler). Maalt paa COD's vindue: class="COD", rect 0,0 2560x1440, `style=0x15000000`, `exstyle=0x0` — dvs. intet i vinduets stil afviste det; det var udelukkende synligheds-tjekket.
- Samtidig maalt: COD (skjult) holdt **5094,8 MB dedikeret VRAM** og brugte **9,18 % GPU-engine** → VRAM-evidensen var der hele tiden.
- Og: en skjult COD **udsender stadig app-presents** (agent-test: `matchedDxgiEventCount=4`, `gameConfirmed=true`, FPS=90-91, `Source=PrimaryApi`) → hele kæden virker, naar blot target bliver valgt.

**Fix i `HardwareMonitorAgent.TryGetVisibleGamePidFallback`:**
1. Synligheds-tjekket filtrerer ikke laengere: `var windowIsUsable = IsWindowVisible(hwnd) && !IsIconic(hwnd);`. Er vinduet ikke brugbart (skjult/minimeret), springes geometri-tjekket over og kandidaten bedoemmes **udelukkende** paa VRAM-reglen. Synligt+stort vindue foretraekkes stadig (score fra areal/alder), mens et baggrundsvindue faar `score=1` (accepteres kun hvis intet bedre findes).
2. `GetWindowThreadProcessId` flyttet op, saa PID er kendt i fejlstien.
3. **Ingen tavse skips laengere:** den tomme `catch {}` logger nu `"Visible window candidate PID=... skipped: <ExceptionType>: <message>"`.
4. `GetWindowRect`-fejl er ikke laengere et hardt afvisningskriterium (area bliver 0 og kandidaten bedoemmes paa VRAM).

**Verifikation (bruger-test):** IconGrid lukket HELT og genstartet med COD koerende i baggrunden:
```
03:37:28.986  Visible game fallback candidate detected behind IconGrid foreground:
              PID=26204 Name=cod Area=3686400 Score=1 Age=955s
03:37:35.067  [NativeFpsAgent] FPS=90 ... TargetPid=26204 Target=cod.exe DXGI=4 Source=PrimaryApi
```
`Score=1` = den nye skjult-vindue-sti, `Age=955s` = spillet havde koert i ~16 min. Bruger: **"saa kom gaming overlayet frem med det samme perfekt"**. `codCrashHandler` (0 MB VRAM) blev korrekt afvist i samme gennemloeb.
- Byg 0 fejl · deploy hash-verificeret (`0556EAD9...`) · IKKE committet (afventer brugerens godkendelse).


## Session findings (2026-09-19, del 6) — dwm.exe blev falsk spil (FIXET)

**Fund (i loggen lige efter del 5-fixet):**
```
04:12:09  Visible game fallback candidate detected ... PID=25884 Name=dwm Area=4480 Score=1 Age=5106s
04:12:17  Auto-registered external game: dwm.exe at C:\WINDOWS\system32\dwm.exe
```
`dwm.exe` (desktop-kompositoren) holdt **921 MB** dedikeret VRAM paa en 2560x1440-session -> over 500 MB-graensen -> slap igennem den nye "skjult vindue bedoemmes paa VRAM"-sti, blev **tracked target** OG **auto-registreret** i `external-games.json` (vedvarende forurening).

**Maalt paa DWM's vindue:** `vis=False ico=True size=160x28 class=[Dwm] title=[DWM Notification Window]` — dvs. et PERMANENT lille, skjult hjaelpevindue. Et titel-tjek ville IKKE have hjulpet (den har en titel).

**Aarsag:** da jeg fjernede `IsIconic/!IsWindowVisible`-filtrene, forsvandt ogsaa *stoerrelses*-kravet for skjulte vinduer — og DWM's 160x28-vindue kvalificerede derfor alene paa VRAM.

**Fix (strukturel, ingen navneliste):** nyt `TryGetWindowSize()` bruger `GetWindowRect`, og for minimerede/skjulte vinduer falder den tilbage til **`GetWindowPlacement().rcNormalPosition`** (den GENDANNEDE stoerrelse, som forbliver spill-stoerrelse ogsaa naar vinduet er skjult). Fallback'en kraever nu `width >= 960 && height >= 540` for ALLE kandidater:
- DWM's notification-vindue = 160x28 -> afvises ("window 160x28 is too small for a game") ✓
- COD skjult = 2560x1440 -> accepteres fortsat ✓ (del 5-fixet er intakt)
- Nye P/Invoke + structs: `GetWindowPlacement`, `WINDOWPLACEMENT`, `POINT` (ingen dubletter i filen).

**Oprydning:** den falske `dwm.exe`-post blev fjernet fra `%APPDATA%\IconGrid\external-games.json` (registret lastes ved opstart, saa den skulle vaek inden genstart).

**Verifikation:**
```
04:16:59.334  Visible window candidate PID=25884 (dwm) skipped: window 160x28 is too small for a game.
04:17:04.047  Visible window candidate PID=25884 (dwm) skipped: window 160x28 is too small for a game.
```
Ingen `Auto-registered external game: dwm.exe` efter fixet. COD var lukket paa testtidspunktet (`cod-processer: 0`), saa intet target = korrekt adfaerd.
- Byg 0 fejl · deploy hash-verificeret (`A1C2795F...`) · app genstartet · IKKE committet (afventer godkendelse).

**Laere til fremtiden:** VRAM alene er IKKE nok som kandidat-filter — kompositoren (dwm) holder store VRAM-maengder som funktion af skrivebordet, ikke af et spil. Et strukturelt vindues-krav (spil-stoerrelse, gendannet rect) er noedvendigt ved siden af VRAM.


## Session findings (2026-09-19, del 7) — spil-lukningsstien verificeret

Bruger lukkede COD; jeg tjekkede hele afslutnings-kaeden (post-conditions):

| Kontrol | Resultat |
|---|---|
| Target ryddet | `native-fps-state.json`: `targetPid=0`, `etwRunning=false`, `debug="No running process matched the current launch session."` — ryddet straks da `cod.exe` forsvandt |
| ETW-session | `logman query -ets \| Select-String IconGrid` = **TOM** → agentens `StopEtwSession()` koerte. Dvs. graceful-stop-fixet (del 4) er nu ogsaa bevist paa den RIGTIGE vej (target doer), ikke kun i den isolerede test |
| Processer | Kun de 3 normale: launcher + monitor-agent + idle FPS-agent. Ingen orphans |
| Baggrunds-scan | **Ingen tavse drops**: alle kandidater maales og logges — `AcPowerNotification` 0MB, `AUEPMaster` 0MB, `CrossDeviceResume` 0MB, `ArmouryCrate.UserSessionHelper` 0MB, `svchost` 0MB, `PowerToys.QuickAccess` 35MB, `explorer` 166MB, `SystemSettings` 4MB → alle korrekt afvist. Ingen falsk positiv (hverken dwm, taskmgr, shell eller svchost) |
| Overlay | Ingen `IsInGame` (`targetPid=0` + `etwRunning=false`) → lukkede korrekt |

**Konklusion:** alle fire fix fra i dag holder ogsaa i afslutningsfasen — VRAM-klassificeringen, agent-livscyklussen, den graceful ETW-stop og baggrunds-/stoerrelsesreglen.

**Dagens commits (alle pushet til origin/main):** `75c0b5a` (Monitor Ping-UI), `b1e8747` (VRAM-klassificering + livscyklus), `d8ee3cb` (graceful FPS-stop + baggrunds-spil), `9aa7845` (dwm/kompositor-regel).

**Naeste session — anbefalet raekkefoelge:**
1. Test exclusive fullscreen (alt-tab ud af COD i exclusive mode) og bekraeft at den gendannede rect stadig giver target.
2. Oprydning: udtraek evidens-/vindues-helperne (`HasAcquisitionEvidence`, `IsStickyTargetConfirmed`, `IsCurrentTargetStillOwned`, `HasUsableFrameSignal`, `TryGetWindowSize`) til `Helpers/Hardware/GameTargetEvidence.cs` → bringer `HardwareMonitorAgent.cs` under 2000 linjer og taettere paa 1500-graensen.
3. Overvej at lade `TrustedLaunch`-flaget (spil startet fra IconGrid) ogsaa omfatte baggrunds-scenariet, saa et spil startet via launcheren aldrig afhaenger af VRAM-maalingen.



## Session findings (2026-09-19, del 8) — "overlay kommer ikke igen efter mange timers kørsel" (FIXET)

**Bruger-rapport:** IconGrid havde kørt i mange timer; COD blev startet igen, men gaming overlayet kom ikke. Genstart af IconGrid eller "reset FPS agent" fiksede det — men det bør ikke være noedvendigt.

**Bevis (trace + config):**
```
19:21:45  Visible window candidate PID=8644 (pythonw) skipped: dedicated VRAM peak 0MB   <- fallback afviste den korrekt
19:22:03  Foreground candidate detected: PID=8644 Name=pythonw                          <- forgrundsstien har ingen VRAM-regel
19:22:03  Foreground game PID changed from null to 8644                                  <- laast som target
(5 timer senere, mens COD koerte:)
22:16:43  Skipping external game registration for PID 8644: no trustworthy game evidence
   targetPid=8644, DXGI=0 D3D9=0 DXGKRNL=0, gameConfirmed=false, Vram=0MB Peak=0MB
```
`config.json` -> `FpsTarget` er **tom** (alle felter null).

**ROOT CAUSE (gammel logik-fejl, ikke relateret til dagens VRAM-arbejde):**
`MatchesConfigTargetProcess(configTarget, process)` afslutter med `return true` naar config-targetet ikke har nogen identitet (ingen `ExecutableName`, ingen `ResolvedExecutablePath`) — den matcher derfor **enhver proces**. Dermed:
1. `IsConfiguredTargetAuthoritative(emptyConfig, nativeState)` -> **true** (fordi `nativeState.TargetPid > 0` og "matchet" lykkedes).
2. Config-grenen koerer -> `nativeOwnsConfigTarget` = true -> `confirmedForegroundGamePid = nativeState.TargetPid` (**bekraeftet uden EN eneste evidens**) og `currentForegroundPidObservedAtUtc = null` (**probe-timeren slaas fra**).
3. Den tidlige `return` (linje ~1198-1203) betyder at hverken non-game-afvisningen eller retarget-stien naas.
4. Fallback'en koerte ALDRIG, fordi den var gated paa `!currentForegroundGamePid.HasValue` — og pythonw holdt altid en vaerdi.
=> Et ikke-spil blev holdt som "bekraeftet spil" i det uendelige; COD i baggrunden kunne aldrig tage target tilbage. Genstart/reset virkede, fordi tilstanden blev genopbygget.

**FIX (3 lag):**
1. `IsConfiguredTargetAuthoritative`: returnerer nu false uden `HasConfiguredTargetIdentity(configTarget)` -> et tomt target kan aldrig vaere autoritativt.
2. `MatchesConfigTargetProcess(configTarget, process)`: returnerer false uden identitet -> "tomt target matcher alt" er ikke laengere muligt.
3. **Challenger-fallback:** `TryGetVisibleGamePidFallback` koeres nu ogsaa naar der holdes et target UDEN spil-evidens (`!HasAcquisitionEvidence(nativeState)`), saa et koerende baggrunds-spil altid selv tager target tilbage — uden genstart eller reset.

**Verifikation (deployet, hash `00FBA671...`):** IconGrid startet mens COD havde koert i baggrunden i 431 s:
```
22:20:33.531  Visible window candidate PID=8644 (pythonw) skipped: dedicated VRAM peak 0MB < 250MB
22:20:33.556  Visible game fallback candidate detected: PID=24584 Name=cod Area=3686400 Score=1 Age=431s
22:20:38.806  [NativeFpsAgent] FPS=92 ... TargetPid=24584 Target=cod.exe DXGI=4 gameConfirmed=true
```
-> COD fanget inden for ~1 s, pythonw afvist, og de tidligere `Skipping external game registration`-linjer er vaek. `Score=1` bekreafter samtidig den skjulte-vindue-vej fra del 5.

**Bonus-fund (deploy):** `deploy-test.cmd` fejlede gentagne gange med "Sharing violation" fordi den koerende monitor-agent var startet af **Task Scheduler** (`parentPid: 0` i state) og derfor holder DLL'erne i op til 60 s (min nye "ingen launcher"-regel), og `taskkill` kan ikke draebe en elevated agent. Scriptet signalerer nu **request-stop-eventet** foerst (`Local\IconGrid.HardwareMonitorAgent.RequestStop`) + 3 s ventetid -> deploy lykkedes paa foerste forsoeg. Linjen er verificeret inde i cmd (`exit=0`).


## Session findings (2026-10-17) — FASE 0: evidens-telemetri (implementeret + deployet)

**Bruger-rapport:** lokale AI-projekter (LM Studio, Grounded-SAM/SAM+DINO) trigger gaming overlay; overlay skal kun komme ved spil. Brugeren afviste fullscreen/monitor-dækning som gate (overlay kræver borderless) og afviste allowlist/blocklist.

**BEVIS (trace.log 17-10-2026):**
```
18:20:23  Foreground candidate detected: PID=22084 Name=WindowsTerminal
18:20:23  Foreground game PID changed from null to 22084. Restarting native FPS agent.
          Target=WindowsTerminal.exe DXGI=2 D3D9=0 DXGKRNL=2 FPS=51
          Snapshot: FPS=51 Source=NativeFpsAgent Vram=43MB Peak=43MB
18:45-18:50  samme med PID=7308, DXGI=2, FPS=156/90/66, holdt i minutter
```
ROOT CAUSE:
- `Native/FpsAgent/src/main.cpp:47` `kRollingFpsWindowSeconds = 0.050` => "DXGI>=2" betyder 2 presents i 50 ms (enhver UI-redraw).
- `main.cpp:1276-1282` saetter `g_lastPrimaryEvidenceTicksUtc` paa `dxgiCount>=2`; `WriteSharedFpsState` (351-393) publicerer `targetPid`/`gameConfirmed` i 5 s.
- `GameVramEvidence.Evaluate` (79-85) returnerer Game paa app-presents FOER VRAM maales; (98-102) VRAM-peak >= 500 MB er ogsaa Game alene.
- `SystemMonitor.FpsTimer_Tick` (779-784) => IsInGame => `MainViewModel` GameLaunched => overlay auto-show.
- Historisk samme fejlklasse: se del 8 ovenfor (`pythonw` laast som target i 5 timer).

**BESLUTTET MODEL (ikke implementeret endnu):** ingen lister, ingen fullscreen-gate. Klassifikation = adfaerd:
3D-engine vs Compute-engine (spil bruger 3D; CUDA/DirectML bruger Compute), sustained present-kadence (>= 20 fps i >= 2-3 s, ikke 2 events/50 ms), input-uafhaengighed (GetLastInputInfo), VRAM kun som tier/korroborering. `Unknown` => ingen auto-show. Hysterese paa acquire/hold/release.

**FASE 0 IMPLEMENTERET (kun additiv telemetri — klassifikationen er UENDRET):**
- NY `Helpers/Hardware/GpuEngineActivity.cs` — per-PID 3D/Compute/Copy/VideoDecode/VideoEncode/Other via PDH `GPU Engine`, med cache + retry.
- NY `Helpers/Hardware/SystemInputIdle.cs` — sekunder siden sidste input (wrap-sikker 32-bit beregning).
- NY `Helpers/Hardware/GameEvidenceProbe.cs` — én trace-linje pr. kandidat: vram/peak/p95, engine-fordeling + shares, presentFps/dxgi/d3d9/dxgkrnl (kun for target), idle, stoerste vindue + monitor-daekning. Plus throttlet top-VRAM-scan hvert 10 s.
- `GpuProcessMemory.cs` — ny `GetTopDedicatedUsage(limit)` + pid-parsing af instansnavne.
- `GameVramEvidence.cs` — p95 over seneste 20 VRAM-samples (`GetRecentPercentileBytes`) + `RecordSample` til telemetri. Peak-hold (15 min) uaendret.
- `HardwareMonitorAgent.cs` — hooks: `GameEvidenceProbe[foreground]`, `[visible]` (Game-verdict eller peak >= 250 MB), `[topvram]` hvert 10 s, samt `3D/Compute/Idle/VramP95` paa `Snapshot:`-linjen.

**BUILD + DEPLOY:** 0 fejl. `C:\icongrid\IconGrid.dll` = 1.241.600 bytes, 17-10-2026 19:20:17 (matcher build).

**FOERSTE KALIBRERINGSFUND (19:20-19:21, 30 s koersel):**
```
GameEvidenceProbe[visible] pid=1796 name=dwm vram=2,58GB peak=2,58GB p95=2,58GB 3D=0,0% Compute=0,0% engines=27 presentFps=-- idle=1,0s win=--
GameEvidenceProbe[visible] pid=25192 name=Discord vram=1,16GB peak=1,16GB p95=1,16GB 3D=9,1% VideoEnc=37,8% engines=14 presentFps=-- idle=1,1s win=--
```
=> VRAM-only-reglen klassificerer baade `dwm` (2,58 GB) og `Discord` (1,16 GB) som spil. Det bekreafter at VRAM skal vaere tier/korroborering, ikke gate. Ingen target blev laast og intet overlay blev vist under testen.

**NAESTE (bruger-test):** koer COD, POE, LM Studio, Grounded-SAM og en browser; sammenlign `GameEvidenceProbe`-linjerne (3D vs Compute, kadence, idle, VRAM) og fastsaet taerskler. Derefter fase 1: sustained kadence + compute-veto i `GameVramEvidence`/`SystemMonitor` + release af sticky target i `PollLockedTarget`.

**BEMAERK:** en elevated `IconGrid.exe` (PID 14548) og `IconGridFpsAgent.exe` (PID 17008) koerte videre efter testen og kunne ikke stoppes fra ikke-elevated shell ("Adgang naegtet") — luk via Task Manager hvis noedvendigt. Ikke committet/pushet (kraever separat godkendelse).


## Session findings (2026-10-17, del 2) — launcher forblev i hide mode efter POE-lukning (FIXET)

**Bruger-rapport:** efter POE 1-lukning kom main-launcheren 2 gange ikke ud af hide mode; efter COD + POE igen virkede det.

**BEVIS (trace.log 19:24-19:27):**
```
19:24:17  Sticky-target: no game foreground detected. Holding current game PID 9364
19:24:19  (POE starter) [SlideAnim] from=0,0 to=-204,8   <- launcher hides
19:24:23  [NativeFpsAgent] TargetPid=9364 Target=explorer.exe DXGI=0 DXGKRNL=1
19:24:28  Configured FPS target changed ... RootPid=21564 CurrentForegroundPid=9364 NativeTargetPid=9364
19:24:28  NativeFPSAgentRunner ... ForegroundGamePid=9364 ... RootPid=21564
19:25:21  [DisplayResolutionService] Game session confirmed running and exited; restoring resolution.
19:25:25..19:25:43  Sticky-target: ... Holding current game PID 9364 (explorer)  <- INGEN GameExited
```

**ROOT CAUSE (to fejl der forstærker hinanden):**
1. `NativeFpsAgentRunner.Start` giver `--root-pid` prioritet og springer derefter config-target + `--trusted-launch` over (linje 83-99). Når POE blev startet fra IconGrid, sendte `TryUpdateForegroundGameTarget` det STALE native target (explorer.exe, PID 9364) som `foregroundGamePid` -> agenten fulgte skrivebordet i stedet for POE. Derfor `Target=explorer.exe` mens POE kørte.
2. Explorer udsender lejlighedsvise DXGI/DxgKrnl presents -> `gameConfirmed` blev (gen)bekræftet -> `IsInGame` forblev true -> `GameExited` fyrede aldrig -> launcher blev i hide mode. Samtidig genstartede agenten hvert ~10 s fordi den aldrig "ejede" config-targetet.

**FIX (målrettet, lille):**
- `HardwareMonitorAgent.TryUpdateForegroundGameTarget`: i config-first-grenen sendes et eksternt foreground-PID nu KUN videre når config-targetet ikke længere er i live (`IsConfiguredTargetAlive`). Er spillet IconGrid startede stadig i live, genstartes agenten med config-target + `--trusted-launch` (og `currentForegroundGamePid` ryddes), så spilllet følges korrekt og eksisterende mål frigives ved spil-luk.
- Diagnostik til næste test: `MainWindow.OnGameLaunched/OnGameExited` logger nu `[GameFlow] GameLaunched: ...` / `[GameFlow] GameExited: autoCloseOverlay=.. gameHideActive=.. hidden=..`, og `LauncherWindowModeController.IsGameHideActive` er eksponeret.

**BUILD + DEPLOY:** 0 fejl. `C:\icongrid\IconGrid.dll` = 1.242.112 bytes, 17-10-2026 19:33:24 (matcher build; første deploy-forsøg kopierede ikke pga. låst DLL — andet forsøg OK).

**NÆSTE TEST:** start IconGrid -> start POE fra IconGrid -> luk POE. Forvent i trace:
```
Target=PathOfExile.exe (IKKE explorer.exe)
[GameFlow] GameExited: autoCloseOverlay=.. gameHideActive=True hidden=True
[SlideVisible] restoreTopDip=...
```
Hvis `[GameFlow] GameExited` udebliver, er det fase-1-problemet (explorer/DXGI-klassifikation) og ikke denne sti.

**BEMÆRK:** Den dybere årsag — at DXGI-present alene klassificerer explorer/dwm/Discord som spil — er stadig aaben og adresseres i fase 1 (3D vs Compute + sustained kadence + input-uafhængighed). Fase 0-telemetrien kører uændret.

**NY BUILD + DEPLOY (2026-10-17 19:37:31):** `dotnet build -t:Rebuild` (0 fejl) → `C:\icongrid\IconGrid.dll` = 1.242.112 bytes, 19:37:31 (matcher build). De første deploy-forsøg kopierede ikke (xcopy fejlede tavst mens DLL'en var kortvarigt låst), så timestamp blev verificeret og bragt på plads med `Copy-Item -Force` + endnu en `deploy-test.cmd`. Dokumenteret i `.local-state/regex-commands-cheatsheet.md` afsnit 16.


## Session findings (2026-10-18) — commit/push af fase 0 + game-exit-fix

- Arbejdstræet blev bygget rent: `dotnet build IconGrid.csproj --nologo -v m` → **Build succeeded** (0 fejl).
- Rebase oven på `origin/main` (4 nye remote-commits: MIT/PolyForm-licens + DPI-bevisst work-area-clamping) gik igennem **uden konflikter**; build + deploy blev kørt igen efter rebase.
- Deploy: `C:\icongrid\IconGrid.dll` = **18-10-2026 17:29:30**, matcher `bin\Debug\...\IconGrid.dll` (verificeret).
- Push: **`189b361`** på `origin/main` (`6e7432c..189b361`) — "feat: phase-0 game evidence telemetry and stable game-exit path" (9 filer, 1027 insertions): `GameEvidenceProbe`, `GpuEngineActivity`, `SystemInputIdle`, `GpuProcessMemory.GetTopDedicatedUsage`, `GameVramEvidence` p95, monitor-hooks, config-target game-exit-fix, GameFlow-diagnostik.
- **Ikke committet:** denne note (kræver separat godkendelse).
- **Næste skridt:** bruger-test af POE/COD-stien (overlay kun ved spil, launcher ud af hide mode ved spil-luk), derefter fase 1 (sustained present-kadence + compute-veto + release af stickytarget).


## Session findings (2026-10-18, del 2) — Fast Copy-siden fjernet helt

**Bruger-ønske:** siden `Views/Settings/Pages/UsbCopyPage.xaml` og AL tilhørende kode skal fjernes.

**Slettet (git rm):**
- `Views/Settings/Pages/UsbCopyPage.xaml` + `.xaml.cs`
- `ViewModels/Settings/UsbCopyViewModel.cs`
- `Helpers/UsbCopy/` (10 filer: FileBrowserEntry, FileBrowserPane, MultiWorkerCopyService, OverwritePolicy, UsbBenchmarkRunner, UsbCopyEngine, UsbCopyLogger, UsbCopyState, UsbDeviceDetector, UsbDeviceInfo)
- `Views/FileCopyProgressWindow.xaml` + `.xaml.cs`
- `Views/OverwritePromptWindow.xaml` + `.xaml.cs`, `Views/OverwritePromptResolver.cs`
- `Views/BenchmarkRunWindow.xaml` + `.xaml.cs`
- `Views/DeleteConfirmWindow.xaml` + `.xaml.cs`, `Views/NewFolderDialogWindow.xaml` + `.xaml.cs`
- `tools/benchmark-runner/` (BenchmarkRunner.csproj + Program.cs; bin/obj ryddet bagefter)

**Referencer fjernet:**
- `SettingsWindow.xaml`: `UsbCopyNavButton` (FC-knap) inkl. tooltip/bindings.
- `SettingsWindow.xaml.cs`: `UsbCopyNavButton_Click` + ubrugt `using IconGrid.ViewModels.Settings;`.
- `LocalizationHelper.cs`: 77 `FastCopy*`-nøgler i både `en` og `da` (154 linjer; 569→415).
- `MainViewModel.Localization.cs`: 77 properties + 77 `OnPropertyChanged(nameof(FastCopy...))` (154 linjer; 528→374).
- `README.md`: Fast USB Copy-rækken i settings-tabellen + hele "Fast USB Copy"-afsnittet.
- `PROJECT_STRUCTURE.md`: Helpers\UsbCopy, UsbCopyViewModel, de 6 hjælpevinduer/handlers, UsbCopyPage, tools\benchmark-runner, runtime-log-sti.

**Verifikation:** ingen resterende `FastCopy|UsbCopy|FileBrowser|MultiWorkerCopy|FileCopyProgress|BenchmarkRun|DeleteConfirmWindow|NewFolderDialogWindow` i cs/xaml/csproj/sln. Build: **0 fejl**.

**Fuld rebuild + deploy (2026-10-18 17:38:18):** `dotnet build IconGrid.csproj -t:Rebuild` → **Build succeeded** (0 fejl). `cmd /c E:\IconGrid-GitHub\deploy-test.cmd` → "Done". Verificeret: `bin\Debug\...\IconGrid.dll` og `C:\icongrid\IconGrid.dll` har samme LastWriteTime **18-10-2026 17:38:18** OG samme SHA256 `98BC6FE3E470E93D97AC71BEFE9212F52B76D77B7F0B006DCE53131D7B6B9098`.

**Ikke committet/pushet** (kræver separat godkendelse).


## Session findings (2026-10-19) — gaming-overlay-agenter kun ved aktivt spil

**Problem:** Idle IconGrid kørte native FPS-agent og skrev `fps-state.json` + `native-fps-state.json` ~60 gange/s. Målt ~60 % af én kerne i idle.

**Ændringer (build 0 fejl, deployet 2026-10-19 ~18:09-18:10):**
- `HardwareMonitorAgent`: lazy start/stop af `NativeFpsAgentRunner` via `ShouldStartNativeFpsAgent` (VRAM-evidens; `Unknown` => start, `NotGame` => afvis). Stop ved spil-exit, afvisning, retarget og død config-target.
- `FpsStateIntervalMs` 2 → 100; `fps-state.json` skrives kun mens `nativeFpsStarted` (10 Hz) + én `--`-state ved sessionsslut.
- `SnapshotIntervalMs` 500 → 1000; eksplicit ydre loop-pacing (FPS-loopets sleep var tidligere det eneste sleep → busy loop i idle).
- `HardwareSnapshotCollector`: `Accept()` 6× → 1× pr. capture (capture 520 ms → ~80-110 ms).
- `PawnIoHelper`: 30 s cache + `InvalidateCache()` (PawnIO-warning rydder den ved retry).
- Visible-window fallback max hvert 2. s; `Visible window candidate`/start-gate-logs throttles 30 s pr. PID.
- `SystemMonitor`: idle poll 1000 ms, 50 ms når `IsInGame`; `fps-state.json` caches indtil `LastWriteTime` ændres; VRAM-klassificering max hvert 2. s.
- Native `main.cpp`: `kStateWriteIntervalMs` 2 → 100; `UpdateTargetState(0)` ved exit så overlayet falder straks.

**Måling (idle, intet spil):** UI 0,78 %, monitor-agent 3,91 % af én kerne (`Get-Process` delta/10 s) = ~4,7 % samlet; 0 skrivninger til begge state-filer; ingen `IconGridFpsAgent`; WS ~321 MB; trace ~0,3 KB/s (1 MB/time). FØR: ~60 % + ~120 skrivninger/s.

**Ikke testet:** in-game stien (kræver et rigtigt spil): FPS-agenten skal starte, overlay inden 1-2 s, `fps-state` ~10 Hz, og agenten skal forsvinde ved spil-exit. Test også et spil startet fra IconGrid (config target).

**Filstørrelse:** `HardwareMonitorAgent.cs` ~2.350 linjer (fortsat over 1500-grænsen; udtræk af evidens-/vindues-helpers er stadig backlog jf. ARCHITECTURE_RULES).

**Commits + push (2026-10-19):** `85f5b2d` (Fast Copy-fjernelse), `b404caa` (lazy overlay-agenter), `3b3e369` (denne note) er pushet til `origin/main`. Arbejdstræet er rent.


## Session findings (2026-10-22) — "Reset FPS-agent" gjort funktionel (event-baseret reset)

**Bruger-ønske:** gennemgå menupunktet "Nulstil FPS-agent" på IconGrid-logoets højreklik-menu.

**Gennemgang (før fix) — 3 strukturelle fejl:**
1. Launcheren er non-elevated (`app.manifest` = `asInvoker`, startup-task `IconGrid` = `Limited`), mens FPS-agenten startes af `IconGrid Monitor`-tasken (`Highest`). `Process.Kill()` gav derfor `Access denied`, som blev slugt i en tom `catch` → falsk besked "FPS-agent kørte ikke".
2. Der blev slettet `fps-state.json` (monitor-agentens publicerede snapshot, genskabes på næste tick). Native agentens egen state er `native-fps-state.json` — og agenten læser den aldrig ved opstart, så sletningen havde ingen effekt.
3. Monitor-agentens in-memory target (`currentForegroundGamePid`, `confirmedForegroundGamePid`, sticky/rejected/override, watchdog) blev ikke ryddet. Watchdoggen genstartede endda agenten med `LastForegroundGamePid` → samme forældede PID.

**Fix (event-baseret, følger RequestStop-mønstret der allerede virker fra non-elevated):**
- `Helpers/Hardware/MonitorAgentLifecycle.cs`: nye events `Local\IconGrid.HardwareMonitorAgent.ResetFps` (ManualReset request) + `...ResetFpsCompleted` (AutoReset kvittering) samt `TrySignalResetFps()` / `WaitForResetFpsCompleted()`.
- `HardwareMonitorAgent`: åbner begge events efter mutex-overtagelse. Ved reset: graceful `Stop()` af native agent (ETW cleanup), slet `native-fps-state.json`, nulstil ALLE sticky/confirmed/override/watchdog-felter, tving foreground-poll i samme iteration, kvittér via completed-event.
- `FpsAgentWatchdog.Reset()`: glemmer tidligere restart-interval/stale-timer.
- `MainViewModel.ResetFpsAgent()`: signalerer monitor-agenten og venter op til 3 s på kvittering; kun hvis ingen monitor-agent lytter, køres direkte fallback (kill + slet BEGGE state-filer + ryd `FpsTarget`). Kill-fejl kastes nu videre i stedet for at blive skjult.
- `Views/Launcher/MainWindow.xaml.cs`: handleren er nu `async` og kører reset via `Task.Run` (ingen UI-frys); fejlbeskeden er lokaliseret.
- Lokalisering: ny nøgle `ResetFpsAgentFailedMessage` (en+da); success-teksten opdateret (scanner straks, ikke "næste tick").

**Build/deploy:** `dotnet build IconGrid.csproj` → **Build succeeded, 0 fejl**. `deploy-test.cmd` → Done. Verificeret: `bin\Debug\...\IconGrid.dll` og `C:\icongrid\IconGrid.dll` = **22-10-2026 22:58:06**.

**Bruger-test:** start IconGrid fra `C:\IconGrid` → højreklik logo → "Nulstil FPS-agent" → forvent i `%APPDATA%\IconGrid\trace.log`: `FPS agent reset requested by the launcher`, `FPS agent reset completed; rescanning for a running game`, `FPS agent reset requested by the launcher. MonitorConfirmed=True`. Med et kørende spil skal agenten starte igen inden for ~1-2 s uden at IconGrid genstartes.

**Ikke committet/pushet** (kræver separat godkendelse).


## Session findings (2026-10-23, aften) — gaming overlay detekterede ikke COD: monitor-agenten var død + shell-target holdt IsInGame

**Bruger-rapport:** gaming overlay starter ikke automatisk, hvis IconGrid ikke kørte før COD blev startet; lige nu kører COD, men overlayet detekterer ikke spillet.

**BEVIS 1 — monitor-agenten var væk (hovedårsag).**
- `tasklist` ved sessionens start: kun `IconGrid.exe` (launcher) kørte; INGEN `IconGrid.exe --monitor-agent` og ingen `IconGridFpsAgent.exe`.
- `trace.log` sidste skrivning fra agenten: **23-10 19:27:58**; COD (`cod22-cod.exe`, PID 6088) blev startet **19:37:26** — dvs. agenten var allerede død før spillet startede.
- `trace.log` 23:02:25: `No monitor agent was listening for an FPS reset request.` (bekræfter at agenten ikke var der, selv om launcheren kørte).
- Windows Application-log **23-10 19:28:00**: `IconGrid.exe` crashede med **0xc0000374 (STATUS_HEAP_CORRUPTION)** i `ntdll.dll`, rapport `AppCrash_IconGrid.exe_627ca38...`. Samme signatur gentages **22-10 17:31, 21-10 01:04, 19-10 23:59** — en reel, tilbagevendende heap-korruption i monitor-agent-processen. WER-rapporten viser at `LibreHardwareMonitorLib.dll`, `System.Management.dll` og `System.Diagnostics.PerformanceCounter.dll` er loadet (HardwareSnapshotCollector/GpuProcessMemory/GpuEngineActivity). Den præcise korrumperende native komponent er ikke isoleret endnu; ingen `error.log`-post fordi processen dør før .NET kan logge.

**BEVIS 2 — forkert "sticky target" holdt IsInGame = true (strukturel årsag til "starter ikke hvis IconGrid kom bagefter").**
- `trace.log` 19:26-19:27 mens COD kørte: `Sticky-target: no game foreground detected. Holding current game PID 11512` + `[NativeFpsAgent] Target=explorer.exe TargetPid=11512 DXGI=0 D3D9=0 DXGKRNL=1/2`.
- explorer.exe blev altså låst som "spil" (native agentens `--root-pid` + DxgKrnl-kernel-presents gav `gameConfirmed=true`), selv om den kun holdt 260 MB VRAM og ingen DXGI/D3D9-presents havde.
- `SystemMonitor` brugte `nativeFpsState.GameConfirmed == true` direkte → `IsInGame=true` allerede før COD blev vist. Derfor skete der ingen false→true-overgang, `GameLaunched` fyrede ikke, og overlayet blev aldrig auto-vist for det rigtige spil.

**FIX 1 — `Helpers/Hardware/MonitorAgentWatchdog.cs` (NY).**
- Launcheren superviserer monitor-agentens heartbeat (`monitor-state.json`, skrives ~1×/s). Mangler/stale >30 s → agenten genstartes via den eksisterende scheduled task `IconGrid Monitor` (`HardwareMonitorTaskManager.TryRunScheduledMonitorTask`, ny) så der IKKE kommer UAC-prompt. Fallback: `StartAgent`.
- Cooldown 60 s mellem restart-forsøg; 15 s check-interval; heartbeat-baseret (ikke mutex) fordi en elevated agents mutex kan give adgangsfejl for den ikke-elevated launcher.
- Startes i `App.OnStartup` efter `MainWindow shown`, disposer i ny `App.OnExit`.
- Beskyttelse mod race: `Interlocked` så to checks ikke genstarter samtidigt.

**FIX 2 — `Helpers/Launcher/SystemMonitor.cs` (evidens-gate).**
- Læser nu native agentens fulde state (`native-fps-state.json`, cachet pr. LastWriteTime, 10 s freshness) via `ReadNativeAgentState()`.
- `IsInGame` = `GameEvidenceVerdict.Game` (VRAM ≥500 MB peak eller app-level presents) ELLER `HasPrimaryGraphicsEvidence(state)` (DXGI/D3D9 ≥2) ELLER trusted launch (spil startet via IconGrid). Den rå `GameConfirmed`-flag alene er ikke længere nok.
- Effekt: explorer/Task Manager/CmdPal kan ikke længere holde `IsInGame=true`; når COD bliver target sker der en ægte false→true-overgang → `GameLaunched` → overlay auto-vises. Samme klassifikationsregel som monitor-agenten (ingen blocklist).

**BUILD + DEPLOY:** `dotnet build IconGrid.csproj` → **Build succeeded, 0 fejl/0 advarsler**. `deploy-test.cmd` → Done. Verificeret: `bin\Debug\net10.0-windows10.0.22621.0\IconGrid.dll` og `C:\IconGrid\IconGrid.dll` = **23-10-2026 23:22**, 1.065.984 bytes.

**LIVE-VERIFIKATION (23:23):** scheduled task `IconGrid Monitor` startet manuelt (samme vej som watchdoggen bruger) → agenten fandt straks COD: `Visible game fallback candidate detected behind IconGrid foreground: PID=6088 Name=cod22-cod`, `Initial foreground game PID detected: 6088`, `Native FPS agent start allowed for PID 6088: verdict=Game`, `[NativeFpsAgent] FPS=159 ... Target=cod22-cod.exe DXGI=8 ... Source=PrimaryApi`, `Snapshot: FPS=159 ... Vram=5,89GB`. Dvs. detektion virker, når agenten kører.

**Bemærkning:** agenten blev startet uden launcher-parent (`ParentPid=0`) og lukker sig selv efter 60 s uden launcher — brugeren skal starte `C:\IconGrid\IconGrid.exe`, så tager launcheren over og watchdoggen holder agenten i live.

**Næste skridt / backlog:**
- Find den præcise heap-korruption (0xc0000374). Kandidater: `LibreHardwareMonitorLib` (PawnIO/WinRing0-sensorer), PDH via `System.Diagnostics.PerformanceCounter` (`GpuProcessMemory`/`GpuEngineActivity`), `SmBiosMemoryParser`. Anbefaling: aktivér WER LocalDumps for `IconGrid.exe` og analysér dump, eller A/B-test med `IsMotherboardEnabled=false` / counter-læsning midlertidigt slået fra.
- `visible fallback` logger stadig `cod22-cod ... window 0x0 is too small` for et hjælpe-vindue, mens et andet vindue (2560x1440) accepteres. Kosmetisk støj, ikke en fejl.
- `external-games.json` indeholder en forkert sti `C:\IconGrid\cod22-cod.exe` (spillet ligger i `E:\Spil\...`) — bør ryddes, så resolutions-lock ikke peger på den forkerte exe.

**Ikke committet/pushet** (kræver separat godkendelse).


## Session findings (2026-10-25) — SVG-sidebar-ikoner forsøgt, derefter droppet (PNG-genoptagelse)

**Bruger-ønske (start):** sidebar-ikonerne skulle følge Windows 11 Lys/Mørk + accentfarve, ligesom resten af launcheren. Nye grå SVG'er ligger i `figma-icons/svg/` (Figma-lag-ID'er: `theme-accent-*`, `fixed-light-*`, `fixed-dark-*`).

**Forsøg (nu rullet tilbage):** Home blev konverteret til et tema-styret vektor-ikon via en ny `Controls/Common/SidebarIcon.xaml(.cs)` (`Kind`/`AccentBrush`/`InkBrush`), plus `IconInkBrush` i `LauncherThemeState` + `MainViewModel`. Rettelse undervejs: en `DataTrigger` på `Button.Tag="Selected"` gjorde hele ikonet hvidt — accent-triggeren i `SettingsWindow.xaml` linje ~168 tilhører `BackButtonStyle` (luk-knappen), ikke `NavButtonStyle`; triggeren blev fjernet.

**Bruger-beslutning (2026-10-25):** SVG/vektor-sporet bruges **ikke** alligevel. Det originale PNG-logo (`figma-icons/figma-logo-x4.png`) skal tilbage på Home-ikonet.

**Rullet tilbage (build 0 fejl, deployet — `IconGrid.dll` 25-10-2026 21:58:25 i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid`; `C:\icongrid\figma-icons\figma-logo-x4.png` findes):**
- `Views/Settings/SettingsWindow.xaml`: Home-knappen bruger igen `<Image Source="pack://siteoforigin:,,,/figma-icons/figma-logo-x4.png" .../>`; `xmlns:controls` fjernet.
- Slettet: `Controls/Common/SidebarIcon.xaml` + `.xaml.cs`.
- `ViewModels/Launcher/LauncherThemeState.cs` + `ViewModels/MainViewModel.cs`: `IconInkBrush` (property, assignment, OnPropertyChanged) fjernet igen.
- `PROJECT_STRUCTURE.md`: `SidebarIcon`-linjen fjernet.
- `.local-state/icon-smoke/` (midlertidigt verifikations-harness) slettet; cheatsheet-afsnit 22 fjernet.

**Status:** Ingen SVG-relateret kode tilbage i repoet. `Views/Settings/SettingsWindow.xaml` er identisk med HEAD (whitespace gendannet via `git checkout`), og Home bruger igen PNG-logoet. `figma-icons/svg/` er fjernet fra disken.

**Ikke committet/pushet** (kræver separat godkendelse).

## Session findings (2026-10-25) — Disks-sektion på Hardware-siden (SMART-helbred + SATA-spindeltilstand)

**Bruger-ønske:** en sektion på `Views/Settings/Pages/HardwarePage.xaml` der viser tilsluttede diske med SMART/helbred, og for SATA-harddiske viser om disken laver I/O eller står i sleep/standby (og dermed om pladerne roterer).

**Arkitektur (alt i den elevated monitor-agent, fordi SMART + ATA pass-through kræver admin):**
- NY `Helpers/Hardware/Storage/`:
  - `PhysicalDiskStatus.cs` + `SmartAttributeStatus.cs` — data-model (INotifyPropertyChanged; `[JsonIgnore]` på udregnede visningstekster). Koder er sprogneutrale ("Healthy"/"Standby"/"SSD") og lokaliseres i UI.
  - `DiskInventoryReader.cs` — WMI `Win32_DiskDrive` + `root\Microsoft\Windows\Storage:MSFT_PhysicalDisk` (identitet, MediaType, BusType, HealthStatus, SpindleSpeed). Waker ikke disken.
  - `StorageSmartReader.cs` — dedikeret `Computer { IsStorageEnabled = true }` (LHM 0.9.6). **Må ikke ind i den 1 Hz `HardwareSnapshotCollector`** — SMART-læsning kan vække en sovende disk. Status = Failing ved value<=threshold, Warning ved normalized<100 på attributter med threshold, NVMe "Available Spare" under threshold, "Percentage Used" >= 90.
  - `AtaDriveReader.cs` — `IOCTL_ATA_PASS_THROUGH_DIRECT`: CHECK POWER MODE (0xE5, vækker ikke) + IDENTIFY DEVICE (0xEC, word 217 rotation rate; vækker, derfor cachet).
  - `DiskActivityReader.cs` — live I/O via WMI `Win32_PerfFormattedData_PerfDisk_PhysicalDisk`.
  - `DiskStatusCollector.cs` — kadence: inventory 30 s, aktivitet 2 s, power-state 3 s, SMART 10 min **og kun for diske der ikke står i Standby** (første læsning af en aldrig-læst disk undtaget, så helbred kendes).
- `HardwareMonitorSnapshot` + `Disks` (List<PhysicalDiskStatus>); agenten sætter `snapshot.Disks` hvert tick og disposer collectoren.
- `SystemMonitor`: `ObservableCollection<PhysicalDiskStatus> Disks` + `ApplyDisks()` der merger pr. index (bevarer item-identitet, så åbne SMART-expandere ikke klapper sammen).
- `HardwarePage.xaml`: nyt "Disks"-kort efter Memory-kortet; pr. disk model, health-chip (prik+tekst), type/bus/størrelse/temp/SMART-summary, spindeltilstand + aktivitet + læse/skrive-rate (kun HDD+SATA), forklarende hint-tekst, og en SMART-detalje-expander (ID/attribut/værdi/grænse/status).
- NYE converters: `DiskStateLocalizationConverter` (kode+language → lokaliseret tekst) og `DiskHealthToBrushConverter` (grøn/gul/rød/grå prik).
- Lokalisering: 35 nye `HardwareDisk*`-nøgler i både en og da + properties/OnPropertyChanged i `MainViewModel.Localization.cs`.

**Rettelse (25-10 22:48) — NVMe-temperatur viste 84 °C:** `StorageSmartReader` tog `Max()` over alle `SensorType.Temperature`-sensorer. LHM rapporterer for Kingston SNV2S1000G: `Composite Temperature=34, Temperature #1=59, #2=71, #3=34, Warning Temperature=74, Critical Temperature=84` — altså var 84 °C **Critical-threshold**, ikke en måling. Retter: vælg `Composite`-sensoren (fallback: første sensor der ikke er Warning/Critical/Threshold). Verificeret i `monitor-state.json`: NVMe = **34 °C**. Sensornavnene logges én gang pr. drev til trace.log (`[StorageSmartReader] ... temperature sensors: ...`) til fremtidig diagnose.

**Rettelse (25-10 22:43) — tekst var sort i mørk tema:** `TemplateSmallTextStyle`/`TemplateCardTitleStyle` sætter `Foreground="{Binding SettingsSubtextForeground/TopBarForeground}"`, men inde i `ItemsControl.ItemTemplate` er DataContext `PhysicalDiskStatus` (ingen tema-properties) → bindingen fejler og teksten blev sort. Nye lokale stile i `HardwarePage.xaml`: `HardwareDiskTitleStyle` og `HardwareDiskSmallTextStyle` (binder til `Window.DataContext.TopBarForeground`/`SettingsSubtextForeground` via `RelativeSource`) samt `HardwareDiskChipStyle` (chip-baggrund `#26FFFFFF` i mørk, `#14000000` i lys via `IsLightTheme`-trigger). 28 small-text-, 2 titel- og 2 chip-referencer i disk-kortet udskiftet.

**Fund undervejs:**
- `PerformanceCounter`-kategorier/navne er **lokaliseret** på dansk Windows ("PhysicalDisk"/"% Idle Time" findes ikke) → skiftet til WMI `Win32_PerfFormattedData_PerfDisk_PhysicalDisk`, som er sproguafhængig.
- Ældre SATA-diske (fx WD5000AAKX) rapporterer `MediaType=0` (unspecified) og `SpindleSpeed=0` i WMI → ATA IDENTIFY word 217 bruges som fallback (cachet pr. disk).

**Rettelse (25-10 22:58) — helbreds-%, temperaturfarver og drevbogstaver (HDS-paritet):**
- **Helbred %** (`HealthPercent`): NVMe → `100 - Percentage Used`; SSD → `SSD Life Left`/`Remaining Life` → `Wear Indicator`/`Media Wearout` → `Wear Leveling` (kun 1-100); HDD → `100 - pending*0.6 - reallocated*1 - uncorrectable*2 - reportedUncorrectable*1 - spinRetry*2` (UDMA CRC ignoreres, det er kabel/signal). Resultat på denne maskine: SUV400 **58 %**, ST1000 **83 %**, SA400 **90 %**, WD **100 %**, NV2 **87 %** (HDS viser 42/83/90/100/87 — SUV400 afviger fordi Kingstons "SSD Life Left"=58 rapporteres mens HDS bruger sin egen levetidsmodel).
- **Temperaturfarver**: `IsTemperatureWarm` (≥40 °C) → gul `#F59E0B`, `IsTemperatureHot` (≥50 °C) → rød `#EF4444`, ellers temaets subtext-farve. Verificeret med harness: 39→neutral, 40→gul, 49→gul, 50→rød.
- **Drevbogstaver**: nyt `DriveLetters` fra `MSFT_Partition` (vist som chip ved disknavnet). **Fælde:** WMI returnerer `DriveLetter` som **ASCII-kode** (68='D'), ikke 0-baseret — 0 betyder "intet bogstav". Verificeret: C:, D:, E:, F:, G:.
- **Fælde 2:** `ComputeHealthPercent` valgte først "Wear Leveling Count" (raw 18667) fordi SMART-listen er sorteret efter id, og "SSD Life Left" (id 231) kommer senere → health blev 100 %. Nu tjekkes hver indikator i prioriteret rækkefølge, og Wear Leveling bruges kun hvis værdien er 1-100.
- UI: health-chip viser nu `● Sund 83 %` (procenten skjules hvis ukendt), drevbogstav-chip ved navnet, temperatur farves efter tærskler.

**Rettelse (25-10 23:12) — model-baseret SSD/HDD-fallback + drevbogstav ved navnet:**
- NY `Helpers/Hardware/Storage/DiskModelClassifier.cs`: regex-baseret fallback der kun bruges når WMI's `MediaType` er "unspecified" og `SpindleSpeed` mangler (ATA IDENTIFY blokeret). SSD-mønstre før HDD-mønstre (fx `SSD`, `MZ-`, `WDS\d`, `SA\d{3}`, Kingston-serier, `ZA\d`, SanDisk/Crucial/Intel), HDD-mønstre (`ST\d{3,}`, `WD\d{2,}`, `WDC WD…`, Samsung/Hitachi `HD|HM|SP`, Toshiba `DT0|MG0|…`, HGST `HDS|HUA|…`). Ukendte modeller forbliver "Unknown" — ingen gæt.
- Verificeret mod 17 modelnavne: WD5000AAKX/WD10EZEX/WD20EARX/ST1000/ST4000/HD103SJ/DT01ACA100 → HDD; Samsung 860 EVO/MZ-/WDS/CT500MX/Kingston SA400/SUV400/SNV2S/SanDisk/ZA → SSD; "Generic ACME 12345" → Unknown.
- Agent-data efter fix: WDC WD5000AAKX = **HDD** (var Unknown).
- UI: drevbogstav-chip flyttet ind **ved siden af modelnavnet** (grid: model Auto | bogstav Auto | spacer * | health Auto), så den ikke længere står ude til højre.

**Rettelse (25-10 23:18) — skjul spindel-rækken når tilstanden er ukendt:** `ShowPowerState` kræver nu også at `PowerState != Unknown` (tidligere: blot HDD+SATA). På maskiner hvor storage-driveren blokerer ATA pass-through forsvinder "Unknown / 0 % / 0 B/s"-rækken og dens hint helt i stedet for at stå som støj. Rækken dukker automatisk op igen hvis CHECK POWER MODE begynder at svare (fx hvis SAT-fallback implementeres). Verificeret i harness: `unknown=False, standby=True, ssd=False`.

**Rettelse (25-10 23:22) — intro-tekst på to linjer:** `HardwareDisksIntro` (en+da) har nu et bevidst `\n` efter første sætning, så "Physical disks connected to this PC." står på linje 1 og agent-forklaringen på linje 2 (uafhængigt af vinduesbredde/sprog). `TextWrapping="Wrap"` er stadig sat, så begge linjer kan ombryde på smalle vinduer.

**Verificeret (build 0 fejl, deployet — `IconGrid.dll` 25-10-2026 23:22:20 i både bin og `C:\icongrid`):**
- Smoke-harness (`.local-state/disk-smoke`, gitignored, ikke-elevated) mod den byggede DLL: 5 diske fundet med korrekt SSD/HDD/NVMe/SATA-klassifikation, live aktivitet/læse-skrive-rate virker, alle lokaliseringskoder + farvekonverteringer verificeret. SMART og ATA power-state er "Unavailable/Unknown" i harnessen fordi den ikke er elevated — det er den forventede fallback.
- **Ikke verificeret live endnu:** SMART-attributter og spindeltilstand i den elevated agent (kræver brugerens test).

**ATA power-state — verificeret end-to-end, men blokeret af denne maskines driver:**
- Startede den elevated agent via scheduled task `IconGrid Monitor` og læste `%APPDATA%\IconGrid\monitor-state.json`: SMART virker (21-29 attributter pr. disk, temperatur 25-84 °C), aktivitet/læse-skrive virker, SSD/HDD/NVMe-klassifikation virker.
- **Power-state er `Unknown` på alle diske her.** `IOCTL_ATA_PASS_THROUGH_DIRECT` giver **win32=87 (ERROR_INVALID_PARAMETER)**, buffered variant giver **1306 (ERROR_NOT_ALL_ASSIGNED)**. Afprøvet: korrekt IOCTL-kode (`0x0004D030`, verificeret mod `ntddscsi.h`: `CTL_CODE(FILE_DEVICE_CONTROLLER, 0x040C, METHOD_BUFFERED, 3)`), struct-størrelse 40 (verificeret), `ATA_FLAGS_DRDY_REQUIRED`, `SeManageVolumePrivilege` aktiveret via `AdjustTokenPrivileges`, både DIRECT og BUFFERED med data-buffer i samme allokering. Controlleren er "Standard SATA AHCI-controller" (ikke RST), men systemet har også en Microsoft Storage Spaces-controller — en sandsynlig årsag er at storage-stakken ikke videresender ATA pass-through til disse fysiske diske.
- Konsekvens: spindeltilstand vises som "Ukendt" med forklarende hint i UI'et. SMART/helbred/aktivitet er upåvirket.
- **Næste forsøg (backlog):** SAT-laget via `IOCTL_SCSI_PASS_THROUGH_DIRECT` (0x0004D014) med ATA PASS-THROUGH(16) CDB (opcode 0x85, protocol 2 = non-data for CHECK POWER MODE). Det er den vej smartmontools bruger, når ATA pass-through afvises. Alternativt kan ATA IDENTIFY hentes via `IOCTL_STORAGE_QUERY_PROPERTY` + `StorageDeviceProtocolSpecificProperty` (ProtocolTypeAta) til at klassificere ældre diske som HDD (WD5000AAKX er stadig "Unknown").

**Test for brugeren:** start `C:\IconGrid\IconGrid.exe` (så kører monitor-agenten også) → Indstillinger → Hardware → fold "Diske" ud. ST1000LM024 (HDD) skal vise spindeltilstand; WD5000AAKX skal nu klassificeres som HDD via ATA IDENTIFY. SMART-tabellen kræver at agenten er elevated (den er). USB-drev kan vise "Ukendt" — forventet.

**Ikke committet/pushet** (kræver separat godkendelse).


## Session findings (2026-10-25) — Settings-vinduets størrelse huskes

**Ændring:** Settings-vinduet gemmer nu sin størrelse (og maksimerede tilstand) ved luk, og gendanner den ved næste åbning. Positionen blev allerede gemt; den gemmes fortsat kun i normal tilstand, så et maksimeret vindue ikke overskriver restore-positionen.

- `Models/ConfigModel.cs`: nye `SettingsWindowWidth`, `SettingsWindowHeight` (double?) og `SettingsWindowIsMaximized` (bool).
- `ViewModels/Settings/MainViewModelConfigState.cs` + `MainViewModelSettingsState.cs` + `MainViewModelSettingsPersistence.cs`: mapping til/fra config.
- `ViewModels/Launcher/WindowStateStore.cs`: `TryGetSavedSettingsWindowBounds(...)` (position/størrelse rapporteres som `double.NaN` når de ikke er gemt) + `SaveSettingsWindowBounds(...)`; `ApplyConfig` udvidet.
- `ViewModels/MainViewModel.cs`: `TryGetSavedSettingsWindowBounds` / `SaveSettingsWindowBounds` (én config-skrivning ved luk).
- `Views/Settings/SettingsWindow.xaml.cs`: `TryApplySavedPosition` → `TryApplySavedBounds` (position, størrelse og max-state; størrelsen kappes til virtual desktop), `SaveWindowBounds()` ved `Closed` (bruger `RestoreBounds` når maksimeret/minimeret), `LocationChanged` ignorerer ikke-normal tilstand.
- `Views/Settings/SettingsWindowCoordinator.cs` + `Views/Launcher/GamingOverlayWindow.xaml.cs`: opdateret til `TryApplySavedBounds()`.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 25-10-2026 23:34:25 i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid`.

**Test for brugeren:** åbn Indstillinger, gør vinduet større (eller maksimér), luk, åbn igen → samme størrelse. Genstart IconGrid for at bekræfte at størrelsen også overlever app-restart (config skrives ved luk).

**Ikke committet/pushet** (kræver separat godkendelse).


### Rettelse (25-10 23:44) — kort var usynlige i lyst tema

**Symptom (bruger):** På Hardware-siden (GPU/Memory/Disks og hero-kortet) "mangler" kortene i lyst tema — de smelter sammen med baggrunden.

**Rodårsag:** `StartsideSectionCardStyle` brugte `StartsideLightCardFill = #F3F4F7`, mens `SettingsWindow`'s baggrund er `TopBarBackground = #F3F3F3` i lyst tema. Forskellen er 1-4 pr. RGB-kanal → kortene er reelt usynlige. I mørkt tema er der kontrast (`#323232` mod `#202020`).

**Fix (design-token, gælder alle settings-sider):**
- `Views/StartsideStyles.xaml`: `StartsideLightCardFill` og `SurfaceFillLightBrush` → `#FFFFFF` (samme som `SettingsCardBackground`; matcher Windows 11's indstillinger: hvide kort på lys grå baggrund). Kort-border `#D1D5DB` er uændret.
- `Views/TemplateGuidelines.xaml`: palette-dokumentation opdateret til `#FFFFFF`.
- Sideeffekt (positiv): `LayoutDropdownComboBoxStyle` i lyst tema bliver også hvid i stedet for næsten-baggrundsfarve.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 25-10-2026 23:44:47 i både `bin` og `C:\icongrid`.

**Ikke committet/pushet** (kræver separat godkendelse).


### Ændring (26-10 03:05) — nyt sidebar-ikon-sæt til settings-vinduet

**Brugeren** havde lagt 7 nye 96×96 PNG-ikoner i `figma-icons/` og slettet de gamle ikoner + `.fig`-kilder.

**Integration:**
- `IconGrid.csproj`: de 6 gamle `Content`-entries erstattet af de 7 nye (`Home.png`, `Shortcut icons.png`, `Layout.png`, `Hardware.png`, `Help.png`, `Gaming overlay.png`, `About IconGrid.png`). `Resolution.png` var ikke refereret og er fjernet.
- `Views/Settings/SettingsWindow.xaml`:
  - Home/Shortcut icons/Layout/Help/About bruger de nye filnavne via `pack://siteoforigin` (mellemrum som `%20`).
  - Hardware-nav skifter fra "HW"-accent-badge til `Hardware.png`; Gaming overlay fra "GO"-badge til `Gaming overlay.png`. Monitor Row Layout ("ML") og Test ("DBG") beholder badges, da der ikke findes nye ikoner til dem.
  - Alle ikoner vises nu ens som 24×24 (gamle Help/About-opacity-tweaks fjernet).
- `deploy-test.cmd`: tilføjet asset-kopiering af `figma-icons\*` til `C:\icongrid` — scriptet kopierede før kun DLL/EXE, så nye assets nåede aldrig testmappen. Kommentaren understreger, at det er assets (ikke brugerdata/config).

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 26-10-2026 03:05:24 i både `bin` og `C:\icongrid`; `C:\icongrid\figma-icons` indeholder de 7 nye PNG'er.

**Test for brugeren:** åbn Indstillinger og se sidebar-navigationen i både lyst og mørkt tema.

**Ikke committet/pushet endnu** (commit/push køres efter denne note).


### Ændring (26-10 03:19) — Monitor Row flyttet ind på Home + launcher-opdeling

**Brugerønske:** "Monitor Row Layout" skal ikke være en selvstændig side i sidebaren, men ligge nederst på Home. Home skal samtidig forklare, at Home/Shortcut icons/Layout (og monitorrækken) hører til **main launcher**, mens **gaming overlay** er en separat launcher, der kun vises under spil.

**Implementeret:**
- `Views/Settings/Pages/MonitorRowLayoutPage.xaml(.cs)` → omdøbt til **`MonitorRowLayoutSection.xaml(.cs)`** (git mv, historik bevaret). Sektionen har ikke længere egen `ScrollViewer`/`TemplatePage`/hero-side; den er nu en ren `StackPanel` med intro-card (16 px titel) + de eksisterende monitor-kort, så den kan ligge inline.
- `Views/Settings/Pages/StartsidePage.xaml`: ny **"Launcher overview"-card øverst** (Main launcher + Gaming overlay forklaring og knap) samt `MonitorRowLayoutSection` nederst i CardsContent.
- `Views/Settings/Pages/StartsidePage.xaml.cs`: `GamingOverlayOverviewButton_Click` → `SettingsWindow.NavigateToGamingOverlayPage()`.
- `Views/Settings/SettingsWindow.xaml(.cs)`: `MonitorLayoutNavButton` og dens click-handler fjernet.
- `Helpers/Settings/LocalizationHelper.cs` + `ViewModels/MainViewModel.Localization.cs`: 5 nye nøgler i både en/da (`HomeMainLauncherOverviewTitle/Body`, `HomeGamingOverlayOverviewTitle/Body`, `HomeGamingOverlayOverviewButton`) + opdateret `MonitorRowLayoutTitle/Intro` til at nævne hoved-launcheren. Ubrugt `MonitorRowLayoutNavTitle` fjernet fra begge sprog.
- Docs/comments opdateret (`AGENT.md`, `PROJECT_STRUCTURE.md`, `ConfigModel.cs`, `MonitorLayoutDefaultsSnapshot.cs`, `MainViewModel.cs`).

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 26-10-2026 03:19:09 i både `bin` og `C:\icongrid`.

**Test for brugeren:** Indstillinger → Home: øverst en forklaring af Main launcher vs. Gaming overlay (knap åbner overlay-indstillingerne), og nederst "Monitorrække (hoved-launcher)" med alle de gamle monitor-indstillinger. "Monitor Row Layout" er væk fra sidebaren.

**Ikke committet/pushet** (kræver separat godkendelse).

## Session findings (2026-10-27) — drevbogstaver på Hardware → Disks i alfabetisk orden

**Brugerønske:** I Disks-sektionen skal drevbogstaverne vises i alfabetisk orden med **C: i toppen**.

**Rodårsag:** Disks-listen blev vist i fysisk disk-indeks-rækkefølge (`ApplyDisks` → `OrderBy(d => d.Index)`), og `DriveLetters` blev bygget i den rækkefølge `MSFT_Partition` blev enumereret. På denne maskine gav det `D:, F:, E:, G:, C:` (C: nederst).

**Implementeret:**
- `Helpers/Hardware/Storage/PhysicalDiskStatus.cs`: ny `[JsonIgnore] PrimaryDriveLetter` — parser `DriveLetters` ("C:, D:") og returnerer det **laveste** bogstav (robust over for usorteret input); `null` når disken ikke har bogstav. `OnPropertyChanged(nameof(PrimaryDriveLetter))` tilføjet i `CopyFrom`.
- `Helpers/Hardware/Storage/DiskInventoryReader.cs` (`ReadDriveLetters`): bogstaverne pr. disk sorteres nu alfabetisk (`OrderBy(letter => letter, StringComparer.Ordinal)`) før de join'es til "C:, D:, E:".
- `Helpers/Launcher/SystemMonitor.cs`: ny privat `ReorderDisks()` kaldes efter merge i `ApplyDisks`. Sortering: diske **med** bogstav først, derefter alfabetisk på `PrimaryDriveLetter`, derefter `Index`; diske uden bogstav sidst. Bruger `ObservableCollection.Move`, så item-identitet og åbne SMART-expandere bevares (ingen Clear/Add).

**Verificeret:**
- Build 0 fejl; deployet — `IconGrid.dll` 27-10-2026 01:10:19, samme størrelse (1.131.520 B) i `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid` (første deploy 01:07:04, genbygget efter en null-guard i `PrimaryDriveLetter`).
- Live `monitor-state.json`: `Index 0=D:, 1=F:, 2=E:, 3=G:, 4=C:` → ny rækkefølge bliver **C:, D:, E:, F:, G:**.
- `.local-state\disk-smoke\Program.cs`: ny `VerifyDriveLetterOrdering()` bekræftede `primary` for `'E:, C:'` = C og ordren `#2 C | #4 C | #0 D | #1 F | #3 G | #5 -` (disk uden bogstav sidst).

**Test for brugeren:** Hardware → Disks. C:-disken skal stå øverst, derefter D:, E:, F:, G:; chip ved disknavnet viser bogstaverne alfabetisk. Bruger-verificeret 27-10 ("det virker perfekt").

**Committet + pushet:** `61b1975` — "Sort Hardware Disks by drive letter (C: first)" (main → origin/main).

### Rettelse (27-10 01:16) — Voltage-tile i CPU-kortet stod skævt

**Symptom (bruger, med screenshot):** I Hardware → CPU stod "Voltage"-tile'en (1,3937 V) ikke på linje med Cores/Threads/Live temp/Live usage/Live clock/Boost — label og værdi sad ~10 px lavere, og tile'ens topkant var tilsvarende lavere.

**Rodårsag:** `Views/Settings/Pages/HardwarePage.xaml` linje 534: Voltage-`Border` havde `Margin="0,10,0,0"` hardkodet oven på `HardwareMetricTileStyle` (der sætter `0,0,10,0`). De 10 px top-margin skubbede hele tile'en ned i den ellers fælles WrapPanel-række. De øvrige CPU-tiles bruger stylens margin uændret.

**Fix:** Margin-override fjernet, så Voltage-tile'en arver `HardwareMetricTileStyle` ligesom de seks andre tiles; efterfølgende sat til `Margin="0"` som de sidste tiles i GPU- og Memory-rækkerne (fjerner kun den afsluttende 10 px-højremargin, ingen forskydning). Én XAML-linje. Ingen ændring af værdiformat eller data — `CpuVoltage` formateres stadig som `F4` ("1,3937 V") i `HardwareSnapshotCollector.FormatVoltage`; kan kortes til 2 decimaler hvis ønsket.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 27-10-2026 01:16:58, samme størrelse (1.131.520 B) i `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid` (første deploy af rettelsen 01:16:09, derefter genbygget med `Margin="0"`).

**Test for brugeren:** Hardware → CPU. Voltage-tile'ens label og værdi skal nu stå på samme vandrette linje som de øvrige tiles. Bruger-verificeret 27-10 ("det spiller tak").

**Committet + pushet:** `34bc5ba` — "Align CPU voltage tile and Automatic-while-in-game toggles" (main → origin/main).

### Rettelse (27-10 01:20) — Gaming overlay → "Automatic while in game": knapperne sad ikke på linje

**Symptom (bruger, med screenshot):** I "Automatic while in game" stod den første switch ("Show gaming overlay automatically on game start") nede til **venstre** under beskrivelsen, mens de to øvrige switches ("Reopen launcher when the overlay closes", "Close gaming overlay when the game exits") stod **højrejusteret** på titel-linjen. De tre knobs var derfor ikke aligned.

**Rodårsag:** `Views/Settings/Pages/GamingOverlayPage.xaml` — den første indstilling var bygget som løse `TextBlock`s + en `ToggleButton` med `Margin="0,8,0,0" HorizontalAlignment="Left"` direkte i `StackPanel`, mens de to andre var `Grid`-rækker med `*`/`Auto` kolonner (titel venstre, switch i kolonne 1). To forskellige layouts i samme sektion.

**Fix:** Første indstilling bygget om til præcis samme `Grid`-mønster som de to andre rækker (titel i `*`, `ToggleButton` i `Auto`-kolonne, beskrivelse som `TextBlock` lige under med `Margin="0,4,0,0"`). Bindingen `AutoShowGamingOverlayOnGameStart` er uændret, så funktionaliteten er identisk — kun layoutet er ensrettet. Ingen nye styles eller farver.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 27-10-2026 01:20:37 (1.132.032 B) i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid`. XAML gennemlæst: alle tre rækker (linje 464-481, 483-500, 502-519) har nu identisk struktur.

**Test for brugeren:** Gaming overlay → "Automatic while in game". Alle tre switches skal stå højrejusteret på hver sin titel-linje. Bruger-verificeret 27-10 ("det virker perfekt").

**Committet + pushet:** `34bc5ba` — "Align CPU voltage tile and Automatic-while-in-game toggles" (main → origin/main).

## Session findings (2026-10-27, fortsat) — Test-siden flyttet ind i About + Test-fane fjernet fra sidebaren

**Brugerønske:** Hele "Test"-siden skal ligge nederst på About-siden, og Test-ikonet skal væk fra sidebaren.

**Implementeret:**
- `git mv Views/Settings/Pages/TestPage.xaml(.cs)` → **`TestSection.xaml(.cs)`** (historik bevaret). Klassen er omdøbt `TestPage` → `TestSection` (også i code-behind), og alle `{x:Type views:TestPage}`-bindinger peger nu på `TestSection`.
- Siden er konverteret fra selvstændig side (ScrollViewer + `TemplatePage` med Hero/Cards) til en **sektion** efter samme mønster som `MonitorRowLayoutSection`: root er en `StackPanel` med et intro-kort (`StartsideSectionCardStyle`, titel "Debug / Test" + summary + forklaring) efterfulgt af de tidligere kort. Ingen ScrollViewer/TemplatePage i sektionen.
- `TestSection.xaml` har fået `<UserControl.Resources>` med `ResourceDictionary Source="/Views/TemplateGuidelines.xaml"` — **kritisk**, se nedenfor.
- `AboutPage.xaml`: hele indholdet er pakket i en `ScrollViewer` (About-siden havde ingen, så det lange indhold ville blive klippet), og `<views:TestSection />` er indsat nederst i `CardsContent` efter de to About-pladsholderkort.
- `SettingsWindow.xaml`: `TestNavButton` (DBG-badge + "Test") fjernet fra sidebaren. `SettingsWindow.xaml.cs`: `TestNavButton_Click` fjernet. Sidebaren er nu Home / Shortcut icons / Layout / Hardware / Help / Gaming overlay / About.
- Docs: `PROJECT_STRUCTURE.md` (TestSection + "embedded in About"), `README.md` (Test-rækken fjernet fra settings-tabellen; About-rækken + `trace.log`-rækken nævner nu Debug/Test-sektionen på About).

**FUND (vigtigt, kostede en crash):** Første version krasjede ved klik på About:
`XamlParseException: Cannot find resource named 'TemplateCardTitleStyle'` (TestSection.xaml linje 19) → "Fatal from dispatcher" → appen lukkede. Årsag: `TemplateCardTitleStyle` m.fl. ligger i `Views/TemplateGuidelines.xaml`, som kun er merget i `TemplatePage`s egne resources — en UserControl der instantieres under About-sidens BAML-load kan ikke slå dem op. `MonitorRowLayoutSection` løser det ved selv at merge `/Views/TemplateGuidelines.xaml`; samme linje er nu tilføjet i `TestSection.xaml`. `TemplateGuidelines.xaml` merger selv `/Views/StartsideStyles.xaml` (App-niveau), så alle tokens/stile er tilgængelige.
**Debug-trick:** runtime-XAML-fejl logges i `%APPDATA%\IconGrid\trace.log` som "Fatal from dispatcher: System.Windows.Markup.XamlParseException ..." med fuld stak — tjek altid den fil efter UI-ændringer.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 27-10-2026 01:30:44 (1.131.008 B) i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid`. `TestSection.xaml` 01:30:24 (inkl. TemplateGuidelines-merge). Ingen `TestPage`/`TestNavButton`-referencer tilbage i `Views`/`ViewModels`/`Helpers`/`Controls` (grep = 0 hits).
**Ikke automatiskt UI-verificeret:** et harness-forsøg (`.local-state\ui-smoke`, slettet igen) kunne ikke indlæse App.xaml, fordi pack-URI'en `pack://application:,,,/Views/StartsideStyles.xaml` altid resolver mod *entry*-assemblyen, og `Application.ResourceAssembly` ikke kan ændres i et separat harness. Sektionsmønstret er dog identisk med det bruger-verificerede `MonitorRowLayoutSection`.

**Test for brugeren:** Indstillinger → About. Nederst skal "Debug / Test" stå med alle de gamle kort (Current instance, IconGrid process overview, Startup diagnostics, UI ETW probe, Trace log, Notes), og sidebaren skal ikke længere have Test/DBG.

### Rettelse (27-10 01:36) — 2 tomme kort fjernet + mørk-tema-tekst i Debug/Test

**Symptom (bruger, med screenshot):** About-siden havde stadig **to tomme pladsholderkort** (MinHeight 160), og i "Debug / Test" var summary-linjen ("Mode: UI | Elevation: STANDARD | PID: …") næsten usynlig i mørkt tema (mørk tekst på mørk baggrund), mens resten af teksten var lys.

**Rodårsag:** `TemplateSmallTextStyle`/`TemplateCardTitleStyle`/`TemplateHeroBodyStyle` sætter `Foreground="{Binding SettingsSubtextForeground}"` / `{Binding TopBarForeground}` **uden RelativeSource** — de forventer at DataContext er `MainViewModel`. `TestSection` satte `DataContext = this` i constructoren (arv fra den gamle TestPage), så de tre style-bindings fejlede og tekstfarven faldt tilbage til systemets mørke standardfarve. Kun de TextBlocks der havde en eksplicit `DataContext.SettingsSubtextForeground, RelativeSource=Window`-binding var læsbare.

**Fix:**
- `TestSection.xaml.cs`: `DataContext = this;` fjernet — alle bindinger i `TestSection.xaml` bruger i forvejen `RelativeSource` (`{x:Type views:TestSection}` eller `AncestorType=Window`), så sektionen arver nu `MainViewModel` fra SettingsWindow og template-stilene virker som på alle andre sider (verificeret: 0 `{Binding`-forekomster uden `RelativeSource` i filen).
- `AboutPage.xaml`: de to tomme `MinHeight="160"`-pladsholderkort fjernet; About = hero-kort + Debug/Test-sektionen.

**Verificeret:** build 0 fejl; deployet — `IconGrid.dll` 27-10-2026 01:35:55 (1.131.008 B) i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\icongrid`.

**Test for brugeren:** About-siden skal nu vise hero + Debug/Test uden tomme kort, og summary-linjen skal være lys/læsbar i mørkt tema. Bruger-verificeret 27-10 ("perfekt alt spiller").

**Committet + pushet:** `dc47eca` — "Move Test page into About and fix its dark-theme text" (main → origin/main). Omfatter Test→TestSection-omdøbelsen, About-indlejringen, sidebaren uden Test, docs og disse to rettelser.


## Build + deploy (2026-11-09 16:47) — frisk build til bruger-test

**Ændringer i koden:** ingen — dette var udelukkende et build/deploy-kald fra brugeren ("lav en ny build og deploy til C:\icongrid").

**Kørt:**
- `cmd /c "dotnet build IconGrid.csproj --nologo -v m > .local-state\build.log 2>&1"` → **Build succeeded**, 0 advarsler, 0 fejl (8,50 s). Log: `.local-state\build.log`.
- `cmd /c "E:\IconGrid-GitHub\deploy-test.cmd > .local-state\deploy.log 2>&1"` → "Stopping IconGrid... Copying runtime files... Done. IconGrid deployed to C:\icongrid". Log: `.local-state\deploy.log`.

**Verificeret:** `IconGrid.dll` er **09-11-2026 16:47** (1.131.008 B) i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\IconGrid` → deploy matcher build. Bevis: `.local-state\deploy-verify.txt`.

**Ikke gjort:** intet committet/pushet (kræver separat bruger-godkendelse, jf. AGENT.md).


## Session findings (2026-11-09) — Gaming overlay: udtræk af visuals + live preview + manuel vis-knap

**Formål (brugerønske):** (1) vis en *ægte* visning af gaming overlayet på "Gaming overlay"-siden (ikke et screenshot), placeret nederst i top-kortet under al teksten/"Transparent while in game"; (2) en knap "Vis gaming overlay" der kan vise overlayet manuelt uden at forstyrre auto-flowet (spilstart → auto-vis → launcher hide). CPU/GPU Voltage, GPU core MHz og frame time i overlayet er udskudt til en senere session.

### Trin 1a — `GamingOverlayBar` UserControl (adfærdsneutralt udtræk)
- Ny `Views/Launcher/GamingOverlayBar.xaml(.cs)`: hele `Window.Resources` (converters, brushes, `MonitorTextStyle`, `MonitorDividerStyle`, `NetworkMonitorTextStyle`, `OverlayFpsTextStyle`, `OverlayCloseButtonStyle`, `OverlayToggleButtonStyle`) + indholds-Grid'en (bar + `PopupPanelsRow`) flyttet 1:1 via script (`.local-state/extract-overlay-bar.ps1`, backup i `.local-state/GamingOverlayWindow.xaml.bak`).
- Alle 9 event-attributter fjernet fra XAML; `GamingOverlayWindow` wirer dem i `WireOverlayBar()` (drag, gear Checked/Unchecked, close, scale-menu, "open settings", slider Preview*/LostMouseCapture). `OverlayRoot`, `PopupPanelsRow`, `SettingsMenuButton`, `CloseButton`, `ScalePopupPanel`, `ScaleMenuButton`, `PopupScaleSlider`, `OpenSettingsButton` er tilgængelige som genererede felter.
- `GamingOverlayWindow.xaml` er nu en tynd shell (34 linjer): Window-egenskaber + WindowChrome + `<views:GamingOverlayBar x:Name="OverlayBar" />`. Window code-behind uændret logik; kun `OverlayBar.`-prefix på elementreferencer (script: `.local-state/patch-overlay-refs.ps1`).
- `ModernSliderStyle` ligger i `Views/StartsideStyles.xaml` (App-niveau) og virker derfor også i UserControl'en.

### Trin 1b — Live preview på Gaming overlay-siden
- `GamingOverlayBar.IsPreview` (CLR-prop, anvendes også i `Loaded` fordi XAML kan sætte den før børnene findes): skjuler gear + luk, kollapser `PopupPanelsRow`, nulstiller `LayoutTransform`.
- `Views/Settings/Pages/GamingOverlayPage.xaml` (~linje 440-480, nederst i hero-kortets StackPanel): lokaliseret overskrift/intro + `Border` (inset-surface, `CornerRadius` 6, `MaxHeight` 140, `IsHitTestVisible="False"`) med `Viewbox Stretch="Uniform"` omkring `<views:GamingOverlayBar IsPreview="True" DataContext="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}}" />`.
- **Vigtigt fund:** elementer inde i `<views:TemplatePage.HeroContent>` ligger i `TemplatePage`s namescope → `x:Name` gav `MC3093` (navn kan ikke registreres). Løsning: ingen x:Name; sæt `IsPreview` i XAML og bind DataContext til vinduets VM (SettingsWindow.DataContext = MainViewModel). Siden har selv `DataContext = this`.
- Preview'et er live (FPS/CPU/GPU/ping opdateres) og følger tema/tekstfarve/overlay-scale via de eksisterende `RelativeSource AncestorType=Window`-triggere.
- Forbehold: "Transparent while in game" gælder kun under et spil, så preview uden for spil viser baren med baggrund.

### Trin 2 — Knappen "Vis gaming overlay"
- `MainViewModel`: nyt `event Action? ShowGamingOverlayRequested` + `RequestShowGamingOverlay()` (ved `GameLaunched`/`GameExited`).
- `MainWindow`: subscribe i ctor, `OnShowGamingOverlayRequested()` → `LogTrace(...)` + `ShowGamingOverlay(GamingOverlayLayout.Horizontal)`; **ingen** `HideForGame`, ingen game-state, ingen ændring af auto-indstillinger. Unsubscribe i teardown.
- `GamingOverlayPage`: `ShowOverlayButton_Click` → `_mainViewModel?.RequestShowGamingOverlay()`; knap med eksisterende `OverlayActionButtonStyle` + lokaliseret titel/intro i både `da` og `en` (`RefreshLocalizedText`). Nye strenge: `OverlayPreviewTitleText`, `OverlayPreviewIntroText`, `ShowOverlayButtonText`, `ShowOverlayButtonIntroText`.
- Auto-flowet er urørt: auto-vis sker kun i `OnGameLaunched`, auto-luk kun i `OnGameExited`, og `RestoreLauncherFromGame` kaldes fortsat (idempotent) når overlayet lukkes.

### Build/deploy
- Build 0 fejl / 0 advarsler (flere delbyg undervejs; CS0104 `UserControl`-ambiguitet mod WinForms og manglende `System.Windows.Media` blev rettet i `GamingOverlayBar.xaml.cs`).
- **Deployet 2026-11-09 17:19** til `C:\IconGrid` — `IconGrid.dll` 1.137.664 B i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\IconGrid`.
- Appen startet manuelt 17:20:32 (`trace.log`: "OnStartup begin"/"Starting launcher UI. Elevated=False"), ingen fatal.

### Status / næste skridt
- **Mangler bruger-verifikation** (runtime-test kan ikke gøres fra agenten): (a) overlayet i spil er visuelt identisk (drag, gear-popup, scale-slider, X, "Open gaming overlay settings"); (b) preview vises i top-kortet med live-tal; (c) "Vis gaming overlay"-knappen åbner overlayet uden at skjule/minimere launcher'en; (d) spilstart → auto-vis + launcher hide uændret; spilluk → auto-luk + launcher-genopretning uændret.
- Ikke committet/pushet (kræver separat godkendelse).
- Næste feature (udskudt): CPU/GPU Voltage, GPU core MHz, frame time i overlayet — preview'et arver dem automatisk, fordi det er samme kontrol.

### Opdatering (17:22) — to robusthedsrettelser efter første deploy
- `GamingOverlayBar.xaml`: `Background="Transparent"` fjernet fra UserControl-roden (undgår en ekstra hit-test-flade over hele overlay-vinduet).
- `GamingOverlayBar.ApplyPreviewState()`: `PopupPanelsRow` kollapses nu kun når `IsPreview=true` (så et åbent gear-popup i det in-game overlay ikke kan blive lukket af `Loaded`).
- **Ny deploy: 2026-11-09 17:22** — `IconGrid.dll` 1.137.664 B i både build-mappen og `C:\IconGrid` (build 0 fejl/0 advarsler).

## Session findings (2026-11-09, fortsat) — Overlay-indhold: vælg hvad overlayet viser

**Brugerønske:** en liste over overlayets elementer med til/fra, så man selv vælger hvad der vises (fx GPU-temp eller ej) — i sit **eget kort** på Gaming overlay-siden (bekræftet: egen sektion/hero-card).

### Datamodel (persisteret i config)
Ny persistence-kæde for 5 booleans (alle default `true`, så eksisterende configs er uændrede):
- `Models/ConfigModel.cs`: `GamingOverlayShowClock/Network/Cpu/Gpu/Fps`
- `ViewModels/Settings/MainViewModelConfigState.cs`: init-props + mapping i `FromConfig`
- `ViewModels/Settings/MainViewModelSettingsState.cs`: props
- `ViewModels/Settings/MainViewModelSettingsPersistence.cs`: skriv til `config.*`
- `ViewModels/MainViewModel.Settings.cs`: `ApplyConfig` + `ResetToDefaults` + `SaveSettingsToConfig`
- `ViewModels/MainViewModel.cs`: backing fields
- `ViewModels/MainViewModel.Overlay.cs`: public properties (gemmer via `SaveSettingsToConfig`) + 4 beregnede divider-flags:
  `GamingOverlayDividerClockNetworkVisible`, `...NetworkCpu...`, `...CpuGpu...`, `...GpuFps...` (kun divider mellem to synlige blokke → ingen løse `|`).

### Overlay (`Views/Launcher/GamingOverlayBar.xaml`)
- `BooleanToVisibilityConverter` (`BoolToVisibility`) tilføjet i resource-dictionary.
- Synlighed bundet pr. blok: clock, ping-prik + netværkslabel, CPU-temp + CPU-bar, GPU-temp + GPU-bar, FPS-grid. Dividerne binder til de beregnede par-flags.
- `GamingOverlayWindow.ViewModel_PropertyChanged`: reagerer nu også på de 5 visnings-flags → `UpdateOverlayMetrics()`, så et **åbent** overlay straks får ny bredde (før blev bredden kun sat ved scale/preset-ændringer).

### Settings-UI (`Views/Settings/Pages/GamingOverlayPage.xaml`)
- Nyt kort **"Overlay-indhold"** som FØRSTE kort i `CardsContent` (lige under hero-kortet), `IsExpanded="True"` og samme `StartsideSectionCardStyle` + `GamingOverlayHeroExpanderStyle` som de øvrige kort.
- 5 rækker i præcis samme Grid-mønster som resten af siden (titel + beskrivelse i `*`, `ToggleSwitchStyle` i `Auto`), to-vejs bundet til `DataContext.GamingOverlayShow*` via `RelativeSource AncestorType=Window`.
- Lokalisering (legacy side-mønster, både `da` og `en`): `OverlayContentTitleText`, `OverlayContentIntroText`, `OverlayContentHintText` + `OverlayContent{Clock,Network,Cpu,Gpu,Fps}TitleText/IntroText`.
- Fordi preview'et i hero-kortet er den samme kontrol, opdateres forhåndsvisningen **live** når man slår ting fra/til.

### Build/deploy
- Build 0 fejl / 0 advarsler (to deploys: 19:17 og **19:18**).
- **Deployet 2026-11-09 19:18** — `IconGrid.dll` 1.152.512 B i både `bin\Debug\net10.0-windows10.0.22621.0` og `C:\IconGrid`.

### Status / næste skridt
- **Mangler bruger-verifikation:** (a) kortet vises øverst med 5 kontakter; (b) slår man fx GPU fra, forsvinder GPU-temp + bar i preview OG i det åbne in-game overlay (bredden tilpasses); (c) indstillingen huskes efter genstart; (d) dividere efterlader ikke løse `|`.
- Ikke committet/pushet (kræver separat godkendelse).
- Stadig udskudt: CPU/GPU Voltage, GPU core MHz, frame time i overlayet (kan tilføjes som flere rækker i samme kort + nye blokke i baren).

### Commit + push (2026-11-09 ~19:25)
- **Committet og pushet (bruger-godkendt):** `82e03b4` — "Gaming overlay: extract bar control, add live preview and content toggles" (16 filer, +1130/−459), pushet til `origin/main` (`79f0663..82e03b4`).
- Denne note er **ikke** committet (kræver separat godkendelse).

## Session findings (2026-11-09, fortsat 2) — GPU core MHz + frame time i overlayet

**Brugerønske:** tilføj GPU core MHz og frame time til gaming overlayet (første del af den udskudte telemetri; CPU/GPU voltage mangler stadig).

### Datakilder (fandtes allerede — ingen ny capture-pipeline)
- **GPU core MHz:** `HardwareSnapshotCollector.CaptureGpuClock()` → `SystemMonitor.GpuClock`. Tidligere formateret med `FormatClock` (GHz over 1000). Ny `FormatGpuClock()` viser **altid MHz** (`1935 MHz`), som undervolt-værktøjer gør. `GpuClock` blev ikke vist andre steder i UI (kun `CpuClock` bruges på Hardware-siden), så formatændringen er sikker.
- **Frame time:** `SystemMonitor.FrameTimeStatus` (1000 / target-FPS). Var ikke bundet i UI før. Formatering ændret fra `F0` (heltal) til **`F1`** (fx `6.9`) for brugbar præcision. Vises som `{FrameTimeStatus} ms`.

### Config (persisteret)
To nye flags, default `true` (eksisterende configs uændrede), ført gennem hele kæden:
`GamingOverlayShowGpuClock`, `GamingOverlayShowFrameTime` i `ConfigModel`, `MainViewModelConfigState`, `MainViewModelSettingsState`, `MainViewModelSettingsPersistence`, `ApplyConfig`/`ResetToDefaults`/`SaveSettingsToConfig`, backing fields i `MainViewModel.cs`.

### VM (`MainViewModel.Overlay.cs`)
- Nye properties `GamingOverlayShowGpuClock` / `GamingOverlayShowFrameTime` (gemmer via `SaveSettingsToConfig`).
- Nye beregnede synligheder `GamingOverlayGpuClockVisible = ShowGpu && ShowGpuClock` og `GamingOverlayFrameTimeVisible = ShowFps && ShowFrameTime` → ingen "løse" værdier hvis GPU/FPS-blokken er slået fra. `ShowGpu`/`ShowFps`-setterne notificerer dem.

### Overlay (`Views/Launcher/GamingOverlayBar.xaml`)
- GPU-ur som `TextBlock` **inde i GPU-blokken** lige efter GPU-baren: `GPU: 62° [bar] 1935 MHz` (synlig via `GamingOverlayGpuClockVisible`).
- Frame time som `TextBlock` **inde i FPS-blokken** lige efter FPS-tallet: `FPS: 144 6.9 ms` (synlig via `GamingOverlayFrameTimeVisible`). Ingen nye dividere → divider-logikken er uændret.
- `GamingOverlayWindow.ViewModel_PropertyChanged` reagerer nu også på de to nye flags → `UpdateOverlayMetrics()`, så et åbent overlay skalerer straks.

### Settings-UI (`GamingOverlayPage`)
- To nye rækker i "Overlay-indhold"-kortet: **"GPU-ur (core MHz)"** og **"Frametid"** (samme Grid-mønster + `ToggleSwitchStyle`).
- Lokalisering (da/en): `OverlayContentGpuClockTitleText/IntroText`, `OverlayContentFrameTimeTitleText/IntroText` — teksterne nævner at de kræver GPU hhv. FPS-blokken.

### Build/deploy
- Build 0 fejl / 0 advarsler.
- **Deployet 2026-11-09 19:42** — `IconGrid.dll` 1.158.144 B i både build-mappe og `C:\IconGrid`.

### Status / næste skridt
- **Mangler bruger-verifikation:** (a) GPU-ur vises som MHz ved siden af GPU-temp; (b) frame time vises som `x.x ms` ved siden af FPS; (c) begge kan slås fra i "Overlay-indhold" og huskes; (d) preview opdateres live.
- Næste telemetri: **CPU/GPU Voltage** (CPU-voltage findes allerede som `SystemMonitor.CpuVoltage`/snapshot `CpuVoltage`; GPU-voltage skal verificeres i LibreHardwareMonitor-sensoren).
- Ikke committet/pushet (kræver separat godkendelse).

### Opdatering (19:57) — FT før FPS + "FT: x.xms"-format
- Brugerønske fra preview-screenshot: frame time skal stå **før** FPS og hedde **`FT: --ms`** (uden mellemrum før `ms`).
- `Views/Launcher/GamingOverlayBar.xaml`: frame time er nu en fast-bredde `Grid` (80 px, kolonner 24 + `*`) med label `FT:` og værdien `{FrameTimeStatus}ms`, placeret **mellem GPU/FPS-divideren og FPS-grid'en** → rækkefølge: `... GPU: 34° ▮ 375 MHz | FT: 6.9ms FPS: 144`. Fast bredde forhindrer at FPS-tallet hopper når frame time skifter mellem fx `6.9ms` og `16.7ms`.
- Lokalisering opdateret (da/en): frame time-introen nævner nu `FT: x.xms` og at den står foran FPS.
- Synlighedslogik uændret: FT er stadig en del af FPS-gruppen (`GamingOverlayFrameTimeVisible = ShowFps && ShowFrameTime`), så der aldrig står en FT uden FPS-blok.
- **Deployet 2026-11-09 19:57** — `IconGrid.dll` 1.158.656 B i både build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).

### Rettelse (19:59) — FT-mellemrum ensrettet
- Brugerfeedback: for stort mellemrum mellem `FT:` og `--ms` (den faste 80 px-grid med **højrejusteret** værdi gav et hul).
- Fix: FT er nu et almindeligt `TextBlock` med tre `Run`s — `"FT: "` + værdi + `"ms"` — præcis som `CPU: 47°` / `Net: 12ms` / `GPU: 34°`. Margin `0,0,9,0` som naboerne.
- Konsekvens: blokken er ikke længere fast-bredde, så FPS kan flytte sig ganske lidt når frame time skifter antal tegn (fx `6.9ms` → `16.7ms`). Det er accepteret til fordel for korrekt afstand.
- **Deployet 2026-11-09 19:59** — `IconGrid.dll` 1.158.144 B i både build-mappe og `C:\IconGrid`.

### Rettelse (20:08) — fast bredde på FT og GPU-ur (ingen ryk i overlayet)
- Brugerfeedback: overlayet rykkede når FT gik fra 1 til 2 decimaler (`6.9ms` → `16.7ms`), fordi rækken er højrejusteret og bredden dermed ændrede sig.
- Fix i `Views/Launcher/GamingOverlayBar.xaml`:
  - FT er nu `StackPanel` med label `"FT: "` + værdi i et **fast 46 px felt, venstrejusteret** → afstanden efter kolon er konstant, og bredden ændrer sig ikke ved 1 vs. 2 decimaler.
  - GPU-uret har fået **fast 58 px bredde** (højrejusteret), så skift fra 3 til 4 cifre (`835 MHz` → `1935 MHz`) ikke flytter rækken.
- **Deployet 2026-11-09 20:08** — `IconGrid.dll` 1.158.656 B i både build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- **Kendt rest (valgfrit):** CPU/GPU-temperatur (`47°` → `100°`) og ping (`12ms` → `150ms`) kan stadig skifte bredde med ét tegn og dermed flytte rækken ganske lidt. Kan løses med samme faste felter, hvis det ønskes.

### Commit + push (2026-11-09 ~20:12)
- **Committet og pushet (bruger-godkendt):** `3ae7261` — "Gaming overlay: add GPU core clock and frame time telemetry" (14 filer, +219/−3), pushet til `origin/main` (`82e03b4..3ae7261`).
- Bruger har testet GPU-ur, FT-format (`FT: x.xms` før FPS) og de faste bredder — **alt godkendt**.
- Noten er committet i en separat opfølgnings-commit (`Note: ...`) efter bruger-godkendelse.

### Rettelse (23:13) — "Overlay content" foldet sammen som default
- `Views/Settings/Pages/GamingOverlayPage.xaml`: `IsExpanded="False"` på "Overlay content"-expanderen (var `True`). Kortet følger nu samme default som de øvrige kort på siden (kun hero-kortet er åbent).
- **Deployet 2026-11-09 23:13** — `IconGrid.dll` 1.158.656 B i både build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Committet og pushet efter bruger-godkendelse (bruger: "det spiller").

## Session findings (2026-11-09, fortsat 3) — Overlayet har nu fast mørk skin (uafhængig af Windows-tema)

**Brugerønske:** gaming overlayet (og preview'et på Gaming overlay-siden) må **ikke** følge Windows-temaet — det skal altid se ud som nu (mørkt). Baggrund: der kommer en **Skin Studio**-side senere.

### Ændringer (`Views/Launcher/GamingOverlayBar.xaml` — den eneste fil)
- **Fjernet alle tema-afhængigheder** i kontrollen:
  - `MonitorTextStyle`: `IsLightTheme`-trigger (der satte tekst til `#111`) er slettet → tekst er altid hvid (og stadig bruger-valgt tekstfarve når transparens er aktiv).
  - `OverlayFpsTextStyle`: `IsLightTheme`-trigger slettet → FPS er altid `#7CFF6B` (`OverlayFpsBrush`).
  - `OverlayCloseButtonStyle` + `OverlayToggleButtonStyle`: de temastyrede light/dark-templates er erstattet af **én** mørk template (hover `#14FFFFFF`, pressed/checked `#21FFFFFF`) og `Foreground` er nu den faste `OverlayBarForegroundBrush` i stedet for `TopBarForeground`.
  - `OverlayRoot` + begge popup-paneler: `{Binding DataContext.TopBarBackground ...}` → `{StaticResource OverlayBarBackgroundBrush}` (3 steder). Transparens-triggerne (Transparent baggrund / kun under spil) er **bevaret** — de er funktionelle, ikke tema.
- **Nye, centraliserede skin-brushes** (klar til Skin Studio): `OverlayBarBackgroundBrush` (#202020), `OverlayBarForegroundBrush` (#E5E5E5). Sammen med `OverlayFpsBrush`, `OverlayMenuBorderBrush`, `OverlayCloseHoverDark`, `OverlayClosePressedDark`, `MonitorHighPingColor` udgør de hele paletten.
- Ubrugte brushes fjernet: `OverlayFpsLightBrush`, `OverlayCloseHoverLight`, `OverlayClosePressedLight`.
- Verificeret: 0 forekomster af `TopBarBackground`, `IsLightTheme`/`IsDarkTheme` og `*Light`-brushes i filen (kun en forklarende kommentar nævner tema).
- Preview'et på settings-siden bruger samme kontrol → er automatisk mørkt uanset tema.

### Build/deploy
- Build 0 fejl / 0 advarsler.
- **Deployet 2026-11-09 23:24** — `IconGrid.dll` 1.156.608 B i både build-mappe og `C:\IconGrid`.

### Status / næste skridt
- **Mangler bruger-verifikation:** (a) overlayet er mørkt også når Windows/app er i lyst tema; (b) preview'et på settings-siden er mørkt; (c) gear/scale-popup og knap-hover ser stadig rigtige ud.
- **Skin Studio (planlagt, ikke startet):** alle skin-værdier ligger nu i `GamingOverlayBar`'s resource-blok. Næste skridt vil være at flytte dem til config-drevne brushes (fx `GamingOverlaySkin*` i `ConfigModel`) og binde dem i stedet for `StaticResource`, så en dedikeret side kan styre dem uden at røre overlayets layout.
- Ikke committet/pushet (kræver separat godkendelse).

## Session findings (2026-11-10) — Skin Studio (fase 1): skin-tokens, editor og billede-import

**Brugerønske:** Skin Studio-side i sidebar'en; overlayet skal kunne skifte "skin" (farver, skrifttype, form) — og skin skal kunne udledes fra brugerens egne spil-billeder (CoD MW4, PoE-league osv.). **Layoutet (monitor-rækkens pladser/orden/mellemrum/skalering) må aldrig ændres af et skin.**

### Skin-model (`Models/GamingOverlaySkin.cs` — ny)
- Tokens: `Name`, `BarBackgroundColor`, `PopupBackgroundColor`, `TextColor`, `AccentColor` (FPS/slider), `CpuBarColor`, `GpuBarColor`, `DividerOpacity`, `FontFamily`, `MonoFontFamily`, `CornerRadius`.
- Afledte brushes (`BarBackgroundBrush`, `PopupBackgroundBrush`, `TextBrush`, `AccentBrush`, `CpuBarBrush`, `GpuBarBrush`, `DividerBrush`, `CornerRadiusValue`) + INotifyPropertyChanged, `ToJson`/`FromJson`/`Clone`/`CopyFrom`.
- 5 indbyggede presets: **Night Ops** (nuværende look), **Tactical Olive**, **Cyber Cyan**, **Ember**, **Arctic**. Neutralt navngivet (ingen tredjeparts-branding i det offentlige repo).
- Fontlister: `AvailableFonts` (Segoe UI, Bahnschrift, Bahnschrift SemiCondensed, Consolas, Cascadia Mono, Impact, Arial Narrow, Tahoma) og `AvailableMonoFonts` — kun installerede Windows-fonte, intet bundlet.

### Persistering
`ConfigModel.GamingOverlaySkinName` + `GamingOverlaySkinJson` → `MainViewModelConfigState`/`MainViewModelSettingsState`/`MainViewModelSettingsPersistence` → `ApplyConfig`/`ResetToDefaults`/`SaveSettingsToConfig`. `MainViewModel.Overlay.cs`: `GamingOverlaySkin`, `ApplyGamingOverlaySkin(skin, save)`, `SaveGamingOverlaySkin()`.

### Overlay (`Views/Launcher/GamingOverlayBar.xaml`)
- Alle tidligere faste farver/fonte bindes nu til `GamingOverlaySkin`: tekstfarve+font, FPS-farve+mono-font, divider-brush+opacity, bar-/popup-baggrund, CPU-/GPU-bar-farve, knap-ikonfarve, corner radius (bar + popups).
- Ubrugte ressourcer fjernet (`OverlayFpsBrush`, `OverlayBarBackgroundBrush/ForegroundBrush`). Ingen tema-triggers tilbage — skin er uafhængig af Windows-tema.

### Skin Studio-siden (`Views/Settings/Pages/SkinStudioPage.xaml(.cs)` — ny)
- Sidebar-knap `SkinStudioNavButton` (Segoe Fluent-ikon `&#xE790;`, da der ikke findes et figma-ikon) + `SkinStudioNavButton_Click`/`ShowSkinStudioPage()` i `SettingsWindow`.
- Kort: **Live preview** (samme `GamingOverlayBar` med `IsPreview`), **Presets** (knapper), **Farver** (6 farvevælgere via `Forms.ColorDialog`), **Skrifter og form** (to fonte + radius-slider), **Fra dine egne billeder** (import + swatch-stribe).
- Alt gemmes automatisk ved ændring (`SaveGamingOverlaySkin()`).
- Lokalisering: 23 nye nøgler i `LocalizationHelper` (en + da), properties + `NotifySkinStudioPropertiesChanged()` i `MainViewModel.Localization.cs` (centraliseret mønster, ikke legacy).

### Billede-analyzer (`Helpers/Skin/SkinPaletteAnalyzer.cs` — ny)
- Lokal, uden AI/API: indlæser 1-N billeder (DecodePixelWidth 160), bucket-tæller farver (5 bit/kanal), vælger klynge → baggrund (mørkeste), popup (lidt lysere), tekst (lyseste, med kontrast-guard), accent/CPU/GPU (mest mættede, brightness-guard). Returnerer også 8 swatches.
- Bevidst designet så en senere **vision-model** kan erstatte/berige den bag samme `Result`-type (reference-drevet analyse, ikke generisk tekstur-prompt).

### Build/deploy
- Build 0 fejl / 0 advarsler (CS0104-ambiguiteter `Color`/`Button`/`UserControl` mod WinForms løst med aliases).
- **Deployet 2026-11-09 23:48 (fundament) og 23:52 (Skin Studio)** — `IconGrid.dll` 1.192.448 B i både build-mappe og `C:\IconGrid`.

### Status / næste skridt
- **Mangler bruger-verifikation:** (a) Skin Studio i sidebar; (b) presets + farvevælgere + fonte + radius slår igennem live i preview og i det åbne overlay; (c) "Importér billeder" udtrækker palette fra CoD/PoE-referencer; (d) indstillingerne huskes efter genstart.
- **Ikke bygget endnu:** AI/vision-analyse (reference → JSON-tokens), img2img-baggrundsplade, `.iconskin` eksport/import, auto-skin pr. spil, baggrundsbillede som token.
- Ikke committet/pushet (kræver separat godkendelse).

## Session findings (2026-11-10, fortsat) — Lag-baseret skin-design (chips, art, logo, typografi)

**Brugerfeedback:** farve-tokens alene er "ikke rigtig skin design" (reference: Gemini-illustration med camo-baggrund, grønne/lilla CPU/GPU-chips, rammer om gear/X, MW4-logo, fed typografi). Ønske: kunne lave samme look med kode.

### Nye design-tokens i `Models/GamingOverlaySkin.cs`
- **Chips:** `ShowChips`, `ChipBackgroundColor` (ARGB understøttet), `ChipBorderColor`, `ChipCornerRadius`, `ChipPadding` + afledte `ChipPaddingThickness`/`ChipBorderThickness`/`ChipBackgroundBrush` (transparent når chips er slået fra → layout uændret).
- **Knapper:** `ButtonBackgroundColor`, `ButtonBorderColor`, `ButtonCornerRadius` (+ `ButtonCornerRadiusValue`).
- **Typografi:** `FontWeight` (Normal/SemiBold/Bold) + `FontWeightValue`.
- **Baggrundsart:** `BackgroundImagePath`, `BackgroundImageOpacity`, `BackgroundTintColor`, `BackgroundTintOpacity` (+ `BackgroundImageBrush` som `ImageBrush` med `UniformToFill`, `HasBackgroundImage`). Billedet indlæses via `BitmapImage` med `OnLoad` og fryses.
- **Logo-slot:** `LogoImagePath`, `LogoWidth` (+ `LogoImageSource`, `HasLogo`).
- `CopyFrom` opdateret med alle nye tokens (bevarer live bindings).

### Bar (`Views/Launcher/GamingOverlayBar.xaml`)
- Baggrundslag: to `Border`s bag indholdet (`BackgroundImageBrush` + `BackgroundTintBrush` med opacity) — `Grid.ColumnDefinitions` flyttet op før indhold (ellers XAML-fejl "property elements cannot be in the middle of content").
- **Logo** som første element i monitor-rækken (kun synlig når `HasLogo`).
- **Chips** omkring CPU- og GPU-gruppen (temp + belastningsbar) — præcis som referencebilledet; GPU-ur/FT/FPS står uden chip.
- Knap-styles: baggrund/kant/radius fra skin (`ButtonBackgroundBrush`, `ButtonBorderBrush`, `ButtonCornerRadiusValue`), stadig med hover/pressed-triggers.
- `MonitorTextStyle` bruger nu også `FontWeightValue`.

### Vigtig fejl fanget undervejs
Et script, der skulle erstatte knap-baggrunde, ramte **5** forekomster i stedet for 2 — inkl. de tre transparens-triggere ("Transparent baggrund"/"kun under spil"). Rettet med et linje-baseret script, der kun ændrer sætningen når forgænger-linjen indeholder `GamingOverlayTransparentBackgroundEffective`. Verificeret: 3 gendannet, 2 tilbage (knapperne).

### Presets
**Tactical Olive** bruger nu de nye lag: chips (mørk oliven-fill + lys oliven-kant), `Bold` Bahnschrift, rammede knapper, tint. Night Ops er stadig rent/fladt som før.

### Build/deploy
- Build 0 fejl / 0 advarsler.
- **Deployet 2026-11-10 00:17** — `IconGrid.dll` 1.200.640 B i både build-mappe og `C:\IconGrid`.

### Status / næste skridt
- **Mangler:** Skin Studio-UI til de nye tokens (chip-farver/padding, knap-farver, font-vægt, **vælg baggrundsbillede**, tint-opacity, **vælg logo** + bredde, "fjern"-knapper), samt kopiering af brugerens billeder til `%APPDATA%\IconGrid\skins\` så stien er stabil.
- Så kan brugeren genskabe MW4-looket med egen camo-tekstur + eget logo (intet tredjeparts-grafik i repoet).
- Ikke committet/pushet.

## Session findings (2026-11-10, fortsat 2) — Gemini HTML-reference gennemgået + effekt-lag implementeret

**Input:** `E:\IconGrid-GitHub\gaming-overlay-html-test\cod_mw4_gaming_overlay.html` (Gemini-mockup). Bruger: "vi skal kun bruge overlayet" (ikke sandbox/modal/knapper udenom).

### Gennemgang af HTML'ens overlay-greb → WPF
| HTML | WPF | Status |
|---|---|---|
| `.camo-pattern` (4× `radial-gradient` i tiles) | `SkinPatternFactory.CreateCamoBrush` → `DrawingBrush` (Tile, 60 px, 4 blobs) | ✅ implementeret |
| Hex-mønster | `CreateHexBrush` (stregter polygon-tile) | ✅ |
| CRT scanlines | `CreateScanlineBrush` (statisk, ingen animation) | ✅ |
| Digitale segmenter i meter-bar | `CreateMeterSegmentsBrush` (tile 4 px lodrette linjer) | ✅ |
| `bg-gradient-to-r from-emerald-600 to-mw-green` | `LinearGradientBrush` (`CpuMeterBrush`/`GpuMeterBrush`) | ✅ |
| `text-shadow`/`box-shadow` glow | `DropShadowEffect` (`ShadowDepth=0`) — **kun på FPS-tallet** for at beskytte FPS | ✅ |
| `from-amber-700 via-amber-900 to-zinc-900` + gold-frame | vertikal `LinearGradientBrush` + `ButtonBorderBrush` | ✅ (uden bevel) |
| `clip-path` polygoner (chamfer/bevel) | kræver `Path`-geometri + `Clip` beregnet på `SizeChanged` | ⏳ næste skridt |
| Orbitron / Chakra Petch / Share Tech Mono | Google Fonts = OFL → må bundles | ⏳ kræver at bruger downloader `.ttf` (agenten kan ikke hente binære filer) |
| Gear-roterer-på-hover | `RotateTransform` + Storyboard-trigger | ⏳ kosmetisk, senere |

### Nye tokens (`GamingOverlaySkin`)
`PatternKind` (None/Camo/Hex/Scanlines), `PatternColorA/B/C`, `PatternLineColor`, `PatternOpacity`, `MeterGradient`, `MeterGradientStartColor/EndColor`, `MeterSegments`, `MeterSegmentColor`, `GlowEnabled`, `GlowColor`, `GlowRadius`, `ButtonGradient`, `ButtonGradientTopColor/BottomColor`. Alle med afledte brushes/effekt: `PatternBrush`, `HasPattern`, `CpuMeterBrush`, `GpuMeterBrush`, `MeterSegmentsBrush`, `AccentGlowEffect`; `ButtonBackgroundBrush` bliver gradient når `ButtonGradient=true`. `CopyFrom` opdateret.

### Bar (`GamingOverlayBar.xaml`)
- Nyt mønster-lag mellem tint og indhold (`PatternBrush` + `PatternOpacity`, synligt via `HasPattern`).
- CPU/GPU-metre: gradient-fill (`CpuMeterBrush`/`GpuMeterBrush`) + segment-overlay (`MeterSegmentsBrush`) i `Grid ClipToBounds`.
- FPS-tallet har `Effect="{Binding GamingOverlaySkin.AccentGlowEffect}"`.

### Preset
**Tactical Olive** aktiverer nu hele effekt-laget: camo-mønster, glow (#00FF66, radius 8), gradient-metre, segmenter og metal-gradient knapper (amber→zinc) — dvs. tæt på HTML-referencen.

### Build/deploy
- Build 0 fejl / 0 advarsler (CS0104-ambiguiteter `Brush`/`Pen`/`Point`/`ColorConverter`/`Brushes` mod `System.Drawing` løst med aliases).
- **Deployet 2026-11-10 00:45** — `IconGrid.dll` 1.210.368 B i både build-mappe og `C:\IconGrid`.

### Status
- **Mangler bruger-verifikation:** vælg Tactical Olive → camo + glow + gradient-metre + metal-knapper i preview og i spil.
- **Ikke bygget:** chamfer/bevel-clip, bundlede fonter (afventer `.ttf`), hover-rotation, Skin Studio-UI til de nye tokens (PatternKind-dropdown, farver, glow-slider, meter-toggles), billede/logo-vælger.
- Ydeevne: kun ét `DropShadowEffect` i brug (FPS) — bevidst for at undgå FPS-tab i spil.
- Ikke committet/pushet.

### Rettelse (00:49) — dividerne er nu hvide og tydelige
- **Problem:** dividerne (`|`) blev tegnet med tekstfarven × `DividerOpacity` (0.35-0.45 i presets) → usynlige på camo/mørk baggrund, så monitor-felterne flød sammen.
- **Fix i `GamingOverlaySkin`:**
  - Nyt token `DividerColor` (default `#FFFFFF`), uafhængigt af `TextColor`.
  - `DividerBrush` bruger nu `DividerColor` i stedet for tekstfarven.
  - `DividerOpacity` default hævet 0.4 → **0.8**, og setter'en clamper til **min. 0.6** (læsbarheds-guard). Eksisterende gemte skins med 0.35-0.45 bliver dermed automatisk løftet til 0.6 ved indlæsning.
  - Presets opdateret: Tactical Olive 0.85, Cyber Cyan/Ember/Arctic 0.75 — alle med `DividerColor = "#FFFFFF"`.
- Bemærk: når transparens er aktiv, følger divideren stadig brugerens valgte tekstfarve (eksisterende trigger) — det er tilsigtet.
- **Deployet 2026-11-10 00:49** — `IconGrid.dll` 1.210.880 B i både build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Ikke committet/pushet.

### Session (00:55) — "MW4"-preset: 1:1-forsøg på Gemini/HUD-referencen
Bruger ville have designet 1:1 og et preset kaldet **MW4**.

**Nye tokens i `GamingOverlaySkin`:**
- Typografi: `FontSize`, `MonoFontSize` (+ `FontSizeValue`, `MonoFontSizeValue`, `BrandFontSizeValue` = 1.7×).
- Brand: `ShowBrand`, `BrandText` ("MW4"), `BrandColor` (+ `HasBrand`, `BrandBrush`).
- Callout-tag: `ShowCalloutTag`, `CalloutTopText` ("X-01"), `CalloutBottomText` ("BLACK SITE"), `CalloutAccentColor`.
- Metre: `CpuMeterBorderColor`, `GpuMeterBorderColor`.
- GPU-accentlinje: `ShowGpuAccentLine`, `GpuAccentLineColor` (den lilla streg i referencen).
- Afskårne hjørner: `ChamferSize` (+ `ChamferSizeValue`).
- `CopyFrom` opdateret med alle.

**Bar (`GamingOverlayBar.xaml` / `.xaml.cs`):**
- Venstre callout-tag: camo-baggrund, "X-01" + guld-prik, "BLACK SITE" i guld, højre accentkant.
- Brand-blok: "MW4" i grøn med glød, mørkegrøn baggrund og grøn bundkant.
- Ur-ikon (Segoe MDL2 `E823`) foran klokkeslættet.
- Metre har nu skin-styrede rammefarver (grøn CPU / lilla GPU).
- Lilla accentlinje efter GPU-chippen.
- **Chamfer-clip:** `ApplyChamfer()` bygger en `StreamGeometry` (top-højre + bund-venstre hjørne skåret) på `SizeChanged` og når `ChamferSize` ændres — WPF kan ikke lave CSS-`clip-path` i XAML.
- Fontstørrelser bindes nu fra skin (labels + FPS/mono).

**Nyt preset "MW4"** (i preset-listen): camo, chips, grøn glød (#00FF66, radius 10), segmenterede gradient-metre, guld-gradient knapper med `#C5A059`-kant, chamfer 14, `FontSize 14`, fontstakke `"Chakra Petch, Bahnschrift, Segoe UI"` og `"Share Tech Mono, Consolas"` (falder tilbage til Windows-fonte hvis Google-fontene ikke er installeret).

**Ikke 1:1 endnu:** knap-bevel (oktagon-klip), callout-taggens skrå højrekant, og de tre Google-fonte (OFL) er ikke bundlet — de skal downloades som `.ttf` af brugeren.

**Deployet 2026-11-10 00:55** — `IconGrid.dll` 1.219.584 B i både build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler). Ikke committet.

### Rettelse (01:09) — FT uden "ms" når der ikke er data
- `SystemMonitor.FrameTimeDisplay` (ny): `"--"` når `FrameTimeStatus` er tom/`--`, ellers `"{værdi}ms"`. `FrameTimeStatus`-setter'en notificerer også `FrameTimeDisplay`.
- `GamingOverlayBar.xaml`: FT viser nu `FrameTimeDisplay` i stedet for `FrameTimeStatus` + fast `ms`-run → **`FT: --`** uden data, **`FT: 7.0ms`** med data.
- Ønskes enheden fjernet helt (også ved tal), er det blot at binde `FrameTimeStatus` uden `ms`-run igen (én linje).
- **Deployet 2026-11-10 01:09** — `IconGrid.dll` 1.219.584 B i build-mappe og `C:\IconGrid` (build 0 fejl).

### Rettelse (01:17) — dividerne var sorte pga. Freeze-bug + nu farve/opacity i Skin Studio
- **Rodårsag (vigtig):** `GamingOverlaySkin.DividerBrush` kaldte `CreateBrush()` (som **fryser** brush'en) og satte derefter `Opacity`. Ændring af en frossen `Freezable` kaster `InvalidOperationException`, WPF-bindings fejlede tavst, og dividerne faldt tilbage til arvet (mørk) forgrund — derfor var de "sorte" uanset `DividerColor`/`DividerOpacity`.
- **Fix:** brush'en bygges nu lokalt (`new SolidColorBrush(color) { Opacity = _dividerOpacity }`) og **fryses først bagefter**. Fallback til `Brushes.White` ved parse-fejl.
- **Skin Studio:** nye kontroller i Farver-kortet:
  - **Divider-farve** (farvevælger, `Tag="DividerColor"` + swatch der viser `DividerBrush`)
  - **Divider-gennemsigtighed** slider (0.6-1.0, `DividerOpacity`)
- Lokalisering (en+da): `SkinStudioDividerColorLabel`, `SkinStudioDividerOpacityLabel` + properties + notify.
- **Deployet 2026-11-10 01:17** — `IconGrid.dll` 1.222.144 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Ikke committet/pushet.

### Rettelse (01:21-01:22) — dividerne er nu tegnede linjer (perfekt lodret centrering)
- **Problem:** dividerne var tekst-glyffen `|`, så deres optiske centrum afhang af font/baseline/size — de sad ikke på linje med tallene.
- **Fix:** `MonitorDividerStyle` (TextBlock) er erstattet af **`OverlayDividerStyle` (Border)** — en tegnet hvid streg (`Width`/`Height`/`Background` fra skin), `VerticalAlignment=Center`, `SnapsToDevicePixels=True`. Alle 4 divider-elementer konverteret (`TextBlock` → `Border`). Transparens-trigger bevaret (divider følger tekstfarven når transparens er aktiv).
- **Nye tokens:** `DividerWidth` (1-6 px, default 1) og `DividerHeight` (4-40 px, default 18) + `DividerWidthValue`/`DividerHeightValue`; med i `CopyFrom`.
- **Skin Studio:** ny slider **"Divider-højde"** (8-40) ved siden af divider-farve og -gennemsigtighed.
- Lokalisering (en+da): `SkinStudioDividerHeightLabel`.
- **Deployet 2026-11-10 01:22** — `IconGrid.dll` 1.223.680 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Ikke committet/pushet.

### Rettelse (01:25) — divider-højde sat ned til 12 px
- Brugeren bad ikke om at ændre højden (min default 18 px var for høj) → **default er nu 12 px**.
- **Migrering:** `FromJson` løfter/sænker kun præcis værdien 18 til 12 (den gamle default), så eksisterende gemte skins ikke skal rettes manuelt. Alle andre eksplicitte værdier bevares.
- MW4-presettet sætter eksplicit `DividerHeight = 12`.
- Slideren "Divider-højde" (8-40) er stadig tilgængelig for finjustering.
- **Deployet 2026-11-10 01:25** — `IconGrid.dll` 1.223.680 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).

### Tilføjelse (01:30) — "Vis gaming overlay"-knap på Skin Studio-siden
- Knappen ligger nu under live-preview'et i hero-kortet på Skin Studio (samme placering som på Gaming overlay-siden): titel + hjælpetekst + blå handlingsknap.
- Kalder `MainViewModel.RequestShowGamingOverlay()` — samme manuelle sti som på Gaming overlay-siden, dvs. **uden** at røre auto-vis/auto-luk-flowet.
- Lokalisering (en+da): `SkinStudioShowOverlayButton`, `SkinStudioShowOverlayIntro` + properties + notify i `MainViewModel.Localization.cs`.
- **Deployet 2026-11-10 01:30** — `IconGrid.dll` 1.225.728 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Ikke committet/pushet.

### Rettelse (01:33) — ur-ikonet foran klokkeslættet fjernet
- `GamingOverlayBar.xaml`: Segoe MDL2 clock-glyph (`&#xE823;`) fjernet; klokkeslættet står nu alene med samme margin som før.
- **Deployet 2026-11-10 01:33** — `IconGrid.dll` 1.225.216 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).

### Rettelse (01:36) — divider-bredde: pixel-snapning mod DPI-halvpixels
- **Observation fra bruger:** nogle dividere så bredere ud end andre.
- **Årsag:** alle dividere har `Width = 1` DIP, men ved 125 %/150 % skalering svarer 1 DIP til 1,25/1,5 fysiske pixel. Uden edge-snap kan en linje derfor blive tegnet som 1 px eller 2 px afhængigt af dens subpixel-position → visuelt forskellig bredde.
- **Fix i `OverlayDividerStyle`:** `RenderOptions.EdgeMode="Aliased"` + `UseLayoutRounding="True"` (ud over `SnapsToDevicePixels`), så kanterne snappes deterministisk til enhedspixels.
- Hvis det stadig varierer på en given DPI, er næste skridt at gøre bredden DPI-eksakt (1 enhedspixel = 1/`DpiScaleX` DIP) via kode i `GamingOverlayBar`; skin-token `DividerWidth` findes allerede.
- **Deployet 2026-11-10 01:36** — `IconGrid.dll` 1.225.216 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).

### Rettelse (01:40) — dividerbredde gjort DPI-eksakt (1 enhedspixel)
- `EdgeMode="Aliased"` var ikke nok: 1 DIP er ikke et helt antal enhedspixels ved 125 %/150 %, så identiske dividere kunne stadig blive 1 eller 2 px.
- **Ny metode `ApplyDividerThickness()` i `GamingOverlayBar`:** finder alle dividere (Border med `OverlayDividerStyle`) via visuelt træ og sætter `Width = round(DividerWidth) / DpiScaleX` → **præcis N enhedspixels** uanset skalering. Kaldes ved `Loaded`, `OnDpiChanged` og når `DividerWidth`-tokenet ændres.
- `using System.Collections.Generic` tilføjet; `EnumerateDividers` recurser visuelt træ og matcher style-instansen.
- **Deployet 2026-11-10 01:40** — `IconGrid.dll` 1.227.776 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Hvis der stadig opfattes forskel, er næste skridt at sætte `DividerWidth` til 2 (så 1 vs. 2 px-afrunding ikke kan ses); token/slider findes allerede.

## Session (2026-11-10, fortsat 3) — Modulært blueprint-system (fundament)

**Brugerønske:** byg Gemini's arkitektur (lag + blokke + skin-JSON) i vores WPF-løsning, 100 % copy af HTML-overlayet.

### Nyt (`Models/OverlaySkinBlueprint.cs`)
- `OverlaySkinBlueprint`: `SkinId`, `Name`, `Theme` (PrimaryColor/AccentColor/CamoImage/FontFamily), `Layers` (List<OverlayLayer>), `Layout` (List<OverlayBlock>) + `ToJson`/`FromJson`/`Clone` + `LayoutView` (synlige blokke).
- `OverlayLayer`: Type (solid/gradient/camo/pattern/image/scanlines), Enabled, Opacity, BlendMode, Color/Color2, Source, PatternKind.
- `OverlayBlock`: `Block` (type) + `Props` (Dictionary<string,string>) med convenience-accessors (`Id`, `Label`, `Text`, `Sub`, `Code`, `ColorHex`, `VramColor`, `Source`, `DataKey`, `Highlight`, `ShowDot`, `HasGrid`, `Width`, `IsVisible`).

### Defaults (`Helpers/Skin/OverlayBlueprintDefaults.cs`)
`CreateMw4()` = 1:1 blueprint af HTML-referencen: lag (solid #0B0D0F → camo 0.85 overlay → scanlines) og layout:
`tag(X-01/BLACK SITE) → logo(MW4) → clock → divider → ping(Net) → divider → gauge(cpu) → divider → gauge(gpu, vramIndicator) → divider → stat(gpuClock) → divider → stat(frameTime/FT) → divider → stat(fps, highlight) → controls(gold-octagon)`.

### Persistering + VM
- `ConfigModel.GamingOverlayBlueprintJson` → `MainViewModelConfigState`/`MainViewModelSettingsState`/`MainViewModelSettingsPersistence` → `ApplyConfig`/`ResetToDefaults`/`SaveSettingsToConfig`.
- `MainViewModel.Overlay.cs`: `GamingOverlayBlueprint`, `GamingOverlayBlueprintJson`, `ApplyGamingOverlayBlueprintJson(json)` (validerer, returnerer false ved fejl), `ResetGamingOverlayBlueprint()`.

### Skin Studio
- Nyt kort **"Blueprint (moduler og lag)"**: JSON-editor (Consolas, 220 px), **"Anvend blueprint"** (grøn/rød feedback + lokaliseret status) og **"Nulstil til MW4"**.
- 7 nye lokaliseringsnøgler (en+da) + properties + notify.

### Status (vigtigt)
- **Blueprintet gemmes, valideres og kan redigeres — men baren renderer stadig den faste XAML.** Næste skridt er rendereren: `ItemsControl` bundet til `LayoutView` med `DataTemplateSelector` (tag/logo/clock/ping/gauge/stat/divider/controls) + databinding til `SystemMonitor`, så JSON'en faktisk styrer rækkefølge/indhold.
- **Deployet 2026-11-10 01:57** — `IconGrid.dll` 1.242.112 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Ikke committet/pushet.

## Session (2026-11-13, fortsat 4) — Modul-renderer: "split i moduler og sæt sammen igen"

**Brugerønske:** divider skal stadig findes (som modul), og vi skal bruge den nuværende standard-overlay som test: skil den ad i moduler, sæt dem sammen igen og verificér at det ligner 1:1.

### Implementeret
- **`OverlayBlueprintDefaults.CreateClassic()`**: klassisk blueprint med præcis den hidtidige modulrækkefølge: `clock → divider → ping → divider → gauge(cpu) → divider → gauge(gpu) → stat(gpuClock) → divider → stat(frameTime) → stat(fps)`. Bruges nu som default/reset (i stedet for MW4, som stadig kan indsættes via JSON-editoren).
- **`OverlayBlockTemplateSelector`** (`Helpers/Skin/`): vælger DataTemplate pr. blok — `clock`, `ping`, `gauge` (cpu/gpu splittet), `stat` (gpuClock/frameTime/fps), `divider`.
- **`GamingOverlayBar.xaml`:** de ~148 linjer håndbyggede monitor-elementer er erstattet af ét `ItemsControl` bundet til `GamingOverlayLayoutBlocks` med `OverlayBlockTemplateSelector`. Templates har VM som DataContext (via `ItemsControl`-ancestor), så alle eksisterende skin-bindings virker uændret. Udseende (chips, metre, FT/FPS-bredder, divider-style) er genbrugt 1:1.
- **`MainViewModel.Overlay.cs`:** `GamingOverlayLayoutBlocks` = blueprint-rækkefølge filtreret af de eksisterende "Overlay-indhold"-kontakter (clock/net/cpu/gpu/gpu-ur/ft/fps) + oprydning af løse dividere (ingen dobbelt/leading/trailing). Der notificeres fra alle relevante sættere og ved blueprint-apply/reset.
- `divider` er nu **et modul** som alle andre (kan fjernes/flyttes/erstattes i JSON).

### Verifikation
- Build 0 fejl / 0 advarsler.
- **Deployet 2026-11-13 00:16** — `IconGrid.dll` 1.246.208 B i build-mappe og `C:\IconGrid`.
- **Mangler bruger-test:** overlayet skal se identisk ud (rækkefølge, chips, dividere, FT/FPS-bredder) og "Overlay-indhold"-kontakterne skal stadig slå moduler til/fra.

### Næste skridt
- Canvas-editoren (fase 1): 3-panels UI, guide-rammer, drag/drop af moduler, lag-panel, baggrundsform. Rendereren er nu på plads, så canvas'et redigerer noget der faktisk virker.
- Ikke committet/pushet.

### Rettelse (00:19) — "IconGrid.Models.OverlayBlock" i overlayet + manglende dividere
- **Symptom (brugerbillede):** overlay/preview viste teksten `IconGrid.Models.OverlayBlock` i stedet for Ur/FPS, og dividerne var væk.
- **Årsager:**
  1. Brugerens gemte blueprint var stadig **MW4**-varianten med `tag`, `logo`, `controls` — bloktyper som rendereren endnu ikke har templates til → WPF faldt tilbage til `ToString()`.
  2. `OverlayDividerBlockTemplate` satte ikke DataContext til VM, så `OverlayDividerStyle`'s bindings (`DividerHeightValue`, `DividerBrush`) fejlede → bredde/baggrund 0 → usynlige dividere.
- **Fix:**
  - Divider-template binder nu `DataContext` til ViewModel (via `ItemsControl`-ancestor) som de øvrige templates.
  - `GamingOverlayLayoutBlocks` filtrerer nu **kun understøttede bloktyper** (clock, ping, gauge, stat, divider) — ukendte skippes i stedet for at blive vist som rå objekter.
  - **Migrering:** gemte blueprints med `skinId = cod_mw4_tactical_v1` indlæses som **Classic** (indtil tag/logo/controls får templates i canvas-fasen).
- **Deployet 2026-11-13 00:19** — `IconGrid.dll` 1.246.208 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler).
- Mangler bruger-verifikation: overlay + preview skal nu vise Ur/Net/CPU/GPU/ur/FT/FPS med hvide dividere — ingen `IconGrid.Models...`-tekst.

### Fase 1 (del 1) — Modul-bygger i Skin Studio (00:22)
- **VM-operationer** (`MainViewModel.Overlay.cs`): `AddGamingOverlayModule(type, id?)`, `RemoveGamingOverlayModule(block)`, `MoveGamingOverlayModule(block, delta)`, `ToggleGamingOverlayModuleHidden(block)` + `CommitBlueprintLayout(...)` der kopierer layoutet til en **ny liste** (så `ItemsControl` refresher), notificerer `GamingOverlayBlueprint`/`...Json`/`GamingOverlayLayoutBlocks` og gemmer.
  - Nye moduler får fornuftige defaults (ping→label Net+dot, gauge→label CPU/GPU+grid, stat→data fps/frameTime/gpuClock).
- **`OverlayBlock.DisplayName`**: menneskelæsbare navne til modul-listen (Clock, Divider, Net, CPU, GPU, GPU clock, FT, FPS).
- **Skin Studio — nyt kort "Modul-bygger"**: bibliotek med 8 tilføj-knapper (Clock, Divider, Ping, CPU, GPU, GPU clock, FT, FPS) + liste over blueprintets moduler med **▲ ▼ ◉ ✕** pr. række. Alt opdaterer preview/overlay live og spejles i JSON-editoren.
- Lokalisering: `SkinStudioModulesTitle`, `SkinStudioModulesIntro` (en+da).
- **Deployet 2026-11-13 00:22** — `IconGrid.dll` 1.253.376 B i build-mappe og `C:\IconGrid`.

### Næste (fase 1, del 2 — canvas)
- 3-panels layout i Skin Studio (værktøjer | canvas med guides/grid | lag + egenskaber).
- Drag/drop af moduler ind på canvas + snapping; lag-panel med lås/synlighed.
- Templates for `tag`, `logo`, `controls` så MW4-blueprintet kan bruges igen.
- Baggrundsform pr. hjørne (radius/chamfer/polygon) — genbrug af chamfer-geometrien.
- Ikke committet/pushet.

### Omskrivning (00:28) — Skin Studio er nu en 3-panels canvas-editor (gammel UI fjernet)
- **Brugerfeedback:** den gamle kort-baserede side (presets/farvekort/JSON-editor) var forkert; det skal være som Gemini-illustrationen: sidebar i **begge** sider af et canvas. Gamle skins/presets fjernet fra UI'en.
- **`SkinStudioPage.xaml` skrevet helt om** til et 3-kolonne layout:
  - **Venstre (230 px):** "Værktøjer" (Move, Node edit, Chamfer/radius, Polygon, Text, Weld, Reset) + **Modul-bibliotek** (Clock, Divider, Ping, CPU, GPU, GPU clock, FT, FPS → klik tilføjer).
  - **Midten:** canvas med **20 px grid-baggrund** (DrawingBrush), **guide-rammer** (Standard Horizontal 900×48 + Vertical 48×400, magenta stiplede), og **den ægte overlay-kontrol** (`GamingOverlayBar`, IsPreview) placeret oven på guiden → live 1:1.
  - **Højre (300 px):** **Lag**-liste (blueprintets moduler med ▲ ▼ ◉ ✕ pr. række), **Egenskaber** for valgt modul (Label + Colour hex, gemmes on lost focus) og **Nulstil til classic**.
- **Fjernet fra UI:** preset-galleri, farvekort, font-kort, billede-import, JSON-editor og "Vis gaming overlay"-knappen på Skin Studio (den findes fortsat på Gaming overlay-siden).
- **VM:** ny `SetGamingOverlayModuleProperty(block, key, value)` til egenskabs-panelet.
- Lokalisering: `SkinStudioToolsTitle`, `SkinStudioModuleLibraryTitle`, `SkinStudioLayersTitle`, `SkinStudioPropertiesTitle`, `SkinStudioCanvasReset` (en+da).
- **Deployet 2026-11-13 00:28** — `IconGrid.dll` 1.245.696 B i build-mappe og `C:\IconGrid`.

### Næste
- Drag/drop + markering af moduler direkte på canvas (selector-håndtag), snapping til guiden.
- Egenskaber udvides pr. modultype (farve, meter-type, bredde).
- Templates for `tag`, `logo`, `controls`; baggrundsform pr. hjørne (radius/chamfer/polygon).
- Ikke committet/pushet.

### Lag-panel omskrevet til Photoshop-stil (14-11 16:54)
**Brugerbillede:** nuværende lag-liste var for "primitiv"; ønsket er Photoshop-layout (blend/opacity-header, ikonrække, øje-kolonne, omvendt rækkefølge, cyan markering, bund-toolbar).

**Implementeret (`SkinStudioPage.xaml`, højre kolonne):**
- Header: **Layers** + ☰-ikon.
- **Blend**-dropdown ("Normal", disabled — WPF har ingen blend modes) + **Opacity**-slider + %-label.
- **Layer:**-ikonrække (✎ paint, ✒ path, ＋ add divider, 🔒 lock).
- **Lag-liste i omvendt rækkefølge** (øverste lag øverst, som Photoshop) via ny VM-property `GamingOverlayLayerItems`; samme `OverlayBlock`-objekter, så redigering rammer blueprintet.
  - Øje-knap pr. række (dæmpet når skjult via `IsHidden`-trigger), navn, ☰-håndtag.
  - Egen `ListBoxItem`-template: hover #1F2429, **selected #123A5A med #2FA8D8-kant**.
- **Bund-toolbar**: 📁 gruppe, ＋ tilføj modul (divider), 🗑 slet valgt.
- **Properties** under: valgt modul, Label, Colour (hex), Reset til classic.

**Ny funktionalitet:**
- **Per-modul opacity virker nu i selve overlayet**: `OverlayBlock.OpacityValue` (props["opacity"]) + `ItemsControl.ItemContainerStyle` binder `Opacity` på hver container. Opacity-slideren skriver til det valgte modul og gemmes.
- `SetGamingOverlayModuleOpacity(block, 0..1)` i VM; ▲/▼ i panelet er vendt, så "op" = senere i rækken.

**Deployet 2026-11-14 16:54** — `IconGrid.dll` 1.249.792 B i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler). Ikke committet.
 
### Lag-panel UI-fixes (17:00) 

- Blend-dropdown: lys baggrund + SORT tekst (#111) via IsHitTestVisible=False (IsEnabled=False gav graa tekst).
- Opacity: 5-kolonne grid (blend | luft | label | slider 90px | pct), label og slider overlapper ikke laengere; pct opdateres live.
- Layer:-label, ikoner og lagnavne sat til lys tekst (#C9D1D9 / #E6EDF3) + HorizontalContentAlignment=Stretch.
- Deployet 17:00, DLL 1.250.304 B (identisk i build-mappe og C:\IconGrid).

### Commit + push (17:10)
- Commit `77ef584` � "Skin Studio: blueprint-driven overlay + Photoshop-style layer panel" (17 filer, +1329/-625).
- Pushet: `16dd1a5..77ef584 main -> main` til github.com/Maxithx/IconGrid. Working tree clean.
- `gaming-overlay-html-test/` tilfoejet .gitignore og er IKKE i repoet (COD-branding).
- Naeste: klik-selektion paa canvas, flyt/snap med haandtag, egenskaber pr. modultype, tag/logo/controls-templates, baggrundsform.

### Gauge-udseende + kode-snippets pr. modul (17:21)

**Formaal:** brugeren skal kunne aendre udseendet af CPU/GPU usage-modulerne og indsaette kode-snippets pr. modul (niveau A = JSON prop-patch, valgt efter aftale).

#### Gauge-moduler (CPU/GPU) faar props
- `meter`: bar | ring | segments | text. 4 nye DataTemplates i `GamingOverlayBar.xaml` (�n pr. stil, CPU+GPU i samme template via IsGpu-grupper).
- `meterColor` (falder tilbage til legacy `color` -> skin-brush), `meterWidth`, `meterHeight` (default afhaenger af stil: bar 12x14, segments 56x10, ring 16).
- `showTemp`, `showPercent`, `hasGrid` styrer temp-tekst, procent-tekst og segmentlinjer.
- `OverlayBlock`: nye accessors MeterStyle/IsGpu/DisplayLabel/ShowTemp/ShowPercent/MeterWidthValue/MeterHeightValue/MeterColorHex/MeterBrush/HasMeterColor.
- Nye helpers: `PercentToLengthConverter` (IValueConverter + IMultiValueConverter), `PercentToArcGeometryConverter` (ring-arc), `OverlayBlockBrushes` (hex->frozen brush med cache). `PercentToHeightConverter` er nu en tom subklasse (bruges af MainWindow).
- Validering: Freeze kun efter alle properties er sat (jf. tidligere brush-bug).

#### Kode-snippets (niveau A)
- `OverlayBlock.Snippet` (JSON, NullValueHandling.Ignore) gemmer senest anvendte snippet.
- `OverlaySnippetLibrary` med 10 faerdige snippets (gauge/ping/stat/any) + `ForBlock()` filtrering.
- VM: `TryApplyGamingOverlaySnippet(block, json, out error)` (JObject -> props) og `ResetGamingOverlaySnippet(block)` (fjerner praecis de noegler snippettet satte).
- UI i hoejre panel under Properties: meter-type, maaler-farve, bredde x hoejde, 3 checkboxes + snippet-bibliotek, editor (Consolas), Anvend/Nulstil + statuslinje.
- `KeepSelection()` genvaelger modulet efter commit (lag-listen bliver en ny liste, ellers mistede panelet sin selection ved hver aendring).
- 18 nye lokaliseringsnoegler (en+da).

**Deployet 2026-11-14 17:21** - `IconGrid.dll` 1.288.704 B identisk i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler). Ikke committet.

**Naeste:** niveau B (fri XAML DataTemplate pr. modul via XamlReader + fallback), klik-selektion paa canvas, drag/snap.

### Lag-panel navne (17:32)
- `OverlayBlock.DisplayName` (kun panel-tekst): gauge -> "CPU (Temp)" / "GPU (Temp)", ping -> "Net (Ping)" (eller "{label} (Ping)").
- Overlayet bruger fortsat `DisplayLabel`, saa suffikserne vises IKKE i gaming overlayet.
- Deployet 17:32, DLL 1.289.216 B (identisk begge steder).

### Lag-panel navne v2 (17:37)
- Gauge hedder nu "CPU (Usage + Temp)" / "GPU (Usage + Temp)" (kun "(Usage)" hvis showTemp=false).
- Ny `OverlayBlock.GaugeDisplayName`; overlayet bruger fortsat `DisplayLabel` (CPU/GPU).

### Separate usage/temp moduler (17:52)
- Nye bloktyper: `usage` (maaler + valgfri %) og `temp` (kun temperatur-chip). `gauge` bevares som kombineret (Usage + Temp) for bagudkompatibilitet.
- `OverlayBlock`: IsGpu daekker gauge/usage/temp, ny IsMeterModule + CpuGpuLabel; ShowTemp er fast false for usage og true for temp. DisplayName: "CPU (Usage)", "CPU (Temp)", "CPU (Usage + Temp)".
- Selector: "gauge" og "usage" deler de 4 maaler-templates; "temp" har ny `OverlayTempBlockTemplate` (CPU/GPU-grupper).
- Modul-bibliotek: CPU usage / GPU usage / CPU temp / GPU temp knapper.
- Maaler-egenskaber (meter-type, farve, stoerrelse, %, grid) vises ogsaa for usage-moduler via IsMeterModule.
- Snippet-bibliotek: +2 usage-snippets og 1 temp-snippet.
- Deployet 17:52, DLL 1.293.312 B (identisk begge steder), build 0/0.

### Dropdown-fix i Skin Studio (17:58)
- Ny `StudioComboBoxStyle` (lys baggrund #E9ECEF + sort tekst #111111 + ComboBoxItem-stil) brugt af Blend (Normal), Meter style og Snippet-bibliotek. Popup-listen var hvid med lys tekst = ulaeselig.

### Overlay-knapper som stylebare lag (18:10)
- `OverlaySkinBlueprint.Chrome` (List<OverlayBlock>) med blokkene "settings" og "close"; EnsureChrome() opretter dem for gamle blueprints (FromJson).
- Modul-bibliotekets knapper ligger fast i hoejre side af baren, men kan nu styles: `icon` (Segoe MDL2-glyph, tom = default), `buttonWidth`/`buttonHeight` (default 36x32 / 44x32), `color` (glyph-farve) og opacity.
- `OverlayBlock`: IsChromeButton, ButtonGlyph, ButtonWidthValue, ButtonHeightValue, ColorBrush, HasColor; DisplayName = "Settings button"/"Close button".
- Baren: knapperne binder Width/Height/Opacity/glyph (+ farve-trigger) til `GamingOverlaySettingsButton` / `GamingOverlayCloseButton` fra VM. Klik/menu-adfaerd er uaendret.
- Skin Studio: begge vises nederst i lag-listen (oeje-knappen skjult, sletning blokeret), og Properties faar en "Overlay button"-sektion med ikon + stoerrelse.
- 3 nye lokaliseringsnoegler (en+da).
- Deployet 18:05, DLL 1.299.456 B (identisk begge steder), build 0/0.

- 18:12: `ApplyPreviewState` viser nu knapperne i Skin Studio-canvasen (IsHitTestVisible=False i preview), saa de kan designes. DLL 1.299.968 B, deployet 18:06 (identisk begge steder).

### Canvas-guide + knap-clip fix (18:20)
- Gear-knappen havde `Margin="0,0,-4,0"` -> X-knappen floej 4 px ud over OverlayRoots CornerRadius-clip. Nu `0,0,4,0` og hoejre gruppe-margin 6 -> begge knapper er helt inde.
- Skin Studio-guiden var hardcoded 900x48 mens overlay-baren er indholdsstyret (~630x44). Guiden (baade label og ramme) binder nu til `CanvasOverlay.ActualWidth/ActualHeight`, saa skabelon og overlay altid matcher.

### Preview-klipping af X-knap (18:30)
- Aarsag: naar baren hostes med uendelig bredde (Canvas/Viewbox) bliver den indholdsmaalt og rapporterer et par px for lidt -> X-knappen floej ud over barens hoejre kant og blev klippet. I GamingOverlayWindow (fast 720x44) var alt korrekt.
- Fix: begge previews faar nu samme faste ramme som vinduet - Skin Studio: `Width=720 Height={GamingOverlayBackgroundHeight}`; Gaming-overlay-siden: `Border Width=720` med hoejde bundet til samme property, inde i Viewbox.
- Guiden i Skin Studio binder fortsat til ActualWidth/ActualHeight, saa den viser 720 x 44 (den reelle ramme).
- Deployet 18:30, build 0/0.

### Preview = 1:1 med gaming overlay (18:47) - LOEST
**Symptom:** gear/X blev "skaaret" i Gaming overlay-sidens preview og i Skin Studio-canvasen, men saa korrekt ud paa skrivebordet.

**Maaling (midlertidig log i `GamingOverlayBar`):** `bar actual=909x27, root actual=673x20, close 44x32, settings 36x32, dpi scale=1`.

**Aarsag:** baren er kun 20 DIP hoej, men knapperne var 32 DIP hoeje i preview. `GamingOverlayWindow` klamper `ActionButtonHeight = Math.Min(32, GamingOverlayBackgroundHeight)`; preview sprang den over -> knapperne floej ud over barens top/bund og blev skaaret.

**Fix (3 dele):**
1. `ApplyPreviewState()` anvender nu samme knap-hoejde-klamp som vinduet (~20 DIP i stedet for 32).
2. Preview skalerer som vinduet: `OverlayRoot.LayoutTransform = ScaleTransform(GamingOverlayUiScale)` + reagerer paa aendringer i UiScale/BackgroundHeight (ny `ViewModel_PropertyChanged`).
3. Begge hosts er auto-sized igen (fast 720-ramme og Viewbox fjernet) -> praecis samme maaling som vinduet, 1:1 i stoerrelse.
- Guide i Skin Studio binder til ActualWidth/ActualHeight -> viser fx 909 x 27.
- 12 px luft mellem gear og X (positiv margin; den gamle -4 margin skubbede X ud over kanten).
- Midlertidig maale-log fjernet igen.

**Deployet 2026-11-14 18:47** - `IconGrid.dll` 1.300.480 B identisk i build-mappe og `C:\IconGrid` (build 0 fejl/0 advarsler). Ikke committet.

### Niveau B: fri XAML-template pr. modul (18:55)
- `OverlayBlock.Template` (JSON "template", NullValueHandling.Ignore) gemmer modulets egen XAML.
- `Helpers/Skin/OverlaySnippetTemplateFactory`: wrapper markup i `<DataTemplate>` (eller accepterer en fuld DataTemplate og injicerer xmlns), `XamlReader.Parse`, cache pr. streng, max 6000 tegn, blokerer x:Class/x:Code/ObjectDataProvider/Assembly=/EventSetter/Process.Start. Returnerer fejlnoegler: empty / too-long / blocked / invalid.
- `OverlayBlockTemplateSelector` returnerer den brugerdefinerede template naar den kan bygges; ellers falder den tilbage til standard-templaten (daarlig markup kan ikke braekke overlayet).
- VM: `TryApplyGamingOverlayTemplate(block, xaml, out error)` + `ResetGamingOverlayTemplate(block)`.
- Skin Studio: ny Expander "Custom template (advanced)" nederst i Properties med editor, Inds�t eksempel / Anvend / Nulstil + statuslinje.
- 6 nye lokaliseringsnoegler (en+da).
- DataContext i templates = OverlayBlock; telemetri/skin naas via `RelativeSource AncestorType=ItemsControl`.
- Deployet 18:52, DLL 1.310.720 B (identisk begge steder), build 0/0.

### Niveau B+ : modulets egen kode i editoren (20:20)
- Ny `Helpers/Skin/OverlayTemplateCatalog.GetStarter(block)`: returnerer startkode der matcher modulets data - gauge/usage (bar+ProgressBar eller text), temp, clock, ping, divider, stat (fps/ft/gpuClock). Skin-farver og telemetri naas via bindings (ingen StaticResource/convertere, da templaten parses for den er i traeet).
- Skin Studio: naar et modul vaelges uden egen template, indlaeses startkoden automatisk + status "Standardkoden ... er indlaest". Knappen "Indlaes modulets kode" genindlaeser den.
- Verificeret med en STA-test (uden for repoet): alle 11 startkoder parser OK via XamlReader.
- Deployet 20:20, DLL 1.316.864 B (identisk begge steder), build 0/0.

### Traekbar hoejre-panel i Skin Studio (20:30)
- Nyt 3. kolonne-grid: 230 (vaerktoejer) | * (canvas, min 320) | 5 px GridSplitter | 340 (lag+egenskaber, min 280 / max 720).
- `GridSplitter` (ResizeBehavior=PreviousAndNext, ShowsPreview, SizeWE-cursor) -> brugeren kan traekke panelet bredere/smallere.
- Knap-raekker i Snippet- og Template-sektionerne er nu WrapPanel med marginer, saa de ombrydes i stedet for at blive klippet ved smal bredde.
- Deployet 20:27, DLL 1.316.864 B (identisk begge steder), build 0/0.

### Close-knap bredde = 36 px som standard (21:39)
- `OverlayBlock.ButtonWidthValue` default er nu 36 for BAADE settings og close (foer 44 for close). Props `buttonWidth` overskriver stadig pr. knap.
- Deployet 21:39, DLL 1.316.864 B (identisk begge steder), build 0/0.

### Knap-kant kan fjernes (21:50)
- Nye chrome-props pr. knap: `borderThickness` (default 1, 0 = ingen ramme) og `borderColor` (hex, ellers skin). `OverlayBlock` har ButtonBorderThicknessValue/ButtonBorderThickness/HasBorderColor/BorderColorBrush; `GetDoubleOr` tillader 0 (i modsaetning til GetPositive).
- Baren: knapperne har nu inline Style BasedOn den gamle knap-stil, som saetter BorderThickness fra prop og BorderBrush via DataTrigger naar borderColor er sat (TemplateBinding i templaten goer resten).
- Skin Studio > Overlay button: nye felter "Kant (px, 0 = ingen ramme)" og "Kant-farve (hex)".
- Deployet 21:43, DLL 1.319.936 B (identisk begge steder), build 0/0.

### Knap-baggrund + overlay-baggrund som lag (22:05)
- Chrome-props: `background` (hex eller "transparent") -> ButtonBackgroundBrush/HasButtonBackground; baren saetter Background via DataTrigger i knappens inline Style.
- Nyt chrome-lag `background` ("Overlay background") oprettes foerst i Chrome af EnsureChrome; IsChromeButton daekker nu background/settings/close, ny IsBackgroundLayer + IsButtonLayer.
- Valg af baggrundslaget viser "Overlay background"-sektion (Bar-farve -> skin.BarBackgroundColor, Hjoerneradius -> skin.CornerRadius) og skjuler den generiske Label/Colour-boks; aendringer gemmes via SaveGamingOverlaySkin().
- Buttons-sektionen har nu ogsaa feltet Baggrund (hex eller transparent).
- Deployet 21:51, DLL 1.324.544 B (identisk begge steder), build 0/0.

### Lag-raekkefoelge: knapper oeverst, baggrund nederst (22:03)
- `GamingOverlayLayerItems` bygger nu listen som: Settings button, Close button -> moduler i omvendt raekkefoelge -> Overlay background. Matcher lag-stakken (knapper ligger over raekken, baggrunden under).

### Show/hide (oeje) paa alle chrome-lag (22:06)
- Oejet er tilbage paa Settings button, Close button og Overlay background (DataTrigger for IsChromeButton fjernet).
- Knapperne: `ApplyPreviewState()` saetter Visibility ud fra `GamingOverlaySettingsButton.IsVisible` / `GamingOverlayCloseButton.IsVisible`, og `ViewModel_PropertyChanged` genanvender den naar de properties (eller LayerItems) aendres -> skjul virker live i baade overlay og preview.
- Baggrunden: `OverlayRoot`-stilen har ny DataTrigger paa `GamingOverlayBackgroundLayer.IsHidden` -> Background=Transparent (kun barens flade skjules, modulerne bliver).
- Sletning er stadig blokeret for chrome-lag; skjulte knapper kan altid vises igen via oejet i panelet.
- Deployet 22:06, DLL 1.325.568 B (identisk begge steder), build 0/0.

### Flytbar vaerktoejsbjaelke (22:53)
- Tools er flyttet ud af venstre panel (nu kun Modul-bibliotek) og ind som en lodret, flydende rail oven paa canvaset.
- Ny ToolRailButtonStyle: 44x42 knapper, FontSize 20, hover/pressed-highlight - ikonerne er markant stoerre end de gamle.
- Rail (56 px bred med grip) ligger i en Canvas over center-grid; grip = Thumb med DragDelta (flyt), dobbeltklik nulstiller til 12,86. Positionen klemmes til canvas-omraadet.
- Grupper med separators: Move/Node | Chamfer/Polygon | Text/Weld | Reset.
- Deployet 22:53, DLL 1.328.128 B (identisk begge steder), build 0/0.

### Toolbar kan vises horisontalt (22:56)
- Ny orienterings-knap i railens header (? / ?). Klik skifter mellem lodret (56 px bred) og vandret (auto bredde) layout.
- `ApplyToolRailOrientation()` saetter ToolRail.Width/Padding, ToolRailPanel.Orientation og omformer separatorerne (1x24 lodret-justeret i vandret mode, 1 px hoejde i lodret mode), og klemmer railen ind i canvas-omraadet bagefter.
- Grippet er nu 26x22 i en header-raekke sammen med orienterings-knappen.
- Deployet 22:56, DLL 1.329.664 B (identisk begge steder), build 0/0.

### Startside-dropdowns ved sprogskift (23:07)
- Launcher visibility, Auto-hide delay, Peek activation og Language brugte default WPF ComboBox uden bredde -> boksen krympede til den valgte teksts bredde (og arvede systemets lyse/hvide stil), saa teksten blev klippet efter sprogskift.
- Alle fire bruger nu den delte `TemplateComboBoxStyle` (220 px, 32 h, lys flade + moerk tekst + ComboBoxItem-stil). Launcher visibility har Width=280, Peek activation Width=300 (lange tekster), Auto-hide delay og Language bruger 220.
- Deployet 23:07, DLL 1.330.176 B (identisk begge steder), build 0/0.

### Sprogskift toemte dropdown-valget (23:09)
- Naar ItemsSource blev udskiftet ved sprogskift, clearede ComboBox sin selection og den synkrone PropertyChanged-gennotifikation kom for tidligt -> boksen stod tom.
- `RefreshLocalizedText()` gennotificerer nu SelectedLauncherHideMode/SelectedAutoHideDelay/SelectedPeekActivationMode via `Dispatcher.BeginInvoke(DispatcherPriority.Background)`, saa bindingen re-vaelger item efter ItemsSource-reset.
- Deployet 23:09, build 0/0.

### Sprogskift: dropdown beholder valget (23:14) - DEFINITIV
- Rod-aarsag: ved sprogskift blev ItemsSource UDSKIFTET (ny List), og WPF-clearede derfor selection -> tom boks. Dispatcher-fix (23:09) var ikke nok.
- Ny `StartsidePage.LauncherOption` (Key + Display, INotifyPropertyChanged). De tre lister er nu `ObservableCollection<LauncherOption>` der oprettes �N gang (noegler faste: 0/1/2, 1/2/3/5/10, 0/1).
- Sprogskift kalder `UpdateOptions(...)` som kun skriver nye Display-tekster -> ingen ItemsSource-reset, selection bevares, teksten skifter sprog live.
- XAML: DisplayMemberPath="Value" -> "Display" paa de tre dropdowns; SelectedValuePath="Key" uaendret.
- Verificeret at `LauncherOption` findes i den deployede DLL.
- Deployet 23:14, build 0/0.

### Overlay-baggrundens firkantede hjoerner (01:38)
- WPF Border klipper IKKE boern til CornerRadius, saa de tre baggrundslag (BackgroundImage/Tint/Pattern, ColumnSpan=2) tegnede firkantede hjoerner bag den runde bar.
- `ApplyChamfer()` klipper nu OverlayRoot til en RectangleGeometry med CornerRadius naar chamfer = 0 (chamfer-stien uaendret); null-klip kun naar baade chamfer og radius er 0.
- `Skin_PropertyChanged` reagerer nu ogsaa paa CornerRadius -> klippet foelger med live.
- Deployet 01:38, DLL 1.330.688 B (identisk begge steder), build 0/0.

### Mindre runde hjoerner som standard (01:42)
- `GamingOverlaySkin._cornerRadius` default 8 -> 4.
- Migration i `GamingOverlaySkin.FromJson`: hvis en gemt skin staar paa praecis 8 (den gamle default, som gav for runde sider paa den 44 px hoje bar) saettes den til 4; andre vaerdier roeres ikke (samme moenster som divider-height 18->12).
- Radius kan stadig aendres i Skin Studio > Overlay background > Corner radius.
- Deployet 01:42, DLL 1.330.688 B (identisk begge steder), build 0/0.

### Hjoerner: aarsag fundet - gemt skin havde 18 (01:45)
- Brugerens config (AppData\Roaming\IconGrid\config.json) havde `"CornerRadius": 18.0` i GamingOverlaySkinJson, saa migreringen 8->4 ramte ikke.
- Migrationen daekker nu BEGGE legacy-defaults (8 og 18) -> 4; andre vaerdier roeres ikke.
- Deployet 01:45, DLL 1.330.688 B (identisk begge steder), build 0/0.

### Ens hover-effekt paa action-knapper (01:50)
- `TemplateActionButtonStyle` (TemplateGuidelines.xaml) havde ingen Template -> WPF standard-hover saa grim ud (Startside: "Aabn indstillinger for gaming overlay").
- Stilen har nu samme ControlTemplate som OverlayActionButtonStyle ("Vis gaming overlay"): Background via TemplateBinding, CornerRadius 4, hover #FF3E3E3E, pressed #FF2B2D31, disabled opacity 0.55, Cursor=Hand.
- Virker for alle sider der bruger TemplateActionButtonStyle.
- Deployet 01:50, DLL 1.331.712 B (identisk begge steder), build 0/0.

### Opdel Usage + Temp i to lag (01:57)
- Ny VM-metode `SplitGamingOverlayGaugeModules()`: hver `gauge`-blok bliver til `usage` + `temp` (temp faar kopi af props/id/label, usage beholder maaler-props og mister showTemp). Returnerer antal opdelte.
- Knap i venstre panel under modul-biblioteket: "Opdel brug + temp i to lag" (SkinStudioSplitGauges).
- Lag-listen afspejler data 1:1 - derfor opdeles modulet frem for kun visningen.
- Deployet 01:57, DLL 1.332.736 B (identisk begge steder), build 0/0.

### Opdel/Saml-knap blev en toggle (02:01)
- Knappen skifter nu tekst efter layoutet: "Opdel brug + temp i to lag" naar der findes `gauge`-moduler, ellers "Saml brug + temp til et lag" (SkinStudioGaugeLayoutActionText).
- Ny `MergeGamingOverlayGaugeModules()`: finder `usage` + `temp` med samme id, merger props (temp-identitet + maaler-indstillinger, showTemp=true) tilbage til `gauge` og fjerner temp-blokken.
- Nye VM-properties: GamingOverlayHasCombinedGauges, GamingOverlayHasMergeableGauges (+ notifikation i CommitBlueprintLayout).
- Handleren hedder nu ToggleGauges_Click og vaelger split/merge ud fra tilstanden.
- Deployet 02:01, DLL 1.335.296 B (identisk begge steder), build 0/0.

### Laggrupper i Skin Studio (02:15)
- Model: `OverlayLayerGroup { Id, Name }` + `OverlaySkinBlueprint.Groups` med `EnsureGroups()` (mindst �n gruppe, default "Gaming overlay"). FromJson sikrer gruppen for gamle blueprints, saa alle eksisterende moduler lander i den.
- `OverlayBlock`: GroupId (props["group"], tom = foerste gruppe), IsGroupLayer, GroupName; DisplayName for "group" = gruppens navn.
- VM: `GamingOverlayLayerItems` viser nu knapper -> [grupp header + gruppens moduler (omvendt)] -> baggrund. Headere er stabile pseudo-blocks (cache pr. gruppe-id). Nye metoder: AddGamingOverlayGroup(), RenameGamingOverlayGroup(), SetGamingOverlayModuleGroup(); GamingOverlayGroups eksponeres.
- UI: gruppe-raekker er fede med skjult oeje; ??-knappen nederst opretter en tom gruppe; Properties viser "Laggruppe > Gruppenavn" for en header og "Flyt til gruppe"-combo for et modul. Sletning er blokeret for gruppe-headere.
- 3 nye lokaliseringsnoegler (en+da).
- Deployet 02:15, DLL 1.341.952 B (identisk begge steder), build 0/0.

### Slet en laggruppe (02:18)
- Ny `RemoveGamingOverlayGroup(groupId)`: modulerne i gruppen flyttes til den foerste tilbagevaerende gruppe (intet lag gaar tabt), og den sidste gruppe kan ikke slettes.
- ??-knappen sletter nu gruppen naar en gruppe-raekke er valgt (ellers som foer modulet). Tooltip: "Delete layer or group".

### Gruppe-sletning fix + knapper til venstre (02:23)
- BUG: gruppe-headerens props brugte noeglen "groupId", men `OverlayBlock.GroupId` laeser "group" -> sletning/omdoeb fik tom id. Headeren skriver nu "group" (samme noegle som moduler).
- De tre bundknapper (Make group / Add module / Delete) er flyttet fra hoejre til VENSTRE side af lag-panelet.

### Gruppe = mappe: ingen snippet/template (02:27)
- `SnippetSection` og `TemplateSection` har faaet navne og skjules naar en gruppe-raekke er valgt (LoadGroupPanel); de vises igen for moduler/chrome.
- En gruppe viser nu kun "Laggruppe > Gruppenavn".
- Deployet 02:27, DLL 1.343.488 B (identisk begge steder), build 0/0.

### Alle lag inde i gruppen (02:29)
- `GamingOverlayLayerItems` laegger nu OGSAA chrome-raekkerne ind i den foerste gruppe: Settings button + Close button i toppen, Overlay background i bunden. Ingen raekker ligger uden for en mappe.
- Moduler uden `group`-prop hoerer (som foer) til foerste gruppe, saa eksisterende layouts er uaendrede i data.

### Gruppe-hierarki: mappe-ikon, indrykning, oeje, sammenklap (02:37)
- Gruppe-raekker: ??-ikon + fedt navn, indrykkede modulnavne (18 px) og et chevron (v/?) i stedet for drag-haandtag. Chevron klik = fold gruppen sammen/ud (huskes i sessionen).
- Gruppens OEJE skjuler alle moduler i gruppen: `OverlayLayerGroup.IsHidden` filtreres i GamingOverlayLayoutBlocks; raekkerne daempes via transient `OverlayBlock.IsGroupHidden` (ikke serialiseret).
- Chrome (Settings/Close/Overlay background) hoerer til foerste gruppe: knapperne skjules i baren via `GamingOverlayFirstGroupHidden`, baggrunden faar Transparent-trigger.
- Deployet 02:37, DLL 1.346.560 B (identisk begge steder), build 0/0.

### Indrykning af moduler i gruppe (02:43)
- Fejl: Margin blev sat som LOKAL vaerdi paa NamePanel (18), og lokale vaerdier vinder over DataTemplate-triggere -> gruppe og modul fik samme indrykning.
- Nu to triggers: IsGroupLayer=True -> Margin 2, IsGroupLayer=False -> Margin 24 (Photoshop-agtig indrykning).
- Deployet 02:43, DLL 1.347.072 B (identisk begge steder), build 0/0.

### Chrome uden for gruppen igen + ingen indrykning (02:47)
- Settings button, Close button og Overlay background er chrome og ligger nu igen UDEN for grupperne (knapper oeverst, baggrund nederst).
- Indrykning: IsGroupLayer=False -> 24 px, men en efterfoelgende IsChromeButton=True-trigger saetter 2 px, saa chrome-raekker ikke ser ud som gruppe-medlemmer.
- GamingOverlayFirstGroupHidden fjernet igen (chrome paavirkes ikke af gruppens oeje).
- Deployet 02:47, DLL 1.346.560 B (identisk begge steder), build 0/0.

### Chrome hoe rer til standardgruppen (02:50)
- De tre chrome-lag (Settings button, Close button, Overlay background) er igen en del af den foerste gruppe "Gaming overlay": knapper oeverst, baggrund nederst, og de indrykkes som gruppe-medlemmer (24 px).
- Gruppens oeje skjuler nu ogsaa chrome: knapperne via GamingOverlayFirstGroupHidden i baren, baggrunden via DataTrigger (Background=Transparent).
- IsChromeButton-indryknings-trigger fjernet igen (chrome er i gruppen).
- Deployet 02:50, DLL 1.347.072 B (identisk begge steder), build 0/0.

### Chevron viste forkert tilstand (02:53)
- Samme WPF-faelde som indrykningen: ChevronGlyph havde lokal Text="v" som vandt over triggeren -> gruppen kunne vaere sammenfoldet (moduler skjult) mens pilen stadig viste udfoldet.
- Nu to triggere (IsCollapsed true -> pil hoejre, false -> pil ned) og ingen lokal Text.
- Sammenfoldet gruppe viser kun sin egen raekke (chrome inkluderet).
- Deployet 02:53, DLL 1.347.072 B (identisk begge steder), build 0/0.

### Label: "Flyt til gruppe" -> "Laggruppe" (02:57)
- Neutral label for modulets gruppe-combo, saa den ogsaa passer naar modulet allerede ligger i en gruppe. en: "Layer group", da: "Laggruppe".
- Deployet 02:57, DLL 1.347.072 B (identisk begge steder), build 0/0.

### Gruppe-info for chrome + drag & drop i lag-listen (03:03)
- Chrome-raekker (Overlay background, Settings button, Close button) viser nu ogsaa "Laggruppe" i Properties, laast til den foerste gruppe (combo disabled).
- Drag & drop i lag-listen: traek et modul og slip paa en gruppe-raekke = flyt til gruppen; slip paa et andet modul = saet det lige over det (og overtag dets gruppe). Folder- og chrome-raekker kan ikke traekkes.
- VM: ny `MoveGamingOverlayModuleBefore(block, target)` (panel er omvendt -> indsaet efter target i Layout).
- Deployet 03:03, DLL 1.348.608 B (identisk begge steder), build 0/0.

### Blaa indsaetnings-linje ved drag i lag-listen (03:12)
- ListBox er wrappet i en Grid med `LayerDropIndicator` (2 px, #2FA8D8, IsHitTestVisible=False) + TranslateTransform.
- `UpdateDropIndicator` (kaldes fra DragOver) placerer linjen: over/under et modul afhaengigt af hvilken halvdel musen er i, eller lige under en gruppe-header (= ind i gruppen). DragLeave/Drop skjuler den.
- Deployet 03:12, DLL 1.349.632 B (identisk begge steder), build 0/0.

### Chrome kan flyttes mellem grupper (03:16)
- Chrome-raekkerne (Settings/Close/Overlay background) opfoerer sig nu som moduler i lag-panelet: de har deres egen `group`-prop, kan traekkes og slippes paa en gruppe (og viser gruppen i Properties med aktiv combo).
- `GamingOverlayLayerItems` placerer chrome pr. gruppe (knapper i toppen, baggrund i bunden af DEN gruppe den hoerer til); `GroupOf()` helper.
- `SetGamingOverlayModuleGroup` tillader nu chrome; `RemoveGamingOverlayGroup` flytter ogsaa chrome til fallback-gruppen.
- Render er uaendret: knapperne sidder stadig fast i hoejre side og baggrunden bag indholdet.
- Deployet 03:16, DLL 1.350.144 B (identisk begge steder), build 0/0.

### Drag nedad fix + undo i lag-panelet (03:21)
- BUG: drop indsatte altid laget OVER maalet, saa man kun kunne flytte opad. `MoveGamingOverlayModuleBefore(block, target, placeBelow)` + `IsLowerHalf()` -> nederste halvdel = indsaet under.
- Undo: `_undoSnapshots` (max 40 JSON-snapshots) pushes i `CommitBlueprintLayout` (springes over under restore). `UndoGamingOverlayLayout()` deserialiserer snapshot, notificerer alt og gemmer.
- UI: ?-knap i lag-toolbaren, disabled naar der ikke er noget at fortryde (CanUndoGamingOverlayLayout).
- Deployet 03:21, DLL 1.351.680 B (identisk begge steder), build 0/0.

### Moerk hover paa lag-toolbar-knapperne (03:26)
- `LayerRowButtonStyle` har nu en ControlTemplate med moerk hover (#2A2F35), pressed (#3A424A), hvidere foreground ved hover og opacity 0.4 naar disabled - i stedet for WPF-standardens lyse boks.
- Deployet 03:26, build 0/0.

### Lock virker nu + paint/path fjernet (03:34)
- "Layer:"-raekken har nu kun + (add) og lock; paint og path er fjernet.
- `OverlayBlock.IsLocked` (props["locked"]) + `OverlayLayerGroup.IsLocked`; gruppe-headeren faar "locked" i props.
- VM `ToggleGamingOverlayLock(row)`: gruppe-header -> gruppe.IsLocked, ellers props["locked"] paa modul/chrome.
- Laas blokerer: RemoveGamingOverlayModule, MoveGamingOverlayModule, MoveGamingOverlayModuleBefore (baade kilde og maal), SetGamingOverlayModuleGroup, RemoveGamingOverlayGroup (laast gruppe) og drag-start.
- Raekken viser et lille laas-glyph ved navnet naar den er laast (LockGlyph + DataTrigger IsLocked).
- Deployet 03:34, DLL 1.353.216 B (identisk begge steder), build 0/0.

### Egenskabs-panel: hoejre kant klippet af scrollbar (03:41)
- Problem: TextBoxene i egenskabs-panelet gik ind under Windows 11's overlay-scrollbar, saa hoejre kant var skjult. Kun de fast-bredde ComboBoxes (fx "Laggruppe") saa paene ud.
- Fix i `SkinStudioPage.xaml`: ScrollViewer for egenskaber fik `Padding="0,0,10,0"` og indre StackPanel `Margin="0,0,2,0"`.
- Build 0/0, deployet 03:41 (DLL 1.353.216 B, identisk begge steder).

### Lag-liste: ☰-haandtag klar af scrollbar (03:46)
- Samme fælde som egenskabs-panelet: naar lag-listen bliver lang nok til at rulle, ville Windows' overlay-scrollbar daekke ☰-haandtaget yderst til hoejre.
- Fix: `RowRoot` i lag-raekkens DataTemplate fik `Margin="0,0,10,0"` - hover/selection-baggrunden er stadig fuld bredde, kun indholdet flyttes 10 px ind.
- Build 0/0, deployet 03:46 (DLL 1.353.728 B, identisk begge steder).
- Ikke committet endnu (afventer brugerens ja).

### Committet og pushet (03:48)
- Commit `fa4ed98` "Skin Studio: working layer lock, lock glyph, scrollbar-safe panels" (5 filer, +91/-12).
- Pushet: `877f74e..fa4ed98 main -> main` til github.com/Maxithx/IconGrid. Working tree clean.
- Deploy fra 03:46 ligger fortsat i `C:\IconGrid` (DLL 1.353.728 B).

### Lag-værktøjslinje samlet i én række (03:51)
- "Layer:"-rækken indeholder nu alle handlinger: ↶ fortryd, 📁 opret gruppe, ＋ tilføj modul (clock), 🗑 slet lag/gruppe, 🔒 lås.
- Den separate knap-række under lag-listen (Grid.Row 4) er fjernet, og grid'et er reduceret fra 6 til 5 rækker; egenskabs-ScrollViewer flyttet til Row 4 med `Margin="0,10,0,0"` som erstatning for den gamle rækkes luft.
- Den gamle "＋ divider"-genvej i Layer-rækken er væk (Divider findes i Modul-biblioteket); der er nu kun ét ＋ = tilføj modul.
- Build 0/0, deployet 03:51 (DLL 1.353.216 B, identisk begge steder). Ikke committet.

### Skabelon-sektionen: korrekte knap-tekster + ensartet layout (04:04)
- Fund: knapperne i Expanderen "Brugerdefineret skabelon (avanceret)" genbrugte snippet-noeglerne, saa der stod "Anvend snippet"/"Nulstil" i XAML-sektionen.
- Nye noegler `SkinStudioTemplateApply` (en "Apply template" / da "Anvend skabelon") og `SkinStudioTemplateReset` (en "Remove template" / da "Fjern skabelon") + properties i MainViewModel.Localization.
- Layout: WrapPanel erstattet af fuld-bredde "Indlaes modulets kode" + 2-kolonne Grid (Anvend | Fjern) med lige brede knapper, saa rækken ikke bryder skævt ved smal panelbredde (UniformGrid med 3 kolonner blev fravalgt - teksten ville blive klippet ved 280 px).
- Verificeret: `ResetGamingOverlayTemplate` saetter kun `block.Template = null` (CommitBlueprintLayout tager undo-snapshot og gemmer settings, men rører ikke props) - punkt 3 var allerede korrekt.
- Build 0/0, deployet 04:04 (DLL 1.354.240 B, identisk begge steder). Ikke committet.

### System-laggruppe: nyt navn + sletningsbeskyttelse (04:13)
- `OverlayLayerGroup`: nye konstanter `DefaultName` ("Standard Gaming Overlay") og `LegacyDefaultName` ("Gaming overlay") + `IsSystem`.
- `EnsureGroups` markerer ALTID gruppe nr. 0 som systemgruppe og migrerer gamle gemte navne ("Gaming overlay" -> "Standard Gaming Overlay"); brugerens egne navne roeres ikke. Kører automatisk fordi `Groups`-settet kalder EnsureGroups (ogsaa ved JSON-deserialisering).
- `OverlayBlock.IsSystemGroup` (props["system"]) -> kan bruges i UI/triggers.
- `RemoveGamingOverlayGroup` afviser systemgruppen (ud over de eksisterende vaern: mindst 1 gruppe tilbage + IsLocked).
- Nye grupper hedder nu "Group N" i stedet for "Gaming overlay N".
- UI: 🗑 hedder nu `DeleteLayerButton`; `UpdateLayerToolbarState()` kaldes fra SelectionChanged og deaktiverer knappen + viser lokaliseret tooltip (ny noegle `SkinStudioSystemGroupProtected`, en+da) naar systemgruppen er valgt.
- Øje paa gruppe/moduler er uaendret - man kan stadig skjule alt i standardgruppen.
- Der findes INGEN Delete-taste-handler i SkinStudioPage i dag, saa der var intet at blokere der; VM-vaernet daekker alle kald.
- Build 0/0, deployet 04:13 (DLL 1.355.264 B, identisk begge steder). Ikke committet.

### Standardgruppen er nu ogsaa beskyttet paa LAG-niveau (04:19)
- Hul: foer kunne man slette moduler inde i "Standard Gaming Overlay" - kun selve gruppe-raekken var beskyttet.
- `OverlayBlock`: nye panel-only felter `IsSystemGroupMember` (saettes naar lag-listen bygges, for moduler OG chrome) og `IsDeleteBlocked` (gruppe -> IsSystemGroup, ellers IsSystemGroupMember).
- VM: `IsInSystemGroup(block)`-helper; `RemoveGamingOverlayModule` afviser nu systemgruppens medlemmer. `SetGamingOverlayModuleGroup` afviser ogsaa flyt UD af systemgruppen (ellers kunne man flytte et lag ud og slette det bagefter).
- UI: `UpdateLayerToolbarState` deaktiverer 🗑 for baade systemgruppen og dens lag + lokaliseret tooltip (ny noegle `SkinStudioSystemGroupMemberProtected`, en+da).
- Stadig tilladt i systemgruppen: skjule (oeje), laase, omdobe gruppen, omarrangere lag, aendre egenskaber/snippets/templates.
- Build 0/0, deployet 04:19 (DLL 1.356.288 B, identisk begge steder). Ikke committet.

### Committet og pushet (04:24)
- En commit med hele bunken: lag-vaerktoejslinje i én raekke, skabelon-knap-tekster/-layout, systemgruppe-beskyttelse (gruppe + lag) og note-opdateringer.
- Deploy fra 04:19 ligger i `C:\IconGrid` (DLL 1.356.288 B).

### Nye lag maa ikke havne i standardgruppen + drag fra bibliotek til canvas (04:27)
- `AddGamingOverlayModule(blockType, id, groupId)` returnerer nu det tilfoejede `OverlayBlock` og saetter props["group"]. Ny `ResolveNewModuleGroup(groupId)`: den valgte gruppe bruges hvis den ikke er systemgruppen; ellers foerste ikke-system-gruppe; findes ingen, oprettes en ny gruppe ("Group N") og modulet lander der. Standardgruppen forbliver praecis som den blev leveret.
- SkinStudioPage: `AddModuleFromSpec()` (bruges af alle 12 biblioteksknapper + "＋"), `SelectedGroupId()`, `SelectLayer()` (ny række markeres saa man ser hvor den landede).
- Drag & drop fra Modul-bibliotek til canvas: `LibraryPanel` starter DragDrop med formatet "IconGrid.ModuleSpec"; `DesignCanvas` har AllowDrop + DragOver/Drop der kalder AddModuleFromSpec. Klik-til-foej virker stadig.
- Modulraekkefoelgen er stadig lineaer (appen er en 1:1 horisontal bar), saa et drop paa canvas tilfoejer bagerst i rækken - ikke paa XY-position.
- Build 0/0, deployet 04:27 (DLL 1.358.336 B, identisk begge steder). Ikke committet.

### Hver gruppe = egen bar paa canvas (04:36)
- Model: `OverlayLayerGroup.Width` / `Height` (0 = auto) - gruppens egen bar-stoerrelse, gemmes i blueprintet.
- Ny converter `Helpers/Converters/FirstValueConverter.cs` (foerste ikke-null/ikke-NaN vaerdi) i namespace IconGrid.Helpers.
- `GamingOverlayBar`: nye DP'er `BlocksSource` (IEnumerable) og `BarHeight` (double, NaN = skin-hoejde). ItemsControl bruger MultiBinding: BlocksSource -> ellers GamingOverlayLayoutBlocks; indre Grid Height: BarHeight -> ellers GamingOverlayBackgroundHeight. Live-overlay og Gaming-overlay-siden saetter dem ikke og er derfor uaendrede.
- VM: `_canvasGroupId`, `CanvasGroup`, `GamingOverlayCanvasTitle`, `GamingOverlayCanvasBlocks` (kun synlige moduler i den valgte gruppe), `GamingOverlayCanvasBarWidth/Height` (NaN = auto), `SetSkinStudioCanvasGroup()`, `SetGamingOverlayGroupBarSize()`; `RaiseCanvasGroupChanged()` kaldes fra CommitBlueprintLayout.
- SkinStudioPage: canvas-bar bundet til BlocksSource/BarHeight/Width; titel over canvas viser gruppenavnet; selection skifter canvas-gruppe; nyt felt i gruppe-egenskaberne "Bar size (width x height, 0 = auto)" + Apply (GroupBarWidthBox/GroupBarHeightBox, GroupBarSize_Click/_LostFocus).
- VIGTIGT: live-overlayet renderer stadig ALLE moduler i én bar - naeste skridt er at vaelge hvilken gruppe der er den aktive/live bar.
- Build 0/0, deployet 04:36 (DLL 1.363.456 B, identisk begge steder). Ikke committet.

### Canvas-menu (File/Edit) + gruppenavn til hoejre (04:42)
- Canvas-headeren er nu en menu-bar: File (Gem overlay / Ny gruppe / Nulstil til classic) og Edit (Fortryd / Laas / Slet) + status-tekst og gruppenavnet yderst til hoejre.
- Nye styles `CanvasMenuBarItemStyle` og `CanvasMenuItemStyle` (moerk popup, hover #2A2F35, disabled-graa) i SkinStudioPage-resources; Menu.Resources har implicit MenuItem-stil + moerk Separator.
- 9 nye lokaliseringsnoegler (Fil/Rediger/Gem overlay/Ny gruppe/Nulstil til classic/Fortryd/Laas laas op/Slet lag eller gruppe/Overlay gemt.).
- VM: `SaveSkinStudioOverlay()` (flusher blueprint til disk; alt autogemmes i forvejen) og `ResetGamingOverlayBlueprint()` kalder nu ogsaa RaiseCanvasGroupChanged.
- Code-behind: MenuSave/MenuNewGroup/MenuReset/MenuUndo/MenuLock/MenuDelete, `ShowCanvasStatus()` med 2,5 s DispatcherTimer, og `DeleteSelectedLayer()` (delt af 🗑 og menuen).
- Build 0/0, deployet 04:42 (DLL 1.370.112 B, identisk begge steder). Ikke committet.

### Chrome (Settings/Close/Background) er nu gruppe-ejet i canvas (04:48)
- Problem: Group 2's canvas viste gear + X selvom Group 2 ikke ejer dem; de er `Chrome`-lag der hoerer til den gruppe de ligger i.
- `GamingOverlayBar`: ny DP `ChromeGroupId`; `OwnsChrome()` slaar op via `MainViewModel.GamingOverlayGroupOwns(block, groupId)`. Er gruppen sat og ejer den ikke chrome-laget, skjules knappen. Uden `ChromeGroupId` (live-overlay + Gaming-overlay-siden) er alt uaendret.
- VM: `GamingOverlayCanvasGroupId` + `GamingOverlayGroupOwns()`; begge rejses i RaiseCanvasGroupChanged.
- Canvas binder `ChromeGroupId="{Binding GamingOverlayCanvasGroupId}"`.
- Resultat: en tom gruppe viser en tom bar (kun skin-baggrund). Vil man have Settings/Close/Background i sin gruppe, traekker man de tre raekker fra standardgruppen ned i den - de er stadig singleton-lag i blueprintet (én udgave pr. blueprint).
- Build 0/0, deployet 04:48 (DLL 1.371.136 B, identisk begge steder). Ikke committet.

### Settings/Close/Background i Modul-biblioteket (04:51)
- Aarsag til at de ikke var i biblioteket: de ligger i blueprintets `Chrome`-liste, ikke i `Layout`, og blev altid oprettet automatisk af `EnsureChrome`.
- VM: `AddGamingOverlayChromeModule(kind, groupId)` (settings/close/background). Findes laget allerede, flyttes det til den valgte gruppe (de er singleton-lag: der er ét gear, ét X og én baggrund pr. blueprint); findes det ikke, oprettes det i gruppen.
- Biblioteket har nu tre ekstra knapper: Settings, Close, Background (Tag `chrome:settings|close|background`), med tooltip om at laget flyttes ind i den valgte gruppe.
- `AddModuleFromSpec` ruter `chrome:`-specs til den nye metode; klik OG drag-and-drop til canvas virker for dem.
- Build 0/0, deployet 04:51 (DLL 1.372.672 B, identisk begge steder). Ikke committet.

### Flere grupper paa canvas + flyt bar med musen (04:59)
- Model: `OverlayLayerGroup.X` / `Y` (canvas-offset). Nye grupper faar `Y = max(Y)+70`, saa de ikke lander oven paa standardgruppen.
- Ny `ViewModels/OverlayCanvasBar.cs`: wrapper pr. gruppe med `Blocks`, `CanvasLeft/Top` (origin 40/82 + X/Y), `BarWidth/BarHeight` (NaN = auto), `GuideVisibility` (valgt = lys guide) og `PlainFrameVisibility`.
- VM: `GamingOverlayCanvasBars` (alle grupper som hver sin bar) + `SetGamingOverlayGroupPosition(groupId, x, y, commit)`; rejses i RaiseCanvasGroupChanged.
- Canvas: den ene bar er erstattet af et ItemsControl (Canvas-panel) der tegner ALLE grupper paa deres position, med navn over hver bar. Bar'ens DataContext er VM (skin), mens Blocks/Chrome/BarHeight/Width hentes fra item via ContentPresenter.
- Flyt: transparent drag-flade oven paa hver bar. Klik paa en anden bar = vaelg den gruppe i lag-listen; er gruppen allerede valgt, kan baren traekkes (TranslateTransform under drag, ét commit + undo-snapshot ved slip).
- Begraensning: enkelt-LAG kan ikke flyttes frit i 2D - baren er en horizontal raekke, saa modulers raekkefoelge aendres i lag-listen. Kun gruppe-baren flyttes i XY.
- Build 0/0, deployet 04:59 (DLL 1.377.792 B, identisk begge steder). Ikke committet.

### Fix: klik paa standardbaren valgte Group 2 (05:04)
- Aarsag: Group 2 blev oprettet FOER grupper fik X/Y, saa den laa i (0,0) og blev tegnet oven paa standardbaren. Den overtog museklikket, og derfor blev Group 2 valgt.
- `EnsureGroups` stagger nu gamle grupper: for index > 0 med X=0 og Y=0 saettes `Y = index * 70`, saa alle bars kan rammes.
- Build 0/0, deployet 05:04 (DLL 1.378.304 B, identisk begge steder). Ikke committet.

### Fix: ny/tom gruppe viste alle moduler (05:08)
- Aarsag: `BlocksSource`/`ChromeGroupId`/`BarHeight`/`Width` blev bundet via `RelativeSource AncestorType=ContentPresenter`, som ikke gav item'et -> DPs var null -> baren faldt tilbage til "alle moduler" (og viste chrome).
- Loesning: `<Grid x:Name="ItemRoot">` i DataTemplate og `ElementName=ItemRoot` + `Path=DataContext.X` for alle fire bindinger (Elements i samme template-namescope).
- `MinWidth="80"` paa canvas-barerne, saa en tom gruppes bar stadig er synlig og kan rammes/flyttes.
- Nu viser en ny gruppe en tom bar - der oprettes ikke moduler i den (AddGamingOverlayGroup roerer kun Groups).
- Build 0/0, deployet 05:08 (DLL 1.378.304 B, identisk begge steder). Ikke committet.

### Bars kan flyttes helt til canvas-kanten (05:10)
- Aarsag til at standardbaren ikke kunne komme i toppen: canvas-origo var fast 40/82, og Y blev klemt til >= 0, saa der altid var ~82 px luft foroven (plus gruppenavnet over baren).
- `OverlayCanvasBar`: `OriginLeft/Top` = 24/24 og ny `MinOffset = -24`; `SetGamingOverlayGroupPosition` klemmer X/Y til [-24, 3000] -> canvas-position kan gaa helt til 0.
- Gruppenavnet ligger nu oven paa baren med `Margin="0,-16,0,0"` og `IsHitTestVisible="False"` (ingen layout-plads), saa baren kan ligge flush i toppen. StackPanel-wrapperen er fjernet.
- Build 0/0, deployet 05:10 (DLL 1.378.304 B, identisk begge steder). Ikke committet.

### Fix: canvas blev centreret lodret i ScrollViewer (05:16)
- Undersoegte gemt config: standardgruppen stod allerede paa `X=-24, Y=-24` (klemt minimum), saa modellen var korrekt - baren laa i canvas-toppen.
- Aarsag til at det SAA forkert ud: ScrollViewer'en centrerede det 1040x620 canvas lodret, saa canvas' top laa ca. 170 px nede paa skaermen.
- Fix: `HorizontalContentAlignment="Left"` + `VerticalContentAlignment="Top"` paa ScrollViewer og `HorizontalAlignment/VerticalAlignment="Left/Top"` paa `DesignCanvas`.
- Build 0/0, deployet 05:16 (DLL 1.378.304 B, identisk begge steder). Ikke committet.

### Simulering af FPS / frame time i Skin Studio (05:27)
- `SystemMonitor`: ny `SimulateOverlayTelemetry` + `OverlayFpsStatus` / `OverlayFrameTimeDisplay`. Naar simuleringen er slaaet til, kommer vaerdierne fra en 400 ms DispatcherTimer med random walk: FPS 95-240, FT = 1000/FPS +/- 0.3 ms (fx "187" og "5.3ms"). Reel telemetri koerer uaendret.
- `FpsStatus`/`FrameTimeStatus`-setterne notificerer ogsaa de nye Overlay*-properties, saa rigtige data virker naar simuleringen er slukket.
- `GamingOverlayBar.xaml`: FPS- og FT-skabelonerne binder nu `SystemMonitor.OverlayFpsStatus` / `OverlayFrameTimeDisplay`.
- VM: `GamingOverlaySimulateTelemetry` (forwarder til SystemMonitor). UI: checkbox "Simuler FPS / FT" oeverst i Egenskaber-panelet (ny noegle `SkinStudioSimulateTelemetry`, en+da).
- Ikke persisteret i config (runtime-only); virker baade i studio-canvas og i live-overlayet mens den er slaaet til.
- Build 0/0, deployet 05:27 (DLL 1.380.352 B, identisk begge steder). Ikke committet.

### Lodret bar-variant + Vis/Gruppe-menuer + live-bar (05:41)
- Model: `OverlayLayerGroup.Orientation` ("horizontal"/"vertical", `IsVertical`) + `OverlaySkinBlueprint.ActiveGroupId` (hvilken gruppe live-overlayet tegner).
- VM: `ActiveGroup`, `GamingOverlayActiveGroupId/Name/IsVertical`, `GamingOverlayLiveBlocks` (kun den aktive gruppes synlige moduler), `SetGamingOverlayActiveGroup()`, `SetGamingOverlayGroupOrientation()`, vis-filter-felterne `GamingOverlayShowHorizontalBars` / `...VerticalBars`.
- `GamingOverlayBar`: nye DP'er `IsVerticalOverride` (bool?), `EffectiveIsVertical`, `ContentHeight`. Vertical = lodret StackPanel, indholdet centreret i kolonne, chrome-knapper i bunden, divider-linjen bliver vandret (Tag-trigger paa ItemsControl), hoejden bliver auto (eller gruppens Height). ItemsSource-fallback er nu `GamingOverlayLiveBlocks`, saa live-overlayet tegner den aktive gruppe (standardgruppen som default).
- Canvas: hver bar faar `IsVerticalOverride` fra sin gruppe; den falske statiske "Vertical 48 x 400"-guide er fjernet.
- Menuer: ny **Vis** (Vis vandrette/lodrette barer, checkmarks) og **Gruppe** (Goer lodret / Goer vandret / Saet som live-bar + viser hvilken gruppe der er live). 8 nye lokaliseringsnoegler. CanvasMenuItemStyle har faaet checkmark-kolonne.
- Build 0/0, deployet 05:41 (DLL 1.391.104 B, identisk begge steder). Ikke committet.

### Fix: crash i Skin Studio - Run.Text binder TwoWay (05:43)
- Fejl: `System.InvalidOperationException: A TwoWay or OneWayToSource binding cannot work on the read-only property 'SkinStudioMenuGroupLiveText'` -> Skin Studio-siden crashede ved load.
- Aarsag: `Run.Text` har `BindsTwoWayByDefault = true`, saa `<Run Text="{Binding <read-only>}" />` kaster. Loesning: `Mode=OneWay` paa begge Run-bindings i "Live-bar:"-menupunktet.
- Tjekket: ingen andre Run-bindings i Views-mappen mangler Mode=OneWay (kun litterale Runs tilbage).
- Build 0/0, deployet 05:43 (DLL 1.391.104 B, identisk begge steder). Ikke committet.

### Fix: lodret bar klippede moduler (05:53)
- Problem: den lodrette bar brugte en FAST hoejde, saa FT/FPS blev klippet, og chrome-knapperne laa oven i teksten.
- Rettelse: `GamingOverlayBar`-XAML bruger nu `MinHeight` (ikke `Height`) bundet til `ContentHeight`. `RecomputeOrientation()` saetter: gruppe-Height hvis sat, ellers skin-hoejde for vandret og 0 for lodret -> kolonnen vokser frit og baggrunden daekker altid alle moduler.
- Lodret: `ContentRow` har faaet bundmargin 44 px, saa ⚙/X-rækken i bunden ikke overlapper sidste modul; chrome-margen er 0,0,0,8.
- Build 0/0, deployet 05:53 (DLL 1.391.104 B, identisk begge steder). Ikke committet.

### Lodret: brede dividere + FPS flugter med FT (05:59)
- Divider i lodret bar er nu en BRED vandret streg: `Width=NaN` + `HorizontalAlignment=Stretch`, `Height=DividerWidthValue`, margin 4,7,4,7. Vandret bar er uaendret (1 px lodret streg).
- FPS-skabelonen er omskrevet saa alle retnings-afhaengige vaerdier ligger i Styles med DataTriggers (lokale vaerdier vandt over DataTemplate-triggere): lodret -> Width auto, TextAlignment Left, Margin 0, MinWidth 34, saa "FPS: 133" flugter med "FT: 7.7ms". Glow-effekten er flyttet til en Setter.
- Build 0/0, deployet 05:59 (DLL 1.392.128 B, identisk begge steder). Ikke committet.

### Lodret: GPU clock align + divider-tykkelse (06:03)
- `ApplyDividerThickness()` er nu akse-bevidst: i en lodret bar (ItemsControl.Tag = true, fundet via `IsInsideVerticalBar`) saettes hoejden til den pixel-praecise tykkelse og bredden holdes stretch (NaN); i vandret bar er det Width som foer. Kaldes ogsaa fra `RecomputeOrientation()`, saa den koerer naar retningen skifter. Det var aarsagen til at dividerne saa forskellige/underlige ud.
- `OverlayStatGpuClockBlockTemplate` ("555 MHz") har faaet Style + DataTrigger: lodret -> Width auto, Margin 0,0,0,9, TextAlignment Left, saa den flugter med resten af kolonnen.
- FT/FPS-divider: der ligger ingen Divider i blueprintet mellem FT og FPS, saa der tegnes ingen (dividers er rigtige lag). Kan tilfoejes ved at traekke Divider fra biblioteket eller ved at indsaette en i standardgruppen.
- Build 0/0, deployet 06:03 (DLL 1.392.640 B, identisk begge steder). Ikke committet.

### Divider mellem FT/FPS + ens divider-tykkelse (06:08)
- `OverlaySkinBlueprint.EnsureFrameTimeFpsDivider()` (kaldes fra `FromJson`): indsaetter en Divider mellem frameTime- og FPS-modulet naar de ligger direkte op ad hinanden. Idempotent - en divider brugeren selv har sat, duplikeres ikke.
- Divider-tykkelsen blev sat foer modulerne var realiseret (tom divider-liste ved load) -> tykkelsen blev 1 DIP = 1.25 device px ved 125% -> skiftende 1/2 px. Nu kaldes `ApplyDividerThickness()` ogsaa fra `OverlayRoot.SizeChanged` og via `Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)`.
- Build 0/0, deployet 06:08 (DLL 1.393.152 B, identisk begge steder). Ikke committet.

### Committet og pushet (05:30)
- Commit med FPS/FT-simuleringen (SystemMonitor + bar-templates + checkbox i Egenskaber) og note-opdateringer.
- Deploy fra 05:27 ligger i `C:\IconGrid` (DLL 1.380.352 B).

### Committet og pushet (05:20)
- En commit med hele bunken: canvas File/Edit-menu, systemgruppe-beskyttelse, chrome som gruppe-lag + biblioteksknapper, én bar pr. gruppe paa canvas, flyt bar med musen, canvas-origo/centrering-fix og note-opdateringer.
- Deploy fra 05:16 ligger i `C:\IconGrid` (DLL 1.378.304 B).


## Session findings (2026-11-18) — Gaming overlay skal kun starte via detection pipeline

Brugerrapport: ved start af Path of Exile 1 springer gaming overlayet "hardcore" frem; spillet skal starte overlayet gennem detection-pipelinen. COD/eksternt startede spil går korrekt gennem VRAM/present-evidens.

ROOT CAUSE (2 genveje udenom pipelinen):
1. `ViewModels/MainViewModel.Items.cs` `RememberFpsTarget` kaldte `GameLaunched?.Invoke()` direkte efter `Process.Start` (via `_onLaunching` i `LauncherItemLaunchManager.LaunchItem`). `MainWindow.OnGameLaunched` skjuler derefter launcheren og viser overlayet. Trace 2026-11-18: `23:05:33.681 WatchProcess started for PID 20868 (PathOfExile.exe)` -> `23:05:33.683 GameLaunched` (2 ms senere), mens FPS-agenten først blev startet `23:05:34.674`, og `23:06:35.931 FindAnyGameProcess gave up after 60s ... No qualifying game process found`. Overlayet var altså oppe før agenten fandtes og uden noget spilvindue.
2. `NativeFpsAgentRunner` sender `--trusted-launch` for config-target (spil startet via IconGrid). I `Native/FpsAgent/src/main.cpp:375-377` er `gameConfirmed = g_trustedLaunch || hasFreshEvidence`, så `targetPid` publiceres med det samme. `SystemMonitor.FpsTimer_Tick` accepterede `isTrustedLaunch && GameConfirmed` som evidens. Bevis: `native-fps-state.json` 23:08:41: `gameConfirmed=true, trustedLaunch=true, targetPid=0`, `matchedDxgi=0 matchedD3D9=0 matchedDxgKrnl=0`; trace 23:08:40: `TargetPid=6544 DXGI=0 D3D9=0 DXGKRNL=0 Events=False`. Nul frames, men overlay åbnet.
   Konsekvens: fordi genvej 1 ikke opdaterer `_wasInGame`, kom der to `GameLaunched` (23:05:33 + 23:08:38) og **nul** `GameExited`.

BEKRÆFTELSE AF AT COD-STIEN ER KORREKT: `GameVramEvidence` (500 MB peak-VRAM ELLER >=2 DXGI/D3D9-presenter; <250 MB + ingen app-presenter = NotGame; DxgKrnl alene tæller aldrig) + `GameEvidenceProbe`-linjer i trace viser vram/peak/p95/3D%/presentFps/vindue/cover, og browser/VS Code/Discord/PowerToys/dwm afvises korrekt. COD kørte ikke i dagens trace (log dækker 20:34-23:11, ingen cod22-linjer); COD's 5,4 GB peak er dokumenteret i `GameVramEvidence.cs`.

ÆNDRING (bruger-valg: "fuldt via detection pipeline"):
- `ViewModels/MainViewModel.Items.cs`: den direkte `GameLaunched?.Invoke()` er fjernet (erstattet af Debug-linje). FPS-target persisteres stadig. Launcher-hide + overlay-show styres nu udelukkende af `SystemMonitor.IsInGame` false->true.
- `Helpers/Launcher/SystemMonitor.cs`: `isTrustedLaunch && GameConfirmed` er fjernet fra `isGameTarget`; trusted launch er ikke længere evidens. Gaten er nu `GameEvidenceVerdict.Game || HasPrimaryGraphicsEvidence` (samme regel som monitor-agentens `HasAcquisitionEvidence`). `--trusted-launch` i agenten er bevaret, så FPS stadig kan vises for emulatorer/ældre titler med kernel-path.

BUILD + DEPLOY: `dotnet build IconGrid.csproj` -> Build succeeded, 0 fejl. `cmd /c deploy-test.cmd` -> Done. Verificeret: `bin\\Debug\\net10.0-windows10.0.22621.0\\IconGrid.dll` og `C:\\icongrid\\IconGrid.dll` = 18-11-2026 23:16:23, 1.393.664 bytes (identisk). App startet: `OnStartup begin` / `Starting launcher UI. Elevated=False` + forhøjet monitor-agent (`Starting hardware monitor agent mode. Elevated=True`); ingen fejl i error.log.

Den kørende app startede på den nye build og loggen blev ryddet + testet af brugeren: `23:26:23` låste agenten `PathOfExile.exe` (PID 7816) med `DXGI=0 D3D9=0 DXGKRNL=0` og overlayet blev IKKE vist; `23:26:56 [GameFlow] GameLaunched` kom først da der var frames; `23:27:06 [GameFlow] GameExited` lukkede overlayet og gendannede launcheren. Trace-log master-switch verificeret af brugeren ("works great").
COMMITTET OG PUSHET: `c7d4443` (2026-11-18).


## Trace-log master switch (2026-11-18)

Brugerønske: på Om-siden (trace-kortet, hvor loggen kan Refresh/Clean) skal man helt kunne slå trace-logging fra.

IMPLEMENTERING:
- `Helpers/Logging/AppTrace.cs`: nye `IsEnabled` / `SetEnabled(bool)` + `DisabledMarkerPath` = `%APPDATA%\IconGrid\trace-logging.disabled`. Markørfil (findes = slået fra) i stedet for config.json, fordi launcher og forhøjet monitor-agent er separate processer: begge ser ændringen inden for 1 s uden genstart og uden config.json-race. `Write()` returnerer tidligt når den er slået fra, så ALLE skrivestier (launcher, monitor-agent, resolution service, storage readers) stopper. Eksisterende trace.log røres ikke; Clear/Refresh virker uændret.
- `ViewModels/MainViewModel.cs`: `TraceLoggingEnabled` (proxy til AppTrace, notificerer `TraceLoggingStateText`).
- `ViewModels/MainViewModel.Localization.cs`: 5 nye tekst-properties + notifikationer.
- `Helpers/Settings/LocalizationHelper.cs`: 4 nye nøgler i både en og da (TraceLoggingToggleTitle/ToggleDescription/EnabledStatus/DisabledStatus). Verificeret med `.local-state/check-loc.ps1`: en=336, da=336, 0 mangler.
- `Views/Settings/Pages/TestSection.xaml` (Trace log-kortet, som ligger nederst på About): ToggleSwitchStyle-kontakt + lokaliseret titel/beskrivelse/status bundet via `DataContext.*` (AncestorType=Window).

BUG FUNDET UNDER TEST: første udgave initialiserede cache-sentinel til `long.MinValue`; `Environment.TickCount64 - long.MinValue` overflower til negativ, så markørfilen blev ALDRIG læst (logging fortsatte). Rettet med eksplicit sentinel-tjek (`!= long.MinValue`).

VERIFIKATION (end-to-end, efter fix): app kørende (launcher + forhøjet monitor-agent). Med markørfil: trace.log stod stille på 8.247.323 B i 6 s (ingen skrivninger). Efter sletning af markør: voksede til 8.248.891 B inden for 6 s. Markøren blev fjernet igen, så logging er slået TIL i den kørende app.

BUILD + DEPLOY: Build succeeded 0 fejl. `bin\\Debug\\...\\IconGrid.dll` = `C:\\icongrid\\IconGrid.dll` = 18-11-2026 23:24:25, 1.397.248 bytes. (Første deploy efter build kopierede ikke DLL'en — processen var stadig ved at lukke; anden deploy OK.) App startet igen, ingen nye fejl i error.log.

NÆSTE TEST (bruger): Indstillinger -> Om -> Trace log: slå kontakten fra, bekræft at status skifter til "Trace-log skrivning er SLÅET FRA." og at trace.log ikke længere vokser; slå til igen og bekræft at den vokser. Bruger-verificeret 2026-11-18 ("works great"). COMMITTET OG PUSHET: `c7d4443`.


## Session findings (2026-11-24) — GPU power saving while game is unfocused

Brugerønske: når et spil (fx Path of Exile) kører men vinduet ikke er aktivt (alt-tab til Windows/YouTube), skal GPU'ens Power Limit og Temp Limit sænkes — som tilvalg i sin egen sektion på Gaming overlay-siden.

IMPLEMENTERING (NVIDIA-only, opt-in, default OFF):
- `Helpers/Hardware/NvmlGpuLimitController.cs` (ny, 585 linjer): NVML P/Invoke. Læser nuværende/default/min/max power + NVML acoustic temp-threshold (samme driver-knap som Afterburners "Temp Limit"; målt ACOUSTIC_CURR=83 °C på dette kort). Skriver kun lavere værdier, clampes til kortets egne grænser, og gendanner originalværdier. Crash-sikring via `%APPDATA%\IconGrid\gpu-limit-restore.json` (original + applied). Backer off hvis Afterburner/brugeren ændrer limit eksternt. `DllNotFoundException`/manglende NVML giver "unavailable", ikke crash (AMD/Intel).
- `Helpers/Hardware/HardwareMonitorAgent.cs`: ejer controlleren (agenten kører allerede elevated — NVML writes kræver admin). Ny `UpdateGpuPowerSaving`: holder et sæt af bekræftede game-PIDs (`gpuKnownGamePids`, døde prunes hvert loop) og slår low-power til når mindst én kendt spilproces lever OG forgrundsvinduet ikke er en af dem. Det gør alt-tab til browser robust, selv hvis FPS-pipelinen senere retargeter. Fokus tilbage til spillet/spil lukket => restore. 4 s hysterese, 60 s retry-backoff; slider-ændringer slår igennem live mens profilen er aktiv. Restore ved agent-exit (`exit:`), `Dispose`, og ved crash via marker.
- Persistens: `ConfigModel` + `MainViewModelSettingsState` + `MainViewModelConfigState` + `MainViewModelSettingsPersistence` (4 felter: `GpuPowerSavingEnabled`, `GpuPowerSavingPowerLimitPercent=70`, `GpuPowerSavingTempLimitEnabled=true`, `GpuPowerSavingTempLimitCelsius=70`).
- UI: nyt Expander-kort "GPU-strømbesparelse" i `Views/Settings/Pages/GamingOverlayPage.xaml` (efter "Automatisk under spil"): master-toggle, power-slider 50-100 %, temp-toggle + slider 60-85 °C, advarsel om MSI Afterburner.
- Lokalisering: 9 nye nøgler i både en+da i `LocalizationHelper`, properties + `NotifyLocalizationPropertiesChanged` i `MainViewModel.Localization` (central pattern; XAML binder via `DataContext.*, AncestorType=Window`).

MÅLTE NVML-FAKTA (denne maskine, RTX 2060 SUPER, driver 616.56): power 175 W, interval 125-175 W, GPU_MAX=89 °C, SLOWDOWN=93 °C, ACOUSTIC_MIN=65, ACOUSTIC_CURR=83, ACOUSTIC_MAX=88. No-op writes fra non-elevated shell gav rc=4 (NO_PERMISSION) => bekræfter at skrivning kræver admin; agenten er elevated.

BUILD + DEPLOY: `dotnet build IconGrid.csproj` => Build succeeded, 0 warnings, 0 errors. `cmd /c deploy-test.cmd` => Done. `bin\Debug\...\IconGrid.dll` = `C:\icongrid\IconGrid.dll` (24-11-2026 01:00:54). App + monitor-agent startet igen; `monitor-state.json` og `gpu-power-saving-state.json` opdateres løbende (agenten kører ny kode uden crash). Trace-logging er slået FRA via marker, så agent-log-linjer kunne ikke verificeres.

DIAGNOSE 2026-11-24 (bruger: "GPU falder ikke" + "blæser kører hele tiden"):
- Power limit ER sænket korrekt: `nvidia-smi` viser 125 W (kortets minimum) i stedet for 175 W, og `gpu-limit-restore.json` blev skrevet. Funktionen virker efter hensigten.
- Kortet bruger kun ~78-85 W ved PoE i baggrunden (60 FPS). Derfor er power limit ikke den begrænsende faktor, og temperaturen bliver 83-84 °C.
- `nvidia-smi -q -d TEMPERATURE`: `GPU Target Temperature Specification = 70 °C`, men `GPU Max Operating Temp = 89 °C`. `nvidia-smi -q -d PERFORMANCE`: `SW Thermal Slowdown = Active`. Kortet kan ikke komme under 83-84 °C ved ~80 W / 74 % fan.
- Fan boost testet: fan 74 %→96 %, temp kun 84→83 °C efter 60 s. Køleren er flaskehalsen. Fan boost er derfor default OFF og blev slået fra igen; fan er tilbage på auto 74 %.
- GPU_MAX (threshold type 3) forsøgt sænket til 70 °C: ingen effekt (`GPU Max Operating Temp` rapporteres stadig 89) → koden er fjernet igen.
- Reference: TechPowerUp målte MSI RTX 2060 Super Gaming X til 67 °C / 33 dBA ved fuld spilbelastning (~175 W). 83-84 °C ved ~80 W er langt over normalen → mistænkt tør thermal paste/pads, støv eller dårligt luftflow. Anbefaling: HWiNFO64 hotspot/memory junction, repaste/repad, rens køler.
- PoE's egen config (`%USERPROFILE%\Documents\My Games\Path of Exile\production_Config.ini`): `background_framerate_limit_enabled=true`, `background_framerate_limit=60`. Det er årsagen til at spillet bliver ved med at rende 55-60 FPS i baggrunden. Sæt Background FPS Cap til 15-20 i Options → Graphics (eller NVIDIA Kontrolpanel → Background Application Max Frame Rate). Det er den effektive vej til lavere temperatur.
- Nyt: agenten skriver `gpu-power-saving-state.json` (1 Hz) med power limit, interval, temp-mål, aktuel temp, fan og besked; Gaming overlay-siden viser "Målt status" + "Opdater status" (opdateres hvert 3. s).

NÆSTE TEST (bruger): Sæt PoE Background FPS Cap til 15-20 og bekræft at temperaturen falder. Overvej repaste hvis hotspot er høj. Power limit kan ikke sænkes under 125 W på dette kort.

OVER-LIMIT NOTE: `HardwareMonitorAgent.cs` er nu 2817 linjer (var 2542; allerede over grænsen). Næste cohesive extraction-kandidat: flyt selve profil-beslutningen (`UpdateGpuPowerSaving`) til en lille `GpuPowerSavingCoordinator`-helper, så agenten kun kalder den.

IKKE COMMITTET/PUSHET — kræver eksplicit bruger-godkendelse.


## GPU-STRØMBESPARELSE: CLOCK-GOVERNOR (24-11-2026, testet live)

Brugerkrav: temperaturgrænsen skal kun gælde når spilvinduet IKKE har fokus, og GPU'en skal reelt drosle ned (længere levetid). Det gjorde de gamle knapper ikke.

MÅLTE FAKTA FØR (PoE i baggrunden ~57 FPS, VS Code i fokus):
- power.limit 125 W (kortets minimum), draw 79-81 W, 83-84 °C, SM 1290-1455 MHz, mem 7000 MHz (fuld), fan 75 %.
- `clocks_event_reasons.active = 0x20` (SW Thermal Slowdown) — IKKE power cap (0x04).
- NVML acoustic threshold = 70 °C og driveren rapporterer `GPU Target Temperature Specification: 70 C`, men kortet lå 13-14 °C over → NVML's temp-knap er kun et akustisk mål, ikke en hård clock-grænse. Power limit kan ikke komme under 125 W. Begge gamle knapper var derfor tandløse på dette kort.
- Vores egen pipeline er ikke årsagen: IconGrid 0,06 % GPU, IconGridFpsAgent 0,00 % GPU.
- PoE's `production_Config.ini`: `background_framerate_limit=60` → PresentMon målte 17,4 ms/frame og 14,1 ms GPU-busy pr. frame i baggrunden.

IMPLEMENTERET (Helpers/Hardware):
- `NvmlGpuLimitController`: nye P/Invoke `nvmlDeviceSetGpuLockedClocks` / `nvmlDeviceResetGpuLockedClocks` (samme mekanisme som `nvidia-smi -lgc`; kræver admin → kun agenten) samt `nvmlDeviceGetClockInfo` / `nvmlDeviceGetMaxClockInfo`. Ny `GovernClocks(targetTempC)`: lukket reguleringssløjfe — clock-loft sænkes 150 MHz mens temp > mål, hæves 100 MHz når temp <= mål-3; gulv 400 MHz, loft = kortets max SM-clock (2100 MHz), min. 2 s mellem ændringer. `ReleaseClockLimit()` frigiver øjeblikkeligt. `Restore()`/`Dispose`/agent-exit frigiver loftet, og `gpu-limit-restore.json` har nu `ClockLockApplied` + `AppliedClockCeilingMhz`, så crash-recovery nulstiller et efterladt loft.
- `HardwareMonitorAgent.UpdateGpuPowerSaving`: governor kører mens lav-strøm-profilen er aktiv og `GpuPowerSavingTempLimitEnabled=true`. Når spillet får fokus frigives clock-loftet med det samme (før den 4 s power-limit-hysterese), så der ikke opstår lav-FPS-spikes.
- `GpuPowerSavingState`: nye felter `SmClockMhz`, `MaxSmClockMhz`, `ClockLimitSupported`, `ClockCeilingMhz`.
- UI: "Målt status" viser nu `clock X MHz · loft Y MHz` (statusformat opdateret i en+da; 0 vises som "–"). Temp-intro/hint (en+da) forklarer at temperaturgrænsen nu ER clock-loftet.

MÅLT EFTER (samme session, PoE stadig uden fokus, mål 70 °C):
- Clock-loft konvergerede 1950 → 600 MHz; GPU 67-70 °C, draw 45-57 W, fan 63-65 %, `throttle = 0x1 (GpuIdle)`, mem stadig 7000 MHz.
- 84 °C → ~68 °C (≈16 °C lavere) og 81 W → ~52 W uden at ændre spillets FPS-cap. VRAM-clock røres bevidst ikke (Temp Limit påvirker ikke VRAM; kun memory-junction-beskyttelse gør, og den rapporteres N/A på dette kort).

VERIFIKATION: endelig build 0 fejl/0 advarsler (01:26:02) → deploy → `C:\IconGrid\IconGrid.dll` = `bin\Debug\...\IconGrid.dll` (01:26:02). Agenten anvendte profilen automatisk inden for ~45 s (PoE uden fokus), og restore-markøren viste `ClockLockApplied=true` med det aktive loft. Efter genstart konvergerede governor'en til loft 550-600 MHz / 67-70 °C / ~52 W.
Status-linjen i "Målt status" viser nu altid tal (clock/loft), også når governor'en holder sit nuværende loft.
LAYOUT (01:38): beskrivelses-teksterne i GPU-kortet (enable/power/temp/fan-intro, hint og målt status) har fået `MaxWidth="680"` + `HorizontalAlignment="Left"`, så de ikke længere løber i én lang linje hen over hele kortet. Build 0/0 (01:38:29) → deployet, DLL-timestamps matcher.
NB: `deploy-test.cmd` startede IKKE appen i denne session (kun "Start:"-echo) → appen blev startet manuelt med `Start-Process C:\IconGrid\IconGrid.exe`.

NÆSTE (bruger-test): alt-tab til PoE → clock-loftet skal frigives inden for ~1 s (SM tilbage til 1300-1900 MHz); alt-tab ud igen → loftet trapper ned igen. Justér "Temperaturgrænse" (60-85 °C) for hvor hårdt kortet må drosles i baggrunden.
TEKNISK GÆLD: `HardwareMonitorAgent.cs` er nu ~2860 linjer; næste extraction-kandidat er fortsat at flytte `UpdateGpuPowerSaving` til en `GpuPowerSavingCoordinator`-helper.

IKKE COMMITTET/PUSHET — kræver eksplicit bruger-godkendelse.


## GPU-KORT: FAN-BOOST FJERNET + REALTIDSSTATUS (24-11-2026)

Brugerønske: (1) "Ekstra køling (100 % fan)" skal væk fra GPU-besparelse-kortet, (2) "Målt status" skal vise realtidstal — gerne både med og uden aktivt spilvindue.

FJERNET (UI + indstilling, ikke motoren):
- `Views/Settings/Pages/GamingOverlayPage.xaml`: fan-boost-Grid (toggle) + intro-tekst er slettet.
- `ConfigModel.GpuPowerSavingFanBoostEnabled`, `MainViewModel._gpuPowerSavingFanBoostEnabled` + property, `MainViewModelSettingsState`/`ConfigState`/`SettingsPersistence`-felterne, `MainViewModel.Localization` (2 properties + 2 notify), `LocalizationHelper` (2 nøgler i en+da) og `GpuPowerSavingState.FanBoostEnabled` er fjernet.
- `HardwareMonitorAgent` sender nu altid `fanBoost: false` til `NvmlGpuLimitController.Apply`.
- BEVIDST BEHOLDT i `NvmlGpuLimitController`: `TryApplyFanBoost`/`TryRestoreFanControl` + `FanBoostApplied` i restore-markøren, så en gammel crash-markør stadig kan rulle fan-styringen tilbage. Agenten kan ikke længere aktivere boost.

REALTIDSSTATUS:
- NVML: ny `nvmlDeviceGetPowerUsage` → `GpuPowerStatus.PowerDrawMw` (faktisk forbrug i W, ikke kun limit).
- Ny `Helpers/Hardware/GpuPowerSample.cs`: `GpuPowerSample` (record: temp/clock/draw/fan/tid) + `GpuPowerSamples` der holder "sidst målt MED spillet i fokus" og "sidst målt UDEN fokus".
- `HardwareMonitorAgent.UpdateGpuPowerSaving` sampler hvert loop (før back-off/hysterese), så tallene er friske uanset profil-state; `GpuPowerSavingState` har fået `PowerDrawMw`, `GameInFocusSample`, `GameNotInFocusSample`.
- UI: `_gpuStatusTimer` 3 s → **1 s**. "Målt status" er nu fire linjer:
  `Lav-strøm-profilen er AKTIV.` / `Nu: GPU 67 °C · clock 600 MHz · loft 600 MHz · 52 W · fan 66 % · grænse 125 W (125-175 W)` / `Med spillet i fokus: … (HH:mm:ss)` / `Uden fokus (lav-strøm aktiv): … (HH:mm:ss)` + agentens besked.
- Nye lokaliseringsnøgler (en+da): `GpuPowerSavingSampleFocusFormat`, `GpuPowerSavingSampleBackgroundFormat`, `GpuPowerSavingSampleNever`; `GpuPowerSavingStatusFormat` er ændret til live-formatet (8 pladsholdere).

MÅLT (24-11, PoE kørende): med spillet i fokus 83 °C / 1305 MHz / 78 W / fan 70 % → uden fokus 67 °C / 600 MHz / 52 W / fan 66 %. Governor'en trappede 1500 → 600 MHz på ~18 s efter alt-tab.
VERIFIKATION: build 0 fejl/0 advarsler (01:45:46) → deploy → DLL-timestamps matcher; app + agent genstartet, state-JSON indeholder `powerDrawMw` + begge samples.
Gammel nøgle `GpuPowerSavingFanBoostEnabled` kan stadig stå i `config.json` — Newtonsoft ignorerer ukendte felter, så den er harmløs.

IKKE COMMITTET/PUSHET — kræver explicit bruger-godkendelse.


## GPU-TEKSTER REVIEWET (24-11-2026)

Brugeren spurgte om "IconGrid går aldrig under kortets minimum" burde hedde "IconGrids Gaming overlay", fordi funktionen kører gennem gaming-overlay-pipelinen.

FAKTA (verificeret i koden): GPU-besparelsen styres af `HardwareMonitorAgent` (elevated) ud fra `knownGamePids` + "spillet har ikke fokus" — dvs. IconGrids **spil-session-detektering**. Gaming overlay-**vinduet** er ikke en forudsætning (det kan være lukket, mens profilen stadig anvendes). Derfor er "Gaming overlay" upræcist/forkert i teksten; i stedet navngives den komponent der faktisk gør arbejdet: hardware-monitor-agenten.

OPDATEREDE NØGLER (en+da): `GpuPowerSavingIntro` (nævner nu clock-loft i stedet for kun temperaturgrænse + "i samme øjeblik spillet får fokus"), `GpuPowerSavingEnableIntro` (nævner spil-session-detektering + at det virker uanset om overlayet er åbent), `GpuPowerSavingPowerIntro` (agenten går aldrig under kortets eget minimum), `GpuPowerSavingTempIntro` (agenten låser clocken), `GpuPowerSavingHint` (præciseret: gælder kun når spilvinduet ikke har fokus).

VERIFIKATION: build 0/0 (01:52:45) → deployet, DLL-timestamps matcher; app + agent genstartet.

IKKE COMMITTET/PUSHET — kræver explicit bruger-godkendelse.


## GPU-TEKSTER: PRODUKTNIVEAU ("IconGrid") — endelig beslutning (24-11-2026)

Brugerens pointe: pipeline detekterer spil → `HideForGame` → gaming overlay startes, så det er samme detektering; hvem der "ejer" power limit i teksten er kosmetisk.

BEKRÆFTET I KODEN: `Views/Launcher/MainWindow.xaml.cs:895-906` `OnGameLaunched()` → `_windowModeController.HideForGame(GameLauncherAutoBehavior)` → (valgfrit) `ShowGamingOverlay(...)`. GPU-besparelsen hænger på samme spil-detektering via FPS-pipelinen (`knownGamePids` + fokus-check), uafhængigt af om overlay-vinduet vises.

BESLUTNING: teksterne bruger **produktnavnet "IconGrid"** (ikke "Gaming overlay", ikke "hardware-monitor-agenten" som primær aktør). Kun enable-subteksten nævner pipelinen + agenten, fordi det forklarer admin-kravet: "den samme spil-detektering, som skjuler launcheren og starter gaming overlayet, via IconGrids hardware-monitor-agent".

Endelige tekster (da): Intro = "Sænk grafikkortets power limit og sæt et loft på GPU-clocken, mens en spil-session kører, men spilvinduet ikke har fokus. De oprindelige værdier gendannes, i samme øjeblik spillet får fokus igen." / PowerIntro = "… IconGrid går aldrig under kortets eget minimum." / TempIntro = "… IconGrid låser GPU-clocken …" / Hint = "… Det hele gælder kun, mens spilvinduet ikke har fokus …". Engelsk 1:1.

VERIFIKATION: build 0/0 (01:57:55) → deployet, DLL-timestamps matcher; app + agent genstartet.

IKKE COMMITTET/PUSHET — kræver explicit bruger-godkendelse.


## BUGFIX: GAMING OVERLAY "HOPPER" NÅR EGEN MENU ÅBNES (24-11-2026)

Brugerrapport: så snart gaming overlayet er oppe og man klikker på tandhjulet ("Overlay settings") eller "Open gaming overlay settings", rykker selve overlay-baren sig.

ROOT CAUSE: `OverlayBar.PopupPanelsRow` er `HorizontalAlignment="Right"` og BREDERE end baren. Vinduets indhold er højrejusteret, og `ApplyWindowSize()` ændrede kun `Width`/`Height` (ikke `Left`). Når menuen åbnede/lukkede, flyttede den højre kant sig derfor = baren gled sidelæns. Samme mekanisme ramte indholds-toggles (ur/net/CPU/GPU/FPS) i `UpdateOverlayMetrics`.

FIX (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
1. `UpdateOverlayMetrics()` gemmer nu højre kant (`Left + Width`) FØR resize, kalder `ApplyWindowSize()` og sætter bagefter `Left = højreKant − Width` (med `_suppressLocationSave`, så positionen ikke gemmes som "brugerens placering"), og kalder til sidst `ApplyPositionPreset()` (preset-tilstande re-ankres præcist, inkl. bund-presets hvor højden vokser; `Custom` returnerer tidligt og beholder den justerede manuelle position).
2. `ApplyWindowSize()` sætter nu `MinWidth` FØR `Width` (rækkefølgen kan ellers forhindre WPF i at skrumpe rammen tilbage efter menuen er lukket).

VERIFIKATION (målt med Win32 `GetWindowRect` + UI Automation på `SettingsMenuButton`, mens PoE kørte):
- FØR: `1631,0 929x24  right=2560`
- MENU ÅBEN: `1582,0 978x173  right=2560`
- EFTER (lukket): `1582,0 978x24  right=2560`
→ højre kant er konstant 2560 = baren flytter sig IKKE længere. Bruger bekræftede: "det virker".

KENDT KOSMETISK REST: vinduets usynlige ramme beholder popup-bredden (978) indtil overlayet genåbnes; baren står korrekt (højrejusteret, kant 2560) og området er gennemsigtigt i et lagdelt WPF-vindue, så klik går igennem til spillet. Ikke rettet videre (kosmetisk).

NOTE: trace-logging blev midlertidigt slået TIL under fejlsøgningen (`trace-logging.disabled` → `.bak`) og er slået fra igen; appen blev genstartet, så den kører uden tracing.

BUILD/DEPLOY: 0 fejl/0 advarsler (02:03:50), deployet, `C:\IconGrid\IconGrid.dll` matcher `bin\Debug\...`.

IKKE COMMITTET/PUSHET — kræver explicit bruger-godkendelse.


## GPU UNDERVOLT VIA MSI AFTERBURNER (24-11-2026)

Brugerønske: undervolt af GPU (ikke CPU) med "par graders" gevinst uden FPS-tab, sat op i IconGrid og sendt til MSI Afterburner.

MÅLTE FAKTA (vigtige, de styrer designet):
- NVML/NVAPI kan IKKE skrive spænding eller V/F-kurve. `nvapi.h` (ligger i `.local-state/PresentMon-reference/.../nvapi.h`) har kun `NvAPI_GPU_GetPstates20` — ingen Set-variant. Afterburner skriver kurven via sin egen signerede kernel-driver (RTCore64) + udokumenterede register-writes. Egen driver blev vurderet og afvist: kræver EV-cert + MS attestation-signering, HVCI/Secure Boot skal slås fra (HVCI er aktiv her), anti-cheat-risiko og brick-risiko.
- **Clock-loft i spil er IKKE en undervolt:** med loft 1200 MHz i PoE var temp stadig 83 °C og effekt 79 W (GPU blev bare 90-95 % udnyttet i stedet). Clock-cap koster FPS uden at sænke watt. Den in-game governor-branch er derfor FJERNET igen; baggrunds-lav-strøm-governoren (600-900 MHz når spillet ikke har fokus) består og virker.
- **Afterburner kan styres via CLI:** AB's egen `MSIAfterburner.cfg` lister `-profile1` … `-profile5` og `-lowpowerprofile` (under `[PredefinedApplications]`). Test: `MSIAfterburner.exe -profile1` fra kold start ændrede power limit 125 → 164,5 W (profilens 94 %). Mens AB kører ignoreres argumentet (single-instance) → derfor: stop AB, start med `-profileN` (kræver admin; AB kører elevated, så kun vores agent kan gøre det).
- **Profil-filen** (`Profiles\VEN_10DE&DEV_1F06&…cfg`) indeholder pr. profil: `PowerLimit`, `ThermalLimit`, `CoreClkBoost`, **`VFCurve=<hex-blob>`**, `FanMode`, `FanSpeed`, samt `[Startup]`, `[Defaults]`, `[Settings]`, `[PreSuspendedMode]`.

IMPLEMENTERET:
- Ny `Helpers/Hardware/AfterburnerProfileController.cs`: finder AB-installationen, parser profilerne (nummer, power %, clock-offset, fan %, om der er en V/F-kurve) og `Apply(n)` = stop AB + start `MSIAfterburner.exe -profileN` (no-op hvis samme profil allerede er anvendt).
- `HardwareMonitorAgent`: opretter controlleren, logger "MSI Afterburner integration ready …", og skifter profil ved **spil-session start/slut** (ikke ved alt-tab): spilprofil mens en session kører, ellers valgfri idle-profil (0 = skift ikke). Ingen profil-skift når funktionen er slået fra.
- Config/VM/persistens: `GpuUndervoltEnabled`, `GpuUndervoltGameProfile` (1), `GpuUndervoltIdleProfile` (0). De tidligere `GpuUndervoltTargetTempC/MaxClockMhz/MinClockMhz` er fjernet igen (clock-ceiling-idéen blev målt forkert).
- `GpuPowerSavingState`: nye felter `AfterburnerAvailable`, `AfterburnerProfile`; `UndervoltActive` = profil > 0. `UndervoltTargetC` fjernet.
- UI (Hardware-siden, nyt kort "GPU undervolt"): master-toggle, to profilkomboboxer ("Profil mens spillet kører" / "Profil når intet spil kører", første valg = "Skift ikke"), målt status (profil/clock/temp/watt/fan, 1 s opdatering) + "Opdater status". Profiler læses af UI'en direkte (kræver ikke admin); kun selve skiftet gør.
- Lokalisering (en+da): 13 nøgler opdateret/tilføjet (`GpuUndervolt*`, inkl. `GpuUndervoltNoProfile`); `GpuUndervoltMinClock*` fjernet.

MÅLT EFTER INTEGRATIONEN (PoE kører, profil 1 = 94 % power):
- Agenten genstartede AB (ny PID) og skrev `MSI Afterburner profile 1 applied.`
- power limit 164,5 W, draw ~73 W, **79 °C**, 1200 MHz, 90 % udnyttelse — mod 83-84 °C ved 175 W før. Bruger: "79 grader uden tab af FPS".
- Effekten kommer fra AB-profilens power limit; en ægte V/F-kurve (lavere spænding ved samme clock) kan give yderligere ~3-5 °C uden FPS-tab — den skal laves i AB's kurve-editor (Ctrl+F) og gemmes som fx profil 2, som derefter vælges i IconGrid.

VERIFIKATION: build 0 fejl/0 advarsler (02:46:46) → deployet; `C:\IconGrid\IconGrid.dll` matcher `bin\Debug\...` (02:46:46). App + agent + AB kører.

NÆSTE MULIGHEDER:
- Fase 2 (valgfri): skrive `VFCurve`-blobbet selv, så kurven kan redigeres i IconGrid. Kræver reverse-engineering af hex-formatet + backup/validering + AB-genstart; ikke gjort (risikabelt).
- CPU-fund fra samme session (endnu ikke handlet): `Minimal processortilstand` står på **99 %** i "AMD Ryzen Balanced", så Ryzen 5 3600 kører 4,20 GHz / 1,40 V næsten konstant → højere idle-temp end nødvendigt. `powercfg`-presets (min state 5 %, boost-off, EPP) er den sikre, driver-fri vej hvis vi vil bygge CPU-sektionen senere.
- `HardwareMonitorAgent.cs` er nu ~2900 linjer (over grænsen); næste extraction-kandidat: flyt GPU/Afterburner-profilbeslutningen til en coordinator-helper.

IKKE COMMITTET/PUSHET — kræver eksplicit bruger-godkendelse.


## FASE 2: ICONGRID GENERERER SELV UNDERVOLT-KURVEN (24-11-2026, verificeret)

`VFCurve`-formatet blev dekodet fra en rigtig profil: **8-byte header (uint32 flags, uint32 pointCount) + N poster à 12 bytes = (uint32 flags, float spænding_mV, float clock_MHz)**. Denne maskine: 128 gyldige punkter, 450 → 1243,75 mV i 6,25 mV-trin, clock 405 → 1950 MHz, blob = 3224 bytes (268 poster, resten nul).

IMPLEMENTERET:
- `AfterburnerProfileController.WriteUndervoltCurve(targetClockMhz, profileNumber)`: læser stock-kurven fra `[Defaults]`, finder første punkt med clock >= mål, **flader alle punkter derfra ud til mål-clocken** (klassisk Afterburner-undervolt), koder blobbet igen med samme header/record-count, laver backup af profil-filen til `%APPDATA%\IconGrid\afterburner-profile-backup-<tid>.cfg`, og erstatter/opretter `[ProfileN]` (PowerLimit/Fan/FanMode kopieres fra `[Defaults]` → power limit forbliver 100 %, det er kurven der undervolter).
- Agenten håndterer en request-fil `%APPDATA%\IconGrid\gpu-undervolt-request.json` (`{"targetClockMhz":1750,"profile":9}`): skriver kurven, anvender profilen (stop AB + start `-profile9`) og rapporterer i state-filen. UI'en skriver requesten, sætter game-profil = 9 og slår undervolt til.
- UI: nyt felt i GPU-undervolt-kortet: slider 1500-1950 MHz + knap "Skriv kurve og anvend" (profil 9 er reserveret til IconGrid-genererede kurver, så brugerens 1-5 aldrig overskrives).
- Bug fundet undervejs: `ReadSectionValues` returnerede den sidste sektion i stedet for den ønskede ("No stock V/F curve was found") → rettet med separat `result`/`current`.

VERIFICERET (dekodet profil-filen efter kørsel):
- Stock: 900 mV→1725, 912,5 mV→1755, 925 mV→1770, max 1950 MHz.
- Profil 9: 912,5 mV→**1750**, alt derover→**1750**, max **1750 MHz**, power limit 100 %.
- Agenten skiftede til profil 9 (ny AB-PID), state: `profile=9`, `MSI Afterburner profile 9 applied.`; backup oprettet (13.490 bytes).
- Build 0 fejl/0 advarsler (03:05:13) → deployet, DLL matcher.

VIGTIGT FOR TEST: ved 4K/60 Hz + vsync kører PoE ofte 1100-1300 MHz, hvor kurven ikke er aktiv (stock-spændingen er der allerede lav) → temperaturgevinsten ses først når kortet *vil* booste højt: vsync fra + tungt map. Målt i den nuværende scene: 84 °C / 81 W / 1245 MHz — dvs. uændret, fordi kortet ikke er oppe i det område kurven ændrer, og fordi køleren stadig er loftet ved ~80 W.

IKKE COMMITTET/PUSHET — kræver explicit bruger-godkendelse (gælder også alt fra Afterburner-integrationen).


## GPU: STOCK-RESTORE + SAMSPIL MED BAGGRUNDS-PROFILEN (24-11-2026)

BRUGERFUND: "alt er slået fra, men jeg er på 79 °C i stedet for 83-85". Screenshot viste `grænse 164 W` (ikke 175 W).

ROOT CAUSE: Agenten havde tidligere lagt Afterburner-profil 1 (94 % = 164,5 W) ind. Vores "slå fra" gendannede kun NVML-grænsen (den vi selv skrev) — Afterburners profil blev siddende i driveren. Oveni: NVML-controllerens baseline blev fanget ved agent-start, hvor AB allerede havde sat 164 W, så "restore" gik til 164 W. Endelig blev `gpu-power-saving-state.json` kun skrevet når mindst én funktion var aktiv → panelet viste gamle tal.

FIXES:
1. `AfterburnerProfileController.WriteStockProfile(8)`: skriver en stock-profil fra `[Defaults]` (PowerLimit 100 %, uændret V/F-kurve) til **profil 8**.
2. Agenten: når undervolt slås FRA og en profil fra os stadig er aktiv → skriv + anvend profil 8 (stock) automatisk. Efter genstart læses forrige state (`afterburnerProfile`) så oprydningen også sker der.
3. State-filen skrives nu **altid** (1 Hz), så panelet viser sandheden uanset om funktionerne er slået til.
4. SAMSPIL: når undervolt er slået TIL, ejer Afterburner power limit → baggrunds-lav-strømprofilen træder til side (frigiver sin NVML-grænse) i stedet for at kæmpe med AB. Baggrundsbesparelse opnås i stedet via "Profil når intet spil kører" (fx profil 1 = 94 %, eller en dedikeret lav-profil).
5. Benchmark: mens en måling kører, springes baggrundsprofilen over (`benchmarkRunning`), så resultater ikke afhænger af, om spillt har fokus (den fejl gjorde den første A/B ugyldig: 48 fps / p99 122 ms fordi PoE var i baggrunden).

VERIFICERET: build 0/0 → deployet. Med alt slået fra: **power limit 175 W, 84 °C**, state `profile=8, limit=175 W`. Profil-filen har nu `[Profile8]` (PowerLimit=100 + stock-kurve) og `[Profile9]` (genereret undervolt).

TEST-MÅLER (bygget samme session): `GpuBenchmarkRunner` + "GPU test"-kort på Hardware-siden. Kører 10 s warmup + valgt måletid (30/60/120 s) pr. profil, PresentMon-CLI til frame times, NVML til temp/watt/clock. Resultat pr. gennemløb: gns. FPS, **p99 frame time**, **% frames > 20 ms** (misset 60 Hz-frist), max-temp, gns. watt/clock. A/B venter 15 s mellem passene. Resultater gemmes i `%APPDATA%\IconGrid\gpu-benchmark-results.json` (seneste 20) og vises i kortet. Første validering: 58,4 fps / p99 21,5 ms / 2,0 % > 20 ms / max 80 °C / 73 W / 1138 MHz på 30 s.

PENDING (næste session):
- UI-hint på undervolt-kortet skal nævne stock-profil 8 + at baggrundsprofilen ikke kører mens undervolt er aktiv (teksten siger stadig kun at AB styrer).
- Kør en gyldig A/B (profil 1 vs 9) **med PoE i fokus** i et tungt map; brug % frames > 20 ms + p99 + max-temp som beslutningsgrundlag.
- `HardwareMonitorAgent.cs` er nu ~3100 linjer (over grænsen) — næste extraction: GPU/Afterburner/benchmark-beslutningerne ud i coordinators.

IKKE COMMITTET/PUSHET — hele Afterburner-integrationen + benchmark står uncommitted og kræver explicit godkendelse.


## DIAGNOSE: GPU HOTSPOT 105 °C = KØLERKONTAKT (24-11-2026) + hotspot-overvågning

BRUGER-MÅLING (HWiNFO): edge 84,1-84,6 °C, **hotspot 104,4-105,0 °C** (delta ~20 °C), fan 2240-2254 RPM, VDDC 0,706 V, kun ~79 W.

FORKLARING: Turing throttler på **hotspot** (~105 °C), ikke på edge. Derfor: pstate P0, mem 7000 MHz, 79 W af 175 W, men clock låst ~1290-1305 MHz med `sw_thermal_slowdown = Active`. Kortet kan ikke afsætte mere end ~80 W → ingen termisk overskud → GPU Boost kan aldrig bruge de 96 W ledige effekt, og 1750-1900 MHz nås ikke i spil (kun i menuer/lette scener). Delta ~20 °C ved 80 W peger på pasta-pump-out/montagetryk/pad-tykkelse trods repaste for 6 mdr. siden. Software (power limit, undervoltkurve) kan ikke hjælpe — hverken grænse eller kurve er aktiv i den tilstand.

IMPLEMENTERET (hotspot i IconGrid):
- `HardwareSnapshotCollector`: læser LHM-sensoren "Hot Spot" (fallback "junction") for GPU'en, beregner `GpuHotspotDeltaC` (hotspot − edge) og lægger begge i `HardwareMonitorSnapshot` → `monitor-state.json` (`gpuHotspot`, `gpuHotspotDeltaC`). Verificeret live: idle `gpu=36 °C hotspot=49 °C delta=13`.
- Nyt opt-in felt i GPU-undervolt-kortet ("Hotspot-overvågning"): toggle `GpuHotspotAlarmEnabled` (default **fra**) + live linje "Hotspot 105,0 °C · GPU 84,6 °C · forskel 20,4 °C".
- Venlig advarsel (amber) når alarmen er slået til OG hotspot ≥ 100 °C eller delta ≥ 15 °C: forklarer at kortet throttler ~105 °C, at hverken power limit eller undervolt hjælper, og anbefaler PTM7950/tykkere pasta + friske pads.
- Config/VM/persistens udvidet med `GpuHotspotAlarmEnabled`; 7 nye lokaliseringsnøgler i en+da.

VERIFIKATION: build 0 fejl/0 advarsler (03:55:40) → deployet; hotspot/delta findes nu i `monitor-state.json`.

IKKE COMMITTET/PUSHET — hele sessionens arbejde (Afterburner-integration, kurve-generator, benchmark, stock-restore, hotspot-overvågning) kræver stadig explicit godkendelse.


## EFTER REPASTE (24-11-2026) — kølerkontakten var årsagen, bekræftet

Brugeren repastede GPU'en (meget lidt pasta, men nok). Målt live efter repaste, PoE kørende i baggrunden:

| Måling | Før repaste | Efter repaste |
|---|---|---|
| GPU edge | 83-84 °C | **69-70 °C** |
| GPU hotspot | 104-105 °C | **83 °C** |
| Hotspot-delta | ~20 °C | **14 °C** |
| Power draw | ~79 W | ~106 W |
| Core clock | ~1290-1305 MHz | **1560-1680 MHz** |
| Fan | 74 % / 2240 RPM | **52 %** |
| Throttle | `SW Thermal Slowdown = Active` | **ingen throttle / ingen power cap** |
| Power limit | 125 W (baggrundsprofil) | 175 W (stock, profilen er slået fra) |

KONKLUSION: Kølerkontakten/pastaen var hele årsagen. Kortet bruger nu MERE effekt (~106 W mod ~79 W) og kører MEGET højere clock (1680 MHz mod ~1300 MHz), men er alligevel 14-15 °C køligere, og blæseren kører langsommere (52 % mod 74 %). Hotspot-delta 14 °C er acceptabelt (ideelt 10-12 °C); PTM7950/eftertryk kan give de sidste par grader, men er ikke nødvendigt nu.

AKTUEL STATUS: `GpuPowerSavingEnabled=false`, power limit 175 W, temp target 83 °C (stock). Baggrundsprofilen er altså ikke aktiv. Med den nu sunde køling kan den slås til igen, hvis man vil have lavere temp i baggrunden, men den er ikke længere nødvendig for at undgå throttling.

Desuden: `IconGrid_3f4s4uqs_wpftmp.csproj` (transient WPF-buildfil) er slettet, og `*_wpftmp.csproj` er tilføjet til `.gitignore`.

IKKE COMMITTET/PUSHET.


## FIX: clock-loft virkede ikke efter repaste (24-11-2026)

BRUGER-RAPPORT: GPU power saving "virker ikke mere" — MHz drosler ikke ned når spilvinduet ikke er aktivt. Status viste "loft 2100 MHz" mens profilen var aktiv.

ROOT CAUSE: `NvmlGpuLimitController.GovernClocks` startede ved `cap` = kortets max SM clock (2100 MHz) og sænkede kun loftet når temperaturen var OVER temp-målet. Efter repasten ligger kortet på 63-69 °C (under 70 °C-målet), så governoren sænkede aldrig noget — den låste reelt clocks til 2100 MHz (ingen cap).

FIX:
- Nyt felt `GpuPowerSavingClockLimitPercent` (30-100 %, default **60**) i ConfigModel/state/persistens/MainViewModel.
- Nyt "Clock-loft"-slider i GPU-strømbesparelse-kortet (Gaming overlay-siden).
- Agenten regner cap ud: `maxSmClockMhz * percent / 100` (60 % af 2100 = **1260 MHz**) og sender den som `maxCeilingMhz` til `GovernClocks`; 100 % = ingen ekstra cap.
- `GovernClocks` re-seeder nu loftet til det nye cap med det samme når cap'en ændres (før ventede den på 150 MHz-trin).
- Clock-governoren kører nu også når temperaturgrænsen er slået fra (så clock-loftet altid virker); 100 % + temp fra => ReleaseClockLimit.
- Loftet frigives straks når spillet får fokus (uændret `ReleaseClockLimit`-sti).

VERIFICERET LIVE (første fix, deployet 04:35:54): PoE i baggrunden => power limit **125 W**, clock **1200-1260 MHz**, temp **63 °C**, forbrug **73 W**, fan **53 %**. Før fix: 1650 MHz / 69 °C / 105 W.

DEL 2 — bruger satte 40 % og så ingen forskel:
- ÅRSAG: Når cap'en ændrede sig, opdaterede `GovernClocks` den interne `_clockCeilingMhz` til det nye cap, men det efterfølgende "uændret"-tjek sprang `nvmlDeviceSetGpuLockedClocks` over. Driveren beholdt derfor den gamle lås (1260 MHz), mens status viste det nye tal (840 MHz). Det lignede en skjult driver-grænse, men var en manglende driver-skrivning.
- FIX: `_clockCeilingDirty` sættes når profil/cap/floor ændres, bypasser "uændret"-tjekket, og ryddes først efter en vellykket `nvmlDeviceSetGpuLockedClocks`. `ReleaseClockLimit` rydder også flaget.
- VERIFICERET LIVE uden genstart (PoE hele tiden i baggrunden, deployet 04:46:35):
  - **40 % → 840 MHz / 55 W / 56 °C**
  - **60 % → 1260 MHz / 72 W / 59 °C**
  - 30-100 % følger slideren; 100 % = ingen ekstra cap. Effektivt minimum er `MinClockCeilingMhz = 400` (sliderens 30 % giver ca. 630 MHz).
- PoE skal **ikke** lukkes for test — det er netop baggrundstilstanden, der måles.

IKKE COMMITTET ENDNU — afventer eksplicit godkendelse til endnu et commit/push.

