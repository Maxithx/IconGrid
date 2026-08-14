# IconGrid — Short-term Memory

> **Full session history:** `.local-state/session-history.md` (gitignored, searchable via `search_notes`)

## Current date
Wednesday, August 12, 2026

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
