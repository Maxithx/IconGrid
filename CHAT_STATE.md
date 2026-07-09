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
- Updated the settings Test page so it shows startup diagnostics:
  - Task Scheduler matches containing `IconGrid`
  - legacy startup leftovers
  - current process elevation
  - current `IconGrid.exe` instance count
- Added a startup mode setting so startup can be switched between:
  - `Legacy Run` registry startup
  - `Task Scheduler` logon startup
- Switching modes now removes the conflicting startup path first, so we do not keep both active by accident.
- Task Scheduler mode now uses a one-time elevated installer path to create or remove the task.
- Fixed the Task Scheduler lookup/unregister commands so they target `IconGrid` correctly instead of a literal `{TaskName}` placeholder.
- Normal startup initialization no longer tries to re-register the startup task on every launch.

## What was found on the machine

- `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` does not contain an `IconGrid` entry.
- Startup folders only contained `desktop.ini`.
- The leftover scheduled task `\IconGrid` was deleted manually in Windows Task Scheduler.
- After logout/login, Task Manager showed `IconGrid (2)` and no longer showed the earlier extra startup chain.

## Best current conclusion

- The leftover scheduled task `\IconGrid` was the concrete cause of the elevated startup instance.
- Once it was removed, startup became stable and the launcher UI worked normally again.
- If startup breaks again, the first thing to inspect is Task Scheduler for a stale `IconGrid` task before changing repo code.

## Critical startup split

- Manual launch of IconGrid keeps the current UAC behavior for the hardware monitor path.
- Windows startup is silent for the launcher UI.
- The hardware monitor now has its own startup task so it can still run elevated for CPU/GPU telemetry.
- Windows-start silent behavior does not remove elevation from manual launches.
- Startup mode `Task Scheduler` now means:
  - one scheduled task for UI startup with `--startup-launch`
  - one scheduled task for the monitor agent with `--monitor-agent`
- The startup UI task no longer starts the monitor directly when launched by Windows.

## Verified working state

- Windows startup now launches IconGrid silently without a UAC prompt.
- Only one IconGrid UI instance starts at Windows login.
- CPU/GPU temperature telemetry works.
- Drag-and-drop works.
- Task Scheduler shows:
  - `IconGrid`
  - `IconGrid Monitor`

## Next step

- Use the Test page to verify Task Scheduler and legacy startup state if the duplicate instance problem returns.
