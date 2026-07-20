# Session State - FPS / ETW

## Current date
- Monday, July 20, 2026

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
