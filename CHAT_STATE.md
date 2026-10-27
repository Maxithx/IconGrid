# IconGrid — Short-term Memory

> **Full session history:** `.local-state/session-history.md` (gitignored, searchable via `search_notes`)

## Current date
October 23, 2026

> **Seneste session:** se nederst — "Session findings (2026-10-27) — drevbogstaver på Hardware → Disks i alfabetisk orden". C: øverst, derefter D:, E:, ... + bogstaver i chippen sorteret (DLL 27-10 01:10). Forrige: Disks-sektion (2026-10-25, DLL 25-10 22:25).

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

