# IconGrid Agent Guide

This file is the entry point for any agent working in this repository.

## Read first

Before making changes, read these files in this order:

1. `CHAT_STATE.md`
2. `README.md`
3. `ARCHITECTURE_RULES.md`

## Purpose

- `CHAT_STATE.md` is the temporary source of truth for the current debugging state.
- `README.md` describes the intended application behavior and project structure.
- `ARCHITECTURE_RULES.md` contains the constraints for keeping the codebase modular and stable.

## Commit and push rules (CRITICAL)

- Do NOT commit or push any changes unless the user explicitly says so.
- All changes must be tested and verified by the user before any commit is made.
- Commits are only made after the user gives clear approval (e.g. "commit", "push", "godkend").
- This applies even for small fixes, typos, or documentation changes.

## File backup rules

- When creating backups of files, always use shell commands (copy, robocopy, xcopy) to copy files directly.
- Do NOT read the file content and write it back as a backup — that is slow, wasteful, and can alter formatting.
- Use `copy "source" "destination"` (Windows) for single file backups.

## Working rules

- Treat `CHAT_STATE.md` as the live note for unresolved issues and recent findings.
- Do not rely on chat history as source of truth.
- If startup or elevation behavior is involved, verify it against the actual code and Windows state.
- If Windows startup creates a duplicate or elevated IconGrid instance, inspect Task Scheduler first for a stale `IconGrid` task before changing application code.
- Keep changes small and verify them with a build before claiming the issue is fixed.
- An active GitHub CLI (`gh`) login is not required for `git commit` or `git push`; use the repository's configured Git credentials. Only require `gh` authentication for GitHub CLI operations such as creating pull requests.
- Do not introduce new UI effects, shadows, borders, chrome, or visual treatments that do not already exist in the current launcher UI.
- Reuse the existing UI language, spacing, and controls; do not invent new styling for overlays or windows.
- Any FPS overlay must be passive and low-risk only; no injection, DLL detouring, or graphics API hooking.
- Gaming overlay windows must remember their last position after shutdown and restore it on next launch.
- The gaming overlay must not minimize together with the launcher or otherwise depend on launcher window state.
- The gaming overlay must not reuse the existing settings page flow; if it needs settings, add a dedicated settings page for it.
- Do not assume the gaming overlay will appear inside a game's fullscreen composition; treat it as a desktop overlay requirement unless proven otherwise.

## Session memory (MCP notes server)

- A local MCP server named `icongrid-notes` provides note tools: `list_notes`, `read_note`, `search_notes`, `update_note`, `check_architecture_rules`, `check_version_consistency`.
- Notes live in:
  - `CHAT_STATE.md` (tracked, repo root) — always use `read_note`/`update_note` with note name `CHAT_STATE` for this file.
  - `.local-state/*.md` (gitignored) — use plain names like `fps-etw` or `ui-launcher`.
- Use `update_note` for structured updates (append under a heading, or replace exact text) instead of manually editing note files with `write_to_file`/`replace_in_file`.
- Run `check_architecture_rules` before starting any large refactor and at the end of each session. Treat VIOLATION findings as required cleanup backlog; do not ignore new violations introduced by an edit.
- Versioning: the app version lives in `AssemblyInfo.cs` (`AssemblyInformationalVersion` is the canonical SemVer, e.g. `0.7.0-beta.1`). Run `check_version_consistency` after any version bump. When a milestone is completed, propose a version bump and sync `README.md` ("Current version") together with `AssemblyInfo.cs`.
- End-of-session ritual (MANDATORY when a session concludes, or when the user says "session slut"/"opdater noter"/equivalent):
  1. `read_note CHAT_STATE`
  2. Append/update the relevant sections with findings from this session under `## Session findings (YYYY-MM-DD)` or the existing date-specific section.
  3. Update `Good next steps` to reflect what remains.
  4. If a `.local-state` technical reference changed materially (e.g. `fps-etw.md`), update it too.
  5. Commit `CHAT_STATE.md` only if the user explicitly approves a commit.
- Do not store chat history as the source of truth; treat `CHAT_STATE.md` + `.local-state` as the persistent memory.

## Current focus

- Fix Windows startup so IconGrid no longer launches extra elevated instances.
- Remove or repair stale Windows startup entries when they are found.
- Preserve the separation between launcher UI and hardware monitor behavior.
- Keep any new overlay or window visually consistent with the existing launcher UI only.
- Keep FPS work opt-in and conservative; do not use hook-based capture paths.
- Keep the gaming overlay independent from launcher minimize behavior and settings navigation.
- Maintain the session memory backlog via the `icongrid-notes` MCP tools instead of ad-hoc file edits.
