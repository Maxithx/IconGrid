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
- validate the shared-memory live path across more real games
- decide whether to add frametime as a companion metric
- decide whether to keep simplifying the fallback FPS layers around the live path
- document installer/setup requirements more formally

- validate the icongrid-notes MCP server in a real session (list/read/search/update)

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
