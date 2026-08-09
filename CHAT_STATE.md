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

## Session 2026-08-04 (eftermiddag): Friske FPS-tests — POE1, POE2, COD, Division 2


- **Formål:** Test FPS pipeline med friske logs — ren test, ingen kodeændringer.
- **Metode:** IconGrid + gaming overlay kørende. Hvert spil testet for korrekt FPS-visning og target-stabilitet når andre vinduer aktiveres.

### Test-resultater (bruger-rapporteret, ~16:45-16:58 CEST)

| Spil | FPS ved fokus | Ved andet vindue aktivt | Recovery |
|------|--------------|------------------------|----------|
| **POE 1** | ✅ Hurtig & korrekt | ✅ Korrekt — intet target-tab | N/A |
| **POE 2** | ✅ Hurtig & korrekt | ⚠️ Target tabes ~1 sek, så OK | ~1 sek |
| **COD** | ✅ Hurtig & korrekt | ⚠️ Target tabes ~1 sek, så OK | ~1 sek |
| **Division 2** | ✅ Hurtig & korrekt | ❌ FPS slukker helt | Ved gen-fokus |

### Nøgle-observationer
- **Alle fire spil** viser korrekt FPS når spillet er i fokus — ETW pipeline + target-lock fungerer.
- **POE 1** er guld-standarden: sticky-target holder perfekt, intet tab ved vindues-skift.
- **POE 2 / COD** har identisk adfærd: ~1 sek target-tab når man klikker på Stifinder/Start-menu, derefter recovery.
- **Division 2** er det værste: FPS slukker helt når spillet mister fokus, og kommer først tilbage når spilvinduet aktiveres igen.

### Mulig root cause
- Når brugeren klikker på et andet vindue, skifter foreground PID. Sticky-target logikken bør holde fast i spil-processen, men gør det ikke konsekvent.
- POE 1's proces overlever foreground-skift bedre — muligvis fordi spillet ikke ændrer render-adfærd ved de-fokus.
- Division 2's proces opfører sig markant anderledes ved de-fokus (suspend? minimer? skjuler render-vindue?), hvilket får native FPS agent til at give helt op.

### Trace-log status
- `native-fps-state.json` (16:58 CEST): viser post-shutdown state — target låst til `SystemSettings.exe` (PID 19440), `etwRunning=false`, `fpsValue=0`. Dette er forventet da hardware monitor allerede var lukket ned.
- `fps-state.json` (16:59 CEST): `fpsStatus="--"`, ingen live FPS — også forventet post-shutdown.
- `trace.log` (16:55-16:59 CEST): viser at hardware monitor forsøgte at låse til `SystemSettings.exe` via visible-game fallback, men den blev rejected som non-game. Ingen spil-relaterede events i trace fordi test-sessionen allerede var afsluttet da loggen blev læst.
- **Vigtigt:** Brugerens verbale test-rapport er den primære datakilde for denne session — trace-loggen blev læst for sent (efter shutdown).

### Dokumentation
- `.local-state/fps-etw.md` opdateret med detaljeret test-sektion under `Fresh FPS tests — 2026-08-04`.
- `.local-state/current-focus.md` opdateres med denne sessions resultater.
- `.local-state/next-session-fps-test.md` markeres som FULDFØRT.

### Næste skridt (fremtidig session)
- Undersøg sticky-target / foreground-skift logik for POE2/COD ~1 sek glitch.
- Undersøg Division 2's proces-adfærd ved de-fokus — hvorfor slukker FPS helt?
- Overvej "grace period" i native FPS agent: hold target i X sekunder efter foreground-skift før opgivelse.
- Kør nye tests med trace-log læst **mens spillene stadig kører** (ikke post-shutdown).

## Session 2026-08-04 (eftermiddag, del 2): Sticky-target grace period implementeret


- **Ændring:** Sticky-target grace period tilføjet i native FPS agent + diagnostisk logging i både C# og C++ lag.
- **Begrundelse:** Test-resultater fra tidligere i dag viste at POE2/COD taber target i ~1 sek ved foreground-skift, og Division 2 taber permanent. Root cause: native agent (`PollLockedTarget`) ryddede `g_targetPid` når matched events stoppede, selvom processen stadig var alive.

### Filændringer
- **`Native/FpsAgent/src/main.cpp`:**
  - Ny konstant `kStickyGracePeriodMs = 3000`
  - Ny atomic `g_lastMatchedEventTicksUtc` — opdateres i `EtwCallback` ved hvert matched event
  - `PollLockedTarget()` omskrevet: når events stopper men process lever, behold target i op til 3000ms. Log "Sticky-target grace period" ved aktivering og "EXPIRED" ved udløb.
  - Forbedret log når process rent faktisk dør: inkluderer process-navn og "Reason: IsProcessAlive=false"
- **`Helpers/Hardware/HardwareMonitorAgent.cs`:**
  - Tilføjet trace-log i sticky-path: `"Sticky-target: no game foreground detected. Holding current game PID..."` med NativeTargetPid og EtwRunning status

### Byg-status
- C# `IconGrid.csproj`: ✅ Byg succes (0 fejl, 0 advarsler)
- Native `FpsAgent.vcxproj`: ⚠️ **Skal bygges manuelt** — MSVC toolchain ikke tilgængelig fra PATH. Byg med Visual Studio: Release x64.
- `check_architecture_rules`: ✅ GRØN

### Dokumentation
- `.local-state/fps-etw.md`: Ny sektion "Sticky-target grace period fix — 2026-08-04" med detaljer om alle ændringer og forventet effekt.
- `.local-state/current-focus.md`: Indeholder allerede test-resultater og next-steps fra tidligere.

### Næste skridt (næste session)
1. Byg native FPS agent (Release x64) i Visual Studio.
2. Start IconGrid med nye builds → gaming overlay.
3. Test POE 1, POE 2, COD, Division 2 — verificér at:
   - POE 1 stadig er perfekt (regression-test)
   - POE 2 / COD ~1 sek glitch er elimineret
   - Division 2 ikke længere slukker permanent (grace period buffer)
4. Læs trace.log mens spillene kører for at se "Sticky-target grace period" beskeder.
5. Dokumentér resultater i CHAT_STATE.md + fps-etw.md.
6. Hvis fixet virker: commit.

## Session 2026-08-04 (aften): Division 2 / POE 2 de-fokus analyse + 'hold sidste FPS' plan


- **Fund:** Division 2 og POE 2 nedsætter render-rate når unfocused — IKKE en FPS pipeline fejl. Nvidia overlay bekræfter: viser ~10 FPS for Division 2 i baggrunden.
- **Sammenfatning:**

### Test-resultater med de-fokus
| Spil | Ved fokus (ETW) | Ved de-fokus (ETW) | Nvidia overlay |
|------|----------------|--------------------|---------------|
| POE 1 | 60-120 FPS, PrimaryApi | 60-120 FPS, PrimaryApi | — |
| POE 2 | 60-100 FPS, PrimaryApi | Events falder, `--` | ~halv FPS |
| COD | Normal | `DXGI=1` per poll, `--` | — |
| Division 2 | 30 FPS, PrimaryApi | `DXGI=1` per poll, `--` | ~10 FPS |

### Hvorfor vores viser `--` men Nvidia viser tal
- Vores: `kMinimumRollingSamples=2` i `main.cpp` — kræver ≥2 frames i 50ms vindue
- Division 2 baggrund: ~1 frame per 500ms → 1 sample → `fpsStatus="--"`
- Nvidia: sandsynligvis længere sampling-periode eller lavere minimum

### Hvorfor spil gør dette (standard Windows/DirectX adfærd)
- Windows prioriterer foreground-vinduet til GPU-scheduling
- Mange spil engine kalder `Present()` med lavere frekvens når unfocused (strømbesparelse)
- Division 2 er et ekstremt tilfælde — dropper til ~1-2 FPS
- POE 2 er moderat — reducerer til ~halvdelen
- POE 1 er perfekt — render med fuld rate selv ved de-fokus

### Anbefalet fix: Hold sidste FPS
- **Ikke** ændre `kMinimumRollingSamples` (det beskytter mod støj)
- I `FpsMeter.cs`: når native agent rapporterer `fpsStatus="--"` men sticky-target er bekræftet og processen lever → hold sidste kendte FPS
- I overlay: vis FPS med lavere opacity når stale

### Dokumentation
- `.local-state/fps-etw.md`: Ny sektion "Division 2 / POE 2 de-fokus render-rate — 2026-08-04 (aften session)" med log-bevis og analyse

### Tidligere session (eftermiddag) opsummering
- Sticky-target grace period implementeret og bygget i native agent (3000ms)
- C# sticky-path trace tilføjet i HardwareMonitorAgent.cs
- Begge builds klar til test

## Session 2026-08-04 (aften, afslutning): FPS pipeline stabil — alle fixes testet


- **Session afsluttet med positive resultater.** Alle fire hovedfix er implementeret og testet med 8 spil.

### FPS pipeline fixes (implementeret i denne session)
1. **Sticky-target grace period** (3000ms) i `Native/FpsAgent/src/main.cpp` — forhindrer target-tab ved foreground-skift
2. **Hold sidste FPS** (`FpsMeter.cs` + `HardwareMonitorAgent.cs`) — viser sidste kendte FPS når spil stopper rendering i baggrunden
3. **Spike filter bypass** (`HardwareMonitorAgent.cs`) — når confirmed target holder, bruges raw ETW FPS
4. **MoveWithRetry crash fix** (`HardwareMonitorAgent.cs`) — fil-lock konflikter crasher ikke længere monitor loopet

### Testede spil (8 titles)
| Spil | Status |
|------|--------|
| POE 1 | ✅ Perfekt sticky-target |
| POE 2 | ✅ Virker, reel FPS svinger i baggrunden |
| COD | ✅ Virker, presenteret vs displayed adfærd |
| Division 2 | ✅ Virker, sidste FPS vises ved de-fokus |
| Stumble Guys | ✅ Virker |
| RHYTHM SPROUT Demo | ✅ Virker |
| Yuzu | ⚠️ Emulator overcount, normaliseret til ~60 FPS |
| Fireworks Mania | ✅ Virker |

### Dokumentation opdateret
- `README.md`: Ny "Currently tested games" sektion med tabel
- `.local-state/fps-etw.md`: Omfattende opdatering med alle test-resultater og fix-detaljer
- `.local-state/current-focus.md`: Opdateret med session resultater
- `.local-state/next-session-fps-test.md`: Markeret som FULDFØRT

### Kendte begrænsninger (ingen blokkere)
- Division 2: Kræver gen-start af IconGrid for at finde `TheDivision2.exe` (EACLaunch stjæler initial target)
- COD: Langsom opstart — intro/startup-kæde tager tid før `Present()` kaldes regelmæssigt
- POE 2: Reel FPS svinger naturligt i baggrunden (33-58), ikke et target problem
- Yuzu: Emulator, bruger DxgKrnl fallback med overcount

### Næste session muligheder (valgfrit)
- Tilføj `EACLaunch` til `IgnoredForegroundProcesses` for hurtigere Division 2 target-acquisition
- Forbedr COD startup-detection timing
- Tilføj `mscopilot` til ignored foreground processes (blev fanget som false target)
- Commit alle ændringer (bruger-godkendelse krævet)

## Session 2026-08-04 (sen aften): Hardware-siden polish — commit b2e05e2 pushet

- **FPS-pipeline commit `b2e05e2` pushet** — sticky-target grace period + hold last FPS + spike filter bypass + crash fix (5 filer, 302 insertions). Commit fra sidste session, der aldrig blev eksekveret, er nu gennemført og pushet til origin/main.
- **Motherboard model-tekst afskåret fix** — `Views/Settings/Pages/HardwarePage.xaml`: fast `Width="180"` ændret til `Width="*"` (model tager al plads), BIOS-kolonne `Width="Auto"`, + `TextWrapping="Wrap"` på både model og BIOS. Identisk mønster som CPU/GPU/Memory sektionerne.
- **RAM-slot detektion via SMBIOS Type 17** — ny `Helpers/Hardware/SmBiosMemoryParser.cs`: parser rå SMBIOS via `GetSystemFirmwareTable("RSMB")`, læser DeviceLocator (fx DIMM_A1), BankLocator og Size (0 = tom slot). `HardwareInfoProvider.LoadMemory()` eksponerer `SlotsUsed` (fx "2/4"). Ny "Slots"-tile i Memory-sektionen (mellem Modules og Voltage). Lokalisering: `HardwareSlotsLabel`.
- **Søg-model link i Motherboard-sektionen** — ny `HardwareSearchButtonStyle` (link-stil: accent-farve, transparent, hover-opacity 0.7). "Søg model"/"Search model"-link (`HardwareSearchBoardLabel`) ved siden af Model-label. `SearchBoardButton_Click` i `HardwarePage.xaml.cs` åbner `https://www.google.com/search?q=<producent>+<model>` via `Process.Start(UseShellExecute = true)`.
- **Verificeret:** `dotnet build` grønt (0 fejl / 0 advarsler) både csproj og sln. `check_architecture_rules` GRØN.
- **VS Code fejl-artefakt:** "CS0103: nameof does not exist" ved linje 223-224 i `MainViewModel.Localization.cs` er en forældet DocumentCompiler-cache, ikke en ægte fejl (faktisk build 0 fejl). Løsning: Developer: Reload Window.
- **MCP notes-server:** `append_to_note` kald afvist af klientens JSON-schema-validering trods korrekt server-schema — samme klient-side begrænsning som dokumenteret tidligere. CHAT_STATE.md opdateret direkte via File API.
- **Næste trin:** commit + push af hardware-siden ændringer (afventer bruger-godkendelse — godkendt i denne session).

## Session 2026-08-05: Gaming overlay theme + transparent background

- **FPS lys/mørk tema-fix:** FPS-tælleren viste lys grøn (`#7CFF6B`) på hvid baggrund ved lyst Windows-tema. Fix: ny `OverlayFpsLightBrush` (`#16A34A` mørk grøn) + `OverlayFpsTextStyle` med `IsLightTheme`-DataTrigger. `GamingOverlayWindow.xaml`.
- **Transparent baggrund toggle:** Ny `GamingOverlayTransparentBackground` indstilling på Gaming overlay-siden med `ToggleSwitchStyle` (samme som startsiden). Baggrunden (`OverlayRoot` + popup-paneler) bliver gennemsigtig når ON.
- **Tekstfarve + farvepalet + custom farvevælger:** Ny `GamingOverlayTextColor` (hex, default `#FFFFFF`). Hybrid-løsning: hvid standard + 7 swatches (Hvid/Sort/Gul/Blå/Grøn/Pink/Cyan) + Windows `ColorDialog` via "Brugerdefineret farve...". Alle overlay-tekst-elementer (labels, FPS, divider, knapper) bruger `GamingOverlayTextBrush` når transparent er aktiv.
- **Dynamisk transparent toggle:** Ny `GamingOverlayAutoTransparentBackground` — "Kun gennemsigtig under spil". `SystemMonitor.IsInGame` (baseret på `FpsStatus != "--"`).
- **FIX uafhængige toggles:** De to toggles påvirker ikke længere hinanden (OR-logik): `Effective = TransparentBackground || (AutoTransparentBackground && IsInGame)`. Auto-toggle er altid synlig; tekstfarve-sektion vises når enten er ON via `GamingOverlayAnyTransparentEnabled`.
- **Selected swatch markering:** Valgt farve markeres med accentfarvet 3px ring via DataTrigger på hver swatch. Custom farver normaliseres til `#RRGGBB` (ToHex) så markeringen også matcher.
- **Filer ændret:** `Views/Launcher/GamingOverlayWindow.xaml`, `Views/Settings/Pages/GamingOverlayPage.xaml` + `.cs`, `Models/ConfigModel.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/MainViewModel.Settings.cs`, `ViewModels/Settings/*` (ConfigState/SettingsState/Persistence), `Helpers/Launcher/SystemMonitor.cs`.
- **Build-status:** 0 fejl, 0 advarsler ✅.
- **Arkitektur:** `check_architecture_rules` GRØN ✅.
- **Detaljer:** Se `.local-state/color-system.md` (afsnit: Gaming overlay FPS light theme, Gaming overlay transparent background, Gaming overlay text color picker, Dynamic transparent overlay when in game, Auto-transparent toggle, FIX Independent transparent toggles, Color swatch selected marker).
- **Næste skridt:** Ingen planlagte — brugeren bekræftede "det spiller rigtig godt. så er vi færdig".

## Session 2026-08-05: Fix — gaming overlay hænger gennemsigtig efter spil lukkes

- **Bug:** Med "Transparent while in game" (AutoTransparentBackground) ON forblev gaming overlayets baggrund gennemsigtig i Windows efter spillet blev lukket.
- **Root cause:** `SystemMonitor.IsInGame` var koblet direkte til `FpsStatus != "--"` (FPS-tælleren). Når spillet lukkede, kunne FPS-data blive hængende i et par sekunder, så `GamingOverlayTransparentBackgroundEffective` forblev true og baggrunden kom ikke tilbage.
- **Fix:** Afkoblede `IsInGame` fra FpsStatus i `Helpers/Launcher/SystemMonitor.cs`:
  - `IsInGame` er nu et separat felt (`_inGame`) sat via `SetInGame(...)`.
  - Primær kilde: native FPS agents `TargetPid` fra shared memory — når spillet lukker sætter agenten `TargetPid=0` øjeblikkeligt.
  - Fallback: hvis shared memory ikke er tilgængelig (agent ikke kørende), bruges live FPS-signaler.
  - Når agenten kører og eksplicit rapporterer `TargetPid=0`, sættes `IsInGame=false` — FPS-feedets decay får ikke lov at holde transparency kørende.
- **Build:** 0 fejl / 0 advarsler. `check_architecture_rules` GRØN.
- **Filer ændret:** `Helpers/Launcher/SystemMonitor.cs`.
- **Opfølgende fixes (samme session):**
  - **Flyt overlay in-game:** `DragMove()` fejler på vinduer med `AllowsTransparency="True"`. `GamingOverlayWindow.xaml.cs` bruger nu Win32 `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)` i stedet, så overlayet kan flyttes også når den er gennemsigtig in-game.
  - **Tekstfarve opdateres ikke når AutoTransparent tændes under spil:** `GamingOverlayTransparentBackground`- og `GamingOverlayAutoTransparentBackground`-setterne manglede `OnPropertyChanged` for `GamingOverlayTextBrush`/`GamingOverlayTextColorHex`. Tilføjet i `ViewModels/MainViewModel.cs`, så valgt hvid farve vises med det samme.

## 2026-08-05 Session: Overlay scale 100-150% + README

Ændret Gaming Overlay scale-grænser fra 0.7-1.2 til 1.0-1.5 (7 filer):
- Views/Settings/Pages/GamingOverlayPage.xaml: Slider Minimum=1.0, Maximum=1.5
- Views/Launcher/GamingOverlayWindow.xaml: Popup-slider Minimum=1.0, Maximum=1.5
- ViewModels/MainViewModel.cs: Setter clamp Math.Max(1.0, Math.Min(1.5, value))
- ViewModels/MainViewModel.Settings.cs: Config-clamp 1.0-1.5 (løfter gamle gemte værdier <100% op til 100%)
- ViewModels/Settings/MainViewModelConfigState.cs: Clamp 1.0-1.5
- Views/Launcher/GamingOverlayWindow.xaml.cs: GetEffectiveScale cap Math.Min(1.5, ...) så 150% virker på 4K
- README.md: Ny dokumentation under 'Gaming overlay monitor' - anbefalet Fullscreen Windowed, Exclusive fullscreen advarsel, opløsningskompensationstabel (4K/1440p/1080p) og DPI-sammenhæng

Build: 0 fejl, 0 advarsler. check_architecture_rules: grønt.
Næste skridt: Manuel UI-test - bekræft at 150% slider på 1440p giver overlay på størrelse med launcher, og at eksisterende gemt lav skala auto-løftes til 100%.

## Overlay scale cap fix (100% effektiv max)

Efter brugerfeedback: GamingOverlayWindow.xaml.cs GetEffectiveScale() cap ændret fra Math.Min(1.5, ...) til Math.Min(1.0, ...).

Ny adfærd:
- 4K @ slider 150% → 100% effektiv (capped, aldrig større end launcher-design)
- 1440p @ slider 150% → 100% effektiv (matcher launcher)
- 1080p @ slider 150% → 75% effektiv

Dette gør monitor-skift forudsigeligt: overlay på 1440p @ 150% falder automatisk tilbage til 100% når vinduet flyttes til 4K i stedet for at blive 150% stort.

README.md kompensationstabel opdateret med '100% (capped)' for 4K-kolonnen.
Build: 0 fejl, 0 advarsler.

## Session 2026-08-05 (nat): Game resolution + per-resolution overlay scale + midlertidig layout-snapshot

Funktioner implementeret (afvent commit):

1. **Automatisk opløsningsskift for spil** — `LauncherItem.GameResolution` (per-genvej, feks. "2560x1440"), `DisplayResolutionService` (Win32 ChangeDisplaySettingsEx mod primaer skaerm), `LauncherItemLaunchManager` skifter før launch + `WatchProcess` genopretter ved exit. Crash-fallback watchdog (30s). RestoreAfterExit-toggle (global) i config-kæden.
2. **Game Resolution-siden** (ny) — per-genvej opløsnings-dropdown + kategori-filter (default Games, brugeren kan vælge kategori). Læsbare dropdowns (sort på hvid). Sidebar-knap i SettingsWindow.
3. **Standard overlay scale per opløsning** — `GamingOverlayResolutionScales` dictionary i config + `MainViewModel.Overlay.cs` (Get/Set/Remove + indbyggede defaults: 4K=100%, 1440p=135%). GamingOverlayWindow lytter på `SystemEvents.DisplaySettingsChanged` og anvender default ved opløsningsskift + åbning. Overlay-slider auto-gemmer ved drag-slip. Settings-UI på GamingOverlayPage viser 1920x1080 – 4096x2160 med slider pr. opløsning.
4. **Midlertidig layout-snapshot** — `WindowLayoutSnapshotService` (ny): `Capture()` samler vinduer via EnumWindows/GetWindowRect (inkl. IconGrid egne vinduer fra Application.Current.Windows), `Restore()` via SetWindowPos. LauncherItemLaunchManager tager snapshot før opløsningsskift og kalder Restore naar `DisplayResolutionService.ResolutionRestored` fyres. Rører IKKE SavedLayouts.
5. **Gaming overlay fixes** — 4K overlay scale virker (fjernet 1.0-cap i GetEffectiveScale), scale %-tekst + "Overlay scale" bruger MonitorTextStyle (sort i lyst tema, hvid/valgt farve ved transparent). README kompensationstabel opdateret.

**AABEN BUG (naeste session):** Gaming overlay flytter sig til hoejresiden naar spillet lukkes og holder ikke sin gemte position. Foreslaaet aarsag: snapshot/restore sætter overlay-vinduet via SetWindowPos med gemt rect, men overlayet bruger også WindowInteropHelper/SendMessage-drag og kan have rettet sig til monitorskift; evt. skal overlayet undtages fra snapshot-restore (det gemmer selv sin position via SaveGamingOverlayWindowPosition) eller restore skal vente til efter resolution er helt tilbage.

Architektur: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549. Build: 0 fejl / 0 advarsler.

Næste skridt: commit dette arbejde, fortsæt i ny frisk chat-session med fix af overlay-position-bug.

## Session 2026-08-05 (tidlig morgen): Fix — gaming overlay flytter sig til højre når spil lukkes

Buggen fra sidste session er nu fixet. Symptom: gaming overlay flyttede sig til højresiden når spillet lukkede og holdt ikke sin gemte position.

Root cause (bekræftet ved kodegennemgang):
- `WindowLayoutSnapshotService.Capture()` registrerede ALLE IconGrid-vinduer (inkl. gaming overlayet) via `Application.Current.Windows`.
- Når spillet lukkede → `RestoreResolution()` fyrede `ResolutionRestored` → `LauncherItemLaunchManager.OnResolutionRestored` kaldte `Restore()` med `SetWindowPos` på overlayet med den midlertidige snapshot-rect.
- Overlayet har SIN EGEN position-persistence (`TryApplySavedPosition` / `SaveGamingOverlayWindowPosition` via `LocationChanged`), så `SetWindowPos` trigger `LocationChanged` → gemte snapshot-recten som den 'rigtige' position → overlayet hoppede til en gammel/stale rect (til højre) og holdt ikke sin gemte position.
- Yderligere: `ResolutionRestored` blev fyret umiddelbart efter `ChangeDisplaySettingsEx` returnerede — restore kørte mod en endnu ikke færdig mode-transition.

Fix implementeret (2 filer):
1. `Helpers/Launcher/WindowLayoutSnapshotService.cs` — `Capture()` ekskluderer nu `GamingOverlayWindow` fra snapshot (via `w.GetType().Name == "GamingOverlayWindow"`). Vigtig detalje: ALLE IconGrid-vinduer er tool-windows (`ShowInTaskbar="False"`), så et `WS_EX_TOOLWINDOW`-tjek ville fjerne alle og gøre snapshot ubrugelig — derfor filter efter type-navn. Launcher + Settings bevares i snapshot. De ubrugte `RegisterIconGridWindow`/`UnregisterIconGridWindow`-metoder er fjernet (ingen referencer).
2. `ViewModels/Launcher/LauncherItemLaunchManager.cs` — `OnResolutionRestored` kalder nu `Restore()` via `RestoreAfterResolutionSettlesAsync()` med ~400ms delay, så restore kører efter mode-transitionen er færdig.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅

Næste skridt (manuel test):
1. Start IconGrid, placer gaming overlayet et bestemt sted
2. Start spil med GameResolution sat (fx 4K → 1440p)
3. Bekræft overlayet + vinduer flytter korrekt under spillet
4. Luk spillet → bekræft overlayet bliver hvor det var + launcher/settings-vinduer vender tilbage
5. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 2): Fix — automatisk overlay scale-skift ved opløsningsskift virkede ikke

Brugerrapport: Med Windows 3840x2160 og overlay scale 100%, spillet sat til 2560x1440 med scale 150% — gaming overlayet skiftede IKKE automatisk til 150% ved spilstart. Men når brugeren rørte scale-slideren i overlay-UI'et, hoppede den til 150%.

Root cause:
- `DisplayResolutionService.TrySetResolution()` kalder `ChangeDisplaySettingsEx(CDS_UPDATEREGISTRY)`, hvorefter `SystemEvents.DisplaySettingsChanged` fyres.
- `GamingOverlayWindow.SystemEvents_DisplaySettingsChanged` kaldte `ApplyResolutionDefaultScale()` MED DET SAMME, men `EnumDisplaySettings(ENUM_CURRENT_SETTINGS)` returnerede STADIG den gamle opløsning (3840x2160) fordi mode-transitionen ikke var færdig.
- `GetGamingOverlayScaleForResolution("3840x2160")` returnerede 100% (1.0) → `_gamingOverlayUiScale` forblev 1.0 → ingen visuel ændring.
- Når brugeren rørte slideren, satte de manuelt `GamingOverlayUiScale=1.5` → `ApplyWindowSize()` kørte → `GetEffectiveScale()` = 1.5 * (2560/3840) = 1.0 → overlayet sprang til korrekt størrelse ('hoppede til 150%').

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- `SystemEvents_DisplaySettingsChanged` er nu debounced med en `DispatcherTimer` på 400ms (`DisplayChangeSettleDelayMs`), så `ApplyResolutionDefaultScale()` først kører når mode-transitionen er færdig og `EnumDisplaySettings` returnerer den NYE opløsning.
- Ny `HandleDisplayChangeSettled` callback: stopper timeren, læser aktuel opløsning og anvender dens default scale.
- Timeren stoppes/ryddes i `GamingOverlayWindow_Closed`.
- Samme 400ms settling-vindue som snapshot-restore-fixet (konsistent timing).

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅

Kombineret med det tidligere fix i denne session (gaming overlay position + 400ms snapshot-restore delay): 3 filer ændret → `Helpers/Launcher/WindowLayoutSnapshotService.cs`, `ViewModels/Launcher/LauncherItemLaunchManager.cs`, `Views/Launcher/GamingOverlayWindow.xaml.cs`.

Næste skridt (manuel test):
1. Windows 4K, overlay scale 100%, spil-genvej med GameResolution 2560x1440 + scale 150%
2. Start spillet → overlay skal automatisk springe til 150% når opløsningen skifter
3. Luk spillet → overlay + vinduer skal vende tilbage, overlay skal beholde position
4. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 3): Fix — overlay scale er nu reel fysisk skala (kompensation fjernet)

Brugerrapport (del 2): Ved 1440p med 150% valgt var overlayet IKKE skaleret op i størrelse, selvom 150% stod valgt. Kun når brugeren rykkede slideren til fx 140% begyndte den at skalere korrekt. Brugeren forventer også at overlayet skalerer tilbage til 4K's valgte scale når spillet lukker.

Root cause:
- `GamingOverlayWindow.GetEffectiveScale()` kompenserede for opløsning: `scale = GamingOverlayUiScale * resolutionFactor`, hvor resolutionFactor = min(1, screen/3840). Ved 1440p → 0.667, så 150% (1.5) × 0.667 = 1.0 → OVERLAYET ÆNDREDE SIG IKKE FYSISK. Når brugeren rykkede slideren til 140% gav 1.4 × 0.667 = 0.93 — en synlig (dog forkert) ændring.
- Den indbyggede default 1440p=135% eksisterede KUN fordi kompensationen gjorde 135% × 0.667 ≈ 90% fysisk — designet til at matche 4K 100%.

Fix implementeret (2 filer + README):
1. `Views/Launcher/GamingOverlayWindow.xaml.cs` — `GetEffectiveScale()` returnerer nu `GamingOverlayUiScale` DIREKTE (slideren = reel fysisk skala; 100% = designstørrelse 720x44, 150% = 1.5x). `ReferenceWidth`/`ReferenceHeight`/`TryGetCurrentScreen`/`Forms`-alias fjernet (ubrugt).
2. `ViewModels/MainViewModel.Overlay.cs` — `GetBuiltInOverlayScaleDefault` returnerer nu 100% for ALLE opløsninger (1440p=135% legacy-default fjernet; den gav kun mening med kompensationen). XML-doc opdateret.
3. `README.md` — 'Overlay scale and resolution compensation'-sektion erstattet med 'Overlay scale': slider = reel fysisk skala, samme % = samme størrelse på alle opløsninger; per-opløsnings-defaults skifter automatisk ved spil-start/exit.

Slutresultat for brugerens scenarie:
- Windows 4K, overlay 100% → spillet starter 2560x1440 → overlay læser 1440p's gemte scale (fx 150%) efter 400ms debounce → overlayet bliver FYSISK 1.5x stort.
- Spillet lukker → vinduerne/snapshot gendannet efter 400ms → opløsning tilbage til 4K → `DisplaySettingsChanged`-debounce → overlay læser 4K's gemte scale (fx 100%) → skalerer tilbage.
- Overlay-positionen røres aldrig (snapshot ekskluderer overlayet).

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Diff (hele sessionen): 5 filer ændret → Helpers/Launcher/WindowLayoutSnapshotService.cs, ViewModels/Launcher/LauncherItemLaunchManager.cs, Views/Launcher/GamingOverlayWindow.xaml.cs, ViewModels/MainViewModel.Overlay.cs, README.md + CHAT_STATE.md

Næste skridt (manuel test):
1. Windows 4K + overlay 100% → spil-genvej GameResolution 2560x1440 + scale 150% → start spil → overlay skal blive FYSISK 1.5x større (ikke bare '150% vises')
2. I spillet: flyt overlay-slideren til et nyt tal → størrelsen skal ændres umiddelbart og proportionelt
3. Luk spillet → overlay skalerer tilbage til 4K's valgte scale (fx 100%) + beholder position
4. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 4): Fix — overlay rykkes ud af view porten ved opløsningsskift

Brugerrapport: Når spillet starter i 2560x1440 og overlayet skalerer til 150%, rykkes overlayet ud af view porten til højre. Brugeren placerer selv overlayet top-højre og ønsker at det tvinges tilbage til top-højre ved opløsningsskift — MEN uden at låse det (brugeren skal kunne flytte overlayet frit).

Årsag: Overlay-positionen gemmes globalt (én position for alle opløsninger). Gamt som 4K top-højre (fx Left ≈ 3100 DIPs) står uden for 1440p-skrivefladen (2560), så når scale stiger til 150% og vinduet bliver større, ender det ude af view.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- Ny `EnsureOnScreen()`-metode (WPF DIPs via `SystemParameters.WorkArea`, samme konvention som `PositionRelativeToOwner`):
  - Helt ude af view porten → flyttes til top-højre hjørne (16px gap) — kun når den er ude.
  - Delvist ude (kun lidt over højre kant) → clamps tilbage i view uden at ændre vertikal position unødigt.
  - Aldrig låst: brugeren kan stadig trække overlayet frit (Win32 caption-drag uændret), og `LocationChanged` gemmer altid den nye position.
- Kaldes fra `GamingOverlayWindow_Loaded` (åbning) og `HandleDisplayChangeSettled` (efter 400ms opløsningsskift-debounce, lige efter scale er anvendt).

Bruger-scenarie nu:
- Overlay placeret top-højre på 4K → spillet starter 1440p + scale 150% → overlay skalerer op og tvinges til top-højre i 1440p-view hvis det står ude.
- Brugeren kan flytte overlayet under spillet — positionen gemmes.
- Spillet lukker → 4K igen → overlay bevarer top-højre (inden for view) → ingen unødig flytning.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Kode: `GamingOverlayWindow.xaml.cs` (EnsureOnScreen + Loaded/HandleDisplayChangeSettled). Samlet session: 6 filer (4 kode + README + CHAT_STATE).

Næste skridt (manuel test):
1. Windows 4K, overlay top-højre. Start spil med GameResolution 2560x1440 + scale 150%
2. Overlay skal skaleres til 150% OG forblive top-højre (ikke ryge ud af view)
3. Flyt overlayet under spillet → det skal kunne flyttes og positionen gemmes
4. Luk spillet → overlay tilbage til 4K's scale + position, launcher/vinduer gendannet
5. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 5): Fix — overlay ligger top-midt i spillet, skal være top-højre

Brugerrapport: Alt virker næsten, men gaming overlayet ligger i top-midten af skærmen i spillet. Placeringen skal være top-højre hjørne.

Årsag: Den tidligere `EnsureOnScreen()` tvingede kun overlayet til top-højre hvis det var HELT uden for view porten. Når brugerens gemte position lå et sted der stadig er delvist inden for 1440p-view'et (fx top-midt), blev den ikke rørt.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- `EnsureOnScreen()` erstattet med `SnapToTopRight()` — placerer ALTID overlayet i top-højre hjørne (16px gap fra `SystemParameters.WorkArea`, WPF DIPs, samme konvention som `PositionRelativeToOwner`).
- Kaldes fra `GamingOverlayWindow_Loaded` (åbning) og `HandleDisplayChangeSettled` (efter 400ms opløsningsskift-debounce) — så hver gang et spil skifter opløsning (start eller exit), nulstilles positionen deterministisk til top-højre.
- ALDRIG låst: brugeren kan flytte overlayet frit (Win32 caption-drag uændret), og `LocationChanged` gemmer altid den nye position.

Bruger-scenarie nu:
- Overlay åbner → top-højre.
- Spillet starter 1440p + scale 150% → overlay skalerer op OG ligger top-højre (aldrig top-midt eller ude af view).
- Brugeren kan flytte overlayet under spillet → position gemmes.
- Spillet lukker → 4K igen → overlay tilbage til top-højre + 4K's scale, launcher/vinduer gendannet.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Samlet session: 6 filer (4 kode + README + CHAT_STATE).

Næste skridt (manuel test):
1. Start spil med GameResolution 2560x1440 + overlay scale 150% → overlay skal ligge top-HØJRE og være 1.5x skaleret
2. Flyt overlayet under spillet → skal kunne flyttes, positionen gemmes
3. Luk spillet → overlay tilbage til top-højre + 4K's scale
4. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 6): Fix — overlay hopper til venstre ved scale-slider + settings-menu

Brugerrapport: Når man slider overlay scale fra 100% til et andet tal og derefter trykker på 'overlay settings'-ikonet, hopper hele gaming overlayet UI til venstre. Burde ikke ske.

Årsag: `ApplyWindowSize()` genberegner `Width`/`MinWidth` fra `MeasureElementWidth(...)` hver gang settings-menuen åbnes/lukkes eller scale ændres. Når den målte bredde er mindre end den aktuelle vinduesbredde, skrumper vinduet — og WPF holder Venstre kant fast, så højre kant (og det højre-justerede indhold, `HorizontalAlignment="Right"`) rykker mod venstre.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- `ApplyWindowSize()` husker nu den nuværende HØJRE kant (`previousRight = Left + Width`) før resize.
- Efter `Width`/`MinWidth` opdateres, re-forankres vinduet: `Left = previousRight - Width` — vinduet vokser/skrumper mod VENSTRE, så højre side (hvor brugeren holder overlayet top-højre) aldrig bevæger sig.
- Overlayet holder dermed placeringen når settings-panelet åbnes/lukkes eller scale-slideren ændres.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1181, HardwareMonitorAgent 1459, SystemMonitor 549 ✅

Næste skridt (manuel test):
1. Overlay top-højre → åbn overlay settings-slideren, slider scale 100%→fx 130%
2. Åbn/luk settings-panelet flere gange → overlay må IKKE hoppe til venstre, højre kant forbliver fast
3. Flyt overlayet frit → position gemmes
4. Commit + push når testen er godkendt

## Session 2026-08-05 (tidlig morgen, del 7): Feat — brugervalgt standard placering for gaming overlay (corner presets)

Brugerrapport: Gaming overlayet ligger top-midt i spillet i stedet for top-højre. Brugeren pegede på fps-overlay-1.7.0-beta (E:\ExternalTools) som inspiration: der kan brugeren VÆLGE en standard-placering fra top-hjørnerne ('Corners or drag-to-place').

Implementeret (inspireret af fps-overlay's POS_TOP_LEFT..POS_BOTTOM_RIGHT + custom):
- Ny `Models/GamingOverlayPositionPreset.cs` enum: TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Custom.
- Config-felt `GamingOverlayPositionPreset` (string, default "TopRight") + fuld persistence-kæde (ConfigModel, MainViewModelSettingsState, MainViewModelConfigState, MainViewModelSettingsPersistence, MainViewModel.Settings ApplyConfig/ApplyDefault/Save, MainViewModel backing field + prop).
- `MainViewModel.GamingOverlayPositionPreset`-prop flyttet til `ViewModels/MainViewModel.Overlay.cs` (naturligt sted; MainViewModel.cs nede på 1182 linjer).
- `GamingOverlayWindow.ApplyPositionPreset()` erstatter `SnapToTopRight()`: placerer overlayet deterministisk efter det valgte preset via `SystemParameters.WorkArea` (WPF DIPs, samme konvention som PositionRelativeToOwner), 16px gap. 'Custom' rører ikke brugerens træk-position. Kaldes fra Loaded + HandleDisplayChangeSettled (efter 400ms opløsningsskift-debounce). Overlayet er aldrig låst — brugeren kan stadig trække det frit.
- Settings-UI: dropdown (ComboBox) på GamingOverlayPage under 'Standard scale per opløsning' med lokaliserede muligheder (da + en) via OverlayPositionTitleText/OverlayPositionIntroText/OverlayPositionPresetItems/SelectedOverlayPositionPreset (forwarder til MainViewModel).

Nu-scenarie: Windows 4K + preset 'Top højre' → spil starter 1440p + scale 150% → overlay springer deterministisk til top-højre i 1440p-view (aldrig top-midt/ude af view) → spil lukker → tilbage til 4K → overlay igen top-højre + 4K's scale.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1182, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- README: ny 'Default position'-sektion under Gaming overlay monitor.
- Filændringer: Models/GamingOverlayPositionPreset.cs (ny), Models/ConfigModel.cs, ViewModels/Settings/MainViewModelSettingsState.cs, MainViewModelConfigState.cs, MainViewModelSettingsPersistence.cs, ViewModels/MainViewModel.Settings.cs, ViewModels/MainViewModel.Overlay.cs, ViewModels/MainViewModel.cs, Views/Launcher/GamingOverlayWindow.xaml.cs, Views/Settings/Pages/GamingOverlayPage.xaml (+ .cs), README.md, CHAT_STATE.md.

Næste skridt (manuel test):
1. Gaming overlay settings → vælg 'Top højre' som standard placering (eller bekræft default)
2. Windows 4K → start spil med GameResolution 2560x1440 + scale 150% → overlay skal ligge TOP-HØJRE og være 1.5x skaleret (aldrig top-midt)
3. Flyt overlayet under spillet → skal kunne flyttes; positionen gemmes
4. Luk spillet → overlay tilbage til top-højre + 4K's scale, launcher/vinduer gendannet
5. Skift preset til fx 'Bund venstre' → overlay skal flytte til bund-venstre ved næste opløsningsskift/åbning
6. Commit + push når testen er godkendt

## Session 2026-08-05 (aften, del 8): Fix — 'Default position'-ændring flytter overlayet øjeblikkeligt

Brugerrapport: Når man vælger en ny 'Default position' (fx Top Right → Top Left) i Gaming Overlay settings, flyttede overlayet sig FØRST når man lukkede og genåbnede overlayet. Det skal flytte med det samme.

Årsag: `ApplyPositionPreset()` blev kun kaldt fra `GamingOverlayWindow_Loaded` (åbning) og `HandleDisplayChangeSettled` (opløsningsskift) — aldrig når brugeren ændrede `GamingOverlayPositionPreset` i settings. `ViewModel_PropertyChanged` lyttede kun på skala-relaterede properties.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- `ViewModel_PropertyChanged` håndterer nu `GamingOverlayPositionPreset` FØRST og kalder `ApplyPositionPreset()` øjeblikkeligt (med `return` så skala-lytteren ikke overtager).
- Når brugeren vælger en ny standard-placering i dropdown'en → `MainViewModel.GamingOverlayPositionPreset` setter → `PropertyChanged` → overlayet flytter straks til den nye preset. Ingen luk/genåbn nødvendig.
- 'Custom' fungerer stadig som før: `ApplyPositionPreset()` returnerer tidligt og rører ikke den træk-te position.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1182, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Ændret: kun `Views/Launcher/GamingOverlayWindow.xaml.cs`.

Næste skridt (manuel test):
1. Åbn gaming overlay + settings-side med 'Default position' dropdown
2. Vælg 'Top venstre' med overlayet synligt → overlay skal flytte til top-venstre MED DET SAMME
3. Vælg 'Bund højre', 'Top midt' osv. → hver ændring flytter overlayet øjeblikkeligt
4. Vælg 'Brugerdefineret' + træk overlayet → position bevares
5. Confirm + commit/push når godkendt

## Session 2026-08-05 (aften, del 9): Fix — overlay sidder nu helt ude i skærmkanten/hjørnet (margin fjernet)

Brugerrapport: Gaming overlayet blev ikke placeret helt oppe i toppen af skærmen og heller ikke ude i siden/kanten. Spurgte om der var margin på.

Årsag: `ApplyPositionPreset()` brugte `const double gap = 16.0` — overlayet blev placeret 16px inde fra kanten.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs`):
- `gap` ændret fra 16.0 til 0.0 — overlayet sidder nu FLUSH mod skærmkanten/hjørnet for alle presets (TopLeft/TopCenter/TopRight/BottomLeft/BottomCenter/BottomRight).
- Kommentar tilføjet der forklarer at marginen er bevidst fjernet.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1182, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Ændret: kun `Views/Launcher/GamingOverlayWindow.xaml.cs` (1 konstant).

Næste skridt (manuel test):
1. Vælg 'Top højre' som Default position → overlay skal sidde HELT oppe i toppen + helt ude i højre kant (ingen margin)
2. Vælg 'Top venstre', 'Bund højre' osv → hver preset sidder flush mod kanten
3. Bekræft at 'Brugerdefineret' stadig bevarer brugerens træk-position
4. Commit + push når testen er godkendt

## Commit gennemført + pushet — klar til ny frisk chat-session

Commit: `1d8ecda` — `feat: user-chosen default position for gaming overlay with corner presets and immediate repositioning` (15 filer, +592/-82).

Hvad der blev commitet (denne session kulminerer her):
1. **Brugervalgt standard-placering** — ny `Models/GamingOverlayPositionPreset.cs` enum (TopLeft/TopCenter/TopRight/BottomLeft/BottomCenter/BottomRight/Custom) + config-felt + fuld persistence-kæde (ConfigModel → SettingsState → ConfigState → Persistence → MainViewModel.Settings/Overlay).
2. **`GamingOverlayWindow.ApplyPositionPreset()`** — placerer overlayet deterministisk efter valgte preset (WorkArea, WPF DIPs, gap=0 = flush mod kanten), kaldt ved Loaded, opløsningsskift-debounce OG øjeblikkeligt når brugeren ændrer preset i settings (ViewModel_PropertyChanged-lytter). Overlayet er aldrig låst — brugeren kan trække det frit (Custom bevarer position).
3. **Settings-UI** — dropdown på GamingOverlayPage under 'Standard scale per opløsning' med da/en lokalisering (OverlayPositionTitleText/IntroText/PresetItems/SelectedOverlayPositionPreset).
4. **Ryddet op** — GamingOverlayPositionPreset-prop bor i MainViewModel.Overlay.cs (MainViewModel.cs nede på 1182 linjer, under grænsen igen).

Verifikation efter commit+push:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1182, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- `git status`: working tree clean, `main` up to date med `origin/main` ✅
- Push: `bfe3d6f..1d8ecda main -> main` — inkl. de 2 tidligere commits der ventede (de var en del af de 3 pushede commits).

Brugerfeedback: "det spiler max så er det på plads" — hele flowet er godkendt:
- Default position dropdown flytter overlayet med det samme
- Presets sidder flush mod skærmkanten/hjørnet (gap=0)
- Overlay skalerer korrekt per opløsning (150% = fysisk 1.5x)
- Overlay vender deterministisk tilbage til valgt preset ved opløsningsskift (spil start/exit)

Næste skridt (ny session):
- Arbejdet er gemt, commitet og pushet — klar til nye opgaver.
- Evt. valgfrit: opdater ARCHITECTURE_RULES.md 'Recent Good Examples' med GamingOverlayPositionPreset-kæden (valgfrit).

## Session 2026-08-05 (aften, del 11): File Size Limit Policy dokumenteret i ARCHITECTURE_RULES.md

Brugeren spurgte om det er okay at bryde filstørrelsesgrænserne (check_architecture_rules) lejlighedsvis og samle op senere. Vi blev enige om: ja — men kun 'deliberate, traceable, time-boxed' (bevidst, sporbar, tidsbegrænset).

Politikken er nu dokumenteret i `ARCHITECTURE_RULES.md` som ny sektion 'File Size Limit Policy (documented tolerance)':
- Grænserne er en proxy for kognitiv kompleksitet, ikke et mål i sig selv. En sammenhængende 1200-linje fil kan være bedre end tre kunstigt splittede 400-linje filer.
- Tre betingelser for acceptabel overskridelse:
  1. Dokumentér det øjeblikkeligt i CHAT_STATE.md (+ evt. TODO.md) — hvad overskrider grænsen og hvad skal udtrækkes
  2. Tidsbegræns det — oprydning skal planlægges inden for et afgrænset antal sessioner. En evig overskridelse er ikke 'senere oprydning', det er permanent forfald
  3. Vurdér ved commit-tid — er der en hurtig kohesiv udtrækning (minutter)? Gør den nu. Ellers: dokumentér og commit alligevel, men sig det eksplicit
- Grænsen fungerer som en 'samtale-trigger' ved commit-tid, ikke som et ubemærket forbud.

Dette afslutter sessionen efter commit `1d8ecda` (pushet). Næste skridt: commit `ARCHITECTURE_RULES.md` + `CHAT_STATE.md` og push.

## Session 2026-08-05 (aften, del 12): Code Comments Language-regel tilføjet

Brugeren ønskede også reglen nedskrevet: kommentarer i koden skal være på engelsk (og på GitHub).

Tilføjet i `ARCHITECTURE_RULES.md` som ny sektion 'Code Comments Language':
- Alle kode-kommentarer, doc-kommentarer og commit-beskeder skal være på engelsk.
- Dansk (eller andre sprog) er kun acceptabelt i user-facing lokaliserede UI-strings (fx `RefreshLocalizedText` / `MainViewModel.Localization`), aldrig i kode-kommentarer eller commit-beskeder.
- Begrundelse: kodebasen er public på GitHub; ikke-engelske kommentarer skaber støj for bidragydere og fremtidige vedligeholdere.

Bemærk: der findes stadig danske kommentarer i eksisterende kode (fx 'Tema' sektionen i MainViewModel.cs og nogle kommentarer i GamingOverlayPage). Reglen gælder from nu — en bagudrettet oprydning kan laves som separat opgave hvis ønsket.

Næste skridt: commit `ARCHITECTURE_RULES.md` (File Size Limit Policy + Code Comments Language) + `CHAT_STATE.md` og push.

## Session 2026-08-05 (aften, del 13): Fix — overlay 'Default position' forkert ved runtime opløsningsskift (Game Resolution)

Brugerrapport: 'Default position' virker korrekt på Windows-skrivebordet (3840x2160). Men når et spil (fx POE1) bruger Game Resolution og skifter runtime til 2560x1440, placeres gaming overlayet forkert. Starter man POE1 UDEN opløsningsskifte (native 3840x2160), virker 'Default position' korrekt. Brugeren bekræftede også at alle spil køres fullscreen windowed (et must for overlayet).

Root cause: `ApplyPositionPreset()` brugte `SystemParameters.WorkArea` — WPF's statiske work-area, der er STALE lige efter et runtime opløsningsskift. Efter 400ms debounce-positionering lå overlayet stadig mod den GAMLE 4K-skriveflade (fx Right≈3840) selvom skærmen faktisk var 1440p (bredde 2560) → overlayet ryger ud til højre/afskåret.

Fix implementeret (`Views/Launcher/GamingOverlayWindow.xaml.cs` — `ApplyPositionPreset()`):
- Bruger nu `Forms.Screen.PrimaryScreen.Bounds` (fysiske pixels — altid opdateret øjeblikkeligt efter mode-skift) i stedet for `SystemParameters.WorkArea`.
- Konverterer til WPF DIPs via `VisualTreeHelper.GetDpi(this)` (scaleX/scaleY divideret) så koordinaterne matcher Window.Left/Top/Width/Height.
- Virker for alle understøttede opløsninger fra 1080p og op — positionen er nu korrekt umiddelbart efter skiftet.

Verifikation:
- Build: 0 fejl / 0 advarsler ✅
- `check_architecture_rules`: GRØN — MainWindow 968, MainViewModel 1182, HardwareMonitorAgent 1459, SystemMonitor 549 ✅
- Ændret: kun `Views/Launcher/GamingOverlayWindow.xaml.cs` (Forms-alias genbruges, VisualTreeHelper).

Næste skridt (manuel test):
1. Gaming overlay 'Default position' = Top højre. Windows 4K → start POE1 med GameResolution 2560x1440
2. Overlay skal ligge korrekt TOP-HØJRE i 1440p med det samme (ikke ude til højre/afskåret)
3. Test også fx 1920x1080 (1080p) som GameResolution — positionen skal følge den valgte preset i alle opløsninger
4. Luk spillet → tilbage til 4K → overlay stadig top-højre
5. Commit + push når testen er godkendt

## Dokumenteret problem til næste session: opløsning skifter tilbage når man alt-tabber væk fra spil med Game Resolution

Brugerrapport (2026-08-05, aften):
- Starter man et spil fra én opløsning til en anden via Game Resolution (fx POE1 3840x2160 → 2560x1440), og man derefter indeni spillet tabber til et andet vindue (fx Stifinder/whatever),
- så MISTER vi target og opløsningen SKIFTER TILBAGE til den opløsning brugeren kørte med inden spillet startede (altså 4K).
- Brugeren sammenligner med vores FPS tæller/ETW pipeline og spørger: kan vi ikke bruge samme target-teknik her? (dvs. sticky-target / target retention som FPS-agenten bruger)

Teknisk kontekst (min vurdering til næste session):
- `DisplayResolutionService.WatchProcess(rootProcessId)` overvåger kun om processen er alive + 30s crash-watchdog. Det burde IKKE kalde RestoreResolution bare fordi foreground skifter.
- Den sandsynlige trigger: `SystemEvents.DisplaySettingsChanged` fyres når spillet selv sætter skærmen tilbage til native ved de-fokus (nogle spil gør dette selv i fullscreen windowed), ELLER vores `DisplaySettingsChanged`-håndtering (debounce i GamingOverlayWindow) misfortolker det som spil-lukning.
- `SystemEvents_DisplaySettingsChanged` i GamingOverlayWindow anvender kun scale + position - den rører IKKE DisplayResolutionService. Så kilden er nok spillet selv, eller WatchProcess's IsProcessAlive mislykkes for spillets reelle render-proces.

Brugerens forslag (sticky-target som FPS/ETW):
- Ligesom native FPS agent holder fast i target (grace period 3000ms når events stopper), bør resolution-låsen også holde fast i spilprocessen på tværs af foreground-skift - kun frigives når processen rent faktisk dør (eller eksplicit afspil-lukning).
- Dvs. `WatchProcess` bør IKKE knyttes til foreground/display-events, men KUN til process-liveness.

Næste skridt (næste session):
1. Undersøg hvad der præcist trigger `RestoreResolution` - log med Debug.WriteLine om WatchProcess dør eller DisplaySettingsChanged misforstås
2. Log spillets PID + hvilke ChangeDisplaySettingsEx kilder der kaldes når man alt-tabber
3. Overvej at gøre `WatchProcess` robust: tjek `HasExited` korrekt (ikke bare første processobjekt), og ignorer display-ændringer der ikke kommer fra os så længe spillet lever
4. Evt. implementér samme sticky-target pattern som FPS-agenten (hold resolution-lås i grace period)
5. Manuel test: POE1 med GameResolution → start → alt-tab til Stifinder → opløsning skal BLIVE i 1440p → tilbage til spillet → alt virker → luk spil → 4K returneres

## Session 2026-08-06: Sticky-target fix godkendt + overlay position diagnose

- **Sticky-target fix for Game Resolution:** `DisplayResolutionService.WatchProcess` fjernede den ubetingede 30s-crash-watchdog (der ALTID gendannede opløsningen uanset om spillet levede). Opløsningslåsen frigives nu KUN når spilprocessen dør (`HasExited`-baseret med PID-reuse-beskyttelse via StartTime) + handoff-fallback hvis en root-launcher dør men en spilproces med samme exe kører. **Godkendt af bruger:** POE1 og POE2 holder nu opløsningen indtil spillet lukkes.
- **Overlay position bug (1440p → 4K):** `ApplyPositionPreset` brugte `VisualTreeHelper.GetDpi` (stale lige efter mode-skift). Fix v1: `GetDpiForSystem` virkede ikke (returnerer system-DPI 96). Fix v2: `GetDpiForMonitor` (MDT_EFFECTIVE_DPI) + `LogTrace` med faktiske beregningstal implementeret.
- **KRITISK DEPLOY-LÆRE** (dokumenteret i `.local-state/gaming-overlay.md`): IconGrid er framework-dependent .NET — al kode ligger i **IconGrid.dll**, IKKE IconGrid.exe. Tidligere kopierede vi kun exe'en → gammel dll kørte. Kopiér **HELE** `bin\Release` til `C:\IconGrid` og luk IconGrid HELT før deploy.
- **Næste skridt:** brugeren genstarter HELT og tester med ny build (dll=01:34) → trace.log vil nu indeholde `[GamingOverlay]`-linjer med faktiske tal → ret eventuel resterende rodårsag (TopRight→top-mid tyder på for stor `Width` i `ApplyPositionPreset`). Derefter UX-fase (snap-indikator + drag→Custom).
- **Nattens overlay-scale-debug er fuldt dokumenteret:** Se `.local-state/gaming-overlay.md` (kronologisk ændringshistorik med build-tider + brugerresultater) og `.local-state/next-session-overlay-scale.md` (komplet, selvstændig prompt til næste chat — kopiér hele filen som første besked). **Anbefalet første skridt i næste session: rull `ApplyScaleSettled` tilbage til 05:11-tilstanden** (synkront `ApplyWindowSize()` → `ApplyPositionPreset()` + `OnLocationChanged`-loop-breaker) og fjern derefter den sidste nedskalerings-frame via clamp/ét-pass-tilgang. 05:33-tilstanden (Left/Top før Width) er en REGRESSION — glitcher både op og ned + ud af viewport. `SetWindowPos` i slider-path (05:02) giver DWM-rysten på AllowsTransparency-vinduer — behold kun til opløsningsskift. Sticky-target fixet (`WatchProcess`, 30s-watchdog fjernet) er GODKENDT og røres ikke.

## Session 2026-08-06 (05:51) — Clamp fix for overlay down-scaling glitch

- Started from 05:11 baseline (synkront `ApplyWindowSize()` → `ApplyPositionPreset()` + loop-breaker) — which was already in the file.
- Added clamp in `ApplyPositionPreset()`: after computing left/top, clamp all four edges against screen bounds before applying to `Left`/`Top`. This ensures that even if WPF separates Width and Left into different layout passes, the overlay position is always within the viewport.
- Files changed: `Views/Launcher/GamingOverlayWindow.xaml.cs` (clamp block ~4 lines in `ApplyPositionPreset`).
- Build: 0 errors, 0 warnings. Architecture check: passed.
- Deployed to `C:\IconGrid` at 05:50.
- READY FOR USER TEST: Start `C:\IconGrid\IconGrid.exe`, open Gaming Overlay, scale slider 150→100. Verify no flash, no off-viewport drift, overlay stays top-right.

- First attempt: clamp-only in ApplyPositionPreset. User test: scaling smooth, but overlay drifts away from TopRight preset position — WPF re-anchors window between Width change (Background prio) and ApplyPositionPreset call.
- Second attempt (06:01): Wrapped entire resize+reposition in `Dispatcher.BeginInvoke(DispatcherPriority.Render)` so WPF batches Width + Left/Top into single layout pass. Clamp still in place as safety net.
- Deployed at 06:01.
- READY FOR USER TEST.


## Session 2026-08-06 (20:32) — Popup slider fix deployed

- Fix: popup slider now skips ALL scale updates during drag (`_isDraggingPopupScaleSlider` guard in ViewModel_PropertyChanged).
- Scale applied once atomically via SetWindowPos in FinishPopupScaleSliderDrag().
- 5% steps restored (TickFrequency=0.05).
- Build: 0 errors. Deployed to C:\IconGrid at 20:32.

## Session 2026-08-06 — Complete summary

## Scale slider — FÆRDIG
- **Popup-slider**: 5% steps, ingen updates under drag, atomisk SetWindowPos når musen slippes.
- **Settings-side slider**: LayoutTransform under drag, debounce + SetWindowPos når settled.
- **Begge sliders**: overlay holder TopRight position, ingen rysten/hop.
- **Release build** deployed til C:\IconGrid (723 KB).

## Uafklaret
- **Gennemsigtigheds-bug**: overlay bliver utilsigtet gennemsigtigt på Windows-skrivebordet. Brugeren har indstillinger for at det kun skal være transparent i spil (Auto transparent background). Skal debugges næste session — IsInGame flaget rapporterer muligvis forkert.

## Session 2026-08-06 (22:07) – 2026-08-07 (03:07) — Komplet session opsummering

### 7 fixes implementeret (alle deployet)

| # | Fix | Fil(er) | Status |
|---|------|---------|--------|
| 1 | ETW re-start efter idle | `main.cpp` +1 linje (StopEtwSession i PollLockedTarget) | ✅ |
| 2 | File-state collision (FileShare.ReadWrite) | `NativeFpsAgentRunner.cs` | ✅ |
| 3 | ConfigTargetLaunchGracePeriod 15s→60s | `HardwareMonitorAgent.cs` | ✅ |
| 4 | EACLaunch handoff fix | `LauncherItemLaunchManager.cs` (background scan) | ✅ |
| 5 | MoveWithRetry when-filter crash | `HardwareMonitorAgent.cs` | ✅ |
| 6 | ResolutionRestoreAgent (self-healing timer) | `MainViewModel.cs` (DispatcherTimer) | ⚠️ Deaktiveret — se nedenfor |
| 7 | Window Reposition Safety Pass | `MainViewModel.cs` (EnumWindows+SetWindowPos) | ⚠️ Deaktiveret — se nedenfor |

### Hvorfor fixes 6+7 er deaktiveret

`ResolutionRestoreAgent` brugte `GetSupportedResolutions().First()` = 4096x2160 som "desktop-native" — forkert (brugerens skærm er 3840x2160). Timeren tvang skærmen til 4096x2160 hvert 500ms → brugeren kunne ikke skifte opløsning manuelt. **Fixet er at gemme den faktiske desktop-opløsning ved startup i config.json og bruge den som `_desktopResolution`.**

### Kendte bugs (til næste session)

1. **Division 2 skifter ikke tilbage til 4K** — EACLaunch dør instantant → rootPid=0 → WatchProcess køres aldrig. EACLaunch-fixet (baggrundsscanning) er deployet men ikke verificeret for Division 2.
2. **Minimerede vinduer off-viewport efter 4K→1440p→4K cyklus** — Window Reposition Safety Pass er implementeret men deaktiveret. Skal re-aktiveres med korrekt desktop-resolution.

### Nuværende deploy-state (C:\IconGrid)

| Fil | Funktion | Sidste deploy |
|-----|----------|--------------|
| `IconGridFpsAgent.exe` | Native ETW FPS agent | 22:47 (ETW re-start fix) |
| `IconGrid.dll` | C# launcher/overlay | 02:35 (timer deaktiveret) |
| `IconGrid.runtimeconfig.json` + dependencies | .NET runtime | 02:35 |

### Anbefalet first step i næste session

1. Start med at re-aktivere `ResolutionRestoreAgent` (uncomment `_resolutionRestoreTimer.Tick += ...` og `.Start()` i MainViewModel konstruktør).
2. Fix `_desktopResolution` til at gemmes i config.json ved app-start (gem `GetCurrentResolution()` når skærmen faktisk er 4K).
3. Læs config.json ved session-start og brug den værdi som target resolution.

- **Overlay scale glitch RESOLVED:** Settings-side slider (GamingOverlayPage) og popup-slider (GamingOverlayWindow) deler nu 5% steps (TickFrequency=0.05). Begge sliders afgiver kun intervaller af 5% → ingen mellemliggende scale-ændringer under drag → WPF Width/Left-cyklussen trigger ikke længere → overlayed hopper/glitcher ikke ved nedskalering. Dette var det sidste åbne emne fra den lange debug-session 2026-08-06 (kl. 01-06).
- **Commit `a452def` verificeret:** `docs: fix Game Resolution description — overlay appears in Exclusive Fullscreen but alt-tabs out` — kun README.md, 1 linje rettet. Working tree clean.
- **Hænge partier opdateret:** Overlay scale glitch fjernet fra listen. Tilbageværende åbne opgaver: gennemsigtigheds-bug (IsInGame rapporterer forkert), FPS file-state collision (FileShare.ReadWrite), ARCHITECTURE_RULES examples + DK kommentarer (optional polish).

## Session 2026-08-06 (aften, del 2): Fix — native FPS agent ETW re-start after idle

- **Problem:** Når IconGrid + gaming overlay har været åbent længe uden spil, stopper den native FPS agent sin ETW-session. Når et spil senere startes, genstarter agenten IKKE ETW → ingen FPS vises. Genstart af IconGrid = ny agent → virker igen.
- **Root cause:** `PollLockedTarget()` ryddede `g_targetPid = 0` når spillet lukkede, men `g_etwRunning` forblev `true`. wmain's eksisterende ETW re-start logik (linje 1557-1573) tjekker `!g_etwRunning && targetPid != 0` — den så `g_etwRunning==true` og genstartede ALDRIG.
- **Fix (1 linje i `Native/FpsAgent/src/main.cpp`):** `PollLockedTarget()` kalder nu `StopEtwSession()` umiddelbart efter `g_targetPid.store(0)` — når target ryddes, ryddes ETW også. Dette lader wmain's eksisterende re-start logik fyre når `FindTargetProcess()` finder et nyt spil: `targetPid != 0 && !g_etwRunning` → `StartEtwSession()`.
- **Bevaret:** Al eksisterende logik — sticky grace period (3000ms), config-target matching, ignored-processes, fast-acquire window (15s), shared memory live path. Ingen regression-risiko.
- **C# build:** 0 fejl, 0 advarsler. `check_architecture_rules` GRØN.
- **Native build:** Bygget med MSBuild (VS 2026 Community, Release x64) — 0 fejl, 0 advarsler. Output: `E:\IconGrid-GitHub\Native\FpsAgent\bin\Release\IconGridFpsAgent.exe`.
- **Deploy:** Både C# (IconGrid.dll, 22:44) og native agent (IconGridFpsAgent.exe, 22:47) deployet til `C:\IconGrid`.
- **Test:** 1) Luk IconGrid helt. 2) Start `C:\IconGrid\IconGrid.exe` + åbn gaming overlay. 3) Vent (ingen spil). 4) Start POE1/POE2 → FPS skal vises. 5) Luk spil. 6) Vent. 7) Start spil igen UDEN at genstarte IconGrid → FPS skal vises igen.

## File-state collision fix — 2026-08-06 (aften, del 3)

- **Problem:** `NativeFpsAgentRunner.ReadState()` brugte `File.ReadAllText()` uden `FileShare.ReadWrite`. Native agent'en skriver til `native-fps-state.json` hver 2ms → reader kolliderede → `IOException: being used by another process`.
- **Log-bevis:** 3 gange mellem 23:04:06 og 23:04:41 i trace.log mens POE1 stadig kørte (FPS=30).
- **Fix i `Helpers/Hardware/NativeFpsAgentRunner.cs`:** Erstattede `File.ReadAllText()` med `FileStream(FileMode.Open, FileAccess.Read, FileShare.ReadWrite)` + 3-retry loop med 50ms delay ved `IOException`. Giver native agentens write-cyklus plads til at færdiggøre.
- **Build:** 0 fejl, 0 advarsler. Deployet til `C:\IconGrid` (IconGrid.dll, 23:10).
- **Kombineret session (2 fixes deployet):** 1) ETW re-start efter idle (native `StopEtwSession()` i `PollLockedTarget`, IconGridFpsAgent.exe 22:47). 2) File-state collision (FileShare.ReadWrite + retry, IconGrid.dll 23:10). Begge er klar til test.

## Session 2026-08-06 (aften, del 4): General target acquisition robustness fix — 3 fixes deployed

- **Root cause (Division 2):** Spil med langsomme opstartskæder (EAC → TheDivision2.exe) ryder ud af 15-sekunders grace-period før spilprocessen spawner. Native agenten får `No running process matched.` og giver op.
- **Fix A (C++, allerede korrekt):** `PollLockedTarget()` poller allerede med 75ms når intet target er låst — intet at ændre.
- **Fix B (C# — `HardwareMonitorAgent.cs`):** `ConfigTargetLaunchGracePeriod` hævet fra 15s til 60s. Dette giver langsomme opstartskæder (EAC, Ubisoft Connect, etc.) tid til at spawn spilprocessen FØR agenten rydder metadata og giver op.
- **Fix C (C# — `SystemMonitor.cs`):** Allerede implementeret! `IsInGame` bruger `TargetPid` fra shared memory som primær kilde (linje 410-424), IKKE `FpsStatus`. Når TargetPid != 0 → IsInGame=true uanset om FPS-data er ankommet.
- **Combined effect:** Spil med langsomme opstartskæder får nu 60 sekunders grace; IsInGame aktiveres øjeblikkeligt når TargetPid sættes (ikke efter FPS ankommer); auto-transparency virker for ALLE spil under opstart.
- **NOTE til Division 2:** Spillet SKAL startes FRA IconGrid (genvej i launcher) for at `config.FpsTarget` sættes til `TheDivision2.exe`. Hvis spillet startes uden for IconGrid, har native agenten ingen konfigureret target og falder tilbage til foreground-detection — hvilket kan fejle hvis EACLaunch-vinduet er i forgrunden i stedet for TheDivision2.exe.

## Session 2026-08-07 (tidlig morgen, del 5): Resolution restore handoff — FindAnyGameProcess fallback

- **Problem (Division 2):** FPS og gennemsigtighed virker nu (efter crash-fixet i del 4), men opløsningen skifter stadig ikke tilbage når spillet lukkes.
- **Root cause:** Division 2's genvej i IconGrid peger på `EACLaunch.exe`. Når `WatchProcess` startes, er root-PID = EACLaunch.exe. Når EACLaunch dør (efter at have spolet videre til TheDivision2.exe), kalder `FindHandoffProcess` med `executableName = "EACLaunch.exe"` — finder INGEN ny `EACLaunch.exe` i proceslisten → opløsningen gendannes selvom `TheDivision2.exe` stadig kører.
- **Fix 1 (tidligere, utilstrækkelig):** `WatchProcess` fik en `gameExecutableName` parameter sat til `Path.GetFileName(item.Path)` — men dette gav SAMME navn (`"EACLaunch.exe"`).
- **Fix 2 (nu):** `FindAnyGameProcess(int deadPid)` — når eksakt-navn handoff fejler, scanner efter enhver proces med et synligt hovedvindue (`MainWindowHandle != IntPtr.Zero`). Foretrækker den nyeste proces. Dette fanger `TheDivision2.exe` (og alle andre spil med launcher→spil kæder) uden at kende exe-navnet.
- **Generalitet:** Løsningen er IKKE spil-specifik — den fungerer for alle spil der bruger en launcher/anti-cheat wrapper (EAC, BattleEye, Ubisoft Connect, Steam-bootstrapper, etc.).
- **Build:** 0 fejl, 0 advarsler. Deployet til `C:\IconGrid` (IconGrid.dll, 00:36).
- **Test:** 1) Luk IconGrid helt. 2) Start `C:\IconGrid\IconGrid.exe`. 3) Start Division 2 fra IconGrid. 4) Spil → FPS vises + overlay gennemsigtigt ✅. 5) Luk Division 2 → opløsning skal skifte tilbage til 4K. (Brugeren testede tidligere at FPS + gennemsigtighed virker; nu verificeres opløsnings-restore).

## Session 2026-08-07 (03:45) — Window Tracking System deployed

`IconGrid.dll` deployed to `C:\IconGrid` at 03:44 (xcopy of bin\Debug\net10.0-windows10.0.22621.0). IconGrid was not running, so no taskkill needed. Ready for manual test.

Build + deploy kl. 03:51 — bugfix: minimerede vinduer må IKKE åbnes af Restore, og on-screen vinduer må IKKE flyttes. Restore rører KUN vinduer der er 100% off-screen. SafetyPass fjernet fra LauncherItemLaunchManager (den var for aggressiv og smed alle vinduer til venstre).

Build + deploy kl. 03:57 — fjernet auto-restore helt. Ingen vinduer røres efter opløsningsskift. Tracking-servicen kører stadig i baggrunden og leverer LastSeenOpenRect data til layout-engine når brugeren anvender et gemt layout. Det er brugerens gemte layout der bestemmer positionering — ikke en automatisk restore.

Godkendt af bruger kl. 04:02 — "spiller det perfekt, som en del af windows". Minimerede og åbne vinduer holder deres position efter opløsningsskift. Tracking-servicen kører i baggrunden (2s poll) og gemmer LastSeenOpenRect per opløsning. Layout-systemet kan query tracking-data når brugeren anvender et gemt layout. Auto-restore fjernet — brugerens gemte layout styrer al positionering.




## Session 2026-08-07 (04:22) — FindAnyGameProcess for resolution restore with launcher chains

Build + deploy kl. 04:22. Erstattet `FindProcessByName(exeName)` i baggrundsscanningen med `FindAnyGameProcess()` — bruger `EnumWindows` til at finde spillets VIRKELIGE proces uanset hvad launcher/anti-cheat wrapper'en hedder. Virker for Division 2 (EACLaunch → TheDivision2.exe), BattleEye, Ubisoft Connect, Steam bootstrapper, og alle andre launcher-kæder. Ingen nye filer, kun én metode ændret i LauncherItemLaunchManager.cs.

Build + deploy kl. 04:36 — fix v2: FindAnyGameProcess() bruger nu `Process.GetProcesses()` + `MainWindowHandle` i stedet for `EnumWindows`. EnumWindows kunne IKKE se Division 2's vindue (exclusive fullscreen/DX12). Process.GetProcesses().MainWindowHandle virker for ALLE fuldskærms-spil. POE1/POE2 regression test: stadig perfekt (bekræftet af bruger kl. 04:28).


## Session 2026-08-07 (04:57) — Division 2 resolution restore: launcher-chain re-target fix

## Root cause (Division 2 resolution restore)

Division 2 launches via a launcher chain (EACLaunch -> TheDivision2.exe). The old code only watched the ROOT process (the launcher). The launcher stays alive in the background after the game exits, so the resolution lock was never released → resolution never restored to 3840x2160.

## Fix v3 (deployed 04:57)

1. **`DisplayResolutionService.RetargetResolutionLock(oldPid, newPid)`** — new method that moves the saved original DEVMODE from the launcher PID to the real game PID and starts a fresh watchdog on it. Resolution is now restored when the GAME exits, not the launcher.

2. **`LauncherItemLaunchManager.LaunchItem`** — the scan now ALWAYS runs (not just when root PID == 0). It finds ANY process with a MainWindowHandle (not game-specific) and re-targets the lock to the real game process. Scan window: 60s (120 × 500ms).

3. **Removed `WS_EX_TOOLWINDOW` filter** from `FindAnyGameProcess` — some DirectX games set this flag on their main window; the `MainWindowHandle != IntPtr.Zero` check is sufficient.

## Status
- Build succeeded (10.4s), 86 files deployed to `C:\IconGrid`.
- Architecture check: only pre-existing `MainViewModel.cs` size violation (1327 lines, unrelated to this change).

## Next steps
- Test Division 2: launch at 2560x1440, exit via in-game menu, verify resolution restores to 3840x2160.
- Check `C:\Users\THXMAN\AppData\Roaming\IconGrid\trace.log` for `[DisplayResolutionService] Re-targeted resolution lock from PID X to PID Y` and `Restored original resolution` lines.
- Confirm POE1/POE2 still work (regression check).

## Session 2026-08-07 (05:08-05:32) — Division 2 resolution restore fix v4: dual-source game process scan

- **Problem:** Fix v3 (FindAnyGameProcess med MainWindowHandle) gendannede stadig ikke opløsningen til 3840x2160 når Division 2 blev lukket.
- **Root cause (fra trace.log 01:29-01:31):** `Process.MainWindowHandle` er `IntPtr.Zero` for BÅDE EACLaunch (tool-window launcher, vinduet var kun 800x450) OG TheDivision2.exe (exclusive fullscreen DX12). Fix v3 fandt derfor ALDRIG spilprocessen — ingen `WatchProcess started`, ingen `Re-targeted resolution lock`. Opløsningen vendte kun tilbage fordi spillet selv gendannede den. POE1/POE2 virkede fordi deres root PID er direkte (MainWindowHandle findes).
- **Fix v4 (deployed 05:32):** `LauncherItemLaunchManager.FindAnyGameProcess()` er nu dual-source:
  1. `Process.MainWindowHandle` + `GetWindowRect` (fuldskærms-windowed spil)
  2. `EnumWindows` + `IsWindowVisible` + `GetWindowRect` (exclusive fullscreen / DX12 / tool-window styled main windows — fanger hvad MainWindowHandle skjuler)
  - Filtre: vindue ≥ 960x540 (ekskluderer EACLaunch 800x450) + proces startet inden for 120s (ekskluderer SystemSettings/copilot/gamle processer). Foretrækker nyeste proces.
  - Scanning kører kontinuerligt i 60s (120 x 500ms) og re-targeter hver gang en NYERE kvalificerende proces dukker op (launcher -> spil).
  - Fallback: hvis `RetargetResolutionLock` fejler (ingen gemt DEVMODE for launcher-PID), bindes fundne PID direkte via `AttachPendingResolution` + `WatchProcess`.
- **Byg:** 0 fejl / 0 advarsler. `check_architecture_rules` grøn (kun kendt MainViewModel 1327-linje overtrædelse, ikke relateret).
- **Deploy:** 86 filer -> C:\IconGrid (hele bin\Debug, inkl. IconGrid.dll).
- **Test (næste skridt):** 1) Start IconGrid. 2) Start Division 2 fra IconGrid (GameResolution 2560x1440). 3) Tjek trace.log for `[LauncherItemLaunchManager] Found real game process via FindAnyGameProcess: PID X. Binding resolution lock.` og `[DisplayResolutionService] Re-targeted...`. 4) Luk Division 2 via in-game-menu -> opløsning skal vende tilbage til 3840x2160 (log `Restored original resolution`). 5) POE1/POE2 regressionstest.

## Session 2026-08-07 (05:40) — Division 2 resolution restore fix v4 BEKRÆFTET af bruger

- **Bruger-bekræftelse:** "SÅDAN nu virker hele kæde i alle 3 spil smooth og hurtig opløsning skift er perfekt POE1 og POE2 og Division 2 se logs også :)"
- **Log-bevis (trace.log 05:35-05:39):**
  - Division 2: `Switched primary display to 2560x1440` (05:35:08) → `Found real game process via FindAnyGameProcess: PID 34156. Binding resolution lock.` (05:35:28) → `WatchProcess started for PID 34156` → `Re-targeted resolution lock from PID 34156 to PID 27424` (05:35:49, spillet oprettede sit rigtige DX12-vindue) → `Process 34156 exited; restoring resolution.` (05:36:34) → `Restored original resolution for process 34156` (05:36:35)
  - POE/POE2 (05:37-05:39): samme mønster med `Restored original resolution`
- **Vigtig observation fra log:** Der var en kort oscillation mellem PID 34156 ↔ 27424 (05:35:49) og dobbelte `Binding`/`WatchProcess`-kald grundet to samtidige scan-iterationer. Slutresultatet var korrekt (opløsning gendannet), og `WatchProcess` annullerer altid den forrige watchdog — så kun den nyeste overlevede. Ingen kodeændring nødvendig; brugeren er fuldt tilfreds.
- **Status: FIX FÆRDIG OG GODKENDT.** Commit `3252f8e` pushet. Næste skridt: commit + push når brugeren ønsker det.

## Session 2026-08-07 (05:48) — Game Resolution siden: Category-område i card

- **Forespørgsel:** På Game Resolution-siden så 'Category' + tilhørende elementer grimt ud fordi de lå i et råt Grid uden ramme, mens resten af siden brugte `StartsideSectionCardStyle`-cards.
- **Fix:** `Views/Settings/Pages/GameResolutionPage.xaml` — Category-filter-området (titel + beskrivelse + kategori-dropdown) er nu omsluttet i en `Border` med `StartsideSectionCardStyle`, `Padding="16"` og `DevInspector.Metadata="Category filter card -> Views/GameResolutionPage.xaml"` — identisk stil som listen nedenunder og resten af siden.
- **Spacing-fix (opfølgning):** `StartsideSectionCardStyle` har allerede indbygget `Margin="0,0,0,5"` (konsistent 5px mellem alle cards på hele appen). Den første version overskrev margin til `0,0,0,12` hvilket skabte uensartet mellemrum — denne override er fjernet (commit `1dfd752`), så begge cards på siden bruger stylens standard 5px-gap præcis som alle andre sider.
- **Byg:** 0 fejl / 0 advarsler. **Deploy:** 86 filer -> C:\IconGrid (XAML kompileres ind i IconGrid.dll).
- **Commits:** `2e25316` "ui: wrap category filter on Game Resolution page in matching card style" + `1dfd752` "ui: use consistent card spacing on Game Resolution page (remove margin override)" — begge pushet.
- **Næste skridt:** Manuel test — åbn Game Resolution-siden og bekræft at Category-cardet har samme udseende OG samme mellemrum som resten af siden.

## Session 2026-08-07 (05:57-06:03) — Gaming Overlay siden: Default position/transparent/farvevælger flyttet over Overlay scale

- **Forespørgsel:** Brugeren ønskede at 'Default position', 'Transparent background', 'Transparent background while in game' og farvevælgeren skulle ligge OVER 'Overlay scale' på Gaming Overlay-siden (de var tidligere nederst bagest).
- **Fix:** `Views/Settings/Pages/GamingOverlayPage.xaml` — hero-stacken omarrangeret. Ny rækkefølge (top → bund): 1) Default position (dropdown) 2) Transparent background (toggle) 3) Transparent while in game (toggle) 4) Farvevælger (swatches + custom, vises når transparent er aktiv) 5) Overlay scale (slider) + per-opløsning defaults (rykket ned i bunden i sin egen Border).
- **Byg:** 0 fejl / 0 advarsler. **Deploy:** 86 filer -> C:\IconGrid.
- **Godkendt af bruger:** "det spiller perfekt" — commit + push udført.
- **Commit:** `519019e` "ui: move overlay position, transparency, and color picker above scale on Gaming Overlay page" — pushet (`fd6b128..519019e main -> main`).

## Session 2026-08-07 (06:08-06:21) — Settings-page designkontrakt: TemplateGuidelines + TemplatePage + lokal UI-design GUIDELINE

- **Forespørgsel:** Brugeren bad om at opdatere `Views/Settings/Pages/TemplatePage.xaml` og `Views/TemplateGuidelines.xaml` så de SPEJLER den faktiske praksis på settings-siderne, normalisere afvigelser, og gemme den opdaterede viden i `.local-state`. Ingen redesign.
- **Analyse:** Den reelle typografi-skala blev kortlagt på tværs af alle settings-sider (Startside, Hardware, GamingOverlay, Layout, GameResolution, GenvejsIkoner, Hjaelp, Test, About): Hero-titel 20/SemiBold/TopBarForeground, Card-overskrift 16/SemiBold, Sektionstitel 14/SemiBold, Body 13/Normal/SettingsSubtextForeground/Wrap, Badge/label 12. TemplateGuidelines kendte kun 20 og 13 — 16/14/12 manglede.
- **Ændringer:**
  1. `Views/TemplateGuidelines.xaml` — tilføjet `TemplateCardTitleStyle` (16), `TemplateSectionTitleStyle` (14), `TemplateSmallTextStyle` (12); opdateret kommentar-blok + `TemplateGuidance`-streng til at dokumentere hele skalaen, farver (TopBarForeground/SettingsSubtextForeground/AccentBrush), card-regler (StartsideSectionCardStyle, padding 28/16/12, 5px-gap) og hero-margins (0,16).
  2. `Views/Settings/Pages/TemplatePage.xaml` — tilføjet Usage-contract-kommentar der beskriver forventet HeroContent/CardsContent-struktur (titel→intro→cards, style-valg pr. niveau).
  3. `Views/Settings/Pages/StartsidePage.xaml` — normaliseret `UiScaleDescription` (tilføjet FontWeight=SemiBold så den matcher alle 14-sektionstitler) og `StartDirectlyInLauncherLabel` (tilføjet FontSize=14 — var på WPF default 12 trods SemiBold, markant mindre end andre sektionstitler).
  4. `Views/Settings/Pages/GamingOverlayPage.xaml` — KUN rækkefølge-flytning (Default position/transparent/farve over scale) fra forrige commit; ingen ændring i denne del.
  5. `.local-state/ui-design-guidelines.md` (NY, dansk, gitignored) — fuld designkontrakt: typografi-skala-tabel med kodeeksempler, farve-brushes, card-regler, TemplatePage-layout, knapper/toggles/inputs, sprog-regel (local-state=dansk, GitHub/kode/commits=engelsk).
- **Sprog-regel (dokumenteret):** `.local-state/*.md` på dansk (gitignored, lokalt); GitHub/kode/XAML-kommentarer/commits på engelsk; UI-brugertekster da+en via lokalisering.
- **Byg:** 0 fejl / 0 advarsler. **Deploy:** 86 filer -> C:\IconGrid.
- **Bruger-godkendt:** planen godkendt ("godkendt") før implementering.
- **Commit:** `e94d9d9` "docs: align settings page template and guidelines with real typography scale" — pushet (`14f2206..e94d9d9 main -> main`).

## Session 2026-08-07 (06:23-06:26) — Grundig stil-audit af alle settings-sider + LayoutPage normalisering

- **Forespørgsel:** Brugeren spurgte om jeg faktisk havde kigget på om ALLE sider bruger samme stil/font. Ærligt svar: den tidligere FontSize-søgning var overfladisk; jeg udførte nu en grundig audit.
- **Grundig audit — 9 settings-sider gennemgået:**
  - Startside: ✅ (normaliseret tidligere: UiScaleDescription + StartDirectlyInLauncherLabel)
  - Hardware: ✅ 20/16/13/12 — konsistent
  - GamingOverlay: ✅ 20/14/13/16/12 — konsistent (efter sektions-flytning i `519019e`)
  - GameResolution: ✅ 20/14/13/12 — konsistent
  - Layout: ⚠️ **3 toggle-labels brugte `FontSize="13"` + `TopBarForeground`** — afveg fra alle andre siders toggle-mønster (flade labels, SettingsSubtextForeground, default 12)
  - GenvejsIkoner: ✅ 20/16/13 — konsistent
  - About: ✅ 20/13 — konsistent
  - Hjaelp: ✅ 20/13 — konsistent
  - Test: ✅ 20/16/13/12/14 + Consolas special — konsistent (diagnostik-side, undtagelse velbegrundet)
- **Fix (deployed 06:26):** `Views/Settings/Pages/LayoutPage.xaml` — 3 toggle-labels (LayoutSkipMinimizedText, LayoutActiveScreenOnlyText, LayoutReserveSlotToggleText) normaliseret fra `FontSize="13"` + `TopBarForeground` til flade `SettingsSubtextForeground` (default 12) — identisk med Startside/Layout-field-label-mønstret. Ikke commitet endnu — afventer bruger-godkendelse.
- **Byg:** 0 fejl / 0 advarsler. **Deploy:** 86 filer -> C:\IconGrid.
- **STATUS — klar til ny session:**
  1. NÆSTE SKRIDT: Bruger-godkendelse af LayoutPage-normaliseringen → commit + push (engelske besked, fx "ui: normalize Layout page toggle labels to match settings design contract").
  2. Tjek arkitektur (check_architecture_rules) før commit.
  3. Manuel test: Layout-siden — bekræft toggle-labels stadig ligner resten af appen efter normalisering.
  4. Opdater `.local-state/ui-design-guidelines.md` hvis nye afvigelser dukker op.

## Session 2026-08-07 (06:32) — LayoutPage normalisering committet + pushet

Bruger-godkendt kl. 06:32: "alt ser godt ud commit og push".
- Commit `9188f9e` "ui: normalize Layout page toggle labels to match settings design contract" (1 fil, +3/-6) — LayoutPage.xaml toggle-labels normaliseret.
- Pushet: `9f2136e..9188f9e main -> main`.
- `check_architecture_rules` før commit: kun kendt, præeksisterende MainViewModel.cs-overtrædelse (1327 linjer, limit 1200) — ikke relateret til XAML-ændringen.
- Working tree nu ren; ingen u-committede ændringer.
- STATUS: LayoutPage-normaliseringen er nu FÆRDIG og pushet. Næste åbne emner: MainViewModel.cs 1327-linje overtrædelse (dokumenteret tolerance), gennemsigtigheds-bug (IsInGame), FPS file-state collision, ARCHITECTURE_RULES examples + DK kommentarer (optional polish).

## Session 2026-08-07 (14:44-15:00): Fix — Transparent while in game hænger efter spil-lukning

Opgave: 'Transparent while in game' (AutoTransparentBackground) forblev i Windows efter spil-lukning. OFF gav baggrund, men ON gav transparent selvom intet spil kørte.

Root cause: `Helpers/Launcher/SystemMonitor.cs` FpsTimer_Tick's FPS-fallback til IsInGame (hasNativeFps/hasCorrectedFps) kunne hænge true pga. 'hold sidste FPS' (fps-state.json LiveFpsValue stale). FPS-pipelinen er godkendt og IKKE rørt.

Fix: IsInGame følger nu UDELUKKENDE native-agentens TargetPid (SetInGame(trackedGamePid > 0)). TargetPid=0 eller utilgængelig shared memory = ikke i spil → solid baggrund i Windows. Spil-start → TargetPid>0 → transparent. XML-doc + kommentar opdateret. Build: 0 fejl / 0 advarsler. Deployet til C:\IconGrid (IconGrid.dll 14:58:56, hele bin\Debug kopieret).

Dokumentation: .local-state/gaming-overlay.md (fix-sektion), .local-state/regex-commands-cheatsheet.md (NY — brugeren bad om at samle gode regex/kommando-mønstre; Select-String viste sig mere pålidelig end search_files pga. gentagne XML-korruptionsfejl).

Næste skridt: Manuel test (1. AutoTransparent ON uden spil → solid baggrund. 2. Start spil → transparent. 3. Luk spil → solid med det samme. 4. OFF→ON uden spil → solid). Ved godkendelse: commit + push.

## Session 2026-08-07 (15:08-15:12): Stavefejl-fix godkendt + kommitteret

Efter bruger-godkendelse af transparent-fixet (tidligere i sessionen, commit 6727847 pushet) rapporterede brugeren danske stavefejl med ÆØÅ på Gaming Overlay-siden (Goer/Naar/Paa/traekker/Saa).

Fix: `Views/Settings/Pages/GamingOverlayPage.xaml.cs` `RefreshLocalizedText()` da-gren — 11 tekster rettet: Goer→Gør, Saa→Så, Paa→På, Naar→Når, storrelse→størrelse, foelger→følger, afhaenger→afhænger, Koer→Kør, noedvendig→nødvendig, foreslaaede→foreslåede, laesbarhed→læsbarhed, skaermen→skærmen, oploesningsskift→opløsningsskift, track→træk, traekker→trækker.

Build: 0 fejl / 0 advarsler (DLL 15:11:16). Deployet til C:\IconGrid (hele bin\Debug). Commit 54f2620 pushet: `6727847..54f2620 main -> main`.

Næste skridt: Manuel test — åbn Gaming Overlay-siden på dansk og bekræft stavningen. Bemærk: FPS-pipelinen er IKKE rørt (bruger-godkendt).

## Session 2026-08-07 (17:19-17:56): Auto-funktioner under spil implementeret

4 nye auto-funktioner implementeret på Gaming Overlay-siden (ny card 'Automatisk under spil'):
1. Launcher-adfærd under spil (dropdown: Gør intet / Auto-skjul / Minimer til proceslinjen) — virker uanset 'Altid øverst'
2. Vis gaming overlay automatisk ved spilstart (placeret ved valgte Default position)
3. Luk gaming overlay når spillet lukkes (via IsInGame→false)
4. Genåbn launcher når overlayet lukkes (manuelt eller automatisk)

Filer: Models/GameAutoBehaviorMode.cs (ny enum), ConfigModel + SettingsState + ConfigState + Persistence + MainViewModel.Settings/Overlay (4 felter), MainViewModel.cs (events GameLaunched/GameExited), MainViewModel.Items.cs (fyrer GameLaunched i RememberFpsTarget), MainWindow.xaml.cs (OnGameLaunched/OnGameExited/OnGamingOverlayClosed), LauncherWindowModeController.cs (HideForGame/RestoreLauncherFromGame), GamingOverlayWindowCoordinator.cs (OverlayClosed event), GamingOverlayPage.xaml/.cs (UI + da/en).

UI-layout: 'Automatisk under spil' card + 'Overlay størrelse' card (flyttet ud af hero til eget card) er nu ØVERST i CardsContent — hero indeholder kun position/transparent/farve.

Build: 0 fejl / 0 advarsler. Deployet til C:\IconGrid (DLL 17:55:46).

Næste skridt: Manuel test af alle 4 funktioner. Commit/push afventer bruger-godkendelse.

## Session 2026-08-07 (18:14) — Auto-funktioner commitet + pushet

Bruger-godkendt ("alt virker som det skal") og committet:
- `13a91f9` feat: add auto-behavior for launcher and gaming overlay during gameplay (14 filer, +495/-22, ny Models/GameAutoBehaviorMode.cs)
- `2026dfa` ui: tighten ping dot spacing in launcher monitor row (1 fil — separat kosmetisk tweak fundet i working tree; margin 6→4)
- Arkitektur: 2 dokumenterede overtrædelser (File Size Limit Policy) — MainWindow.xaml.cs 1006 (limit 1000, +6 fra auto-funktions-wiring), MainViewModel.cs 1353 (limit 1200, kendt præeksisterende). Build: 0 fejl / 0 advarsler.
- Næste skridt: reducer MainWindow.xaml.cs 1006→1000 (små dryp, fx træk en håndfuld metoder ud) for at vende tilbage til grøn; MainViewModel 1353 er den kendte dokumenterede tolerance.

## Session 2026-08-07 (19:02) — Gaming overlay side: foldbare sektioner + Live vs Trend fjernet

Bruger-godkendt og committet i dag:
- `0000b5a` feat: make all gaming overlay settings sections collapsible and remove Live vs Trend card (2 filer, +580/-524): Alle 4 sektioner på Gaming Overlay-siden (hero + Automatik under spil + Overlay størrelse + FPS setup) er nu foldbare expanders (Layout-sidens mønster: transparent header + Segoe Fluent pil &#xE70D; roteret via BooleanToAngleConverter, default foldet ind). 'Planlagte indstillinger / Live vs trend'-cardet fjernet helt + ubrugte FpsResponsiveness/fPlannedOverlaySettings-properties ryddet fra code-behind. Overskriftstørrelser normaliseret: alle foldbare headers = 16/SemiBold/TopBarForeground (hero var 20).
- `3628bed` docs: document collapsible section typography rule in settings template guidelines (TemplateGuidelines.xaml): ny 'Collapsible sections'-sektion — foldbare headers skal ALTID bruge Card/block-niveau (16/SemiBold/TopBarForeground) uanset hero/almindeligt card, så folded/unfolded læser konsistent; indhold beholder 14/13/12. TemplateGuidance opdateret.
- Byg: 0 fejl / 0 advarsler. Deployet til C:\IconGrid. Working tree ren efter push.
- Næste åbne emner: MainViewModel.cs 1327-linje overtrædelse (dokumenteret tolerance), arkitektur-check-status ved næste kørsel.

## Session 2026-08-07 — GameResolutionPage (kategori + hero card)

- GameResolutionPage (Views/Settings/Pages/GameResolutionPage.xaml/.cs):
  - Kategori-dropdown viser nu lokaliserede navne (Spil/Software/Udvikling i stedet for Games/Software/Develop) ved dansk sprog.
  - Ny `CategoryOption`-klasse (Key + lokaliseret DisplayName) bruges i AvailableCategories/SelectedCategory.
  - `TabNameLocalizationConverter.LocalizeCategoryName(categoryName, language)` er nu en offentlig statisk metode, genbrugt af både LauncherTabsBar (konverteren) og GameResolutionPage.
  - Kategori-filteret er flyttet fra sit eget kort ind i hero-card'et, over 'Vælg en opløsning for hver genvej'-introen.
  - Bygget med `dotnet build IconGrid.csproj`: 0 fejl, 0 advarsler.
  - Bemærk: check_architecture_rules viser præeksisterende overtrædelser i Views/Launcher/MainWindow.xaml.cs (1006 linjer) og ViewModels/MainViewModel.cs (1353 linjer) - ikke forårsaget af denne opgave.
- Næste skridt: manuel UI-test af GameResolutionPage (skift sprog til dansk og bekræft dropdown + hero card-layout).

- Supplerende: Oprettet `.local-state/project-structure.md` (gitignored) — reference-dokument over hele IconGrid mappe-/filstrukturen (rode, Assets, Controls, Helpers, Models, Native, ViewModels, Views, IconGrid.Package, tools, .local-state). Bruges som hurtig navigations-reference i fremtidige sessioner.
  - Bemærk: dokumentet er gitignored og kun lokalt.
  - Næste skridt: manuel UI-test af GameResolutionPage (skift sprog til dansk og bekræft dropdown + hero card-layout).


## Session 2026-08-07 (aften) — Fix: kategori-dropdown viste engelsk trods dansk

- Brugeren rapporterede at kategori-dropdown'en STADIG viste engelsk ved dansk sprog.
- Årsag: `DisplayMemberPath="DisplayName"` cacher SelectionBox-strengen i WPF ComboBox.
  Når `CategoryOption.DisplayName` ændres ved sprogskift, opdateres dropdown-listen,
  men det lukkede felt kan blive ved med at vise den gamle (engelske) streng.
- Fix:
  1. Erstattede `DisplayMemberPath="DisplayName"` med et `ComboBox.ItemTemplate`
     (`<TextBlock Text="{Binding DisplayName}" />`) — selection-boxen følger nu live bindinger.
  2. `MainViewModel_PropertyChanged` kalder nu også `RefreshShortcuts()` når Language ændres,
     så kategorierne genopbygges (Clear + re-add) og den nye lokaliserede tekst tvinges frem.
- Bygget: 0 fejl, 0 advarsler.
- Næste skridt: manuel UI-test — åbn Game Resolution, skift sprog til dansk, og bekræft at
  dropdown-en viser Spil/Software/Udvikling (både i den lukkede boks og i listen).

## Session 2026-08-07 (aften) — ENDELIG fix: kategori-dropdown viste engelsk

- Rodårsag (fundet efter 2 forsøg): Brugerens faktiske kategorier i `%APPDATA%\IconGrid\items.json` er:
  `Develop, Games, Internet, Monitor, Passlock, Software, Windows`.
- `TabNameLocalizationConverter.LocalizeCategoryName` oversatte KUN `Games/Software/Develop` — så
  `Internet`, `Monitor`, `Passlock`, `Windows` blev returneret uændret (på engelsk) ved dansk sprog.
- Fix:
  1. `Helpers/Settings/LocalizationHelper.cs`: + `TabInternet`, `TabMonitor`, `TabPasslock`, `TabWindows`
     (en + da). Dansk: Internet→Internet, Monitor→Overvågning, Passlock→Passlock, Windows→Windows.
  2. `Helpers/Converters/TabNameLocalizationConverter.cs`: udvidede switch'en med de 4 nye kategorier.
  3. (Tidligere) XAML ItemTemplate i stedet for DisplayMemberPath + RefreshShortcuts() ved sprogskift
     — for at tvinge selection-boxen til at gengive ny tekst.
- Resultat ved dansk sprog: Games→Spil, Develop→Udvikling, Monitor→Overvågning; Internet/Passlock/
  Software/Windows forbliver (korrekt, da de er ens/produktnavne). Bygget: 0 fejl, 0 advarsler.

## Session 2026-08-07 (aften) — Build kopieret til C:\icongrid

- Brugeren tester iconGrid fra `C:\icongrid` (ikke fra bin\Debug).
- Lukkede kørende IconGrid/IconGridFpsAgent-processer (godkendt) og kopierede hele
  `bin\Debug\net10.0-windows10.0.22621.0\*` til `C:\icongrid` med `Copy-Item -Recurse -Force`.
- Bekræftet: `C:\icongrid\IconGrid.dll` (874.496 bytes) og `IconGrid.exe` (179.200 bytes)
  har nu LastWriteTime 07-08-2026 19:55:36 — samme som det nye build. Kopiering OK, EXIT=0.
- Lærdom: Test-mappen er `C:\icongrid`. Efter build: luk IconGrid-proces hvis nødvendigt,
  kopiér `bin\Debug\net10.0-windows10.0.22621.0\*` → `C:\icongrid` -Recurse -Force, og verificér
  via `Get-Item C:\icongrid\IconGrid.dll`.
- Næste skridt: manuel UI-test i C:\icongrid-build'et — åbn Game Resolution med dansk sprog,
  bekræft at dropdown viser Spil/Udvikling/Overvågning osv.

## Session 2026-08-07 (aften) — Committet og pushet

- Brugeren bekræftede at det hele spiller nu (kategori-dropdown viser dansk ved dansk sprog, kategori i hero card).
- Commit: `b68bfbd` "Fix localized category dropdown and merge category selector into hero card on Game Resolution page"
  - 5 filer, 190 insertions, 46 deletions:
    CHAT_STATE.md, Helpers/Converters/TabNameLocalizationConverter.cs,
    Helpers/Settings/LocalizationHelper.cs, Views/Settings/Pages/GameResolutionPage.xaml(.cs).
- Push: `git push origin main` → `338f906..b68bfbd main -> main` (OK).
- Lærdom genbekræftet: `&&` er IKKE gyldig i PowerShell — kør git add/commit/push hver for sig.
- Opgaven er dermed fuldt afsluttet: lokaliseret dropdown + hero card + build kopieret til C:\icongrid + commit/push.

## Session 2026-08-07 (aften) — Release-pakke til vennen

- Byggede self-contained Release-publish: `dotnet publish IconGrid.csproj -c Release -r win-x64 --self-contained true -o E:\IconGrid-GitHub\publish\0.7.0-beta.1` (EXIT=0, 517 filer).
- Verificerede at `Tools\FpsAgent\IconGridFpsAgent.exe` er med i publish (Test-Path True) + Assets/figma-icons.
- Pakkede release-zip: `E:\IconGrid-GitHub\publish\IconGrid-0.7.0-beta.1-win-x64.zip` — 83.200.171 bytes (~83 MB), 07-08-2026 20:11:21.
- Zip'en er self-contained win-x64: vennen kan køre IconGrid.exe direkte uden at installere .NET 10 runtime.
- gh CLI er IKKE installeret på maskinen, så der blev ikke oprettet en GitHub Release — zip-filen kan sendes direkte.
- Næste skridt for brugeren: Send `E:\IconGrid-GitHub\publish\IconGrid-0.7.0-beta.1-win-x64.zip` til vennen.
  - Vennen pakker zip'en ud, kører `IconGrid.exe`.
  - Data gemmes i `%APPDATA%\IconGrid` (config.json, items.json) — nye maskiner starter med tomt setup.
  - Hvis FPS/hardware-monitor skal virke: brugerniveau kan kræve task scheduler / evt. elevat for monitor-delen (README: 'Hardware monitor startup').

## Session 2026-08-07 (aften) — publish/ gitignored (vigtigt!)

- Brugeren gjorde opmærksom på at `publish/`-mappen (83 MB release-zip) IKKE må på internettet.
- `.gitignore` manglede `publish/` (git viste `?? publish/`). Tilføjede linjen `publish/` under 'Projekt-specifikke mapper'.
- Commit `343d096` "Ignore publish/ folder so release builds never get pushed" (1 insertion).
- Push OK: `3945a80..343d096 main -> main` — publish/ kom IKKE med.
- LÆRDOM (vigtig): `publish/` og `E:\IconGrid-GitHub\publish\` er RELEASE-ARTIFAKTER (zip + ukomprimeret publish-mappe).
  De må ALDRIG stages/pushes. `.gitignore` har nu `publish/`, så det er beskyttet.
  Release-zip ligger på: `E:\IconGrid-GitHub\publish\IconGrid-0.7.0-beta.1-win-x64.zip` (~83 MB).
- Commit-historik i dag: b68bfbd (kategori-fix), 3945a80 (CHAT_STATE), 343d096 (.gitignore).

## Session 2026-08-07 (aften) — Brugerændring: LanguageSectionTitle

- Brugeren ændrede `Helpers/Settings/LocalizationHelper.cs`: `LanguageSectionTitle` da: "Sprog og region" → "Sprog".
- Byggede: `dotnet build IconGrid.csproj` → 0 fejl, 0 advarsler (BUILD_EXIT=0).
- Lukkede kørende IconGrid (2 proc + FpsAgent) og kopierede `bin\Debug\net10.0-windows10.0.22621.0\*` → `C:\icongrid` (EXIT=0).
- Verificeret: `C:\icongrid\IconGrid.dll` = 2026-08-07 20:39:59, 874.496 bytes (nyt build).
- Næste: commit + push af ændringen (LocalizationHelper.cs evt. + CHAT_STATE.md).

## Session 2026-08-08 (17:03) — Commit & push: Game-only launcher behavior, overlay settings navigation, hardening

- Commit `98268fb` "Fix game-only launcher behavior, overlay settings navigation, and hardening" (8 files, +136/-19) pushet til origin/main.
- Ændringer:
  - **Helpers/Hardware/EtwAccessRequirements.cs**: FPS setup status accounts for elevated native agent.
  - **Helpers/Hardware/HardwareMonitorAgent.cs**: Ignore SystemSettings/mscopilot as game candidates; handle anti-cheat access-denied as 'alive' so Division 2 overlay stays open.
  - **ViewModels/MainViewModel.cs**: Bindings/overlay logic for game-only behavior.
  - **ViewModels/MainViewModel.Items.cs**: Treat only Games-category items as games (no launcher hide/overlay for non-games); auto-show overlay for externally started games via IsInGame transition.
  - **Views/Launcher/GamingOverlayWindow.xaml.cs**: Open Gaming Overlay settings page directly from overlay.
  - **Views/Settings/SettingsWindow.xaml.cs**: Keep launcher hidden in-game during settings navigation.
  - **README.md**: Document game auto-hide/auto-show/auto-scale.
  - **figma-icons/Resolution.png**: Added (image asset).
- Working tree ren efter push.
- Næste skridt: Manuel test af game-only behavior — start en non-Games genvej (fx fra Software/Develop kategori) og bekræft at launcher ikke auto-skjuler og overlay ikke auto-åbner. Start et Games-genvej og bekræft at auto-skjul/auto-show fungerer. Test Division 2 overlay overlevelse med anti-cheat access-denied.

- **Deploy til C:\IconGrid (18:12):** Bygget (Debug, 0 fejl/0 advarsler) kopieret til test-mappe. IconGrid.dll ~875 KB, timestamp 18:10:46.


## Session 2026-08-08 (18:00-19:06) — Gaming Overlay settings reorganization + Game Resolution embedded

- **Commit `98268fb`** (pushed): Game-only launcher behavior, overlay settings navigation, hardening (8 files, +136/-19).
- **Commit `1cbb2bf`** (pushed): Sidebar icon replaced with Resolution.png (1 XAML file).
- **Root cause: Resolution.png not in .csproj** — added Content Include to copy to output.
- **Session continued with 3 major changes (NOT yet committed — about to commit + push):**

### 1. Overlay scale card — missing intro text
- `GamingOverlayPage.xaml.cs`: Added `OverlayScaleIntroText` property with da/en localization.
- `GamingOverlayPage.xaml`: Header now shows title + intro text (same pattern as other cards).

### 2. Game Resolution moved from sidebar into Gaming Overlay page
- `GamingOverlayPage.xaml`: New collapsible card at bottom with `<views:GameResolutionPage />` embedded.
- `GamingOverlayPage.xaml.cs`: Added `GameResolutionTitleText` + `GameResolutionIntroText` with da/en localization.
- `GameResolutionPage.xaml`: Stripped TemplatePage/ScrollViewer wrapper (no longer standalone). Added current resolution + Hz display at top ("Nuværende opløsning: 3840x2160 @ 144Hz" in accent color).
- `GameResolutionPage.xaml.cs`: Category now defaults to "Games" (previously first alphabetically). Added `CurrentResolutionLabelText` + `CurrentResolutionValue` with `RefreshCurrentResolution()`.
- `DisplayResolutionService.cs`: Added `GetCurrentResolutionWithHz()` static method.
- `SettingsWindow.xaml`: Removed GameResolutionNavButton from sidebar.
- `SettingsWindow.xaml.cs`: Removed GameResolutionNavButton_Click handler.

### 3. Current resolution + Hz display
- New static method `DisplayResolutionService.GetCurrentResolutionWithHz()` returns e.g. "3840x2160 @ 144Hz".
- Displayed at top of Game Resolution section with label (da: "Nuværende opløsning" / en: "Current resolution") and value in accent color.

### Build & deploy
- All builds: 0 errors, 0 warnings.
- Deployed to `C:\IconGrid` after each build.

### Icons pending
- Added to `TODO.md` under "Gaming overlay icons (pending)": 5 icons needed for collapsible headers.

### Files changed (uncommitted, 6 files)
- `GamingOverlayPage.xaml`, `GamingOverlayPage.xaml.cs`
- `GameResolutionPage.xaml`, `GameResolutionPage.xaml.cs`
- `DisplayResolutionService.cs`
- `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`
- `TODO.md`

## Session 2026-08-08 (18:00-20:44) — Launcher idle hide + Gaming Overlay page restructuring

## Status at session end

### Completed and committed (3 commits pushed)
- `98268fb` — Game-only launcher behavior, overlay settings navigation, hardening (8 files)
- `1cbb2bf` — Resolution.png sidebar icon (XAML + csproj fix)
- `0d1d05b` — Game Resolution moved into Gaming Overlay page, overlay scale intro text, current resolution + Hz (11 files, +278/-169)
- `fcca091` — TODO.md cleanup (only active tasks: icons + system guide)

### Completed but NOT committed (working tree dirty)

**Gaming Overlay page restructuring + auto-hide feature — 11 files changed:**

1. **Game Resolution embedded as collapsible card** in Gaming Overlay page (bottom). Removed from sidebar.
2. **Overlay scale card** now has intro text matching other cards.
3. **Current resolution + Hz** displayed in Game Resolution section (e.g. "3840x2160 @ 144Hz") via new `DisplayResolutionService.GetCurrentResolutionWithHz()`.
4. **Launcher idle hide system** — full persistence chain + controller + UI:
   - `Models/LauncherHideMode.cs` (new) — AlwaysVisible/Manual/Auto/AutoAndManual
   - Persistence chain: ConfigModel → SettingsState → ConfigState → Persistence → MainViewModel.Settings
   - `Helpers/Launcher/LauncherWindowModeController.cs` (rewritten) — `ToggleManualHide()`, `PeekStripClicked()`, proximity timer, `SlideToPeek()` (6px), coordination with game-hide
   - `Controls/Launcher/LauncherGrid.xaml` + `.cs` — mini ▼ button (Segoe UI, fixed from Segoe Fluent Icons) + `IdleHideClick` event
   - `Views/Settings/Pages/StartsidePage.xaml` + `.cs` — "Launcher synlighed" dropdown with 4 modes + full da/en localization
   - Persistence now saves correctly (SelectLauncherHideMode notified after items rebuild)

### INCOMPLETE — needs wiring in next session

**Missing connections in `MainWindow.xaml.cs`:**
1. `IdleHideButton_Click` handler NOT yet added to call `_windowModeController?.ToggleManualHide()`
2. `ViewModel_PropertyChanged` NOT yet subscribed to `LauncherHideMode` changes (need to call `_windowModeController?.ApplyIdleHideMode()` when setting changes)

**Missing connections in `MainWindow.xaml`:**
- `IdleHideClick="IdleHideButton_Click"` IS already wired in XAML (line 651)

### Paint/drag-drop false trigger (bug)
- Drag-drop into Paint triggers game auto-hide/overlay because native FPS agent sees mspaint.exe as foreground.
- NOT fixed this session — needs `mspaint` added to ignored foreground processes.

## Next session first steps
1. Use `write_to_file` for MainWindow.xaml.cs since `replace_in_file` keeps failing on this file.
2. Add `IdleHideButton_Click` handler + `LauncherHideMode` PropertyChanged subscription.
3. Build + deploy → test manual/auto hide.
4. Fix paint/drag-drop false trigger.
5. Commit + push (await approval).

## Files changed (uncommitted)
- Models/LauncherHideMode.cs (NEW)
- Models/ConfigModel.cs
- ViewModels/Settings/MainViewModelSettingsState.cs
- ViewModels/Settings/MainViewModelConfigState.cs
- ViewModels/Settings/MainViewModelSettingsPersistence.cs
- ViewModels/MainViewModel.Settings.cs
- ViewModels/MainViewModel.cs
- Helpers/Launcher/LauncherWindowModeController.cs (rewritten)
- Controls/Launcher/LauncherGrid.xaml + .cs
- Views/Settings/Pages/StartsidePage.xaml + .cs
- Views/Launcher/MainWindow.xaml (XAML wiring done)
- Views/Launcher/MainWindow.xaml.cs (handler NOT wired yet — 2 lines missing)
- Models/GameResolutionPage.xaml (stripped TemplatePage wrapper)
- Views/Settings/Pages/GamingOverlayPage.xaml + .cs
- Helpers/Launcher/DisplayResolutionService.cs (GetCurrentResolutionWithHz)
- Views/Settings/SettingsWindow.xaml + .cs (removed GameResolution sidebar)
- TODO.md

## Session findings (2026-08-08)

- **21:07** — Deployed Debug build to C:\IconGrid: IdleHideButton_Click handler + LauncherHideMode PropertyChanged subscription wired in MainWindow.xaml.cs, ApplyIdleHideMode made public in LauncherWindowModeController.cs. Both dll timestamps match (21:06:36).

- **21:41** — Deployed Debug build to C:\IconGrid: idle auto-hide delay persistence (ConfigModel → MainViewModel → UI slider), mode descriptions on StartsidePage, PeekStripClicked on click instead of DragMove. Both dll timestamps match (21:40:45).

- **22:13** — Genbygget med --no-incremental (ren build). Deployer til C:\IconGrid. String-søgning i DLL for IsSettingsWindowOpen var upålidelig (property name compiles til IL, ikke string literal). Deployment verificeres via timestamps.



## Session 2026-08-09 (00:00-00:50) — Auto-hide system fixes + proximity zone debugging

## Summary
Massiv session med debugging af auto-hide proximity zone. Proximity trigger var 200px forskudt til venstre — root cause var at `_window.Left`/`_window.Width` IKKE opdateres under WPF `BeginAnimation` (animationssystemet rører kun den renderede position). Fixet med Win32 `GetWindowRect`.

## Changes deployed (C:\IconGrid, 00:42)
- **Proximity zone fix:** Win32 `GetWindowRect` bruges nu i `IdleProximityTimer_Tick` + `IsCursorInPeekZone()` — giver den FAKTISKE renderede position uanset animationer.
- **Direction-aware SlideToPeek:** Launcher i top-halvdel → slide op. Launcher i bund-halvdel → slide ned (6px synlig i bunden).
- **_originalTop caching:** `SlideToPeek` gemmer original position via `GetWindowRect`; `SlideToVisible` restorer den. Ellers gik launcher til toppen.
- **_isHidden fix:** Sættes nu eksplicit (`true` i SlideToPeek, `false` i SlideToVisible) — ikke afledt fra `targetTop < 0` (virkede ikke for bottom-peek).
- **Auto-re-hide:** Når launcher vises via hover/proximity, starter `_idleHideDelayTimer` så den auto-hider igen efter X sekunder uden interaktion.
- **Fjernet Auto+Manual mode:** Kun 3 modes: Altid synlig / Manuel / Auto.
- **Settings-åben → ingen hide:** `IsSettingsWindowOpen` property + `SettingsWindowCoordinator` wiring + check i `HandleMouseLeave`/`IdleHideDelayTimer_Tick`/`HandleAutoHideTick`.
- **PeekActivationMode:** Ny dropdown på startsiden: 'Ved hover' (proximity) / 'Ved klik på striben'. Fuld persistence-kæde.
- **Auto-hide delay:** Bruger-konfigurerbar 1/2/3/5/10 sekunder. Fuld persistence-kæde.
- **BoolToVisibilityConverter:** Registreret globalt i App.xaml (fix til SettingsWindow crash).
- **Forklarende tekster** på startsiden under Launcher synlighed dropdown.

## Known issues for next session
1. **Proximity zone stadig upålidelig** — selv med GetWindowRect trigger hover kun i et smalt område. Skal debugges videre.
2. **Bottom-peek test mangler** — brugeren har ikke testet launcher i bunden endnu.
3. **Process-linje overlap** — hvis launcher placeres nederst på skærmen (nær proceslinjen), ved programmet ikke at det er 'bunden'. Skal måske bruge `WorkArea` i stedet for `Screen.Bounds`.
4. **Work-area clamping** — launcher kan trækkes ud af viewport. `ClampWindowToWorkArea()` findes men kaldes ikke fra DragMove.

## Files modified this session
- Helpers/Launcher/LauncherWindowModeController.cs (hele auto-hide controlleren)
- Views/Launcher/MainWindow.xaml.cs (IdleHideButton_Click, PropertyChanged handlers)
- Models/LauncherHideMode.cs (fjernet AutoAndManual)
- Models/ConfigModel.cs (IdleAutoHideDelaySeconds, PeekActivationMode)
- ViewModels/Settings/* (SettingsState, ConfigState, Persistence)
- ViewModels/MainViewModel.cs + .Settings.cs (nye properties + wiring)
- Views/Settings/Pages/StartsidePage.xaml + .cs (UI: modes, delay, peek activation, descriptions)
- Views/Settings/SettingsWindowCoordinator.cs (IsSettingsWindowOpen tracking)
- App.xaml (BoolToVisibilityConverter globalt)

## NOT committed — alt er uncommitted (working tree very dirty ~15+ files)

## Session 8/9 2026 — Auto-hide & proximity fixes

### Fixes applied (build succeeded, deployed to C:\IconGrid)

#### 1. DPI mismatch — ROOT CAUSE of proximity zone offset
- `GetWindowRect` and `Forms.Cursor.Position` return **physical pixels**
- `SystemParameters.WorkArea` and `_peekHeight` use **WPF logical pixels (DIP)**
- On PerMonitorV2 (manifest confirmed: `dpiAwareness=PerMonitorV2`), 150% scaling means physical coords are 1.5× DIP coords.
- **Fix:** Added `GetWindowDpiScale()` using `PresentationSource.FromVisual(_window).CompositionTarget.TransformToDevice.M11`. All physical coords (`rect.Left`, `rect.Right`, `cursorPos.X/Y`) are now divided by `dpiScale` before comparison with DIP values.

#### 2. _originalTop DPI mismatch
- `SlideToPeek()` stored `currentRect.Top` (physical px) without converting to DIP.
- `SlideToVisible()` restored it via `SlideTo(restoreTop)` which applies to `Window.TopProperty` (DIP).
- **Fix:** Convert `currentRect.Top / dpiScale` for `_originalTop`.

#### 3. Taskbar overlap detection (bottom-peek direction)
- `SlideToPeek()` used `(area.Top + area.Bottom) / 2.0` for direction — but `area` is `WorkArea` which excludes taskbar.
- **Fix:** Use `Screen.Bounds` (includes taskbar) for center Y calculation, `WorkArea.Bottom` for actual peek position. This correctly detects top vs bottom when launcher overlaps taskbar zone.

#### 4. DragMove clamping
- `ClampWindowToWorkArea()` existed but was never called after `DragMove()`.
- **Fix:** Added clamping in `Window_MouseLeftButtonDown` after `DragMove()`, gated by new `AllowMultiMonitorDrag` toggle (default false).

#### 5. New setting: AllowMultiMonitorDrag
- Added through full pipeline: `ConfigModel` → `MainViewModelConfigState` → `MainViewModelSettingsState` → `MainViewModel.Settings.cs` → `MainViewModel` public property.
- Default: false (clamping enabled). When false, DragMove is followed by `ClampWindowToWorkArea()`.

### Trace.log format updated
- Now includes `dpiScale`, `isPeekBottom`, and uses DIP-normalized zone coords.

### Files changed
- `Helpers/Launcher/LauncherWindowModeController.cs` — DPI fix, Screen.Bounds direction, originalTop fix
- `Views/Launcher/MainWindow.xaml.cs` — DragMove clamping + AllowMultiMonitorDrag gate
- `Models/ConfigModel.cs` — +AllowMultiMonitorDrag
- `ViewModels/Settings/MainViewModelConfigState.cs` — +AllowMultiMonitorDrag
- `ViewModels/Settings/MainViewModelSettingsState.cs` — +AllowMultiMonitorDrag
- `ViewModels/MainViewModel.cs` — +_allowMultiMonitorDrag field + AllowMultiMonitorDrag property
- `ViewModels/MainViewModel.Settings.cs` — wiring for AllowMultiMonitorDrag

## Session 2026-08-09 — README restructure + screenshot placeholders

## README completely restructured

- Old README was ~323 lines of developer-chronology mixed with user docs — hard to read, missing new features.
- New README has clear sections: Main Launcher → Gaming Overlay → Settings pages → Game Resolution → Architecture.
- All features now documented: launcher hide modes (Always visible/Manual/Auto), peek activation, carousel/grid toggle, position presets, per-resolution scale, transparent background, text color picker.
- Settings pages table simplified from 8 to 7 (Game Resolution absorbed into Gaming Overlay).
- 6 TODO screenshot placeholders with descriptions:
  - `Assets/git-img/IconGrid-full.png` — Full launcher with shortcuts and live monitor strip
  - `Assets/git-img/IconGrid-peek.png` — Launcher in auto-hide peek mode (10px strip)
  - `Assets/git-img/IconGrid-carousel.png` — Carousel view with 4 icons + horizontal scrollbar
  - `Assets/git-img/GamingOverlay-in-game.png` — Gaming overlay in-game (FPS, CPU, GPU, network)
  - `Assets/git-img/Settings-Startside.png` — Startside settings (hide mode dropdown + peek toggle)
  - `Assets/git-img/Settings-GamingOverlay.png` — Gaming Overlay settings (scale slider + position preset + transparency)
- Old screenshots retained: `Floatingicon.png`, `IconGrid-collapsed.png`, `Settings.png`.
- Game Resolution detail section now has "Configured from the **Gaming Overlay** settings page" note.

## Session commits (this session)

- `9ba68c2` Fix auto-hide proximity zone DPI mismatch, add DragMove clamping, slide animation hop fix (8 files)
- `a3b996d` Add launcher hide mode settings UI, idle hide button, SettingsWindowCoordinator tracking (10 files)
- `464b97a` docs: restructure README — user-oriented flow, 6 screenshot placeholders, Game Resolution consolidated under Gaming Overlay (README.md)

## Next session

- Take the 6 new screenshots and replace the TODO placeholders
- Consider replacing the old screenshots with updated versions
- Test bottom-peek with launcher in bottom half of screen (verified working in code, needs UI test)

## Session 2026-08-09 (nat) — Non-game foreground detection + peek-zone fixes

## Session 2026-08-09 (nat) — Non-game foreground detection + peek-zone fixes

### Fix 1 (DEPLOYED): `IsNonRenderingProcess` — optimistic graphics-DLL gate

`Helpers/Hardware/HardwareMonitorAgent.cs`:
- Refactored `IsGraphicsProcess` → `IsNonRenderingProcess`. The old version REJECTED processes when `CreateToolhelp32Snapshot` returned a valid snapshot but didn't show DirectX DLLs. This caused false negatives for UWP/GamePass games (cod22-cod.exe) where AppContainer isolation hides system-DLLs from the snapshot.
- New logic: returns `true` ONLY when module enumeration SUCCEEDS AND proves zero graphics API DLLs loaded. Returns `false` (don't reject) when snapshot is unavailable, empty, or throws an exception — letting the native FPS agent probe the process instead.
- Added "WmiPrvSE", "notepad", "mspaint" to `IgnoredForegroundProcesses`.

**Result**: Battle.net launched outside IconGrid does NOT trigger hide/gaming overlay. COD started from Battle.net is correctly detected (gaming overlay shows). Notepad from IconGrid shortcut does NOT trigger overlay.

### Fix 2 (NOT YET IMPLEMENTED): Battle.net/Steam from "Games" category triggers overlay

**Root cause**: `RememberFpsTarget()` in `ViewModels/MainViewModel.Items.cs` line 146 calls `IsGameItem()` which only checks `item.Category == "Games"`. If Battle.net/Steam shortcuts are placed in the Games category, they trigger `GameLaunched` → `OnGameLaunched` → `HideForGame` + overlay.

**Fix**: In `RememberFpsTarget`, before the `IsGameItem` check, check whether the launched item's process name is a known launcher. Copy the pattern from `HardwareMonitorAgent.IsLikelyLauncherName()`:

```csharp
// In MainViewModel.Items.cs, add this check in RememberFpsTarget:
var processName = Path.GetFileNameWithoutExtension(item.Path);
if (IsKnownLauncherProcess(processName))
{
    Debug.WriteLine($"Skipping FPS target for known launcher: {item.DisplayName} ({processName})");
    return;
}

// New helper method:
private static bool IsKnownLauncherProcess(string processName)
{
    if (string.IsNullOrWhiteSpace(processName))
        return false;
    var normalized = Path.GetFileNameWithoutExtension(processName).Trim();
    return normalized.Equals("Battle.net", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("Battle.net Launcher", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("steam", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("upc", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("EADesktop", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase) ||
           normalized.Equals("launcher", StringComparison.OrdinalIgnoreCase);
}
```

**File**: `ViewModels/MainViewModel.Items.cs` — add `IsKnownLauncherProcess` and call it in `RememberFpsTarget` BEFORE the `IsGameItem` check.

### Fix 3 (NOT YET IMPLEMENTED): Peek-zone dead after manually closing gaming overlay

**Root cause**: `OnGamingOverlayClosed()` in `MainWindow.xaml.cs` line 833 only calls `RestoreLauncherFromGame()` when `RestoreLauncherAfterOverlayClosed` setting is true. But `HideForGame()` sets `_isGameHideActive = true` and stops ALL idle-hide timers. When the user manually closes the overlay while `_isGameHideActive` is true, that flag is never cleared → `IdleProximityTimer_Tick` returns immediately (line 284 guard: `if (_isGameHideActive || !IsIdleHideActive || !_isHidden)`) → peek zone doesn't react.

**Fix**: In `OnGamingOverlayClosed`, ALWAYS call `RestoreLauncherFromGame()` — the flag cleanup must happen unconditionally. `RestoreLauncherAfterOverlayClosed` should only control whether the launcher becomes VISIBLE or stays hidden, but the game-hide state must ALWAYS be cleared:

```csharp
private void OnGamingOverlayClosed()
{
    // Always clear the game-hide state so idle-hide/peek can work again.
    _windowModeController?.RestoreLauncherFromGame();

    // The "restore launcher" setting controls whether the launcher becomes
    // VISIBLE (slides to original position) or stays at its peek position.
    // But the game-hide flag must ALWAYS be cleared — otherwise the user
    // is stuck with a dead peek strip and no way to show the launcher.
    // RestoreLauncherFromGame() already handles the SlideToVisible logic
    // based on the current idle-hide mode, so we just need to call it.
}
```

**File**: `Views/Launcher/MainWindow.xaml.cs` — modify `OnGamingOverlayClosed()` method (line 833-840).

### Unapplied changes in working tree

- `Helpers/Hardware/HardwareMonitorAgent.cs` — IsNonRenderingProcess + ignored-list entries (WmiPrvSE, notepad, mspaint)
- Built and deployed to `C:\IconGrid` (Debug). NOT committed.

### Next session first steps
1. Read this CHAT_STATE.md section
2. Implement Fix 2 (MainViewModel.Items.cs — IsKnownLauncherProcess check)
3. Implement Fix 3 (MainWindow.xaml.cs — OnGamingOverlayClosed unconditionally calls RestoreLauncherFromGame)
4. Build + deploy + test
5. Commit + push (await user approval)
