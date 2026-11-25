# IconGrid installer

Builds a standard Windows `setup.exe` with Inno Setup 6.

## Requirements

- .NET 10 SDK (for `dotnet publish`)
- Inno Setup 6 (`winget install --id JRSoftware.InnoSetup --exact`)
- The native FPS agent built in Release (`Native\FpsAgent\bin\Release\IconGridFpsAgent.exe`)

The installer is framework-dependent: the target machine needs the **.NET 10 Desktop Runtime (x64)**.
The setup checks for it and offers to open Microsoft's official download page when it is missing.

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
