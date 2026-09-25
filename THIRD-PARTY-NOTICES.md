# Third-party notices

IconGrid itself is licensed under the PolyForm Strict License 1.0.0 (see `LICENSE`) — it is
source-available and free to use for noncommercial purposes, but it may not be changed,
distributed, sublicensed, or sold. The third-party components below are licensed more
permissively by their respective owners. All third-party components remain under their own
licenses, and the notices here are provided to satisfy their attribution requirements.

## NuGet packages (direct)

| Component | Version | License |
|---|---|---|
| LibreHardwareMonitorLib | 0.9.6 | Mozilla Public License 2.0 (MPL-2.0) |
| Newtonsoft.Json | 13.0.3 | MIT License |
| System.Management | 10.0.2 | MIT License |
| System.ServiceProcess.ServiceController | 8.0.0 | MIT License |

## NuGet packages (transitive)

| Component | Version | License |
|---|---|---|
| HidSharp | 2.6.4 | Apache License 2.0 |
| BlackSharp.Core | 1.0.4 | Mozilla Public License 2.0 (MPL-2.0) |
| DiskInfoToolkit | 1.1.2 | Mozilla Public License 2.0 (MPL-2.0) |
| RAMSPDToolkit-NDD | 1.4.2 | Mozilla Public License 2.0 (MPL-2.0) |

## Microsoft runtime components

| Component | License |
|---|---|
| .NET / WPF / Windows Forms runtime libraries | MIT License |
| Microsoft.Windows.SDK.NET, WinRT.Runtime (Windows SDK projections) | MIT License |

## Bundled tools

| Component | License |
|---|---|
| Intel PresentMon (`Tools/PresentMon/`) | MIT License — Copyright (C) Intel Corporation |

PresentMon is redistributed unmodified as a separate executable and is used for
frame-time collection. Its license text is available in the upstream
PresentMon repository (<https://github.com/GameTechDev/PresentMon>).

## Notes on copyleft components

The LibreHardwareMonitorLib, BlackSharp.Core, DiskInfoToolkit, and RAMSPDToolkit-NDD
components are licensed under MPL-2.0. They are used here **unmodified** as compiled
assemblies. The MPL-2.0 obligations apply to those files/assemblies themselves
(their source remains available under MPL-2.0 upstream); the rest of IconGrid is not
placed under MPL-2.0 by this use.

## Trademarks

Hardware vendor names and logos (for example Intel, AMD, and NVIDIA in `Assets/Hw-logo/`)
are trademarks or registered trademarks of their respective owners and are used only
for identification purposes. IconGrid is an independent project and is not affiliated
with, endorsed by, or sponsored by any hardware vendor or Microsoft.
