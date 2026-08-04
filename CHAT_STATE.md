# Session State - FPS / ETW

## Current date
- Monday, August 3, 2026

## Current status
- ETW FPS works now in IconGrid.
- Path of Exile DX12 shows stable and accurate FPS in the gaming overlay.
- The decisive fix on this machine was:
  - add the user to `Brugere af ydelseslog` (`Performance Log Users`)
  - restart Windows or log out/in
- The gaming overlay UI/settings flow is now integrated and committed.
- The overlay FPS path now prefers a direct shared-memory `live` FPS path from the native ETW worker.
- File-based FPS state still exists as fallback / diagnostics, but it is no longer the preferred hot path.
- The gaming overlay now shows a single live FPS number again.
- The gaming overlay row also has:
  - fixed-width FPS layout so the number does not shift when digits change
  - subtle `|` dividers between monitor sections
  - green FPS accent styling
- `README.md` has been updated to describe the real ETW/FPS architecture instead of the old roadmap wording.

## Night session findings (Aug 2-3, 2026)
- PoE2 background FPS drop (60 → 30) was confirmed to be the game's own `Background FPS Cap`, not an IconGrid bug.
- After setting PoE2 `Background FPS Cap = 60`, the overlay shows ~55-63 FPS even when the game window is not active.
- Confirmed in trace.log:
  - `Source=PrimaryApi`, `DXGI>0`, FPS 54-61 also with `Foreground=False` after the cap change
- New confirmed regression in our pipeline: spike filter holds 32-34 FPS while raw FPS is already 54-60.
  - Pattern: `rawFps=59 lastStable=32 ... Foreground=False` -> `Spike filtered (jump)`
  - The generic `rawFps > lastStable * 1.5` jump rule treats the 30→60 background recovery as a spike.
  - The existing "stronger primary signal" bypass does not fire in this case.
- New confirmed session-lifecycle issue:
  - `native-fps-state.json` shows `targetPid=0`, `debugMessage="No running process matched the current launch session."`
  - while ETW still receives raw events (`preFilterDxgiEventCount=7961`, `etwRunning=true`).
  - This is the stale saved launch-session ownership described in `.local-state/fps-etw.md`.
- New confirmed file-state collision in hot path:
  - `[NativeFpsAgentRunner] Native FPS agent state read failed: ... being used by another process` (twice in a row).
- New confirmed atomic-write failure:
  - `Hardware monitor agent failed: System.UnauthorizedAccessException: Access to the path is denied.`
  - `MoveFile(...) HardwareMonitorAgent.cs:1653` from `WriteFpsState:1644` in `Run:383`.
- Traced timeline (Aug 3, ~01:39-01:41): POE1 on PID 16284 working well (57-61 FPS), then background cap behavior and spike-hold patterns visible in the same session.
- Traced timeline (Aug 3, ~02:03): no game running, native agent stuck at `targetPid=0` with ETW still delivering raw events.

## Proven result
- Before group membership:
  - non-elevated `StartTraceA => 5`
  - elevated `StartTrace => 0`, but `EnableTraceEx2(...) => 1450`
- After adding the user to `Brugere af ydelseslog` and rebooting:
  - ETW starts successfully
  - graphics providers enable successfully
  - frame events arrive
  - FPS is displayed correctly

## Latest log proof
- `logs/native-fps-state.json` shows:
  - `etwStartAttemptCount = 1`
  - `etwStartFailureCount = 0`
  - `lastEtwError = ""`
  - `dxgKrnlEnabled = true`
  - `dxgiEnabled = true`
  - `d3d9Enabled = true`
  - `etwEventsReceived = true`
- `logs/trace.log` shows repeated live samples with:
  - `EtwRunning=True`
  - `Events=True`
  - `DxgKrnlEnabled=True`
  - `DxgiEnabled=True`
  - live FPS values such as `97`, `98`, `110`

## Key conclusion
- The blocker was not anti-cheat and not bad FPS math.
- The blocker was ETW permissions / access state on this Windows machine.
- `Performance Log Users` membership was the missing requirement for this setup.
- ETW capture is solved from a functionality standpoint.
- The main remaining FPS issue is now display responsiveness, not ETW access.

## Product implication
- If IconGrid should work automatically for normal users, installer or first-run setup should:
  - request admin once
  - add the current user to `Performance Log Users`
  - require logout/login or reboot before FPS ETW is expected to work

## Current technical state
- The largest display-latency step after ETW capture has now been reduced by adding shared memory between the native FPS worker and the overlay-side reader.
- The current effective hot path is:
  - native ETW worker
  - shared-memory live FPS publish
  - launcher / gaming overlay direct read
- File-based state still exists for fallback, diagnostics, and broader monitor flow integration.
- The remaining FPS issue is now mostly about the last bit of feel for very tiny drops/spikes, not about ETW access or the main IPC direction.

## Known follow-ups (not yet fixed)
- Spike filter recovery: allow the overlay to follow raw FPS when the source is strong (`Source=PrimaryApi`, `DXGI>0`) and raw FPS is stable for 2-3 samples, instead of holding `lastStable`.
- Session lifecycle: when a config target is set but not matchable, actively relock to the same exe name/path among running processes instead of staying in "No running process matched".
- Session cleanup: when a game exits or switches, clear `lockedExecutableName/Path/RootPid` and config-target runtime metadata together so the next launch starts clean.
- File-state collision: use `FileShare.ReadWrite` or retry on the reader side, or reduce `native-fps-state.json` dependence in the hot path in favor of shared memory.
- Atomic write: `MoveWithRetry` should also handle `UnauthorizedAccessException` with a short retry.

## Relevant files
- `.local-state/fps-etw.md`
- `.local-state/ui-launcher.md`
- `.local-state/current-focus.md`
- `README.md`
- `Helpers/Hardware/HardwareMonitorAgent.cs`
- `Helpers/Hardware/NativeFpsAgentRunner.cs`
- `Helpers/Hardware/FpsMeter.cs`
- `Helpers/Launcher/SystemMonitor.cs`
- `Views/Launcher/GamingOverlayWindow.xaml`
- `Views/Launcher/GamingOverlayWindow.xaml.cs`
- `Views/Settings/Pages/GamingOverlayPage.xaml`
- `Views/Settings/Pages/GamingOverlayPage.xaml.cs`
- `Views/Settings/Pages/TestPage.xaml`
- `Views/Settings/Pages/TestPage.xaml.cs`
- `Native/FpsAgent/src/main.cpp`

## Latest commits
- `b25e98e` `Tighten ignore rules for local ETW and tooling artifacts`
- `ed634f0` `Add native ETW FPS pipeline and overlay monitor integration`
- `9bc508e` `Polish gaming overlay UI and add FPS setup/settings flow`
- `165d7e8` `Improve ETW FPS overlay responsiveness and shared-memory live path`

## Good next steps
- check_architecture_rules is now GREEN (zero violations): MainWindow 968, MainViewModel 998, HardwareMonitorAgent 1432, SystemMonitor 501 all PASS. The architecture refactor goals are complete; future refactors are optional polish, not limit-driven.
- Optional polish: A4 (rebind XAML/code-behind directly to LauncherLayoutMeasurements.* and remove delegate properties) — further MainViewModel line reduction beyond the 1200 limit is no longer needed.
- Update ARCHITECTURE_RULES.md (Recent Good Examples) with LauncherLayoutMeasurements.cs, PawnIoWarningController.cs, MonitorPollingController.cs, the Fase B helper classes, and the C1-C4 partial-class files (MainViewModel.Localization/Settings/Layout/Items).
- validate the shared-memory live path across more real games
- decide whether to add frametime as a companion metric
- decide whether to keep simplifying the fallback FPS layers around the live path
- document installer/setup requirements more formally

## Architecture status (2026-08-03)

- `check_architecture_rules` tool added to the icongrid-notes MCP server (file size + method-count guardrails).
- First report shows 3 VIOLATIONs:
  - `Views/Launcher/MainWindow.xaml.cs`: 3473 lines (limit 1000) — move layout engine to Helpers/Launcher/WindowLayoutEngine.cs.
  - `ViewModels/MainViewModel.cs`: 1940 lines (limit 1200) — extract UI/layout measurement state.
  - `Helpers/Hardware/HardwareMonitorAgent.cs`: 1680 lines (limit 1500) — keep worker focused.
- `Helpers/Launcher/SystemMonitor.cs`: passes (within limit).
- AGENT.md now requires running `check_architecture_rules` before large refactors and at session end.

## Security cleanup (Aug 3, 2026)

- GitHub repo is PUBLIC (confirmed via anonymous `git ls-remote`) — treat everything in it as world-readable until made private.
- Both signing certs (`certificate/icongrid-dev.pfx`, `certificate/vs-icongrid.pfx`) were tracked in git and publicly exposed.
- Fixed: `git rm --cached` removed both certs from tracking (files kept on disk), `.gitignore` hardened with `*.pfx` / `*.key` / `*.pem` / `.env` / `.env.*`, and a repo security checklist added to ARCHITECTURE_RULES.md.
- Commits pushed: `4665b9f` (remove signing certs + security checklist), `834424d` (harden .gitignore).
- Verified: `git ls-files` no longer contains any `.pfx`/`.key`/`.pem`/`.env` files; certs still exist locally (`certificate/`).
- IMPORTANT: certs remain in git history — if they were ever used for release signing, ROTATE them. Making the repo private closes exposure permanently.
- Resolved: repo set to PRIVATE by user (2026-08-03) — closes exposure of old certs permanently, including git history.
- Resolved: old local MCP server copy `C:\Users\THXMAN\Documents\Cline\MCP\notes-server` DELETED (2026-08-03). The active server runs directly from the repo (`tools/mcp-notes-server/`) and is registered in Cline MCP config with args `E:/IconGrid-GitHub/tools/mcp-notes-server/src/index.js`.
- Documented in AGENT.md: MCP server runs directly from repo, fresh-clone setup (npm install + config snippet), and note that the old copy is deleted. Commit `e99f095` pushed.

## MCP server end-to-end test (Aug 3, 2026)

- Verified the MCP server works from a fresh-clone of the repo (HEAD e99f095): created a git worktree, ran `npm install` (93 packages, no errors), and ran a Node stdio JSON-RPC test.
- initialize: OK — serverInfo `icongrid-notes-server` v0.1.0, protocolVersion `2024-11-05`.
- tools/list: OK — all 6 tools returned (list_notes, read_note, search_notes, update_note, check_architecture_rules, check_version_consistency).
- tools/call list_notes: OK — returned CHAT_STATE.md + 7 .local-state notes using ICONGRID_WORKSPACE env var.
- Conclusion: the repo copy (`tools/mcp-notes-server/`) is complete and self-sufficient after `npm install`; nothing else is needed for a fresh machine.

## MCP server test tooling (Aug 3, 2026)

- Added reusable e2e test script `tools/mcp-notes-server/test/e2e.mjs` (spawns server over stdio, verifies initialize, tools/list, list_notes, check_version_consistency, check_architecture_rules).
- Added `tools/mcp-notes-server/TESTING.md` guide (automated test, manual test in Cline, terminal smoke test). Test results are logged in CHAT_STATE.md, not a separate file.
- Added `npm test` script to `tools/mcp-notes-server/package.json`.
- Verified: `node test/e2e.mjs` from the repo passes all 13 checks.
- Commits pushed: `194c716` (test tooling).

## MCP server update_note fix (Aug 3, 2026)

- Problem: `update_note` failed 4x this session with "Invalid JSON argument" even with minimal JSON
  (`{"note":"CHAT_STATE","operation":"append","heading":"x","content":"y"}`).
- Diagnosis: the server itself was healthy — `node tools/mcp-notes-server/test/e2e.mjs` passed all checks
  over stdio (the same transport Cline uses). The failure is client-side (Cline's strict JSON-schema
  validation against `inputSchema`): `heading` is described as "Required for append" but is NOT in the
  `required` array of `update_note`, so Cline can reject the call before it ever reaches the server.
- Fix (all committed in this repo, server runs directly from repo so commit = active):
  - Added two dedicated tools with precise `required` fields, robust against strict client validation:
    - `append_to_note` (required: `note`, `heading`, `content`)
    - `replace_in_note` (required: `note`, `find`, `content`)
  - Kept `update_note` as a LEGACY alias (backwards compatibility), now with:
    - tightened `isValidUpdateArgs` (requires `heading` for append / `find` for replace)
    - specific error messages via `buildUpdateArgsError` (e.g. "heading is required when operation=append")
    - `console.error('[update_note] args received:', ...)` logging on stderr so we can see what Cline
      actually sends.
- e2e test extended (`tools/mcp-notes-server/test/e2e.mjs`, 31 checks): calls `update_note` (append +
  replace), `append_to_note`, `replace_in_note` over stdio against a throwaway note
  (`.local-state/__e2e_update_test.md`, created + deleted by the test so no real notes are modified),
  verifies the schemas' `required` arrays, and checks error behavior for missing `heading`/`find`.
  Result: `=== ALL TESTS PASSED ===` (exit 0).
- `TESTING.md` updated: manual test now recommends `append_to_note` / `replace_in_note`, and documents
  `update_note` as a legacy alias.
- Next session: if Cline still rejects `update_note`, use `append_to_note` / `replace_in_note` instead;
  the server logs every `update_note` call to stderr for diagnostics.

## Refactor Fase 1 (Aug 3, 2026) — MainWindow layout engine

- Created `Helpers/Launcher/WindowLayoutEngine.cs` with the full window-arranging engine (BuildSlots, MatchWindows*, ResolvePreset, BuildOrderedAssignments, CollectCandidateWindows, NormalizeRectToWorkArea, SlotsClose, etc. + Win32 externs).
- `MainWindow.ArrangeWindowsFromPreset` now delegates to the engine; `TrySaveLayout` and `AutoDetectAndEnableDynamicLayout` also use engine helpers.
- Result: MainWindow.xaml.cs reduced 3473 → 2677 lines; methods ~166 → ~145 (per check_architecture_rules).
- Build: succeeded, 0 warnings / 0 errors.
- Backups in `_backups/refactor-2026-08-03/Fase1/` (orig, refactored, engine .bak).
- Awaiting: manual UI test (arrange presets, save/load layout) + user approval before commit checkpoint.

## Refactor Fase 2 (Aug 3, 2026) — WindowStateStore

- Created `ViewModels/Launcher/WindowStateStore.cs` owning all window-position persistence (main window, settings, gaming overlay, floating icon): 8 fields + 8 methods + ApplyConfig/ResetFloatingPosition/ApplyToSettingsState.
- MainViewModel now delegates to `_windowStateStore` for all TryGet/Save positions; ApplyConfig/ApplyDefaultSettingsState/SaveSettingsToConfig wired through the store.
- Honest result: MainViewModel reduced only 1940 → 1886 lines (~54). The position store is a small domain; the gain is SRP/cohesion, not line count.
- Build: passed, 0 warnings/0 errors. Backups in `_backups/refactor-2026-08-03/Fase2/`.
- NOTE: To get MainViewModel under 1200 lines, the big remaining win is extracting the layout-measurement block (ContentAreaHeight/CalculateContentAreaHeight/NotifyContentHeightChanged/header height) into a LauncherLayoutMeasurements-like class — that is a larger future step, not done in this fase.
- Awaiting: manual UI test (window/floating-icon/overlay position persistence survives restart) + user approval before commit.

## Refactor Fase 3 (Aug 3, 2026) — FpsNormalizer

- Created `Helpers/Hardware/FpsNormalizer.cs` with `FpsNormalizerState` (owns last-stable-FPS/COD-anchor/signal state + `Normalize`) and public `FpsNormalizationResult` record.
- `HardwareMonitorAgent` (static class) now holds `private static readonly FpsNormalizerState FpsNormalizer = new();` and the two call sites use `FpsNormalizer.Normalize(...)` + `FpsNormalizer.ResetBaseline()`; the 6 local state variables and the 216-line `NormalizeNativeFps` method (plus `FpsNormalizationDebugState` record) were removed.
- Result: **HardwareMonitorAgent.cs reduced 1680 → 1432 lines — now UNDER the 1500 limit** (the only one of the three large files to pass). Methods ~49 → ~46.
- Build: passed, 0 warnings/0 errors. Backups in `_backups/refactor-2026-08-03/Fase3/`.
- Awaiting: manual UI/FPS test (gaming overlay FPS still stable, spike filter still works) + user approval before commit.

## Refactor session complete (Aug 3, 2026)

- All three large-file refactors done, tested, committed and pushed:
  - Fase 1 `a96c46b`: MainWindow.xaml.cs 3472→2677 lines (WindowLayoutEngine.cs)
  - Fase 2 `0888884`: MainViewModel.cs 1940→1886 lines (WindowStateStore.cs)
  - Fase 3 `0827321`: HardwareMonitorAgent.cs 1680→1432 lines (FpsNormalizer.cs) — NOW under the 1500 limit, the only one of the three to fully pass.
- `check_architecture_rules` end-state: HardwareMonitorAgent passes; MainWindow (2677) and MainViewModel (1886) still exceed limits.
- Remaining future win for MainViewModel: extract the layout-measurement block (ContentAreaHeight/CalculateContentAreaHeight/header-height/Notify*) into a `LauncherLayoutMeasurements`-like class — larger step, not done.
- ARCHITECTURE_RULES.md updated with the three new focused classes as Recent Good Examples.
- Backups of all phases in `_backups/refactor-2026-08-03/` (orig/refactored/committed per phase).

## Refactor Fase A (Aug 3, 2026) — LauncherLayoutMeasurements

- Created `ViewModels/Launcher/LauncherLayoutMeasurements.cs` owning all layout-measurement state and calculations: `_headerHeight`, `_iconRowSpacing`, `_lastRowPaddingAdjust`, `_isIconPanelExpanded`, constants (TileSlotWidth/BaseTileSlotHeight/padding/FixedSettingsHeight), `CalculateContentAreaHeight`, `ContentAreaMaxHeight`, `ContentHostHeight`, `ContentMinWidth/ContentWidth/ContentMaxWidth`, `WindowDesiredWidth/Height`, `IconMargin`, `SetHeaderHeight`, `SetIconRowSpacing`, `SetLastRowPaddingAdjust`, `ApplyMeasurementState`, `NotifyWorkAreaChanged`, `RefreshLayoutMeasurements`, `NotifyContentHeightChanged` + own INotifyPropertyChanged wiring. Follows the LauncherLayoutState/WindowStateStore pattern (bool-returning Set methods).
- MainViewModel now delegates all layout measurement to `_layoutMeasurements` (thin bound properties + PropertyChanged forwarding subscription), and `ApplyConfig`/`ApplyDefaultSettingsState`/`SaveSettingsToConfig` use `ApplyMeasurementState`/`IconRowSpacing`/`LastRowPaddingAdjust`/`CalculateContentAreaHeight`.
- Honest result: MainViewModel reduced 1886 → 1799 lines (~87), ~70 → ~69 methods. Not yet under the 1200 limit — the biggest remaining wins are the settings-persistence block (ApplyConfig/SaveSettingsToConfig/ApplyDefaultSettingsState) and the localization-bindings block.
- Build: passed both Debug and Release, 0 warnings/0 errors.
- `check_architecture_rules` end-state: MainViewModel 1799, MainWindow 2677, HardwareMonitorAgent 1432 OK, SystemMonitor 501 OK.
- Manual UI test: PASSED (window sizing, row spacing, last-row padding, icon panel expand/collapse, overlay height switch, header measurement, settings persistence after restart).
- Backups in `_backups/refactor-2026-08-03/FaseA/` (MainViewModel.cs.orig, MainViewModel.cs.refactored, LauncherLayoutMeasurements.cs.bak).
- Pending: commit checkpoint (awaiting user approval), optional A4 (rebind XAML/code-behind directly to LayoutMeasurements and remove delegate properties), then Fase B (MainWindow reduction).

## Refactor Fase B (Aug 3, 2026) — MainWindow reduction

- Goal: reduce `MainWindow.xaml.cs` (2677 lines, limit 1000, ~145 methods) toward ~1000.
- All 6 extractions done, each followed by build (0 warnings / 0 errors), `check_architecture_rules`, and manual UI test after functional steps:
  - **B1** `Helpers/Launcher/LauncherWindowInterop.cs` — Win32 interop (WndProc, DWM, UIPI drop filters, WM_SETICON, EnumWindows, PickIconDlg) + dynamic taskbar/tray icon lifecycle. MainWindow: 2677 → 2182 lines (~145 → ~114 methods).
  - **B2** `Helpers/Launcher/LauncherWindowModeController.cs` — full/floating mode, window sizing/positioning, work-area clamping, auto-hide sliding. MainWindow: 2182 → 2087 lines (~114 → ~111).
  - **B3** `Helpers/Launcher/DevOverlayController.cs` — dev-inspector overlay hover metadata. MainWindow: 2087 → 1867 lines (~111 → ~101).
  - **B4** `Helpers/Launcher/LauncherDragDropHelper.cs` — file/launcher-item drag & drop. MainWindow: 1867 → 1726 lines (~101 → ~99).
  - **B5** `Helpers/Launcher/LauncherShortcutActions.cs` — shortcut administration, icon helpers, Windows-shortcut loading. MainWindow: 1726 → 1433 lines (~99 → ~89).
  - **B6** `Helpers/Launcher/LayoutMenuController.cs` — layout-menu population/checks, save/rename/delete layout, layout-card highlighting, slot/link buttons, TrySaveLayout. MainWindow: 1433 → 1441 lines (constructor wiring; methods ~89).
- `check_architecture_rules` end-state (Aug 3, 2026):
  - `Views/Launcher/MainWindow.xaml.cs`: 1441 lines (limit 1000) | ~89 methods — still VIOLATION.
  - `ViewModels/MainViewModel.cs`: 1799 lines (limit 1200) | ~69 methods — still VIOLATION.
  - `Helpers/Hardware/HardwareMonitorAgent.cs`: 1432 lines — PASSES.
  - `Helpers/Launcher/SystemMonitor.cs`: 501 lines — PASSES.
- Manual UI tests passed per phase; final full smoke test PASSED (layout menu, layout cards/IG marking, gaming overlay, icon/theme, tray, floating/full, drag & drop, shortcut menus).
- Backups in `_backups/refactor-2026-08-03/FaseB/B1/` … `B6/`.
- COMMITTED: `6ba1105` "refactor: extract MainWindow helpers into six focused launcher classes (Fase B)" (8 files, +2207/-1339). Pushed: PENDING (not pushed yet).
- Next steps:
  - Continue MainWindow reduction toward 1000: remaining clusters are PawnIo warning window (~6 methods), monitor timer, window closing/exit, tab toggle, remaining event handlers.
  - Continue MainViewModel reduction toward 1200: biggest remaining wins are the settings-persistence block (ApplyConfig/SaveSettingsToConfig/ApplyDefaultSettingsState) and the localization-bindings block.
  - Consider wiring LayoutMenuController's layout-preset/slot/link handlers fully into MainWindow (currently MainWindow still hosts those handler methods directly; the controller owns menu population/checks and TrySaveLayout) — follow-up for a later fase if needed.

## Refactor Fase C (Aug 3, 2026) — C1-C4: MainViewModel PASSES the 1200-line limit — ALL files green

- Continued the MainViewModel reduction this session; each step followed by build (0 warnings / 0 errors) and `check_architecture_rules`. MainViewModel is a `partial class`; the four biggest self-contained blocks moved to dedicated partial-class files:
  - **C1** `ViewModels/MainViewModel.Localization.cs` — 110 localization binding properties + `NotifyLocalizationPropertiesChanged` + `ApplyLocalizationState`. MainViewModel: 1798 → 1557 lines.
  - **C2** `ViewModels/MainViewModel.Settings.cs` — `ApplyConfig`, `ApplyDefaultSettingsState`, `LoadConfiguredState`, `SaveSettingsToConfig`. MainViewModel: 1557 → 1452 lines.
  - **C3** `ViewModels/MainViewModel.Layout.cs` — all layout properties/state (LayoutPreset, SkipMinimized, CurrentMonitorOnly, ReserveIconGridSlot, IconGridSlot, SaveLayout/RenameLayout/DeleteLayout, notify helpers). MainViewModel: 1452 → 1253 lines.
  - **C4** `ViewModels/MainViewModel.Items.cs` — tabs/items administration (AddTab/ClearCurrentCategory/RenameTab/RemoveTab/RemoveItem/RenameItem/UpdateItemIcon/MoveItemWithinCategory/HandleFileDrop/LaunchItem/RememberFpsTarget/CreateCustomShortcut/SetIcon/SaveItemsToFile/LoadItemsFromFile/MaybeMigrateItemsFromLegacy). MainViewModel: 1253 → **998 lines**.
- `check_architecture_rules` end-state (Aug 3, 2026, after C4): **"All checked files are within ARCHITECTURE_RULES.md limits."** — MainWindow 968, MainViewModel 998, HardwareMonitorAgent 1432, SystemMonitor 501 all PASS. First time zero violations.
- Commit: see git log (C1-C4 committed together this session; the B7-B10 MainWindow refactor commit `acd5b49` and the MCP fix `42ce00b` are already pushed).

## Refactor Fase B (Aug 3, 2026) — B7-B10: MainWindow PASSES the 1000-line limit

- Continued the MainWindow reduction this session (after the `update_note` MCP fix, commit `42ce00b`); each step followed by build (0 warnings / 0 errors), `check_architecture_rules`, and manual UI test (user-confirmed "alt virker"):
  - **B7** `Helpers/Launcher/PawnIoWarningController.cs` — PawnIo-missing warning window lifecycle, retry timer, SystemMonitor subscription. MainWindow: 1441 → 1359 lines (~89 → ~82 methods).
  - **B8** `Helpers/Launcher/MonitorPollingController.cs` — 2s hardware-polling DispatcherTimer, re-entrancy guard, enable/disable callback wired into LauncherWindowModeController. MainWindow: 1359 → 1316 lines (~82 → ~80 methods).
  - **B9** Removed duplicate layout methods from MainWindow — `PromptAndSaveLayout`, `TrySaveLayout`, `PopulateLayoutMenu`, `UpdateLayoutMenuChecks`, `LayoutPresetMenuItem_Click`, `LayoutSlotButton_Click`, `LayoutLinkButton_Click`, `RenameLayoutMenuItem_Click`, `DeleteLayoutMenuItem_Click`, `ArrangeWindowsFromPreset` now delegate to the existing `LayoutMenuController` (which already owned the full implementations from B6). MainWindow: 1316 → 1020 lines (~80 → ~77 methods).
  - **B10** Removed dead code `AutoDetectAndEnableDynamicLayout` (~50 lines, not referenced anywhere, not bound in XAML). MainWindow: 1020 → **968 lines** — now UNDER the 1000-line limit!
- `check_architecture_rules` end-state (Aug 3, 2026, after B10):
  - `Views/Launcher/MainWindow.xaml.cs`: 968 lines (limit 1000) — **PASSES** (first time).
  - `ViewModels/MainViewModel.cs`: 1799 lines (limit 1200) — still the ONLY remaining VIOLATION.
  - `Helpers/Hardware/HardwareMonitorAgent.cs`: 1432 lines — PASSES.
  - `Helpers/Launcher/SystemMonitor.cs`: 501 lines — PASSES.
- Pre-B7 MainWindow state is preserved in git at commit `42ce00b` (backup/rollback point).
- Next session: MainViewModel reduction toward 1200 (settings-persistence block ApplyConfig/SaveSettingsToConfig/ApplyDefaultSettingsState + localization-bindings block) is now the only remaining architecture VIOLATION, followed by the optional A4 (rebind XAML directly to LayoutMeasurements).
- Commit: see git log (B7-B10 committed together with the MCP fix this session — `42ce00b` = MCP fix, and the B7-B10 refactor commit follows).

## Carousel view mode (Aug 3-4, 2026)

- Added a new "Carousel" view mode for launcher shortcut icons, controlled by a toggle on the GenvejsIkoner settings page (localized "Karruselvisning"/"Carousel view").
- Grid mode unchanged: UniformGrid Columns=IconsPerRow with vertical scroll. Carousel mode: single row, max 4 icons visible, horizontal scrolling in the same viewport (window neither taller nor wider).
- `LauncherLayoutMeasurements` now owns persisted `IconViewMode` ("Grid"/"Carousel"); `CalculateContentAreaHeight` forces rows=1 in carousel so the window does not grow taller. ContentWidth/ContentMinWidth unchanged.
- Persistence chain updated: ConfigModel.IconViewMode, MainViewModelConfigState, MainViewModelSettingsState, MainViewModelSettingsPersistence, ApplyConfig/ApplyDefaultSettingsState. MainViewModel exposes IconViewMode + IsCarouselViewMode (two-way binding for the toggle).
- `LauncherGrid.xaml` now has two ScrollViewers: GridScrollViewer (vertical, PanningMode=VerticalOnly) and CarouselScrollViewer (horizontal, PanningMode=HorizontalOnly). Shared IconTileTemplate preserves drag-and-drop and context menu exactly. Hardcoded 4px scrollbar style removed; both scrollbars now use the shared SettingsPageScrollBarStyle / new SettingsPageScrollBarHorizontalStyle (thin, theme-aware).
- `StartsideStyles.xaml` gained SettingsPageScrollBarThumbHorizontalStyle + SettingsPageScrollBarHorizontalStyle (horizontal sibling of the existing vertical style).
- Mouse wheel scrolls horizontally in carousel via CarouselScrollViewer_PreviewMouseWheel routing Delta to ScrollToHorizontalOffset.
- EnableContentScroll toggle controls both vertical (grid) and horizontal (carousel) scrollbars.
- Build: 0 warnings / 0 errors. `check_architecture_rules` GREEN: MainWindow 968, MainViewModel 1037, HardwareMonitorAgent 1432, SystemMonitor 501 all pass.
- Manual UI test PASSED (user-confirmed): toggle grid<->carousel, max 4 visible, horizontal wheel/drag scroll, window size unchanged, thin scrollbars, scrollbar on/off both modes, sliders, drag&drop and context menus, persistence after restart, reset-to-defaults returns to grid.
- Commit: `767e75e` "feat: add carousel view mode for launcher shortcut icons with thin theme-aware scrollbars" (13 files, +288/-84).
- Pushed to origin: `5eaf211..767e75e main -> main` (2026-08-04).
- Next steps: update ARCHITECTURE_RULES.md Recent Good Examples is still optional.

## Shortcut grid/carousel layout polish (Aug 4, 2026)

- Commit `eb7fe34` "feat: polish shortcut grid/carousel layout — tight zero-gap rows, symmetric padding, compact tiles, carousel scrollbar reserve" adds polish on top of the carousel feature (`767e75e`): tighter zero-gap rows, symmetric padding, compact tiles, and carousel scrollbar reserve in the grid/carousel layout.
- Pushed to origin: `767e75e..eb7fe34 main -> main` (2026-08-04).
- Working tree clean; no other unpushed commits.

## Carousel fix: exactly 4 icons visible (Aug 4, 2026)

- User reported the carousel showed ~4.5 icons per row. Root cause: the horizontal StackPanel let each icon use its natural width (label 130 + margin 28 = 158px) while the viewport was ~708px inner width → ~4.44 icons.
- Fix: carousel cells now use the same width as grid UniformGrid columns. Added `CarouselCellWidth(iconsPerRow)` to `LauncherLayoutMeasurements.cs` = (ContentWidth - 40) / iconsPerRow; new `CarouselHorizontalPadding = 40` constant so carousel inner width (20+20 padding) matches grid (20+20). Changed carousel Border padding from `23,20,23,20` to `20,20,20,20` in `LauncherGrid.xaml`, and bound the carousel ContentPresenter Width to `CarouselCellWidth` (via new `MainViewModel.CarouselCellWidth` property + IconsPerRow notification).
- Result: ContentWidth 748 − 40 = 708 → cell = 708/4 = 177px = exactly a grid column. Now exactly 4 icons visible in carousel, same spacing as grid.
- Build: 0 warnings/0 errors. `check_architecture_rules` GREEN. Manual UI test PASSED (user-confirmed).
