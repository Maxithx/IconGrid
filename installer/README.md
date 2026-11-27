# IconGrid installer

Builds a standard Windows `setup.exe` with Inno Setup 6.

## Requirements

- .NET 10 SDK (for `dotnet publish`)
- Inno Setup 6 (`winget install --id JRSoftware.InnoSetup --exact`)
- The native FPS agent built in Release (`Native\FpsAgent\bin\Release\IconGridFpsAgent.exe`)

The installer is **self-contained**: the .NET 10 Desktop runtime files are bundled, so the target machine does not need a separate .NET installation.

It also adds the installing Windows user to `Performance Log Users` (`Brugere af ydelseslog`) for FPS/ETW access. A sign-out/in or reboot is required before the new group membership is active.

## Build

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Or double-click `build-installer.cmd`.

Output:

```text
.local-state\installer\output\IconGrid-<version>-Setup.exe
```

The installer places IconGrid in `Program Files\IconGrid`, creates Start Menu shortcuts,
offers an optional desktop shortcut, and preserves `%APPDATA%\IconGrid` user data when uninstalling.
