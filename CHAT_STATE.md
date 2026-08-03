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
