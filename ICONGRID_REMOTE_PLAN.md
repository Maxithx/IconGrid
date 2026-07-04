# IconGrid Remote Plan

## Goal

Build `IconGrid.Remote` as a separate project first, then integrate it later into the `IconGrid` settings experience without coupling it to the launcher/topbar/icon-grid parts.

## Principle

`IconGrid` launcher UI is not part of the remote project.

Only the settings-style UI language should be reused:
- window/content layout
- card styling
- spacing
- typography
- scroll behavior
- theme direction

## Order

### NR1

Refactor `IconGrid` settings UI so navigation and right-side content are structurally separate, while still looking like one unified interface.

This is the most important first step because it makes later integration of `IconGrid.Remote` much simpler.

### NR2

Create a reusable settings content shell that can be hosted:
- inside `IconGrid` settings
- inside a future standalone `IconGrid.Remote` app

### NR3

Create a standalone `IconGrid.Remote` UI prototype using the same content-shell design, but without sidebar in the first version.

### NR4

Define remote desktop MVP architecture and tech stack.

### NR5

Implement remote engine separately from UI.

### NR6

Integrate `IconGrid.Remote` back into `IconGrid` settings as a page/module.

## NR1 Scope

Split the current settings experience into clearer parts:

- `SettingsWindow`
  - owns outer window/chrome/back button/general host behavior

- `SettingsNavigationPane`
  - owns sidebar only
  - nav items, selected state, icons, click routing

- `SettingsContentHost`
  - owns right-side page hosting only
  - scroll host
  - page container

- `SettingsContentShell`
  - reusable right-side visual shell
  - hero block
  - card stack spacing
  - consistent width/margins/scrollbar behavior

## NR1 Success Criteria

- Sidebar and right-side content are separate in code structure.
- Visual result stays effectively unchanged.
- Existing settings pages still work.
- Right-side content can later be reused without the sidebar.

## `IconGrid.Remote` Structure

Recommended future structure:

- `IconGrid.Remote.Core`
  - session models
  - connection state
  - protocol abstractions
  - auth abstractions
  - no WPF UI

- `IconGrid.Remote.Host`
  - screen capture
  - input handling
  - host lifecycle

- `IconGrid.Remote.Viewer`
  - standalone desktop app
  - reused settings-style content shell

- `IconGrid.Remote.Relay`
  - optional later
  - rendezvous/relay/self-hosted network layer

## Remote MVP

Version 1 should be small:

- connect to another machine
- show remote screen
- mouse and keyboard control
- simple connection state UI
- LAN-first or controlled environment first

## Not In V1

Do not build these first:

- TeamViewer-scale unattended access
- file transfer
- multi-monitor support
- clipboard sync
- mobile clients
- NAT traversal complexity
- relay mesh/network scaling
- advanced codec optimization

## Technology Direction

Use open source and modern components.

Suggested direction:

- UI prototype: WPF, because it matches current `IconGrid` skills and design reuse
- remote engine: separate and modern, likely Rust-backed later if performance/safety is a priority
- integration boundary: API/IPC/service boundary, not tightly mixed UI and engine code

## Working Method

To keep token usage under control:

- one small goal per session
- no large mixed refactor + architecture + feature sessions
- create a short checkpoint after each phase
- only read the files needed for the current step

## Next Recommended Step

Implement only `NR1`:

- separate settings navigation from right-side content host
- preserve current visuals
- stop there

After that, define the first `IconGrid.Remote` UI prototype.
