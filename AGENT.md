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

## Commit and push rules (CRITICAL — ask FIRST, every time)

- NEVER commit or push on your own initiative. NEVER assume approval.
- Before ANY commit or push, ask the user explicitly, e.g.: "Skal dette committes og pushes?"
- Wait for an explicit positive answer ("commit", "push", "godkend", "ja") before running `git commit` / `git push`.
- A previous approval for an earlier change does NOT carry over to later changes in the same session. Ask again for each new change.
- Deployment to `C:\IconGrid` for testing is NOT approval to commit/push. The user must test and give separate, explicit commit approval.
- This applies even for small fixes, typos, or documentation changes — including `CHAT_STATE.md`.
- If in doubt: ask. Asking takes one message; an unwanted push is a trust violation.

## File backup rules

- When creating backups of files, always use shell commands (copy, robocopy, xcopy) to copy files directly.
- Do NOT read the file content and write it back as a backup — that is slow, wasteful, and can alter formatting.
- Use `copy "source" "destination"` (Windows) for single file backups.

## Command cheatsheet (avoid repeated shell errors)

- Before running any shell command, consult E:\IconGrid-GitHub\.local-state\regex-commands-cheatsheet.md (good/bad command patterns).
- After every command failure or when a better pattern is found, append the finding to that file. Keep it up to date continuously.
- Critical rule: never use PowerShell dollar-variables inside a one-line powershell -Command call executed from the tool; the cmd wrapper strips them (ParserError). Use variable-free expressions instead.

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

## Testing IconGrid locally (deploy to C:\IconGrid)

- The user tests IconGrid by copying from `E:\IconGrid-GitHub\bin` to `C:\IconGrid` and running it from there. **Always deploy to `C:\IconGrid` after every build, without waiting for the user to ask.**
- After every deploy, note the deploy timestamp in `CHAT_STATE.md` under `## Session ...` so the user can verify which build is running.
- **CRITICAL — copy the whole build output, not just the .exe.** IconGrid is a framework-dependent .NET app: all code lives in `IconGrid.dll`, NOT in `IconGrid.exe`. Copying only the exe leaves the old dll in place and the user keeps running the previous build (this burned a long debugging session on 2026-08-06).
- Steps to deploy a change for testing:
  1. Build the configuration you want the user to test, e.g. `dotnet build IconGrid.csproj` (the user tests Debug builds).
  2. **NEVER use `xcopy /e` or `Copy-Item -Recurse` to copy the ENTIRE build output to `C:\IconGrid`** — this can overwrite the user's data files (`config.json`, `items.json`) with stale or empty copies from `bin\Debug`. Only copy DLLs + EXEs + runtime config files.
  3. **PREFER `deploy-test.cmd`** in the repo root — it stops running processes and copies only DLLs, EXEs, runtimeconfig.json, and deps.json. Run it as: `cmd /c E:\IconGrid-GitHub\deploy-test.cmd`.
  4. If deploying manually: `taskkill /f /im IconGrid.exe`, then `xcopy /y /q "E:\IconGrid-GitHub\bin\Debug\net10.0-windows10.0.22621.0\*.dll" "C:\icongrid\" >nul` (repeat for *.exe, *.runtimeconfig.json, *.deps.json).
  3. Make sure all IconGrid processes are fully closed first (launcher + hardware-monitor agent, e.g. `IconGrid.exe` and `IconGridFpsAgent.exe` in Task Manager > Details). A running process keeps the old dll loaded.
  4. Verify the copy: `C:\IconGrid\IconGrid.dll` must have the same (or newer) LastWriteTime as the freshly built dll in `bin\<Config>\net10.0-windows10.0.22621.0\`. If it still shows an old timestamp, the dll was not copied and the test is meaningless.
  5. Check Task Manager that the process StartTime is after the copy timestamp.
- If the user reports "nothing changed" after a code fix, FIRST check whether `C:\IconGrid\IconGrid.dll` is stale (wrong build config or exe-only copy) before debugging the code further.
- Diagnostic logging: trace.log lives at `C:\Users\<user>\AppData\Roaming\IconGrid\trace.log`. When a build includes new `[GamingOverlay]`-style trace lines, use them to confirm the NEW code is actually running before analyzing behavior.

## Session memory (MCP notes server)

- A local MCP server named `icongrid-notes` provides note tools: `list_notes`, `read_note`, `search_notes`, `update_note`, `check_architecture_rules`, `check_version_consistency`.
- **The MCP server runs DIRECTLY from the repo** — the repo (`tools/mcp-notes-server/`) is the single source of truth and is backed up on GitHub. There is no separate/older local copy to use; the old `C:\Users\THXMAN\Documents\Cline\MCP\notes-server` copy has been deleted (2026-08-03).
- Fresh-clone setup: in `tools/mcp-notes-server/` run `npm install` once, then register the server in Cline's MCP config (`cline_mcp_settings.json`) with:
  ```json
  "icongrid-notes": {
    "command": "node",
    "args": ["E:/IconGrid-GitHub/tools/mcp-notes-server/src/index.js"],
    "env": { "ICONGRID_WORKSPACE": "E:\\IconGrid-GitHub" },
    "disabled": false,
    "autoApprove": []
  }
  ```
  (Replace the `args`/`env` paths if the repo lives at a different location.)
- Keep the server in sync with the repo: any change to `tools/mcp-notes-server/` is versioned with git like normal code.
- Notes live in a two-part memory structure:
  - `CHAT_STATE.md` (tracked, repo root) — **short-term memory** (~30 lines). Contains Current date, Current status, Architecture status, Working tree status, and Good next steps. This is the ONLY file a new AI needs to read to get started. Search with note name `CHAT_STATE`.
  - `.local-state/session-history.md` (gitignored) — **long-term memory index**. Links to per-date session files in `sessions/`. Search with plain name `session-history`.
  - `.local-state/sessions/*.md` (gitignored) — **per-date session logs**. Max ~500 lines each. Split into a new file when approaching 500 lines. Named `YYYY-MM-DD.md`. The index file must be updated when a new session file is created.
  - `.local-state/*.md` (gitignored) — topic-specific memory (e.g. `fps-etw`, `ui-launcher`, `gaming-overlay`). Use plain names like `fps-etw` or `ui-launcher`.
- Use `search_notes` to search across ALL notes at once — it covers both `CHAT_STATE.md` and all `.local-state/**` files.
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

## Localization design pattern (da/en)

Every settings page or UI component that displays user-facing text MUST support both Danish and English following this pattern:

1. **Add keys to `Helpers/Settings/LocalizationHelper.cs`** — insert keys in BOTH the `["en"]` and `["da"]` dictionaries. The `"da"` section is below line ~276. Key naming convention: `MonitorRowLayoutTitle`, `MonitorRowLockDlWidthTitle`, etc. (PascalCase, descriptive prefix for the feature).

2. **Add readonly properties in `ViewModels/MainViewModel.Localization.cs`** — each key gets a one-liner: `public string MonitorRowLayoutTitle => _localizationState.Get(Language, "MonitorRowLayoutTitle");`

3. **Add `OnPropertyChanged` calls in `NotifyLocalizationPropertiesChanged()`** (same file, ~line 126) — so the UI rebinds when the user switches language.

4. **Bind XAML texts to the MainViewModel properties** — `Text="{Binding MonitorRowLayoutTitle}"` instead of hardcoded strings. The settings pages inherit MainViewModel as DataContext from the SettingsWindow.

**Do NOT follow the GamingOverlayPage/StartsidePage pattern** of `DataContext = this` + `RefreshLocalizedText()` — that pattern predates the centralized localization system and is legacy. The centralized pattern (LocalizationHelper → MainViewModel.Localization → XAML bindings) is the preferred approach for new pages.

**Note:** MonitorRowLayoutPage (2026-08-10) is the first fully centralized-localized page. Use it as the template for future settings pages.

## Architecture rules

`ARCHITECTURE_RULES.md` defines all modularity guardrails (file size limits, refactor workflow, feature placement rules, security checklist). Read it before any large refactor. Run `check_architecture_rules` after each step. The `.clinerules` file at the repo root enforces the same rules for the agent's runtime behavior.

## Current focus

- Fix Windows startup so IconGrid no longer launches extra elevated instances.
- Remove or repair stale Windows startup entries when they are found.
- Preserve the separation between launcher UI and hardware monitor behavior.
- Keep any new overlay or window visually consistent with the existing launcher UI only.
- Keep FPS work opt-in and conservative; do not use hook-based capture paths.
- Keep the gaming overlay independent from launcher minimize behavior and settings navigation.
- Maintain the session memory backlog via the `icongrid-notes` MCP tools instead of ad-hoc file edits.
