# IconGrid Chat State

This file is the stable working note for the current startup/elevation issue.
It is intended to survive chat drift and should be treated as the temporary source of truth.

## Current problem

- Resolved: Windows startup now launches `IconGrid (2)` instead of `IconGrid (4)`.
- Drag-and-drop from Windows shortcuts into the icon area works again.
- CPU/GPU temperature telemetry works during startup.
- UAC appears when IconGrid starts with Windows, and that is currently accepted as okay.

## What has already been changed in the repo

- Removed the code path that launched `schtasks.exe` during app startup.
- Kept the hardware monitor as a separate elevated child process.
- Expanded startup cleanup to remove:
  - `HKCU` and `HKLM` `Run` entries
  - `StartupApproved` entries
  - startup-folder shortcuts containing `IconGrid`
  - AppCompat `Layers` entries for `IconGrid.exe`

## What was found on the machine

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` does not contain an `IconGrid` entry.
- Startup folders only contained `desktop.ini`.
- The leftover scheduled task `\IconGrid` was deleted manually in Windows Task Scheduler.
- After logout/login, Task Manager showed `IconGrid (2)` and no longer showed the earlier extra startup chain.

## Best current conclusion

- The leftover scheduled task `\IconGrid` was the concrete cause of the elevated startup instance.
- Once it was removed, startup became stable and the launcher UI worked normally again.
- If startup breaks again, the first thing to inspect is Task Scheduler for a stale `IconGrid` task before changing repo code.

## Next step

- No immediate action required.
- If the problem reappears, re-check Windows Task Scheduler before changing repo code again.
